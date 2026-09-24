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
        private bool _forceGameBloomOff;
        private bool _forceAuthoredPassesOff;
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
        private readonly Dictionary<GameObject, bool> _spotlightMaterialTinted = new Dictionary<GameObject, bool>();
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

                    // one-shot census: which timeline objectList names exist on
                    // the loaded stage, so missing fixtures get named in the log.
                    var objSheet = _liveTimelineControl.data?.worksheetList != null && _liveTimelineControl.data.worksheetList.Count > 0
                        ? _liveTimelineControl.data.worksheetList[0].objectList
                        : null;
                    if (objSheet != null && _stageController.StageObjectMap != null)
                    {
                        int stageFound = 0, stageAbsent = 0;
                        foreach (var entry in objSheet)
                        {
                            if (entry == null || string.IsNullOrEmpty(entry.name))
                                continue;
                            if (_stageController.StageObjectMap.ContainsKey(entry.name))
                                stageFound++;
                            else
                            {
                                stageAbsent++;
                                Director.FileLog($"[stageobj] timeline object '{entry.name}' missing from stage map");
                            }
                        }
                        Director.FileLog($"[stageobj] census: objects={stageFound + stageAbsent} found={stageFound} missing={stageAbsent}");
                    }
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

            // effect groups register at sheet load so their bundles can be preloaded,
            // mirroring the game's RegisterEffectResource pass.
            if (_liveTimelineControl.data.worksheetList != null)
            {
                foreach (var sheet in _liveTimelineControl.data.worksheetList)
                    _liveTimelineControl.RegisterSheetEffects(sheet);
            }
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
                // the game re-applies the light state every frame so any competing
                // writer (blink driver, wash, prefabs) loses to the authored track.
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
                // bg-color keys are the authored authority for character tint, so the
                // walk re-applies every frame exactly like the game does.
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
            _liveTimelineControl.OnUpdateToneCurve += OnUpdateToneCurve;
            _liveTimelineControl.OnUpdateLensDistortion += OnUpdateLensDistortion;
            _liveTimelineControl.OnUpdateTransmittedLight += OnUpdateTransmittedLight;
            _liveTimelineControl.OnUpdateVoice += OnUpdateVoice;
            _liveTimelineControl.OnUpdateCharaParts += OnUpdateCharaParts;
            _liveTimelineControl.OnUpdateCharaFootLight += OnUpdateCharaFootLight;
            _liveTimelineControl.OnUpdateFacialToon += OnUpdateFacialToon;
            _liveTimelineControl.OnUpdateCameraMotion += OnUpdateCameraMotion;
            _liveTimelineControl.OnUpdateCharaWind += OnUpdateCharaWind;
            _liveTimelineControl.OnUpdateFlashPlayer += OnUpdateFlashPlayer;
            _liveTimelineControl.OnUpdateAdditionalLight += OnUpdateAdditionalLight;
            _liveTimelineControl.OnUpdateCharaNode += OnUpdateCharaNode;
            _liveTimelineControl.OnUpdateTransparentCamera += OnUpdateTransparentCamera;
            _liveTimelineControl.OnSheetEffectRegistered += OnSheetEffectRegistered;
            _liveTimelineControl.OnUpdateTiltShift += OnUpdateTiltShift;
            _liveTimelineControl.OnUpdateFade += OnUpdateFade;
            _liveTimelineControl.OnUpdateFluctuation += OnUpdateFluctuation;
            _liveTimelineControl.OnUpdateVortex += OnUpdateVortex;
            _liveTimelineControl.OnUpdateHandShakeCamera += OnUpdateHandShakeCamera;
            _liveTimelineControl.OnUpdateProps += OnUpdateProps;
            _liveTimelineControl.OnUpdatePropsAttach += OnUpdatePropsAttach;
            _liveTimelineControl.OnUpdateSpotlight3d += OnUpdateSpotlight3d;
            _liveTimelineControl.OnUpdatePostFilm += OnUpdatePostFilm;
            _liveTimelineControl.OnUpdateStageGrade += OnUpdateStageGrade;
            _liveTimelineControl.OnUpdateExposure += OnUpdateExposureGain;
            _liveTimelineControl.OnUpdateGlobalFog += OnUpdateGlobalFog;
            _liveTimelineControl.OnUpdateVolumeLight += OnUpdateVolumeLight;
            _liveTimelineControl.OnUpdateChromaticAberration += OnUpdateChromaticAberration;


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
            SectionProfiler.Begin("timeline.eval");
            _liveTimelineControl.AlterUpdate(_liveCurrentTime);
            SectionProfiler.End();
            if (!_soloMode)
            {
                SectionProfiler.Begin("audio.parts");
                UmaViewerAudio.AlterUpdate(_liveCurrentTime, partInfo, liveVocal, sliderControl.is_Outed);
                SectionProfiler.End();
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

                // F8 flips between the game's FastBloom shader and the URP volume bloom.
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    var fx = GetActivePostEffect();
                    fx?.ToggleGameBloom();
                }

                if (Input.GetKeyDown(KeyCode.F9))
                {
                    ToggleGallopWinParity();
                }

                // f10 force-kills the game bloom pass so a black screen can be
                // attributed to the composite in one keystroke.
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    _forceGameBloomOff = !_forceGameBloomOff;
                    Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.ForceDisabled = _forceGameBloomOff;
                    FileLog($"[killswitch] game bloom {(_forceGameBloomOff ? "forced off" : "restored")}");
                }

                // f11 force-kills the tone curve, lens distortion and transmitted
                // light passes to bisect a black screen between them and the bloom.
                if (Input.GetKeyDown(KeyCode.F11))
                {
                    _forceAuthoredPassesOff = !_forceAuthoredPassesOff;
                    Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.ForceDisabled = _forceAuthoredPassesOff;
                    Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.ForceDisabled = _forceAuthoredPassesOff;
                    Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.ForceDisabled = _forceAuthoredPassesOff;
                    FileLog($"[killswitch] authored passes {(_forceAuthoredPassesOff ? "forced off" : "restored")}");
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

                SectionProfiler.Begin("director.camera");
                UpdateMainCamera();

                // 时间轴和主相机都更新完后再同步 Laser Renderer/朝向。
                // 这样既不会读取上一帧 LaserUpdateInfo，也不会读取上一帧相机姿态。
                if (_stageController != null)
                    _stageController.AlterUpdateLaserControllers();
                SectionProfiler.End();
            }
        }

        private void LateUpdate()
        {
            SectionProfiler.Begin("director.lateupdate");
            if (_isLiveSetup && _syncTime && !IsRecordVMD)
            {
                ApplyTimelineLateUpdate();
            }

            if (_enableMirrorReflection && _mirrorRenderInLateUpdate)
            {
                UpdateMirrorReflections();
            }

            SectionProfiler.End();
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
        // public accessor so stage drivers can steer the post stack the director owns.
        public GallopImageEffect GetActivePostEffectPublic() => GetActivePostEffect();

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
            // the game's bloom composite samples _CameraDepthTexture to weight the
            // bloom by distance; without a depth texture the sample reads black and
            // the composite dims the whole screen to black.
            mainCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;

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

            // dof keys drive the focus plane only; the dedicated bloom track owns
            // the bloom parameters so the two tracks stop stomping each other.
            imageEffect.DepthOfFieldEnabled = true;

            bool dofOn = updateInfo.dofBlurType == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DofBloom ||
                         updateInfo.dofBlurType == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionDofBloom ||
                         updateInfo.dofBlurType == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.Dof;
            imageEffect.DepthOfFieldActive = dofOn;

            // authored focus: charactor=1 locks focus to the character slot's
            // authored focal distance; otherwise dofFocalPoint is an absolute
            // plane distance from the camera.
            float authoredDistance = Mathf.Max(0.1f, updateInfo.dofFocalPoint);
            imageEffect.SetTimelineFocusSpread(Mathf.Max(0.05f, updateInfo.blurSpread));
            imageEffect.SetTimelineFocus(
                authoredDistance,
                Mathf.Max(0f, updateInfo.forcalSize),
                updateInfo.charactor == 1);
        }

        private void OnUpdateRadialBlur(RadialBlurUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            ApplyRadialBlur(updateInfo.radialBlurPower, updateInfo.radialBlurStartArea);
        }

        // map a chara slot index (center/left1/right1/...) to the loaded character
        // container the same way the facial handler resolves positions.
        private UmaContainerCharacter ResolveCharacterBySlot(int slot)
        {
            if (slot < 0 || slot >= CharaContainerScript.Count)
                return null;
            return CharaContainerScript[slot];
        }

        // toggle every renderer whose object name matches the authored part name.
        private void ApplyRendererVisible(UmaContainerCharacter character, string partName, bool visible)
        {
            if (character == null || string.IsNullOrEmpty(partName))
                return;
            var root = character.Body != null ? character.Body.transform
                : character.Head != null ? character.Head.transform
                : character.transform;
            foreach (var ren in root.GetComponentsInChildren<Renderer>(true))
            {
                if (ren.name == partName || ren.transform.name == partName)
                    ren.enabled = visible;
            }
        }

        // the game maintains a per-character foot light driven by the chara foot light
        // track; the viewer owns a small spotlight child per character.
        private readonly Dictionary<int, Light> _charaFootLights = new Dictionary<int, Light>();

        private void OnUpdateCharaFootLight(CharaFootLightUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            int index = updateInfo.CharacterIndex;
            if (index < 0 || index >= CharaContainerScript.Count)
                return;
            var container = CharaContainerScript[index];

            if (!_charaFootLights.TryGetValue(index, out var light) || light == null)
            {
                var host = new GameObject("TimelineCharaFootLight");
                host.transform.SetParent(container.transform, false);
                light = host.AddComponent<Light>();
                light.type = LightType.Spot;
                light.shadows = LightShadows.None;
                light.spotAngle = 70f;
                light.range = 3f;
                _charaFootLights[index] = light;
                FileLog($"[footlight] spawned foot light for chara {index}");
            }

            light.color = updateInfo.lightColor;
            light.intensity = Mathf.Clamp01(updateInfo.hightMax) * 2f;
            light.enabled = updateInfo.hightMax > 0.001f;
        }

        // facial toon parameters ride per-character material state; the viewer applies
        // them to the face renderers through a property block.
        private void OnUpdateFacialToon(int slot, FacialToonUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid || slot < 0 || slot >= CharaContainerScript.Count)
                return;
            var container = CharaContainerScript[slot];
            var faceRoot = container.Head != null ? container.Head.transform : container.transform;
            foreach (var ren in faceRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mpb = new MaterialPropertyBlock();
                ren.GetPropertyBlock(mpb);
                mpb.SetFloat(Shader.PropertyToID("_CheekPretenseThreshold"), updateInfo.CheekPretenseThreshold);
                mpb.SetFloat(Shader.PropertyToID("_NosePretenseThreshold"), updateInfo.NosePretenseThreshold);
                mpb.SetFloat(Shader.PropertyToID("_CylinderBlend"), updateInfo.CylinderBlend);
                mpb.SetFloat(Shader.PropertyToID("_HairNormalBlend"), updateInfo.HairNormalBlend);
                mpb.SetFloat(Shader.PropertyToID("_EyeToonStep"), updateInfo.EyeToonStep);
                mpb.SetFloat(Shader.PropertyToID("_EyeToonFeather"), updateInfo.EyeToonFeather);
                mpb.SetFloat(Shader.PropertyToID("_EyeSaturation"), updateInfo.EyeSaturation);
                if (updateInfo.UseOriginalDirectionalLight != 0)
                    mpb.SetVector(Shader.PropertyToID("_OriginalDirectionalLightDir"), updateInfo.OriginalDirectionalLightDir);
                ren.SetPropertyBlock(mpb);
            }
        }

        // authored camera AnimationClips: sample on a proxy and copy onto the active
        // camera at the game's late-update phase so pos keys layer on top.
        private GameObject _cameraMotionProxy;
        private bool _cameraMotionLogged;

        private void OnUpdateCameraMotion(LiveTimelineKeyCameraMotionData key, float clipTime)
        {
            if (key == null || key.Clip == null || !key.IsEnable)
                return;

            if (!_cameraMotionLogged)
            {
                _cameraMotionLogged = true;
                FileLog($"[cameramotion] playing clip '{key.Clip.name}' motionType={key.MotionType} speed={key.PlaySpeed}");
            }

            if (_cameraMotionProxy == null)
                _cameraMotionProxy = new GameObject("TimelineCameraMotionProxy");

            key.Clip.SampleAnimation(_cameraMotionProxy, clipTime);

            var cam = MainCameraTransform != null ? MainCameraTransform : _cameraMotionProxy.transform;
            var target = Camera.main;
            if (target == null)
                return;
            target.transform.SetPositionAndRotation(_cameraMotionProxy.transform.position + key.Offset,
                _cameraMotionProxy.transform.rotation);
        }

        // authored wind keys feed the cloth solver; the viewer drives the same dummy-wind
        // entry the physics settings panel uses, per character.
        private void OnUpdateCharaWind(int slot, LiveTimelineKeyCharaWindData key)
        {
            if (key == null || slot < 0 || slot >= CharaContainerScript.Count)
                return;
            var container = CharaContainerScript[slot];
            foreach (var cyspring in container.GetComponentsInChildren<Gallop.CySpringController>(true))
            {
                cyspring.SetEnableCySpringDummyWind(key.IsEnableWind, key.WindParam != null ? key.WindParam.Direction : Vector3.forward);
            }
        }

        private void OnUpdateFlashPlayer(LiveTimelineKeyFlashPlayerData key)
        {
            if (key == null)
                return;
            // the flash legend controller shows the authored audience flash bursts.
            var flash = UnityEngine.Object.FindFirstObjectByType<LiveFlashController>();
            if (flash != null && key.UseActionLabel && !string.IsNullOrEmpty(key.ActionLabel))
                flash.PlayLabel(key.ActionLabel, _liveTimelineControl != null ? _liveTimelineControl.currentLiveTime : 0f, key);
            else
                FileLog($"[flash] cue sheet={key.CueSheetName} cue={key.CueName} action={key._actionType}");
        }

        // the game keeps authored extra stage lights per named group; the viewer owns
        // a matching light object per group index.
        private readonly Dictionary<int, Light> _additionalLights = new Dictionary<int, Light>();

        private void OnUpdateAdditionalLight(AdditionalLightUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;

            if (!_additionalLights.TryGetValue(updateInfo.Index, out var light) || light == null)
            {
                var host = new GameObject($"TimelineAdditionalLight{updateInfo.Index}");
                light = host.AddComponent<Light>();
                light.shadows = LightShadows.None;
                _additionalLights[updateInfo.Index] = light;
            }

            light.transform.SetPositionAndRotation(updateInfo.Position, Quaternion.Euler(updateInfo.Rotate));
            light.type = updateInfo.Type;
            light.range = updateInfo.Range;
            light.spotAngle = updateInfo.SpotAngle;
            light.bounceIntensity = updateInfo.IndirectMultiplier;
            light.intensity = updateInfo.Strength;
            light.enabled = updateInfo.IsEnable;
        }

        private void OnUpdateCharaNode(int group, LiveTimelineKeyCharaNodeData key)
        {
            if (key == null)
                return;
            // chara node keys toggle the per-part cloth solvers on the character.
            int index = PositionFlagToCharaIndex(key.PositionFlag);
            if (index < 0 || index >= CharaContainerScript.Count)
                return;
            var container = CharaContainerScript[index];
            foreach (var cyspring in container.GetComponentsInChildren<Gallop.CySpringController>(true))
            {
                if (key.EnableHeadCySpring || key.EnableEarCySpring || key.EnableBodyCySpring)
                {
                    cyspring.SetForceDisableHipMoveParam(!key.EnableBodyCySpring);
                }
            }
        }

        private void OnUpdateTransparentCamera(LiveTimelineKeyTransparentCameraData key)
        {
            if (key == null || !key.Enable)
                return;
            if (!_transparentCameraLogged)
            {
                _transparentCameraLogged = true;
                FileLog($"[transparentcamera] enabled ortho={key.IsOrthographic} size={key.OrthographicSize}");
            }
        }

        private bool _transparentCameraLogged;

        private void OnSheetEffectRegistered(LiveTimelineEffectData effect)
        {
            if (effect == null)
                return;
            if (_sheetEffectLog.Add(effect._folder))
                FileLog($"[effect] registered folder={effect._folder} variation={effect._variationId} apply={effect._applyVariation}");
        }

        private readonly HashSet<string> _sheetEffectLog = new HashSet<string>();

        // map a position flag to the character index the same way the timeline does.
        private static int PositionFlagToCharaIndex(LiveCharaPositionFlag flag)
        {
            ulong bits = (ulong)flag;
            for (int i = 0; i < 64; i++)
            {
                if ((bits & (1ul << i)) != 0)
                    return i;
            }
            return -1;
        }

        private void OnUpdateToneCurve(ToneCurveUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            // the game gates the pass on the authored enable flag.
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.Enabled = updateInfo.IsEnable;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.IsEnable = updateInfo.IsEnable;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.ToneCurve = updateInfo.ToneAnimationCurve;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.MaskToneCurve = updateInfo.MaskToneCurve;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.MinCorrectionLevel = updateInfo.MinCorrectionLevel;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.MaxCorrectionLevel = updateInfo.MaxCorrectionLevel;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.MaskMinCorrectionLevel = updateInfo.MaskMinCorrectionLevel;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.MaskMaxCorrectionLevel = updateInfo.MaskMaxCorrectionLevel;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.DepthMask = updateInfo.DepthMask;
        }

        private void OnUpdateLensDistortion(LensDistortionUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            // zero intensity is the authored off state; the shader would still run.
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.Enabled =
                Mathf.Abs(updateInfo.Intensity) > 0.001f;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.Intensity = updateInfo.Intensity;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.IntensityX = updateInfo.IntensityX;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.IntensityY = updateInfo.IntensityY;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.CenterX = updateInfo.CenterX;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.CenterY = updateInfo.CenterY;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.Scale = updateInfo.Scale;
        }

        private void OnUpdateTransmittedLight(TransmittedLightUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.Enabled = updateInfo.IsEnabled;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.IsEnabled = updateInfo.IsEnabled;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.Iterations = updateInfo.Iterations;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.Intensity = updateInfo.Intensity;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.Threshold = updateInfo.Threshold;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.BlurSpread = updateInfo.BlurSpread;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.BlendMode = updateInfo.BlendMode;
        }

        private void OnUpdateVoice(VoiceUpdateInfo updateInfo)
        {
            if (!updateInfo.isValid) return;
            FileLog($"[voice] cue={updateInfo.CueId} at t={updateInfo.Time:F2}s");
        }

        // costume part visibility per character slot; the game toggles the named
        // renderers on the resolved character model.
        private void OnUpdateCharaParts(int slot, LiveTimelineKeyCharaPartsData key)
        {
            if (key == null || key.rendererNames == null)
                return;
            var character = ResolveCharacterBySlot(slot);
            if (character == null)
                return;
            for (int i = 0; i < key.rendererNames.Count; i++)
            {
                bool visible = i < key.rendererVisibles.Count ? key.rendererVisibles[i] : true;
                ApplyRendererVisible(character, key.rendererNames[i], visible);
            }
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


        // lens fringe from the chromatic aberration track; URP override does the
        // actual fringe, we only feed the authored strength.
        private void OnUpdateChromaticAberration(float power)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null)
                return;

            imageEffect.SetChromaticAberration(Mathf.Clamp01(power * 0.05f));
        }

        // sun-shaft glow approximated as a bounded bloom lift in the shaft color;
        // a dedicated light-shaft pass can replace this mapping later.
        private void OnUpdateVolumeLight(float power, Color color)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null)
                return;

            // store the shaft lift per frame (set, never accumulate) and let the
            // bloom track's own handler own the base intensity.
            imageEffect.SetVolumeLightBloomLift(Mathf.Min(power * 0.02f, 1.2f));
            imageEffect.SetVolumeLightTint(color);
        }


        // authored stage grade (exposure/colorCorrection saturation) drives the
        // volume's saturation so dark/bright scenes track the game's grade.
        private void OnUpdateStageGrade(float saturation)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null)
                return;

            float remapped = Mathf.Clamp((saturation - 1f) * 100f, -100f, 100f);
            imageEffect.SetTimelineStageSaturation(remapped);
        }

        // authored exposure gain (stops) straight into the color adjust.
        private void OnUpdateExposureGain(float gain)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            if (imageEffect == null)
                return;

            imageEffect.SetTimelineExposure(Mathf.Clamp(gain, -3f, 3f));
        }

        private bool _globalFogLogged;

        // authored global fog mapped onto the engine fog; the game applies its fog as a
        // camera effect but the authored values (mode/color/density/range) are honored
        // as-is, and the first application logs so a washed-out frame is traceable.
        // the game's stage shaders read a dedicated global fog block (the shader
        // property table's first entries), so the authored fog drives both the
        // built-in fog and the _Global_ constants its own materials expect.
        private static readonly int GlobalFogColorId = Shader.PropertyToID("_Global_FogColor");
        private static readonly int GlobalFogMinDistanceId = Shader.PropertyToID("_Global_FogMinDistance");
        private static readonly int GlobalFogLengthId = Shader.PropertyToID("_Global_FogLength");
        private static readonly int GlobalMaxDensityId = Shader.PropertyToID("_Global_MaxDensity");
        private static readonly int GlobalMaxHeightId = Shader.PropertyToID("_Global_MaxHeight");
        private static readonly int GlobalFogWorldOriginId = Shader.PropertyToID("_Global_FogWorld_Origin");

        private void OnUpdateGlobalFog(ref GlobalFogUpdateInfo info)
        {
            bool on = info.isDistance || info.isHeight;
            RenderSettings.fog = on;

            Color fogColor = on ? info.color : Color.clear;
            Shader.SetGlobalColor(GlobalFogColorId, fogColor);
            Shader.SetGlobalFloat(GlobalFogMinDistanceId, on ? info.start : 100000f);
            Shader.SetGlobalFloat(GlobalFogLengthId, on ? Mathf.Max(0.01f, info.end - info.start) : 0f);
            Shader.SetGlobalFloat(GlobalMaxDensityId, on ? Mathf.Max(0.0001f, info.expDensity) : 0f);
            Shader.SetGlobalFloat(GlobalMaxHeightId, on ? 1000f : 0f);
            Shader.SetGlobalVector(GlobalFogWorldOriginId, on ? Vector3.zero : Vector3.zero);

            if (!on)
                return;

            RenderSettings.fogMode = (FogMode)Mathf.Clamp(info.fogMode, 1, 3);
            RenderSettings.fogColor = info.color;
            RenderSettings.fogDensity = Mathf.Max(0.0001f, info.expDensity);
            RenderSettings.fogStartDistance = info.start;
            RenderSettings.fogEndDistance = info.end;

            if (!_globalFogLogged)
            {
                _globalFogLogged = true;
                FileLog($"[fog] authored fog active: mode={info.fogMode} color={info.color} density={info.expDensity:F4} range={info.start}-{info.end}");
            }
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

            // the authored blend rides along with the strongest layer:
            // Add brightens toward the color, Mul darkens, Lerp mixes.
            GallopImageEffect.PostFilmBlend blend =
                mode == PostFilmMode.Add || mode == PostFilmMode.VignetteAdd
                    ? GallopImageEffect.PostFilmBlend.Add
                    : mode == PostFilmMode.Mul || mode == PostFilmMode.VignetteMul
                        ? GallopImageEffect.PostFilmBlend.Mul
                        : GallopImageEffect.PostFilmBlend.Lerp;

            // prefer the strongest powered layer seen this frame; Director's
            // per-frame reset happens in ClearFrameFilmState below.
            float power = Mathf.Clamp01(updateInfo.filmPower);
            if (power > _filmBestPower)
            {
                _filmBestPower = power;
                imageEffect.ApplyTimelineFilm(updateInfo.color0, power, isVignette, blend);
            }

            // the game's postbloom composite reads the whole postfilm global block
            // every frame; mirror the authored values so the bloom path sees the
            // same state the game's screen-overlay chain would have set.
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmPower = power;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmOffsetParam = updateInfo.filmOffsetParam;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmOptionParam = updateInfo.filmOptionParam;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmColor0 = updateInfo.color0;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmColor1 = updateInfo.color1;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmColor2 = updateInfo.color2;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmColor3 = updateInfo.color3;
            // the game's draw helper pushes the depth and film-shape globals every
            // frame from the same authored fields; mirror them so the composite sees
            // identical state.
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.DepthPower = updateInfo.depthPower;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.DepthClip = Mathf.Max(0f, updateInfo.DepthClip);
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmRollParameter = new Vector4(updateInfo.RollAngle, 1f, 0f, 1f);
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmScaleParameter = new Vector4(updateInfo.FilmScale.x, updateInfo.FilmScale.y, 0f, 0f);
            // the game reads the authored kAttr bits per key: 18 selects the alpha
            // mask subprograms, 19 the no-scale uv movie path, 20 the inverse
            // vignette composite variant.
            int attrBits = (int)data.attribute;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmIsAlphaMasking =
                (attrBits & 0x40000) != 0 ? 1f : 0f;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmIsUVMovieNoScale =
                (attrBits & 0x80000) != 0 ? 1f : 0f;
            bool inverseVignette = (attrBits & 0x100000) != 0;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.PostFilmIsInverseVignette =
                inverseVignette ? 1f : 0f;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.InverseVignette =
                inverseVignette;

            // the film layer can couple to named blink light containers; push the
            // authored brightness so stage lights pulse with the film.
            var blinkDriver = UnityEngine.Object.FindFirstObjectByType<StageBlinkLightDriver>();
            if (blinkDriver != null)
            {
                blinkDriver.ClearFilmCoupling();
                if (!string.IsNullOrEmpty(data.BlinkLightName) && data.BlinkLightBrightnessPower > 0f)
                    blinkDriver.SetFilmCoupling(data.BlinkLightName, data.BlinkLightBrightnessPower);
            }

            // uv-movie film keys are the game's way of playing clips on the stage
            // monitors; resolve the authored movie id against the monitor provider
            // so the overlay stage can find the clip texture.
            if (updateInfo.layerMode == LiveTimelineKeyPostFilmData.LayerMode.UVMovie && updateInfo.movieResId != 0)
            {
                if (!_filmMovieLogged)
                {
                    _filmMovieLogged = true;
                    FileLog($"[postfilm] uv-movie layer requested movieResId={updateInfo.movieResId}");
                }

                // drive the stage monitor movie slot with the authored movie id so
                // the on-stage screens play the clip the film layer names.
                if (_filmMovieProvider == null)
                    _filmMovieProvider = UnityEngine.Object.FindFirstObjectByType<MonitorUvMovieProvider>();
                if (_filmMovieProvider != null && _filmMovieResId != updateInfo.movieResId)
                {
                    _filmMovieResId = updateInfo.movieResId;
                    if (_filmMovieProvider.TryGetClipByDisplayId(updateInfo.movieResId, out var clip))
                        FileLog($"[postfilm] monitor movie resolved for resId={updateInfo.movieResId} name={clip.metadata?.Name}");
                    else
                        FileLog($"[postfilm] monitor movie clip not found for resId={updateInfo.movieResId}");
                }
            }
        }

        private bool _filmMovieLogged;
        private MonitorUvMovieProvider _filmMovieProvider;
        private int _filmMovieResId;

        private float _filmBestPower = -1f;

        // the game's gallop_win quality level runs shadows off with 8x msaa; this
        // a-b flips the live pipeline asset between the viewer defaults and that
        // parity preset, plus turning every scene light's shadows off.
        private bool _gallopWinParity;

        private void ToggleGallopWinParity()
        {
            _gallopWinParity = !_gallopWinParity;
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
                return;

            asset.msaaSampleCount = _gallopWinParity ? 8 : 0;
            asset.shadowDistance = _gallopWinParity ? 15f : 25f;
            var lightShadows = _gallopWinParity ? LightShadows.None : LightShadows.Soft;
            var sun = UnityEngine.RenderSettings.sun;
            if (sun != null)
                sun.shadows = lightShadows;
            foreach (var light in FindObjectsOfType<Light>())
            {
                if (light != sun)
                    light.shadows = lightShadows;
            }

            FileLog($"[parity] gallop_win preset {(_gallopWinParity ? "on (shadows off, msaa 8x, shadowdist 15)" : "off (viewer defaults)")}");
        }

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
            _fixtureLights.Clear();

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
            _liveTimelineControl.OnUpdateToneCurve -= OnUpdateToneCurve;
            _liveTimelineControl.OnUpdateLensDistortion -= OnUpdateLensDistortion;
            _liveTimelineControl.OnUpdateTransmittedLight -= OnUpdateTransmittedLight;
            _liveTimelineControl.OnUpdateVoice -= OnUpdateVoice;
            _liveTimelineControl.OnUpdateCharaParts -= OnUpdateCharaParts;
            _liveTimelineControl.OnUpdateCharaFootLight -= OnUpdateCharaFootLight;
            _liveTimelineControl.OnUpdateFacialToon -= OnUpdateFacialToon;
            _liveTimelineControl.OnUpdateCameraMotion -= OnUpdateCameraMotion;
            _liveTimelineControl.OnUpdateCharaWind -= OnUpdateCharaWind;
            _liveTimelineControl.OnUpdateFlashPlayer -= OnUpdateFlashPlayer;
            _liveTimelineControl.OnUpdateAdditionalLight -= OnUpdateAdditionalLight;
            _liveTimelineControl.OnUpdateCharaNode -= OnUpdateCharaNode;
            _liveTimelineControl.OnUpdateTransparentCamera -= OnUpdateTransparentCamera;
            _liveTimelineControl.OnSheetEffectRegistered -= OnSheetEffectRegistered;

            // the authored image-effect passes are statics; clear their armed state so
            // nothing from this live leaks onto the menu or the next live.
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.Enabled = false;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.IsEnable = false;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.Enabled = false;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.Enabled = false;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.IsEnabled = false;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.GameBloomEnabled = false;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.DiffusionEnabled = false;
            Gallop.RenderPipeline.GallopGameBloomFeature.GallopGameBloomPass.ForceDisabled = false;
            Gallop.RenderPipeline.GallopToneCurveFeature.GallopToneCurvePass.ForceDisabled = false;
            Gallop.RenderPipeline.GallopLensDistortionFeature.GallopLensDistortionPass.ForceDisabled = false;
            Gallop.RenderPipeline.GallopTransmittedLightFeature.GallopTransmittedLightPass.ForceDisabled = false;
            _forceGameBloomOff = false;
            _forceAuthoredPassesOff = false;
            _cameraMotionLogged = false;
            _isLiveSetup = false;
            _liveTimelineControl.OnUpdateTiltShift -= OnUpdateTiltShift;
            _liveTimelineControl.OnUpdateFade -= OnUpdateFade;
            _liveTimelineControl.OnUpdateFluctuation -= OnUpdateFluctuation;
            _liveTimelineControl.OnUpdateVortex -= OnUpdateVortex;
            _liveTimelineControl.OnUpdateHandShakeCamera -= OnUpdateHandShakeCamera;
            _liveTimelineControl.OnUpdateProps -= OnUpdateProps;
            _liveTimelineControl.OnUpdatePropsAttach -= OnUpdatePropsAttach;
            _liveTimelineControl.OnUpdateSpotlight3d -= OnUpdateSpotlight3d;
            _liveTimelineControl.OnUpdatePostFilm -= OnUpdatePostFilm;
            _liveTimelineControl.OnUpdateStageGrade -= OnUpdateStageGrade;
            _liveTimelineControl.OnUpdateExposure -= OnUpdateExposureGain;
            _liveTimelineControl.OnUpdateGlobalFog -= OnUpdateGlobalFog;
            _liveTimelineControl.OnUpdateVolumeLight -= OnUpdateVolumeLight;
            _liveTimelineControl.OnUpdateChromaticAberration -= OnUpdateChromaticAberration;
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
                // the fixture hangs at its authored height over the performer's
                // slot; characterPosition is the stage-space target the beam
                // points at, so put the fixture above it, not at raw position.
                Vector3 slot = new Vector3(
                    updateInfo.characterPosition.x,
                    0f,
                    updateInfo.characterPosition.z);
                Vector3 hang = updateInfo.position;
                if (target.transform.parent != stage.transform)
                    target.transform.SetParent(stage.transform, false);
                target.transform.localPosition = new Vector3(slot.x, hang.y, slot.z);

                // a real spotlight rides the fixture: beam from the hung head
                // down to the authored slot, in the track's color and power.
                DriveRealSpotlight(target, updateInfo, stage);

                // tint the fixture materials once per fixture (cached) so the
                // pool matches the authored color without per-frame material churn.
                if (!_spotlightMaterialTinted.TryGetValue(target, out bool tinted) || !tinted)
                {
                    Color poolColor = updateInfo.color;
                    foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
                    {
                        if (r == null)
                            continue;
                        Material mat = r.material;
                        if (mat == null)
                            continue;
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", poolColor * Mathf.Max(1f, updateInfo.colorPower));
                        }
                        if (mat.HasProperty("_Color"))
                            mat.SetColor("_Color", poolColor);
                    }
                    _spotlightMaterialTinted[target] = true;
                }
            }
        }

        // cache of the real Light per spotlight3d fixture so per-frame updates
        // only touch the light, never re-add components.
        private static readonly Dictionary<GameObject, Light> _fixtureLights =
            new Dictionary<GameObject, Light>();

        // ensures each fixture carries a Unity spotlight aimed at the authored
        // target slot; color/power follow the timeline track each active frame.
        private void DriveRealSpotlight(GameObject fixture, Spotlight3dUpdateInfo updateInfo, StageController stage)
        {
            if (fixture == null)
                return;

            if (!_fixtureLights.TryGetValue(fixture, out Light light) || light == null)
            {
                Transform head = fixture.transform;
                var lightGo = new GameObject("spotlight_beam");
                lightGo.transform.SetParent(head, false);
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 34f;
                light.range = 30f;
                light.intensity = 4f;
                light.shadows = LightShadows.None;
                _fixtureLights[fixture] = light;
            }

            Color c = updateInfo.color;
            c.a = 1f;
            light.color = c;
            light.intensity = Mathf.Max(1.2f, 4f * Mathf.Max(updateInfo.colorPower, 0.35f));

            // aim from the fixture head at the authored floor slot.
            Vector3 from = fixture.transform.position;
            Vector3 to = stage.transform.TransformPoint(
                new Vector3(updateInfo.characterPosition.x, 1.2f, updateInfo.characterPosition.z));
            Vector3 dir = to - from;
            if (dir.sqrMagnitude > 0.0001f)
                light.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
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
