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
                }
            }
        }

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
            // sizeType semantics need the game's enum to apply safely; the values are
            // logged once per change so the mapping can be tuned with real data.
            if (_lastNodeScaleKey == (updateInfo.characterFlag, updateInfo.sizeType, updateInfo.scaleRatePer))
                return;
            _lastNodeScaleKey = (updateInfo.characterFlag, updateInfo.sizeType, updateInfo.scaleRatePer);
            Director.FileLog($"[nodescale] characterFlag={updateInfo.characterFlag} targetFlag={updateInfo.targetFlag} sizeType={updateInfo.sizeType} scaleRatePer={updateInfo.scaleRatePer}");
        }
        private (int, int, float) _lastNodeScaleKey = (-1, -1, -1f);

        private void OnTitle(ref LiveTimelineControl.TitleUpdateInfo updateInfo)
        {
            // the viewer has no song-title overlay yet; record the authored actions so
            // the trigger timing is visible in the log for a future title layer.
            Director.FileLog($"[title] action type={updateInfo.actionType} frame={updateInfo.actionFrame}");
        }
    }
}
