using System;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// Viewer-side driver for the original confetti particle effect.
    /// The confetti prefab (pfb_env_live_cmn_confetti000) loads successfully, but its
    /// original controller script is missing. This driver binds to the live timeline
    /// object updates and drives the confetti particle system based on timeline data.
    /// </summary>
    public class StageConfettiDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private StageController _stage;
        private ParticleSystem _confettiSystem;
        private Transform _confettiTransform;
        private bool _bound;
        private bool _found;

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
            FindConfetti();
        }

        private void Unbind()
        {
            if (_ctl != null && _bound)
                _ctl.OnUpdateObject -= OnObjectUpdate;

            _ctl = null;
            _stage = null;
            _bound = false;
            _found = false;
            _confettiSystem = null;
            _confettiTransform = null;
        }

        private void FindConfetti()
        {
            if (_found) return;

            // Search stage object map for confetti
            if (_stage != null && _stage.StageObjectMap != null)
            {
                foreach (var kv in _stage.StageObjectMap)
                {
                    string name = kv.Key ?? "";
                    if (name.IndexOf("confetti", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (kv.Value != null)
                        {
                            _confettiTransform = kv.Value.transform;
                            _confettiSystem = kv.Value.GetComponentInChildren<ParticleSystem>(true);
                            _found = true;
                            return;
                        }
                    }
                }
            }

            // Fallback: search by name
            var confetti = GameObject.Find("pfb_env_live_cmn_confetti000(Clone)");
            if (confetti != null)
            {
                _confettiTransform = confetti.transform;
                _confettiSystem = confetti.GetComponentInChildren<ParticleSystem>(true);
                _found = true;
            }
        }

        private void LateUpdate()
        {
            if (_ctl == null || _stage == null)
                BindIfPossible();

            if (!_found)
                FindConfetti();
        }

        private void OnObjectUpdate(ref ObjectUpdateInfo updateInfo)
        {
            if (updateInfo.data == null || updateInfo.data.name == null)
                return;

            string name = updateInfo.data.name;
            if (name.IndexOf("confetti", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            if (!_found)
                FindConfetti();

            if (_confettiTransform == null)
                return;

            // Apply transform from timeline
            var updateData = updateInfo.updateData;
            _confettiTransform.localPosition = updateData.position;
            _confettiTransform.localRotation = updateData.rotation;
            _confettiTransform.localScale = updateData.scale;

            // Control particle emission based on render enable
            if (_confettiSystem != null)
            {
                var emission = _confettiSystem.emission;
                emission.enabled = updateInfo.renderEnable;

                if (updateInfo.renderEnable && !_confettiSystem.isPlaying)
                    _confettiSystem.Play();
                else if (!updateInfo.renderEnable && _confettiSystem.isPlaying)
                    _confettiSystem.Stop();
            }

            // Enable/disable renderers
            var renderers = _confettiTransform.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null)
                    r.enabled = updateInfo.renderEnable;
            }
        }
    }
}
