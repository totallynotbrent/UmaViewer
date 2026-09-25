using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Renders the authored monitor camera view into a render texture and feeds it to
    /// every stage monitor surface, mirroring the game's Director monitor-texture path:
    /// the active view renders into one shared texture that all monitor materials read.
    /// </summary>
    public class MonitorCameraFeed : MonoBehaviour
    {
        private const int FeedWidth = 1024;
        private const int FeedHeight = 576;

        private RenderTexture _feedTexture;
        private Camera _feedCamera;
        private LiveTimelineControl _ctl;
        private bool _censusLogged;
        private readonly List<Renderer> _monitorRenderers = new List<Renderer>();

        private void LateUpdate()
        {
            var dir = Director.instance;
            if (dir == null)
                return;
            if (_ctl == null)
                _ctl = dir._liveTimelineControl;
            if (_ctl == null)
                return;

            var sheet = _ctl.GetMainLiveSheet();
            if (sheet == null)
                return;

            bool hasMonitorTrack =
                sheet.monitorCameraPosKeys != null && sheet.monitorCameraPosKeys.Count > 0;
            if (!hasMonitorTrack)
            {
                if (isActiveAndEnabled && _feedCamera != null && _feedCamera.enabled)
                {
                    _feedCamera.enabled = false;
                    Director.FileLog("[monitorfeed] no monitor camera track; feed disabled");
                }
                return;
            }

            EnsureFeedCamera(dir);
            if (!EvaluateMonitorCamera(sheet))
                return;

            _feedCamera.enabled = true;
            CollectMonitorRenderers();
            ApplyFeedToMonitors();

            if (!_censusLogged)
            {
                _censusLogged = true;
                Director.FileLog($"[monitorfeed] live feed active: posGroups={sheet.monitorCameraPosKeys.Count} lookGroups={(sheet.monitorCameraLookAtKeys?.Count ?? 0)} monitors={_monitorRenderers.Count} res={FeedWidth}x{FeedHeight}");
            }
        }

        // one hidden camera renders from the authored monitor position; the game shares
        // one texture across every multiCamera/monitor so a single feed matches it.
        private void EnsureFeedCamera(Director dir)
        {
            if (_feedTexture == null)
            {
                _feedTexture = new RenderTexture(FeedWidth, FeedHeight, 24, RenderTextureFormat.DefaultHDR)
                {
                    name = "MonitorLiveFeed",
                    filterMode = FilterMode.Bilinear,
                };
                _feedTexture.Create();
            }

            if (_feedCamera == null)
            {
                var go = new GameObject("MonitorFeedCamera");
                go.transform.SetParent(dir.transform, false);
                _feedCamera = go.AddComponent<Camera>();
                _feedCamera.enabled = false;
                _feedCamera.depth = -10f;
                _feedCamera.targetTexture = _feedTexture;
                _feedCamera.clearFlags = CameraClearFlags.SolidColor;
                _feedCamera.backgroundColor = Color.black;
                _feedCamera.fieldOfView = 40f;
            }
        }

        // evaluate the first pos group + its matching lookat group with the shared
        // key lookup, interpolating toward the next key the way the camera drivers do.
        private bool EvaluateMonitorCamera(LiveTimelineWorkSheet sheet)
        {
            var posGroup = sheet.monitorCameraPosKeys[0];
            if (posGroup == null || posGroup.keys == null || posGroup.keys.Count == 0)
                return false;

            float frame = _ctl.currentFrame;
            LiveTimelineControl.FindTimelineKey(
                out var curBase, out var nextBase, posGroup.keys, frame);
            var cur = curBase as LiveTimelineKeyMonitorCameraPositionData;
            if (cur == null)
                return false;
            var next = nextBase as LiveTimelineKeyMonitorCameraPositionData;

            Vector3 pos = cur.GetValue(_ctl);
            if (next != null && next.IsInterpolateKey())
            {
                float t = LiveTimelineControl.CalculateInterpolationValue(
                    cur, next, frame);
                pos = Vector3.Lerp(pos, next.GetValue(_ctl), t);
            }
            _feedCamera.transform.position = pos;

            if (sheet.monitorCameraLookAtKeys != null && sheet.monitorCameraLookAtKeys.Count > 0)
            {
                var lookGroup = sheet.monitorCameraLookAtKeys[0];
                if (lookGroup != null && lookGroup.keys != null && lookGroup.keys.Count > 0)
                {
                    LiveTimelineControl.FindTimelineKey(
                        out var lookCurBase, out _, lookGroup.keys, frame);
                    if (lookCurBase is LiveTimelineKeyMonitorCameraLookAtData lookCur)
                    {
                        Vector3 target = lookCur.GetValue(_ctl);
                        Vector3 dir = target - pos;
                        if (dir.sqrMagnitude > 1e-6f)
                            _feedCamera.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
            }

            return true;
        }

        // monitor surfaces are the renderers using the stage monitor shader; the feed
        // rides a property block so the movie/image slot driver can still override.
        private void CollectMonitorRenderers()
        {
            if (_monitorRenderers.Count > 0)
                return;
            _monitorRenderers.Clear();
            var stage = GetComponent<StageController>();
            var roots = new List<Renderer>();
            if (stage != null)
                roots.AddRange(stage.GetComponentsInChildren<Renderer>(true));
            var dir = Director.instance;
            if (dir != null)
                roots.AddRange(dir.GetComponentsInChildren<Renderer>(true));
            foreach (var rend in roots)
            {
                if (rend == null || rend.sharedMaterial == null)
                    continue;
                var mats = rend.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null || mat.shader == null)
                        continue;
                    if (mat.shader.name.Contains("Stage/Monitor"))
                    {
                        _monitorRenderers.Add(rend);
                        break;
                    }
                }
            }
        }

        private void ApplyFeedToMonitors()
        {
            if (_monitorRenderers.Count == 0)
                return;
            foreach (var rend in _monitorRenderers)
            {
                if (rend == null)
                    continue;
                var block = new MaterialPropertyBlock();
                rend.GetPropertyBlock(block);
                block.SetTexture(Shader.PropertyToID("_MainTex"), _feedTexture);
                rend.SetPropertyBlock(block);
            }
        }

        private void OnDestroy()
        {
            if (_feedCamera != null)
                Destroy(_feedCamera.gameObject);
            if (_feedTexture != null)
                Destroy(_feedTexture);
        }
    }
}
