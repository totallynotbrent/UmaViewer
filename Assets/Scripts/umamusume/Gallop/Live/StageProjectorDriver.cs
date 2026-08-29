using System;
using System.Collections.Generic;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// Viewer-side driver for original game projector objects (projector_01..projector_18).
    /// These objects have missing original scripts. This driver binds to the live timeline
    /// OnUpdateObject event and applies position/rotation/scale/renderEnable from the
    /// timeline data. It also handles projector material properties when available.
    /// </summary>
    public class StageProjectorDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private StageController _stage;
        private readonly Dictionary<string, Transform> _projectorTransforms = new Dictionary<string, Transform>(32);
        private readonly Dictionary<string, bool> _projectorActiveState = new Dictionary<string, bool>(32);
        private bool _bound;

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

            _ctl.OnUpdateObject += OnObjectUpdate;
            _bound = true;
            ScanProjectors();
        }

        private void Unbind()
        {
            if (_ctl != null && _bound)
                _ctl.OnUpdateObject -= OnObjectUpdate;

            _ctl = null;
            _stage = null;
            _bound = false;
            _projectorTransforms.Clear();
            _projectorActiveState.Clear();
        }

        private void ScanProjectors()
        {
            _projectorTransforms.Clear();

            // Scan the stage object map for projector-like names
            if (_stage != null && _stage.StageObjectMap != null)
            {
                foreach (var kv in _stage.StageObjectMap)
                {
                    string name = kv.Key ?? "";
                    if (name.IndexOf("projector", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("projector_", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (kv.Value != null)
                            _projectorTransforms[name] = kv.Value.transform;
                    }
                }
            }

            // Also scan children of the stage controller for projector_XX naming
            var stageChildren = GetComponentsInChildren<Transform>(true);
            foreach (var t in stageChildren)
            {
                if (t == null) continue;
                string n = t.name ?? "";
                if (n.StartsWith("projector_", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_projectorTransforms.ContainsKey(n))
                        _projectorTransforms[n] = t;
                }
            }
        }

        private void LateUpdate()
        {
            if (_ctl == null || _stage == null)
                BindIfPossible();
        }

        private void OnObjectUpdate(ref ObjectUpdateInfo updateInfo)
        {
            if (updateInfo.data == null || updateInfo.data.name == null)
                return;

            string name = updateInfo.data.name;

            // Only handle projector objects
            if (name.IndexOf("projector", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            // Find the matching transform
            if (!_projectorTransforms.TryGetValue(name, out var target) || target == null)
            {
                // Try to find it by scanning the stage hierarchy
                if (_stage != null && _stage.StageObjectMap.TryGetValue(name, out var go) && go != null)
                {
                    target = go.transform;
                    _projectorTransforms[name] = target;
                }
                else
                {
                    return;
                }
            }

            // Apply transform data from timeline
            var updateData = updateInfo.updateData;

            target.localPosition = updateData.position;
            target.localRotation = updateData.rotation;
            target.localScale = updateData.scale;

            // Apply render enable/disable
            bool shouldRender = updateInfo.renderEnable;
            if (_projectorActiveState.TryGetValue(name, out bool wasActive) && wasActive == shouldRender)
                return;

            _projectorActiveState[name] = shouldRender;

            // Enable/disable all renderers in the projector hierarchy
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null)
                    r.enabled = shouldRender;
            }
        }
    }
}
