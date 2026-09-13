using System;
using System.Collections.Generic;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// Viewer-side driver for LED/flare/light billboard objects.
    /// These objects have missing original scripts but their meshes and materials load.
    /// This driver binds to timeline blink/wash light events and applies color/intensity.
    /// </summary>
    public class StageLedDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private StageController _stage;
        private readonly Dictionary<string, Renderer> _ledRenderers = new Dictionary<string, Renderer>(64);
        private readonly Dictionary<string, MaterialPropertyBlock> _mpbCache = new Dictionary<string, MaterialPropertyBlock>(64);
        private bool _bound;
        private static readonly int PID_BlinkColor = Shader.PropertyToID("_BlinkLightColor");
        private static readonly int PID_Color = Shader.PropertyToID("_Color");
        private static readonly int PID_EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int PID_Intensity = Shader.PropertyToID("_Intensity");

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

            if (_ctl == null || _stage == null)
                return;

            if (_bound)
                return;

            _ctl.OnUpdateBlinkLight += OnBlinkLightUpdate;
            _ctl.OnUpdateWashLight += OnWashLightUpdate;
            _bound = true;
            ScanLedObjects();
        }

        private void Unbind()
        {
            if (_ctl != null && _bound)
            {
                _ctl.OnUpdateBlinkLight -= OnBlinkLightUpdate;
                _ctl.OnUpdateWashLight -= OnWashLightUpdate;
            }

            _ctl = null;
            _stage = null;
            _bound = false;
            _ledRenderers.Clear();
            _mpbCache.Clear();
        }

        private void ScanLedObjects()
        {
            _ledRenderers.Clear();

            if (_stage == null || _stage.StageObjectMap == null)
                return;

            foreach (var kv in _stage.StageObjectMap)
            {
                string name = kv.Key ?? "";
                if (name.IndexOf("light", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("led", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("flare", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("billboard", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("blink", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (kv.Value == null)
                    continue;

                var renderers = kv.Value.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    string key = r.gameObject.name;
                    if (!_ledRenderers.ContainsKey(key))
                    {
                        _ledRenderers[key] = r;
                        _mpbCache[key] = new MaterialPropertyBlock();
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (_ctl == null || _stage == null)
                BindIfPossible();
        }

        // BlinkLightUpdateInfoDelegate signature: (LiveTimelineBlinkLightData data, ref BlinkLightUpdateInfo updateInfo, float currentLiveTime)
        private void OnBlinkLightUpdate(LiveTimelineBlinkLightData data, ref BlinkLightUpdateInfo updateInfo, float currentLiveTime)
        {
            if (data == null)
                return;

            // Extract color from color0Array if available
            Color blinkColor = Color.white;
            if (updateInfo.color0Array != null && updateInfo.color0Array.Length > 0)
            {
                blinkColor = updateInfo.color0Array[0];
            }

            // Extract intensity from powerArray if available
            float intensity = 1f;
            if (updateInfo.powerArray != null && updateInfo.powerArray.Length > 0)
            {
                intensity = Mathf.Lerp(updateInfo.powerMin, updateInfo.powerMax, updateInfo.powerArray[0]);
            }

            string timelineName = data.name ?? "";
            ApplyColorToMatchingRenderers(timelineName, blinkColor, intensity);
        }

        // WashLightUpdateInfoDelegate signature: (ref WashLightUpdateInfo updateInfo)
        private void OnWashLightUpdate(ref WashLightUpdateInfo updateInfo)
        {
            // WashLightUpdateInfo doesn't have color/intensity fields directly
            // It has NameHash, IsEnabledRaycast, etc.
            // We can use the NameHash to identify which light group is being updated
        }

        private void ApplyColorToMatchingRenderers(string timelineName, Color color, float intensity)
        {
            foreach (var kv in _ledRenderers)
            {
                string rendererName = kv.Key;
                if (rendererName.IndexOf(timelineName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetRendererColor(kv.Value, rendererName, color, intensity);
                }
            }
        }

        private void SetRendererColor(Renderer renderer, string key, Color color, float intensity)
        {
            if (renderer == null || !renderer.enabled)
                return;

            if (!_mpbCache.TryGetValue(key, out var mpb))
            {
                mpb = new MaterialPropertyBlock();
                _mpbCache[key] = mpb;
            }

            renderer.GetPropertyBlock(mpb);

            // keep the base albedo in 0-1 and let only the emissive channels carry
            // the hdr values, so an led does not blow out its own surface.
            Color finalColor = color * intensity;
            mpb.SetColor(PID_BlinkColor, finalColor);
            mpb.SetColor(PID_Color, color);
            mpb.SetColor(PID_EmissionColor, finalColor);
            mpb.SetFloat(PID_Intensity, intensity);

            renderer.SetPropertyBlock(mpb);
        }
    }
}
