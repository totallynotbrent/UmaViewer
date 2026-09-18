using Gallop.Live.Cutt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Gallop.ImageEffect;

namespace Gallop.Live
{
    public class Director : MonoBehaviour
    {
        private static Director _instance = null;
        public LiveTimelineControl _liveTimelineControl; //Edited to public

        // direct-to-file logger that bypasses Unity's Debug/logMessageReceived routing,
        // which is unreliable in this IL2CPP build during playback (only boot-time
        // warnings reliably reach the mirror log). appended synchronously, flushed per line.
        private static object _debugLock = new object();
        private static string _debugPath = null;
        private static bool _debugStarted = false;
        public static void FileLog(string line)
        {
            try
            {
                lock (_debugLock)
                {
                    if (_debugPath == null)
                        _debugPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "UmaViewer.log");
                    if (!_debugStarted)
                    {
                        File.AppendAllText(_debugPath,
                            $"[UmaViewer] log start {DateTime.Now:yyyy-MM-dd HH:mm:ss} build=[{BuildCommit.Sha}]\n");
                        _debugStarted = true;
                    }
                    File.AppendAllText(_debugPath, line + "\n");
                }
            }
            catch { }
        }
        [SerializeField]
        public float _liveCurrentTime;  //Edited to public
        public bool _isLiveSetup; //Edit to pulic
        public StageController _stageController; //Edited to public
        [SerializeField]
        private GameObject[] _cameraNodes;
        private Camera[] _cameraObjects;
        private Transform[] _cameraTransforms;
        [SerializeField]
        private CameraLookAt _cameraLookAt;
        private int _activeCameraIndex  = 1;
        private readonly int[] kTimelineCameraIndices = new int[3] { 1, 2, 3 };
        [SerializeField] private bool _enableMirrorReflection = true;
        [SerializeField] private List<MirrorReflection> _mirrorReflections = new List<MirrorReflection>();
        [SerializeField] private bool _mirrorRenderInLateUpdate = true;

        [SerializeField]
        private GallopImageEffect _mainGallopImageEffect;

        public static Director instance => _instance;

        //real work start
        public LiveEntry live;
        private const string CUTT_PATH = "cutt/cutt_son{0}/cutt_son{0}";
        private const string STAGE_PATH = "3d/env/live/live{0}/pfb_env_live{0}_controller000";
        private const string SONG_PATH = "sound/l/{0}/snd_bgm_live_{0}_oke_01";
        private const string VOCAL_PATH = "sound/l/{0}/snd_bgm_live_{0}_chara_{1}_01";
        private const string RANDOM_VOCAL_PATH = "sound/l/{0}/snd_bgm_live_{0}_chara";
        private const string LIVE_PART_PATH = "live/musicscores/m{0}/m{0}_part";

        private UmaViewerBuilder Builder => UmaViewerBuilder.Instance;

        public List<Transform> charaObjs;

        public List<UmaContainerCharacter> CharaContainerScript = new List<UmaContainerCharacter>();

        public List<Animation> charaAnims;
        public List<UmaViewerAudio.CuteAudioSource> liveVocal = new List<UmaViewerAudio.CuteAudioSource>();
        public UmaViewerAudio.CuteAudioSource liveMusic = new UmaViewerAudio.CuteAudioSource();

        public PartEntry partInfo;

        public bool _syncTime = false;
        public bool _soloMode = false;

        public int characterCount = 0;
        public int allowCount = 0;

        public int liveMode = 1;

        public LiveViewerUI UI;

        public float totalTime;

        public SliderControl sliderControl;

        public bool IsRecordVMD;

        public bool RequireStage = true;

        private bool _lateTimelineAppliedThisFrame;

        public Transform MainCameraTransform => _mainCameraTransform;

        private Transform _mainCameraTransform;
    private MaterialPropertyBlock _cachedGlobalLightMPB, _cachedBgColorMPB; // ponytail: lazy init in Awake/Initialize to avoid ctor not allowed

        // ponytail: respects isUseHQParticle flag from LiveTimelineData; stdlib already has particlePrefabNames, use flag to skip HQ load
        public bool ShouldUseHQParticle => _liveTimelineControl?.data?.isUseHQParticle ?? false;
        private static readonly Dictionary<string, UmaDatabaseEntry> _laserBundleCache
            = new Dictionary<string, UmaDatabaseEntry>();

        // timeline-driven screen-space state: fullscreen fade color, camera shake noise,
        // and per-name logging so an unresolvable props/spotlight name warns once.
        private GameObject _fadeQuad;
        private Material _fadeMaterial;
        private readonly HashSet<string> _propsMissingLogged = new HashSet<string>();
        private readonly HashSet<string> _spotlightMissingLogged = new HashSet<string>();
        private readonly Dictionary<string, GameObject> _spotlight3dInstances = new Dictionary<string, GameObject>();

        public bool isTimelineControlled
        {
            get
            {
                if (_liveTimelineControl != null)
                {
                    return _liveTimelineControl.data != null;
                }
                return false;
            }
        }

        public float CalcFrameJustifiedMusicTime()
        {
            if (isTimelineControlled)
            {
                return Mathf.RoundToInt(musicScoreTime * 60f) / 60f;
            }
            return musicScoreTime;
        }

        public float musicScoreTime => Mathf.Clamp(smoothMusicScoreTime, 0f, 99999f);

        private float smoothMusicScoreTime => _liveCurrentTime;//temp to liveCurrentTime

        // true once the backing track has actually finished playing (and isn't a
        // looping clip), so the concert ends with the music instead of the dancers
        // running on past the last beat.
        private bool IsMusicFinished()
        {
            if (!_syncTime) return false;
            if (liveMusic == null || liveMusic.sourceList.Count == 0) return false;
            var src = liveMusic.sourceList[0];
            if (src == null || src.clip == null) return false;
            if (src.loop) return false;
            return !src.isPlaying && _liveCurrentTime > 1f;
        }

        public void Initialize()
        {
            if (live != null)
            {
                _instance = this;
                Builder.LoadAssetPath(string.Format(CUTT_PATH, live.MusicId), transform);
                if (RequireStage)
                {
                    Debug.Log(live.BackGroundId);

                    string stagePath = string.Format(STAGE_PATH, live.BackGroundId);
                    if (UmaViewerMain.Instance.AbList.TryGetValue(stagePath, out var stageEntry) &&
                        !UmaAssetManager.Exist(stageEntry))
                    {
                        // 正常从 LoadLive 进入时已经异步预载；这里仅作为其他入口的同步兜底。
                        PreloadStageBundlesBeforeInstantiate(live.BackGroundId);
                    }

                    Builder.LoadAssetPath(stagePath, transform);
                    

                    _liveTimelineControl.StageObjectMap = _stageController.StageObjectMap;
                }


                //Make CharacterObject

                var characterStandPos = _liveTimelineControl.transform.Find("CharacterStandPos");
                int counter = 0;
                var standPos = characterStandPos.GetComponentsInChildren<Transform>();
                var count = _liveTimelineControl.data.characterSettings.useHighPolygonModel.Length;
                for (int i = 0; i < count; i++)
                {
                    if (i < characterStandPos.childCount)
                    {
                        var newObj = Instantiate(standPos[i + 1], transform);
                        newObj.gameObject.name = string.Format("CharacterObject{0}", counter);
                        charaObjs.Add(newObj.transform);
                        counter++;
                    }
                    else
                    {
                        var newObj = Instantiate(standPos[i % characterStandPos.childCount + 1], transform);
                        newObj.gameObject.name = string.Format("CharacterObject{0}", counter);
                        charaObjs.Add(newObj.transform);
                        counter++;
                    }
                };


                //Get live parts info
                UmaDatabaseEntry partAsset = UmaViewerMain.Instance.AbList[string.Format(LIVE_PART_PATH, live.MusicId)];
                UmaViewerAudio.LastAudioPartIndex = -1;

                Debug.Log(partAsset.Name);

                AssetBundle bundle = UmaAssetManager.LoadAssetBundle(partAsset);
                TextAsset partData = bundle.LoadAsset<TextAsset>($"m{live.MusicId}_part");
                partInfo = new PartEntry(partData.text);

            }

        }

        public void InitializeUI()
        {
            // ponytail: stdlib already has UI serialized; use it if assigned, else Find once and cache
            if (UI == null) UI = GameObject.Find("LiveUI")?.GetComponent<LiveViewerUI>();
            if (UI == null) { Debug.LogWarning("[Director] LiveUI not found"); return; }

            sliderControl = UI.ProgressBar.GetComponent<SliderControl>();
            LiveViewerUI.Instance.RecordingUI.SetActive(IsRecordVMD);
            LiveViewerUI.Instance.RecordingText.text = $"�� Recording...\r\n VMD will be saved in {Path.GetFullPath(Application.dataPath + UnityHumanoidVMDRecorder.FileSavePath)}";
        }

        // Phase 4 (S5): HQ particle instantiation.
        // Loads particlePrefabNames from the asset manifest when isUseHQParticle is set.
        // Official behavior: flame/gas/fireworks are particle prefabs instantiated under the stage root.
        private void InitializeHQParticles()
        {
            var data = _liveTimelineControl?.data;
            if (data == null || data.particlePrefabNames == null || data.particlePrefabNames.Length == 0)
            {
                Debug.Log("[Director] HQ particle requested but particlePrefabNames is empty");
                return;
            }

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
            {
                Debug.LogWarning("[Director] HQ particle skipped: AbList unavailable");
                return;
            }

            if (_hqParticleRoot == null)
            {
                _hqParticleRoot = new GameObject("HQParticles").transform;
                _hqParticleRoot.SetParent(transform, false);
            }

            int loaded = 0;
            foreach (var prefabName in data.particlePrefabNames)
            {
                if (string.IsNullOrEmpty(prefabName))
                    continue;

                if (main.AbList.TryGetValue(prefabName, out var entry))
                {
                    var bundle = UmaAssetManager.LoadAssetBundle(entry);
                    if (bundle == null)
                    {
                        Debug.LogWarning($"[Director] HQ particle bundle load failed: {prefabName}");
                        continue;
                    }

                    var prefab = bundle.LoadAllAssets<GameObject>()?.FirstOrDefault(p => p != null);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[Director] HQ particle prefab missing in bundle: {prefabName}");
                        continue;
                    }

                    var go = Instantiate(prefab, _hqParticleRoot);
                    go.name = $"HQParticle_{Path.GetFileNameWithoutExtension(prefabName)}";
                    loaded++;

                    // The prefabs instantiate silently; start their particle systems so
                    // fire/gas/firework effects actually run (instantiate-on-its-own leaves
                    // them idle under HQParticles).
                    foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (ps == null || !ps.emission.enabled)
                            continue;
                        var mainModule = ps.main;
                        if (mainModule.playOnAwake)
                            continue;
                        mainModule.playOnAwake = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[Director] HQ particle entry not in AbList: {prefabName}");
                }
            }

            Debug.Log($"[Director] HQ particles loaded: {loaded}/{data.particlePrefabNames.Length}");
        }

        private Transform _hqParticleRoot;

        public void InitializeTimeline(List<LiveCharacterLoadData> characters, int mode)
        {
            totalTime = _liveTimelineControl.data.timeLength;
            // Generalized (was 1004-only): MainLive sheet TotalTimeLength is authoritative when valid.
            // Official uses the main sheet length, not data.timeLength (which includes extended sheets).
            var mainSheet = _liveTimelineControl.GetMainLiveSheet();
            if (mainSheet != null && mainSheet.TotalTimeLength > 1f && mainSheet.TotalTimeLength <= totalTime)
            {
                totalTime = mainSheet.TotalTimeLength;
            }

            liveMode = mode;

            allowCount = characters.Count;

            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name != "")
                {
                    characterCount += 1;
                }
            }
            if (characterCount == 1)
            {
                _soloMode = true;
            }

            _liveTimelineControl.InitCharaMotionSequence(_liveTimelineControl.data.characterSettings.motionSequenceIndices);
            // ponytail: respects isUseHQParticle - skip HQ particle load, ceiling: load light variants via particlePrefabNames
            if (!ShouldUseHQParticle) Debug.Log("[Director] ShouldUseHQParticle check: HQ particles skipped for " + (live!=null?live.MusicId.ToString():"?"));
            else InitializeHQParticles();

            // Phase 4 (S5): props evaluator — resolves propsSettings.propsDataGroup conditions
            // and attaches chara props (mic 🎤 etc.) to their named joints.
            LivePropsEvaluator.EvaluateAndAttach(
                _liveTimelineControl.data,
                CharaContainerScript,
                _liveTimelineControl.data.characterSettings?.motionSequenceIndices);


            _liveTimelineControl.OnUpdateLipSync += delegate (LiveTimelineKeyIndex keyData_, float liveTime_)
            {
                var prevKey = keyData_.prevKey as LiveTimelineKeyLipSyncData;
                var curKey = keyData_.key as LiveTimelineKeyLipSyncData;
                var nextKey = keyData_.nextKey as LiveTimelineKeyLipSyncData;
                for (int k = 0; k < charaObjs.Count; k++)
                {
                    if (k < CharaContainerScript.Count)
                    {
                        var container = CharaContainerScript[k];
                        container.FaceDrivenKeyTarget.AlterUpdateAutoLip(prevKey, curKey, liveTime_, ((int)curKey.character >> k) % 2);
                    }
                }
            };

            _liveTimelineControl.OnUpdateFacial += delegate (FacialDataUpdateInfo updateInfo_, float liveTime_, int position)
            {
                if (position < charaObjs.Count)
                {
                    var container = CharaContainerScript[position];
                    container.FaceDrivenKeyTarget.AlterUpdateFacialNew(ref updateInfo_, liveTime_);
                }
            };

            _liveTimelineControl.OnUpdateGlobalLight += delegate (ref GlobalLightUpdateInfo updateInfo)
            {
                var tmpPos = -(updateInfo.lightRotation * Vector3.forward).normalized;
                if (_cachedGlobalLightMPB == null) _cachedGlobalLightMPB = new MaterialPropertyBlock();
                // ponytail: cache MPB - allocates once per frame, not per locator; ceiling: per-renderer MPB if you need per-uma rim offset
                // character pop: the rim + toon-bright response is fed straight from the real
                // global-light update info, with no user-configurable brightness boost.
                float charaBoost = 1f;
                _cachedGlobalLightMPB.Clear();
                _cachedGlobalLightMPB.SetFloat("_RimShadowRate", updateInfo.globalRimShadowRate);
                _cachedGlobalLightMPB.SetColor("_RimColor", updateInfo.rimColor * charaBoost);
                _cachedGlobalLightMPB.SetFloat("_RimStep", updateInfo.rimStep);
                _cachedGlobalLightMPB.SetFloat("_RimFeather", updateInfo.rimFeather);
                _cachedGlobalLightMPB.SetFloat("_RimSpecRate", updateInfo.rimSpecRate * charaBoost);
                _cachedGlobalLightMPB.SetFloat("_RimHorizonOffset", updateInfo.RimHorizonOffset);
                _cachedGlobalLightMPB.SetFloat("_RimVerticalOffset", updateInfo.RimVerticalOffset);
                _cachedGlobalLightMPB.SetFloat("_RimHorizonOffset2", updateInfo.RimHorizonOffset2);
                _cachedGlobalLightMPB.SetFloat("_RimVerticalOffset2", updateInfo.RimVerticalOffset2);
                _cachedGlobalLightMPB.SetColor("_RimColor2", updateInfo.rimColor2 * charaBoost);
                _cachedGlobalLightMPB.SetFloat("_RimStep2", updateInfo.rimStep2);
                _cachedGlobalLightMPB.SetFloat("_RimFeather2", updateInfo.rimFeather2);
                _cachedGlobalLightMPB.SetFloat("_RimSpecRate2", updateInfo.rimSpecRate2 * charaBoost);
                _cachedGlobalLightMPB.SetFloat("_RimShadowRate2", updateInfo.globalRimShadowRate2);
                _cachedGlobalLightMPB.SetFloat("_UseOriginalDirectionalLight", 1);
                _cachedGlobalLightMPB.SetVector("_OriginalDirectionalLightDir", tmpPos);
                foreach (var locator in _liveTimelineControl.liveCharactorLocators)
                {
                    if (locator != null && updateInfo.flags.hasFlag(locator.liveCharaStandingPosition) && locator is LiveTimelineCharaLocator charaLocator)
                    {
                        var container = charaLocator.UmaContainer;
                        if (container)
                        {
                            foreach (var renderer in container.Renderers)
                            {
                                renderer.SetPropertyBlock(_cachedGlobalLightMPB);
                            }
                        }
                    }
                }
            };

            _liveTimelineControl.OnUpdateBgColor1 += delegate (ref BgColor1UpdateInfo updateInfo)
            {
                foreach (var locator in _liveTimelineControl.liveCharactorLocators)
                {
                    var EFlags = (LiveCharaPositionFlag)updateInfo.flags;
                    if (locator != null && (updateInfo.flags == 0 || EFlags.hasFlag(locator.liveCharaStandingPosition)) && locator is LiveTimelineCharaLocator charaLocator)
                    {
                        var container = charaLocator.UmaContainer;
                        if (container)
                        {
                            if (_cachedBgColorMPB == null) _cachedBgColorMPB = new MaterialPropertyBlock();
                            _cachedBgColorMPB.Clear();
                            _cachedBgColorMPB.SetColor("_CharaColor", updateInfo.color);
                            _cachedBgColorMPB.SetColor("_ToonDarkColor", updateInfo.toonDarkColor);
                            _cachedBgColorMPB.SetColor("_ToonBrightColor", updateInfo.toonBrightColor);
                            _cachedBgColorMPB.SetColor("_OutlineColor", updateInfo.outlineColor);
                            _cachedBgColorMPB.SetFloat("_Saturation", updateInfo.Saturation);
                            foreach (var renderer in container.Renderers)
                            {
                                renderer.SetPropertyBlock(_cachedBgColorMPB);
                            }
                        }
                    }
                }
            };

            SetupCharacterLocator();
            InitializeCamera();
            InitializeMirrorReflections();
            UpdateMainCamera();
            InitializeMultiCamera(_liveTimelineControl);
            for (int i = 0; i < kTimelineCameraIndices.Length; i++)
            {
                int num = kTimelineCameraIndices[i];
                if (num < _cameraObjects.Length)
                {
                    _liveTimelineControl.SetTimelineCamera(_cameraObjects[num], i);
                }
            }
            _liveTimelineControl.OnUpdatePostEffect_BloomDiffusion += OnUpdatePostEffect_BloomDiffusion;
            _liveTimelineControl.OnUpdateHdrBloom += OnUpdateHdrBloom;
            _liveTimelineControl.OnUpdatePostEffect_DOF += OnUpdatePostEffect_DOF;
            _liveTimelineControl.OnUpdateRadialBlur += OnUpdateRadialBlur;
            _liveTimelineControl.OnUpdateTiltShift += OnUpdateTiltShift;
            _liveTimelineControl.OnUpdateFade += OnUpdateFade;
            _liveTimelineControl.OnUpdateFluctuation += OnUpdateFluctuation;
            _liveTimelineControl.OnUpdateVortex += OnUpdateVortex;
            _liveTimelineControl.OnUpdateHandShakeCamera += OnUpdateHandShakeCamera;
            _liveTimelineControl.OnUpdateProps += OnUpdateProps;
            _liveTimelineControl.OnUpdatePropsAttach += OnUpdatePropsAttach;
            _liveTimelineControl.OnUpdateSpotlight3d += OnUpdateSpotlight3d;
            _liveTimelineControl.OnUpdatePostFilm += OnUpdatePostFilm;


            _liveTimelineControl.OnUpdateCameraSwitcher += delegate (int cameraIndex_)
            {
                if (cameraIndex_ < 0)
                {
                    _activeCameraIndex = 0;
                }
                else if (cameraIndex_ < kTimelineCameraIndices.Length)
                {
                    _activeCameraIndex = kTimelineCameraIndices[cameraIndex_];
                }
            };
            
        }

        public void InitializeCamera()
        {
            if (_cameraObjects == null)
            {
                _cameraObjects = new Camera[_cameraNodes.Length + 1];
                _cameraTransforms = new Transform[_cameraNodes.Length + 1];
                for (int i = 0; i < _cameraNodes.Length; i++)
                {
                    GameObject gameObject = _cameraNodes[i];
                    Camera camera = gameObject.GetComponent<Camera>();
                    if (camera == null)
                    {
                        camera = gameObject.GetComponentInChildren<Camera>();
                    }
                    //camera.cullingMask = num;
                    _cameraObjects[i] = camera;
                    _cameraTransforms[i] = camera.transform;
                }
            }
        }

        public void InitializeMultiCamera(LiveTimelineControl control)
        {
            var cameraCount = control.data.multiCameraSettings.cameraNum;
            MultiCamera[] cameras = new MultiCamera[cameraCount];
            var root = new GameObject("MultiCameras");
            root.transform.SetParent(control.transform);
            for (int i = 0; i < cameraCount; i++)
            {
                var camObj = new GameObject($"MultiCamera_{i}");
                camObj.transform.SetParent(root.transform);

                var cam = camObj.AddComponent<MultiCamera>();
                cam.Initialize();
                cameras[i] = cam;
                control.MultiRecordFrames.Add(new List<LiveCameraFrame>());
            }
            control.SetMultiCamera(cameras);
        }

        private void UpdateMainCamera()
        {
            if (_cameraObjects == null) return;
            for (int i = 0; i < _cameraNodes.Length; i++)
            {
                bool activeSelf = _cameraNodes[i].activeSelf;
                bool flag = i == _activeCameraIndex;
                _cameraNodes[i].SetActive(flag);
                if (i == 0 && activeSelf != flag && flag && _cameraLookAt != null)
                {
                    _cameraLookAt.ActivationUpdate();
                }
            }
            _mainCameraTransform = _cameraTransforms[_activeCameraIndex];
        }

        private void SetupCharacterLocator()
        {
            if (!_liveTimelineControl) return;
            for (int i = 0; i < CharaContainerScript.Count; i++)
            {
                var container = CharaContainerScript[i];
                container.LiveLocator = new LiveTimelineCharaLocator(container);
                container.LiveLocator.liveCharaStandingPosition = (LiveCharaPosition)i;
                _liveTimelineControl.liveCharactorLocators[i] = container.LiveLocator;
                container.LiveLocator.liveCharaInitialPosition = container.transform.position;
            }
        }

        public void InitializeMusic(int songid, List<LiveCharacterLoadData> characters)
        {

            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name != "" && i < partInfo.SingerCount)
                {
                    var charaid = characters[i].CharaEntry.Id;

                    var entry = UmaViewerMain.Instance.AbSounds.FirstOrDefault(a => a.Name.Contains(string.Format(VOCAL_PATH, songid, charaid)) && a.Name.EndsWith("awb"));
                    if (entry == null)
                    {
                        List<UmaDatabaseEntry> entries = new List<UmaDatabaseEntry>();
                        foreach (var random in UmaViewerMain.Instance.AbSounds.Where(a => (a.Name.Contains(string.Format(RANDOM_VOCAL_PATH, songid)) && a.Name.EndsWith("awb"))))
                        {
                            entries.Add(random);
                        }
                        if (entries.Count > 0)
                        {
                            entry = entries[UnityEngine.Random.Range(0, entries.Count - 1)];
                        }
                    }

                    if (entry != null)
                    {
                        Debug.Log(entry.Name);
                        liveVocal.Add(UmaViewerAudio.ApplySound(entry.Name.Split('.')[0], i));
                    }
                }
            }


            liveMusic = UmaViewerAudio.ApplySound(string.Format(SONG_PATH, songid), -1);
        }

        public void Play()
        {

            foreach (var vocal in liveVocal)
            {
                UmaViewerAudio.Play(vocal);
            }
            UmaViewerAudio.Play(liveMusic);

            _isLiveSetup = true;
            _liveCurrentTime = 0;

            if (IsRecordVMD)
            {
                foreach (var container in CharaContainerScript)
                {
                    var rootbone = container.transform.Find("Position");
                    var newRecorder = rootbone.gameObject.AddComponent<UnityHumanoidVMDRecorder>();
                    newRecorder.UseParentOfAll = true;
                    newRecorder.UseAbsoluteCoordinateSystem = true;
                    newRecorder.Initialize();
                    if (!newRecorder.IsRecording)
                    {
                        newRecorder.StartRecording(true);
                    }
                }
            }
        }

        private void OnTimelineUpdate(float _liveCurrentTime)
        {
            // film layers evaluate in this pass; reset the strongest-layer
            // tracker before the three PostFilm events fire.
            _filmBestPower = -1f;
            _liveTimelineControl.AlterUpdate(_liveCurrentTime);
            if (!_soloMode)
            {
                UmaViewerAudio.AlterUpdate(_liveCurrentTime, partInfo, liveVocal, sliderControl.is_Outed);
            }
        }

        private void ApplyTimelineLateUpdate()
        {
            if (_lateTimelineAppliedThisFrame || _liveTimelineControl == null)
                return;

            _liveTimelineControl.AlterLateUpdate();
            _lateTimelineAppliedThisFrame = true;
        }

        bool isExit;
        void Update()
        {
            if (isExit) return;

            if (_isLiveSetup)
            {
                _lateTimelineAppliedThisFrame = false;

                if ((!UmaViewerMain.TryConsumeEscapeForFullScreen() && Input.GetKeyDown(KeyCode.Escape)) ||
                    (!sliderControl.is_Touched && !sliderControl.is_Outed &&
                     (_liveCurrentTime >= totalTime || IsMusicFinished())))
                {
                    ExitLive();
                }

                // F7 toggles the bloom/diffusion stage glow on and off at runtime.
                if (Input.GetKeyDown(KeyCode.F7))
                {
                    var fx = GetActivePostEffect();
                    if (fx != null)
                    {
                        fx.BloomAndDiffusionEnabled = !fx.BloomAndDiffusionEnabled;
                    }
                }

                if (_syncTime == false)
                {
                    if(liveMusic.sourceList.Count == 0)
                    {
                        _syncTime = true;
                    }
                    else if (liveMusic.sourceList[0].time > 0.01)
                    {
                        _liveCurrentTime = UI.ProgressBar.value * totalTime;
                        _liveCurrentTime = Mathf.Clamp(_liveCurrentTime, 0f, Mathf.Max(0f, totalTime - 0.001f));
                        _liveCurrentTime = liveMusic.sourceList[0].time;
                        _syncTime = true;
                    }
                }
                else
                {
                    if (IsRecordVMD)
                    {
                        _liveCurrentTime += (1 / 60f);
                        if (liveMusic != null)
                        {
                            UmaViewerAudio.Stop(liveMusic);
                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.Stop(vocal);
                            }
                        }

                        UI.ProgressBar.SetValueWithoutNotify(_liveCurrentTime / totalTime);
                        OnTimelineUpdate(_liveCurrentTime);
                        ApplyTimelineLateUpdate();
                    }
                    else if (sliderControl.is_Outed)
                    {
                        _liveCurrentTime = UI.ProgressBar.value * totalTime;

                        if (liveMusic != null)
                        {
                            UmaViewerAudio.SetTime(liveMusic, _liveCurrentTime);

                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.SetTime(vocal, _liveCurrentTime);
                            }

                            UmaViewerAudio.Play(liveMusic);

                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.Play(vocal);
                            }
                        }

                        OnTimelineUpdate(_liveCurrentTime);
                        ApplyTimelineLateUpdate();

                        sliderControl.is_Outed = false;
                        sliderControl.is_Touched = false;
                        _syncTime = false;
                    }
                    else if (sliderControl.is_Touched)
                    {
                        _liveCurrentTime = UI.ProgressBar.value * totalTime;

                        if (liveMusic != null)
                        {
                            UmaViewerAudio.Stop(liveMusic);
                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.Stop(vocal);
                            }
                        }

                        OnTimelineUpdate(_liveCurrentTime);
                        ApplyTimelineLateUpdate();
                    }
                    else
                    {
                        // drive the dance from the real music position so the choreography
                        // stays locked to the beat (a free-running clock drifts when the
                        // timeline length differs from the clip and never ends cleanly).
                        if (liveMusic != null && liveMusic.sourceList.Count > 0)
                        {
                            var audio = liveMusic.sourceList[0];
                            if (audio != null && audio.clip != null)
                            {
                                _liveCurrentTime = audio.time;
                            }
                        }
                        _liveCurrentTime = Mathf.Clamp(_liveCurrentTime, 0f, Mathf.Max(0f, totalTime - 0.001f));
                        UI.ProgressBar.SetValueWithoutNotify(_liveCurrentTime / totalTime);
                        OnTimelineUpdate(_liveCurrentTime);
                        ApplyTimelineLateUpdate();
                    }
                }

                UpdateMainCamera();

                // 时间轴和主相机都更新完后再同步 Laser Renderer/朝向。
                // 这样既不会读取上一帧 LaserUpdateInfo，也不会读取上一帧相机姿态。
                if (_stageController != null)
                    _stageController.AlterUpdateLaserControllers();
            }
        }

        private void LateUpdate()
        {
            if (_isLiveSetup && _syncTime && !IsRecordVMD)
            {
                ApplyTimelineLateUpdate();
            }

            if (_enableMirrorReflection && _mirrorRenderInLateUpdate)
            {
                UpdateMirrorReflections();
            }

        }

        private void FixedUpdate()
        {
            LiveViewerUI.Instance.UpdateLyrics(_liveCurrentTime);
        }

        DateTime ExitTime;
        private void ExitLive()
        {
            isExit = true;
            if (_liveTimelineControl.IsRecordVMD)
            {
                ExitTime = DateTime.Now;
                SaveCameraVMD();
                SaveMultiCameraVMD();
                SaveCharacterVMD();
            }
            UmaSceneController.LoadScene(
                "Version2",
                null,
                delegate
                {
                    // 等旧 LiveScene 完全销毁后再清理，避免过场期间角色/舞台对象失去资源。
                    UmaAssetManager.UnloadAllBundle(true);
                });
        }

        private void SaveCharacterVMD()
        {
            foreach (var container in CharaContainerScript)
            {
                var rootbone = container.transform.Find("Position");
                if (rootbone.gameObject.TryGetComponent(out UnityHumanoidVMDRecorder recorder))
                {
                    if (recorder.IsRecording)
                    {
                        recorder.StopRecording();
                        recorder.SaveLiveVMD(live, ExitTime, $"Live{live.MusicId}_Pos{CharaContainerScript.IndexOf(container)}", Config.Instance.VmdKeyReductionLevel);
                    }
                }
            }
        }

        private void SaveMultiCameraVMD()
        {
            for (int i = 0; i < _liveTimelineControl.data.worksheetList[0].multiCameraPosKeys.Count; i++)
            {
                var frames = _liveTimelineControl.MultiRecordFrames[i];
                frames[0].FovVaild = true;
                var fov = _liveTimelineControl.data.worksheetList[0].multiCameraPosKeys[i].keys.thisList;
                fov.ForEach(k =>
                {
                    var keyframe = frames.Find(f => f.frameIndex == k.frame);
                    if (keyframe != null)
                    {
                        var index = frames.IndexOf(keyframe);
                        keyframe.FovVaild = true;
                        if (index + 1 < frames.Count) frames[index + 1].FovVaild = true;
                        if (index - 1 > 0) frames[index - 1].FovVaild = true;
                        if (index - 2 > 0) frames[index - 2].FovVaild = true;
                        if (index - 3 > 0) frames[index - 3].FovVaild = true;
                    }
                });

                UnityCameraVMDRecorder.SaveLiveCameraVMD(live, ExitTime, frames, i);
            }
        }

        private void SaveCameraVMD()
        {
            var frames = _liveTimelineControl.RecordFrames;
            frames[0].FovVaild = true;
            var fov = _liveTimelineControl.data.worksheetList[0].cameraFovKeys.thisList;
            fov.ForEach(k =>
            {

                var keyframe = frames.Find(f => f.frameIndex == k.frame);
                if (keyframe != null)
                {
                    var index = frames.IndexOf(keyframe);
                    keyframe.FovVaild = true;
                    if (index + 1 < frames.Count) frames[index + 1].FovVaild = true;
                    if (index - 1 > 0) frames[index - 1].FovVaild = true;
                    if (index - 2 > 0) frames[index - 2].FovVaild = true;
                    if (index - 3 > 0) frames[index - 3].FovVaild = true;
                }
            });

            UnityCameraVMDRecorder.SaveLiveCameraVMD(live, ExitTime, frames);
        }

        public static List<UmaDatabaseEntry> GetLiveAllVoiceEntry(int songid, List<LiveCharacterLoadData> characters)
        {
            List<UmaDatabaseEntry> entryList = new List <UmaDatabaseEntry>();
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name != "")
                {
                    var charaid = characters[i].CharaEntry.Id;

                    var entry = UmaViewerMain.Instance.AbSounds.FirstOrDefault(a => a.Name.Contains(string.Format(VOCAL_PATH, songid, charaid)) && a.Name.EndsWith("awb"));
                    if (entry == null)
                    {
                        List<UmaDatabaseEntry> entries = new List<UmaDatabaseEntry>();
                        foreach (var random in UmaViewerMain.Instance.AbSounds.Where(a => (a.Name.Contains(string.Format(RANDOM_VOCAL_PATH, songid)) && a.Name.EndsWith("awb"))))
                        {
                            entries.Add(random);
                        }
                        if (entries.Count > 0)
                        {
                            entry = entries[UnityEngine.Random.Range(0, entries.Count - 1)];
                        }
                    }

                    if (entry != null)
                    {
                        entryList.Add(entry);
                    }
                }
            }

            var bgEntry = UmaViewerMain.Instance.AbSounds.FirstOrDefault(a => a.Name.Contains(string.Format(SONG_PATH, songid)) && a.Name.EndsWith("awb"));
            if (bgEntry != null)
            {
                entryList.Add(bgEntry);
            }
            return entryList;
        }
        public static List<UmaDatabaseEntry> GetLivePreloadEntries(
            LiveEntry live,
            List<LiveCharacterLoadData> characters,
            bool requireStage)
        {
            var result = new List<UmaDatabaseEntry>();
            if (live == null)
                return result;

            result.AddRange(GetLiveAllVoiceEntry(live.MusicId, characters));

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return result;

            void AddByKey(string key)
            {
                if (main.AbList.TryGetValue(key, out var entry) && entry != null)
                    result.Add(entry);
            }

            // Cutt 和歌曲 part 也提前加载，避免进入场景后同步卡顿。
            AddByKey(string.Format(CUTT_PATH, live.MusicId));
            AddByKey(string.Format(LIVE_PART_PATH, live.MusicId));

            if (requireStage && !string.IsNullOrEmpty(live.BackGroundId))
            {
                string folderPrefix = $"3d/env/live/live{live.BackGroundId}/";

                // 保留该舞台目录下全部 AssetBundle，不删 laser、light、monitor 等任何资源。
                foreach (var kv in main.AbList)
                {
                    UmaDatabaseEntry entry = kv.Value;
                    if (entry == null || !entry.IsAssetBundle)
                        continue;

                    bool keyMatches = kv.Key.StartsWith(
                        folderPrefix,
                        StringComparison.OrdinalIgnoreCase);

                    bool nameMatches = entry.Name != null && entry.Name.StartsWith(
                        folderPrefix,
                        StringComparison.OrdinalIgnoreCase);

                    if (keyMatches || nameMatches)
                        result.Add(entry);
                }
            }

            return result
                .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private void PreloadStageBundlesBeforeInstantiate(string bgId)
        {
            if (string.IsNullOrEmpty(bgId))
                return;

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return;

            string folderPrefix = $"3d/env/live/live{bgId}/";
            var required = new List<UmaDatabaseEntry>();

            foreach (var kv in main.AbList)
            {
                UmaDatabaseEntry entry = kv.Value;
                if (entry == null || !entry.IsAssetBundle)
                    continue;

                bool keyMatches = kv.Key.StartsWith(
                    folderPrefix,
                    StringComparison.OrdinalIgnoreCase);

                bool nameMatches = entry.Name != null && entry.Name.StartsWith(
                    folderPrefix,
                    StringComparison.OrdinalIgnoreCase);

                if (keyMatches || nameMatches)
                    required.AddRange(UmaAssetManager.SearchAB(main, entry));
            }

            // 兜底路径也只做“新增加载”，不调用任何 Unload；依赖去重后每个只处理一次。
            foreach (UmaDatabaseEntry entry in required
                         .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                         .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                         .Select(g => g.First()))
            {
                UmaAssetManager.LoadAssetBundle(
                    entry,
                    neverUnload: false,
                    isRecursive: false);
            }

            Debug.Log($"[StagePreloadFallback] bgId={bgId}, bundles={required.Count}");
        }
        private void InitializeMirrorReflections()
        {
            if (!_enableMirrorReflection)
                return;

            _mirrorReflections.Clear();

            AddMirrorReflections(_mirrorReflections, GetComponentsInChildren<MirrorReflection>(true));

            if (_stageController != null)
                AddMirrorReflections(_mirrorReflections, _stageController.GetComponentsInChildren<MirrorReflection>(true));

            if (_mirrorReflections.Count == 0)
                AddMirrorReflections(_mirrorReflections, FindObjectsOfType<MirrorReflection>(true));

            if (_mirrorReflections.Count == 0)
            {
                Debug.Log("[Mirror] No MirrorReflection found.");
                return;
            }

            Camera mainCam = null;
            if (_cameraObjects != null && _activeCameraIndex >= 0 && _activeCameraIndex < _cameraObjects.Length)
                mainCam = _cameraObjects[_activeCameraIndex];

            if (mainCam == null)
                mainCam = Camera.main;

            for (int i = 0; i < _mirrorReflections.Count; i++)
            {
                var mirror = _mirrorReflections[i];
                if (mirror == null) continue;

                mirror.Initialize(mainCam, i, false);
                mirror.SetupBaseCamera(mainCam, GetMainCameraFovFactor);
            }

            Debug.Log($"[Mirror] Initialized {_mirrorReflections.Count} mirrors.");
        }

        private static void AddMirrorReflections(List<MirrorReflection> target, MirrorReflection[] mirrors)
        {
            if (target == null || mirrors == null)
                return;

            for (int i = 0; i < mirrors.Length; i++)
            {
                var mirror = mirrors[i];
                if (mirror == null || target.Contains(mirror))
                    continue;

                target.Add(mirror);
            }
        }

        private float GetMainCameraFovFactor()
        {
            return 1f;
        }

        private void UpdateMirrorReflections()
        {
            if (_mirrorReflections == null || _mirrorReflections.Count == 0)
                return;

            Camera mainCam = null;
            if (_cameraObjects != null && _activeCameraIndex >= 0 && _activeCameraIndex < _cameraObjects.Length)
                mainCam = _cameraObjects[_activeCameraIndex];

            if (mainCam == null)
                mainCam = Camera.main;

            for (int i = 0; i < _mirrorReflections.Count; i++)
            {
                var mirror = _mirrorReflections[i];
                if (mirror == null) continue;

                mirror.SetBaseCamera(mainCam);
                mirror.SetFovFactorGetter(GetMainCameraFovFactor);
                mirror.UpdateMirrorParams();
                mirror.ForceRenderOnce();
            }
        }
        private GallopImageEffect GetActivePostEffect()
        {
            if (_mainGallopImageEffect != null)
                return _mainGallopImageEffect;

            Camera mainCamera = null;

            if (_cameraObjects != null &&
                _activeCameraIndex >= 0 &&
                _activeCameraIndex < _cameraObjects.Length)
            {
                mainCamera = _cameraObjects[_activeCameraIndex];
            }

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
                return null;

            _mainGallopImageEffect =
                mainCamera.GetComponent<GallopImageEffect>();

            if (_mainGallopImageEffect == null)
            {
                _mainGallopImageEffect =
                    mainCamera.gameObject
                        .AddComponent<GallopImageEffect>();
            }

            // Post-FX only runs if the camera renders post-processing.
            mainCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            return _mainGallopImageEffect;
        }
        private void OnUpdatePostEffect_BloomDiffusion(PostEffectUpdateInfo_BloomDiffusion updateInfo)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            

            if (imageEffect == null) return;

            DofDiffusionBloomOverlayParam param =
                imageEffect.DofDiffusionBloomOverlayParam;

            param.IsEnableBloom =
                updateInfo.IsEnabledBloom;

            param.BloomDofWeight =
                updateInfo.bloomDofWeight;

            param.BloomThreshold =
                updateInfo.threshold;

            param.BloomIntensity =
                updateInfo.intensity;

            param.BloomBlurSize =
                updateInfo.BloomBlurSize;

            param.BloomBlendMode =
                updateInfo.BloomBlendMode;

            param.IsEnableDiffusion =
                updateInfo.IsEnabledDiffusion;

            param.DiffusionBlurSize =
                updateInfo.diffusionBlurSize;

            param.DiffusionBright =
                updateInfo.diffusionBright;

            param.DiffusionThreshold =
                updateInfo.diffusionThreshold;

            param.DiffusionSaturation =
                updateInfo.diffusionSaturation;

            param.DiffusionContrast =
                updateInfo.diffusionContrast;
        }

        private void OnUpdateHdrBloom(ref HdrBloomUpdateInfo updateInfo)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null) return;

            DofDiffusionBloomOverlayParam param =
                imageEffect.DofDiffusionBloomOverlayParam;

            if (updateInfo.enable)
            {
                param.BloomIntensity =
                    Mathf.Min(6f, Mathf.Max(param.BloomIntensity, updateInfo.intensity * 0.7f));
                param.BloomBlurSize =
                    Mathf.Max(param.BloomBlurSize, Mathf.Clamp(updateInfo.blurSpread * 1.5f, 0f, 8f));
                param.IsEnableBloom = true;
            }
        }

        // no valid key at this frame means the timeline holds nothing: leave the dof
        // parameters at their current values instead of forcing defaults.
        private void OnUpdatePostEffect_DOF(PostEffectUpdateInfo_DOF updateInfo)
        {
            if (!updateInfo.isValid) return;

            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null) return;

            imageEffect.DepthOfFieldEnabled = true;
            bool dofOn = updateInfo.dofBlurType != DofDiffusionBloomOverlayParam.DofDiffusionBloomType.None;
            imageEffect.DofDiffusionBloomOverlayParam.IsEnableBloom = dofOn;
            imageEffect.DofDiffusionBloomOverlayParam.BloomDofWeight = Mathf.Clamp01(updateInfo.forcalSize / 10f);
            imageEffect.DofDiffusionBloomOverlayParam.BloomThreshold =
                Mathf.Clamp(updateInfo.BallBlurBrightnessThreshhold, 0f, 4f);
            imageEffect.DofDiffusionBloomOverlayParam.BloomIntensity =
                Mathf.Clamp(updateInfo.BallBlurBrightnessIntensity, 0f, 8f);
            imageEffect.DofDiffusionBloomOverlayParam.BloomBlurSize =
                Mathf.Clamp(updateInfo.BallBlurSpread / 10f, 0f, 10f);
        }

        private void OnUpdateRadialBlur(RadialBlurUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            ApplyRadialBlur(updateInfo.radialBlurPower, updateInfo.radialBlurStartArea);
        }

        private void OnUpdateTiltShift(TiltShiftUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            ApplyTiltShift(updateInfo.blurArea, updateInfo.maxBlurSize, updateInfo.roll);
        }

        private void OnUpdateFade(FadeUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            ApplyFadeColor(updateInfo.fadeColor);
        }

        private void OnUpdateFluctuation(FluctuationUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid || !updateInfo.IsEnable) return;
            ApplyRadialBlur(updateInfo.MovePower * 4f, 0.25f);
        }

        private void OnUpdateVortex(VortexUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid || !updateInfo.IsEnable) return;
            ApplyTiltShift(6f, updateInfo.RotVolume * 4f, 0f);
        }


        // the game composites up to three PostFilm layers; the strongest active
        // layer drives the volume tint this frame.
        private void OnUpdatePostFilm(
            LiveTimelineKeyPostFilmData data,
            ref PostFilmUpdateInfo updateInfo,
            float currentLiveTime)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null)
                return;

            var mode = data.filmMode;
            if (mode == PostFilmMode.None)
            {
                imageEffect.ClearTimelineFilm();
                return;
            }

            bool isVignette =
                mode == PostFilmMode.VignetteLerp ||
                mode == PostFilmMode.VignetteAdd ||
                mode == PostFilmMode.VignetteMul;

            // prefer the strongest powered layer seen this frame; Director's
            // per-frame reset happens in ClearFrameFilmState below.
            float power = Mathf.Clamp01(updateInfo.filmPower);
            if (power > _filmBestPower)
            {
                _filmBestPower = power;
                imageEffect.ApplyTimelineFilm(updateInfo.color0, power, isVignette);
            }
        }

        private float _filmBestPower = -1f;

        private void OnUpdateHandShakeCamera(HandShakeCameraUpdateInfo updateInfo)
        {
            // camera shake disabled by request; the keys still evaluate but apply nothing.
        }

        private void OnUpdateProps(PropsUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            StagePropsDriver.ApplyPropsColor(updateInfo.color, updateInfo.rendererEnable);
        }

        private void OnUpdatePropsAttach(PropsAttachUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            LivePropsEvaluator.AttachToJoint(updateInfo._attachJointName, updateInfo._offsetPosition);
        }

        private void OnUpdateSpotlight3d(Spotlight3dUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            ApplySpotlight3d(updateInfo.assetName, updateInfo);
        }

        private void OnDestroy()
        {
            UnbindTimelineEvents();

            if (_instance == this)
                _instance = null;
        }
        private void UnbindTimelineEvents()
        {
            if (_liveTimelineControl == null)
                return;

            _liveTimelineControl.OnUpdatePostEffect_BloomDiffusion -=
                OnUpdatePostEffect_BloomDiffusion;

            _liveTimelineControl.OnUpdateHdrBloom -=
                OnUpdateHdrBloom;

            _liveTimelineControl.OnUpdatePostEffect_DOF -= OnUpdatePostEffect_DOF;
            _liveTimelineControl.OnUpdateRadialBlur -= OnUpdateRadialBlur;
            _liveTimelineControl.OnUpdateTiltShift -= OnUpdateTiltShift;
            _liveTimelineControl.OnUpdateFade -= OnUpdateFade;
            _liveTimelineControl.OnUpdateFluctuation -= OnUpdateFluctuation;
            _liveTimelineControl.OnUpdateVortex -= OnUpdateVortex;
            _liveTimelineControl.OnUpdateHandShakeCamera -= OnUpdateHandShakeCamera;
            _liveTimelineControl.OnUpdateProps -= OnUpdateProps;
            _liveTimelineControl.OnUpdatePropsAttach -= OnUpdatePropsAttach;
            _liveTimelineControl.OnUpdateSpotlight3d -= OnUpdateSpotlight3d;
            _liveTimelineControl.OnUpdatePostFilm -= OnUpdatePostFilm;
        }

        // radial blur keys drive the existing motion-blur volume override: power maps to
        // intensity, the start area to the clamp band; no radial-blur pass exists here.
        private void ApplyRadialBlur(float power, float startArea)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null) return;

            imageEffect.MotionBlurIntensity = Mathf.Clamp(power, 0f, 1f);
        }

        // tilt-shift keys have no dedicated pass: maxBlurSize folds into the bloom scatter
        // band and roll is dropped (a roll would need a second camera rotation pass).
        private void ApplyTiltShift(float blurArea, float maxBlurSize, float roll)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null) return;

            imageEffect.BloomScatterBoost = Mathf.Clamp01(maxBlurSize / 25f);
        }

        // fade keys set a fullscreen quad color in front of the camera; alpha 0 hides it.
        private void ApplyFadeColor(Color color)
        {
            Camera mainCamera = MainCameraTransform != null ? MainCameraTransform.GetComponent<Camera>() : null;
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            if (_fadeQuad == null)
            {
                _fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(_fadeQuad.GetComponent<Collider>());
                _fadeQuad.name = "TimelineFadeQuad";
                _fadeMaterial = new Material(Shader.Find("Sprites/Default"));
                _fadeQuad.GetComponent<MeshRenderer>().material = _fadeMaterial;
                _fadeQuad.transform.SetParent(mainCamera.transform, false);
                _fadeQuad.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                _fadeQuad.transform.localScale = new Vector3(10f, 10f, 1f);
            }

            if (_fadeMaterial != null)
            {
                _fadeMaterial.color = color;
                _fadeQuad.SetActive(color.a > 0.001f);
            }
        }

        // hand-shake keys drive a perlin-noise camera offset so the shake reads on the
        // real camera transform; applied per frame in LateUpdate.

        // resolve the spotlight3d entry by name: cut data names carry an editor ordinal
        // prefix ("1st : spotlight3d002"), strip it and match the StageObjectMap first,
        // then binder-instanced fixtures; log an unresolvable name once.
        private void ApplySpotlight3d(string entryName, Spotlight3dUpdateInfo updateInfo)
        {
            StageController stage = _stageController;
            if (stage == null)
                return;

            string bare = StripOrdinalPrefix(entryName);
            if (string.IsNullOrEmpty(bare))
                return;

            GameObject target = null;
            if (stage.StageObjectMap != null)
            {
                if (!stage.StageObjectMap.TryGetValue(bare, out target) &&
                    !stage.StageObjectMap.TryGetValue(entryName, out target))
                {
                    target = null;
                }
            }

            if (target == null && _spotlight3dInstances.TryGetValue(bare, out GameObject cached))
                target = cached;

            if (target == null)
            {
                if (!string.IsNullOrEmpty(updateInfo.assetName))
                {
                    // instance the spotlight fixture from its bundle on first use;
                    // keys carry bare asset names like "spotlight3d000".
                    target = InstanceSpotlightFixture(bare, updateInfo.assetName, stage);
                }
            }

            if (target == null)
            {
                if (_spotlightMissingLogged.Add(entryName))
                    Director.FileLog($"[spotlight3d] could not resolve spotlight entry '{entryName}'");
                return;
            }

            target.SetActive(updateInfo.isActive);
            if (updateInfo.isActive)
            {
                target.transform.localPosition = updateInfo.position;
                target.transform.localRotation = Quaternion.Euler(updateInfo.rotation);
                target.transform.localScale = updateInfo.scale;
            }
        }

        // loads the shared spotlight3d controller prefab once and clones it per
        // entry; clones live under the stage so the blink driver can find them.
        private GameObject InstanceSpotlightFixture(string entryKey, string assetName, StageController stage)
        {
            if (_spotlight3dInstances.TryGetValue(entryKey, out GameObject existing) && existing != null)
                return existing;

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return null;

            UmaDatabaseEntry entry = null;
            foreach (var kv in main.AbList)
            {
                string keyFile = System.IO.Path.GetFileName(kv.Key);
                if (string.Equals(keyFile, "pfb_env_live_cmn_spotlight3d_controller" + assetName.Substring("spotlight3d".Length), StringComparison.OrdinalIgnoreCase))
                {
                    entry = kv.Value;
                    break;
                }
            }

            if (entry == null)
                return null;

            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry, neverUnload: true, isRecursive: true);
            if (bundle == null)
                return null;

            GameObject prefab = bundle.LoadAsset<GameObject>("pfb_env_live_cmn_spotlight3d_controller" + assetName.Substring("spotlight3d".Length));
            if (prefab == null)
            {
                foreach (GameObject go in bundle.LoadAllAssets<GameObject>())
                {
                    if (go != null) { prefab = go; break; }
                }
            }

            if (prefab == null)
                return null;

            GameObject instance = Instantiate(prefab, stage.transform);
            instance.name = entryKey;
            if (stage.StageObjectMap != null)
                stage.StageObjectMap[entryKey] = instance;
            _spotlight3dInstances[entryKey] = instance;
            Director.FileLog($"[spotlight3d] instanced fixture '{entryKey}' for asset '{assetName}'");
            return instance;
        }

        // cut data entry names are editor display names like "1st : spotlight3d002";
        // strip the ordinal + separator so only the real key name remains.
        private static string StripOrdinalPrefix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            int idx = name.IndexOf(':');
            if (idx < 0)
                return name.Trim();

            string head = name.Substring(0, idx).Trim();
            if (head.Length == 0)
                return name.Trim();

            // only strip when the head is a plain ordinal token (1st / 2nd / 3rd / 4th)
            bool isOrdinal = head.EndsWith("st", StringComparison.Ordinal) ||
                             head.EndsWith("nd", StringComparison.Ordinal) ||
                             head.EndsWith("rd", StringComparison.Ordinal) ||
                             head.EndsWith("th", StringComparison.Ordinal);
            if (!isOrdinal)
                return name.Trim();

            string digits = head.Substring(0, head.Length - 2);
            if (!int.TryParse(digits, out _))
                return name.Trim();

            return name.Substring(idx + 1).Trim();
        }
    }

}
