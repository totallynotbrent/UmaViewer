using System;
using System.Collections.Generic;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// consumes the authored stage-effect tracks (lens flare, projector drive,
    /// particle emitters, particle-group flicker, light shafts, node scale, title)
    /// and applies them to the real stage objects each frame.
    /// </summary>
    public class StageLensFlareStageDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private StageController _stage;
        private bool _bound;

        private readonly Dictionary<string, Renderer[]> _flareRenderers = new Dictionary<string, Renderer[]>();
        private readonly Dictionary<string, ParticleSystem> _emitters = new Dictionary<string, ParticleSystem>();
        private readonly Dictionary<string, Renderer[]> _projectorRenderers = new Dictionary<string, Renderer[]>();
        private readonly Dictionary<string, ParticleSystem> _particleGroups = new Dictionary<string, ParticleSystem>();
        private bool _shaftsLogged;

        private void OnEnable()
        {
            BindIfPossible();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void BindIfPossible()
        {
            var dir = Director.instance;
            _ctl = dir ? dir._liveTimelineControl : null;
            _stage = dir ? dir._stageController : null;
            if (_ctl == null || _stage == null || _bound)
                return;

            _ctl.OnUpdateLensFlare += OnLensFlare;
            _ctl.OnUpdateProjector += OnProjector;
            _ctl.OnUpdateParticle += OnParticle;
            _ctl.OnUpdateParticleGroup += OnParticleGroup;
            _ctl.OnUpdateLightShafts += OnLightShafts;
            _ctl.OnUpdateNodeScale += OnNodeScale;
            _ctl.OnUpdateTitle += OnTitle;
            _ctl.OnUpdateFacialNoise += OnFacialNoise;
            _ctl.OnUpdateCharaMotionNoise += OnCharaMotionNoise;
            _ctl.OnUpdateSweatLocator += OnSweatLocator;
            _bound = true;
        }

        private void Unbind()
        {
            if (_ctl != null && _bound)
            {
                _ctl.OnUpdateLensFlare -= OnLensFlare;
                _ctl.OnUpdateProjector -= OnProjector;
                _ctl.OnUpdateParticle -= OnParticle;
                _ctl.OnUpdateParticleGroup -= OnParticleGroup;
                _ctl.OnUpdateLightShafts -= OnLightShafts;
                _ctl.OnUpdateNodeScale -= OnNodeScale;
                _ctl.OnUpdateTitle -= OnTitle;
                _ctl.OnUpdateFacialNoise -= OnFacialNoise;
                _ctl.OnUpdateCharaMotionNoise -= OnCharaMotionNoise;
                _ctl.OnUpdateSweatLocator -= OnSweatLocator;
            }
            _bound = false;
            _flareRenderers.Clear();
            _emitters.Clear();
            _projectorRenderers.Clear();
            _particleGroups.Clear();
        }

        private bool TryStageObject(string name, out GameObject go)
        {
            go = null;
            if (string.IsNullOrEmpty(name) || _stage == null || _stage.StageObjectMap == null)
                return false;
            if (_stage.StageObjectMap.TryGetValue(name, out go))
                return go != null;
            return false;
        }

        private Renderer[] GetRenderers(Dictionary<string, Renderer[]> cache, string name)
        {
            if (cache.TryGetValue(name, out var cached))
                return cached;
            if (!TryStageObject(name, out var go))
                return null;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            cache[name] = renderers;
            return renderers;
        }

        // authored flare track: enable drives the fixture's visibility, brightness and
        // color drive the emission tint on the flare renderers.
        private MaterialPropertyBlock _flareMpb;
        private static readonly int FlareEmissionColor = Shader.PropertyToID("_EmissionColor");

        private void OnLensFlare(ref LiveTimelineControl.LensFlareUpdateInfo updateInfo)
        {
            var renderers = GetRenderers(_flareRenderers, updateInfo.name);
            if (renderers == null)
                return;

            _flareMpb ??= new MaterialPropertyBlock();

            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                r.enabled = updateInfo.enableFlare;
                if (!updateInfo.enableFlare)
                    continue;
                r.GetPropertyBlock(_flareMpb);
                _flareMpb.SetColor(FlareEmissionColor, updateInfo.color * Mathf.Max(0f, updateInfo.brightness));
                r.SetPropertyBlock(_flareMpb);
            }
        }

        // authored projector drive: color/power/size steer the projector light material
        // and the projector's throw size.
        private static readonly int ProjectorColor = Shader.PropertyToID("_Color");

        private void OnProjector(ref LiveTimelineControl.ProjectorUpdateInfo updateInfo)
        {
            var renderers = GetRenderers(_projectorRenderers, updateInfo.name);
            if (renderers == null)
                return;

            float speed = updateInfo.speed;
            if (_projectorScroll.TryGetValue(updateInfo.name, out float last) && !Mathf.Approximately(last, speed))
                _projectorScroll[updateInfo.name] = speed;

            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null)
                        continue;
                    if (m.HasProperty(ProjectorColor))
                        m.SetColor(ProjectorColor, updateInfo.color * Mathf.Max(0f, updateInfo.power));
                    // the authored motion speed scrolls the cookie texture; motionID
                    // selects the pattern, speed 0 pins it in place.
                    if (m.HasProperty(ProjectorMainTex))
                        m.SetTextureOffset(ProjectorMainTex,
                            new Vector2(0f, Mathf.Repeat(Time.time * speed * 0.1f, 1f)));
                }
            }
        }
        private readonly Dictionary<string, float> _projectorScroll = new Dictionary<string, float>();
        private static readonly int ProjectorMainTex = Shader.PropertyToID("_MainTex");

        private void OnParticle(ref LiveTimelineControl.ParticleUpdateInfo updateInfo)
        {
            if (!_emitters.TryGetValue(updateInfo.name, out var system))
            {
                if (!TryStageObject(updateInfo.name, out var go))
                    return;
                system = go.GetComponentInChildren<ParticleSystem>(true);
                _emitters[updateInfo.name] = system;
                if (system == null)
                    return;
            }
            if (system == null)
                return;

            var emission = system.emission;
            emission.rateMultiplier = updateInfo.emissionRate;
            if (updateInfo.emissionRate > 0f && !system.isEmitting)
                system.Play();
            else if (updateInfo.emissionRate <= 0f && system.isEmitting)
                system.Stop();
        }

        private void OnParticleGroup(ref LiveTimelineControl.ParticleGroupUpdateInfo updateInfo)
        {
            if (!_particleGroups.TryGetValue(updateInfo.name, out var system))
            {
                if (!TryStageObject(updateInfo.name, out var go))
                    return;
                system = go.GetComponentInChildren<ParticleSystem>(true);
                _particleGroups[updateInfo.name] = system;
                if (system == null)
                    return;
            }
            if (system == null)
                return;

            // the authored flicker pair rides the group's main-module start color; the
            // light rate scales brightness and the dark rate scales the floor.
            var main = system.main;
            Color baseColor = Color.white;
            main.startColor = new ParticleSystem.MinMaxGradient(
                baseColor * updateInfo.flickerDarkRate,
                baseColor * updateInfo.flickerLightRate);
        }

        // authored shaft track: our pipeline has no dedicated shaft pass, so the
        // enabled/alpha state folds into the bloom-lift used for volume light.
        private void OnLightShafts(ref LiveTimelineControl.LightShaftsUpdateInfo updateInfo)
        {
            if (!_shaftsLogged && updateInfo.enabled)
            {
                _shaftsLogged = true;
                Director.FileLog($"[lightshafts] authored shafts active: '{updateInfo.name}' scale={updateInfo.scale:F2} alpha={updateInfo.alpha.x:F2}");
            }

            var fx = Director.instance ? Director.instance.GetActivePostEffectPublic() : null;
            if (fx != null)
            {
                float lift = updateInfo.enabled ? Mathf.Clamp01(updateInfo.alpha.x) * 0.05f : 0f;
                fx.SetVolumeLightBloomLift(Mathf.Min(lift, 1.2f));
            }
        }

        private void OnNodeScale(ref LiveTimelineControl.NodeScaleUpdateInfo updateInfo)
        {
            // the game's node-scale system: characterFlag bit i+1 selects character i,
            // and the key's percentage scales that character's model; the enum
            // (Direct/Actual/Small) picks the preset style but the rate is the driver.
            if (_lastNodeScaleKey == (updateInfo.characterFlag, updateInfo.sizeType, updateInfo.scaleRatePer))
                return;
            _lastNodeScaleKey = (updateInfo.characterFlag, updateInfo.sizeType, updateInfo.scaleRatePer);
            Director.FileLog($"[nodescale] characterFlag={updateInfo.characterFlag} targetFlag={updateInfo.targetFlag} sizeType={updateInfo.sizeType} scaleRatePer={updateInfo.scaleRatePer}");

            var containers = Director.instance ? Director.instance.CharaContainerScript : null;
            if (containers == null)
                return;

            float rate = Mathf.Max(0.01f, updateInfo.scaleRatePer * 0.01f);
            for (int i = 0; i < containers.Count && i < 31; i++)
            {
                // characterFlag bit (i+1): bit 0 is the Default slot the game skips.
                if ((updateInfo.characterFlag & (1 << (i + 1))) == 0)
                    continue;

                var container = containers[i];
                if (container == null)
                    continue;

                var position = container.transform.Find("Position");
                if (position == null)
                    continue;

                // the authored percentage rides on the character's natural body scale
                // so a 100% key leaves the stage exactly as loaded.
                float baseScale = container.BodyScale > 0f ? container.BodyScale : 1f;
                float applied = baseScale * rate;
                position.localScale = new Vector3(applied, applied, applied);
            }
        }
        private (int, int, float) _lastNodeScaleKey = (-1, -1, -1f);

        private TextMesh _titleMesh;
        private float _titleTargetAlpha;
        private float _titleAlpha;

        // the authored title actions: actionType 1 fades the song title in, 2 fades it
        // out, over actionFrame frames; the overlay is a world-space text quad riding
        // the main camera so no scene edits are needed.
        private void OnTitle(ref LiveTimelineControl.TitleUpdateInfo updateInfo)
        {
            if (_titleMesh == null)
            {
                var camera = Camera.main;
                if (camera == null)
                    return;

                var go = new GameObject("TimelineTitleOverlay");
                go.transform.SetParent(camera.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.35f, 1.2f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

                _titleMesh = go.AddComponent<TextMesh>();
                _titleMesh.fontSize = 64;
                _titleMesh.characterSize = 0.5f;
                _titleMesh.anchor = TextAnchor.MiddleCenter;
                _titleMesh.alignment = TextAlignment.Center;
                _titleMesh.color = new Color(1f, 1f, 1f, 0f);

                var live = Director.instance ? Director.instance.live : null;
                _titleMesh.text = live != null ? live.SongName : string.Empty;
            }

            // authored ordinal actions: 1 = fade in, 2 = fade out.
            bool fadeIn = updateInfo.actionType == 1;
            _titleTargetAlpha = fadeIn ? 1f : 0f;
            _titleFadeDuration = Mathf.Max(1, updateInfo.actionFrame) / 60f;
            _titleFadeClock = 0f;

            Director.FileLog($"[title] {(fadeIn ? "fade in" : "fade out")} over {updateInfo.actionFrame} frames");
        }
        private float _titleFadeDuration = 1f;
        private float _titleFadeClock = 999f;

        // the noise + sweat tracks are alive but their solvers need the game's per-
        // character noise profiles (missing-script components), so their authored state
        // logs once per change until those land.
        private int _lastFacialNoiseFlag = -1;
        private void OnFacialNoise(ref LiveTimelineControl.FacialNoiseUpdateInfo updateInfo)
        {
            if (updateInfo.enableCharacterBitFlag == _lastFacialNoiseFlag)
                return;
            _lastFacialNoiseFlag = updateInfo.enableCharacterBitFlag;
            Director.FileLog($"[facialnoise] enableCharacterBitFlag={updateInfo.enableCharacterBitFlag}");
        }

        private (float, float) _lastMotionNoise = (-1f, -1f);
        private void OnCharaMotionNoise(ref LiveTimelineControl.CharaMotionNoiseUpdateInfo updateInfo)
        {
            var key = (updateInfo.sideBaseBias, updateInfo.backBaseBias);
            if (key == _lastMotionNoise)
                return;
            _lastMotionNoise = key;
            Director.FileLog($"[motionnoise] side bias/range/freq={updateInfo.sideBaseBias}/{updateInfo.sideRange}/{updateInfo.sideFrequency} back={updateInfo.backBaseBias}/{updateInfo.backRange}/{updateInfo.backFrequency}");
        }

        private void OnSweatLocator(ref LiveTimelineControl.SweatLocatorUpdateInfo updateInfo)
        {
            if (_sweatLogged.Add(updateInfo.name))
                Director.FileLog($"[sweat] '{updateInfo.name}' owner={updateInfo.owner} alpha={updateInfo.alpha:F2} randomVisibleCount={updateInfo.randomVisibleCount}");
        }
        private readonly HashSet<string> _sweatLogged = new HashSet<string>();

        private void Update()
        {
            if (_titleMesh == null || _titleAlpha == _titleTargetAlpha)
                return;

            _titleFadeClock += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_titleFadeClock / _titleFadeDuration);
            _titleAlpha = Mathf.Lerp(_titleAlpha, _titleTargetAlpha, t);
            if (Mathf.Abs(_titleAlpha - _titleTargetAlpha) < 0.01f)
                _titleAlpha = _titleTargetAlpha;

            var c = _titleMesh.color;
            c.a = _titleAlpha;
            _titleMesh.color = c;
        }
    }
}
