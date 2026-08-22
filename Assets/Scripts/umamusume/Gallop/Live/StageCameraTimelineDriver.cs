using System;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// Viewer-side driver for CameraTimeline objects (CameraTimeline1/2/3).
    /// These objects have missing original scripts. This driver applies
    /// camera position/rotation from the timeline to the actual camera.
    /// </summary>
    public class StageCameraTimelineDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private StageController _stage;
        private Transform _cameraTransform;
        private bool _bound;

        [Header("Camera Settings")]
        [SerializeField] private float _positionSmoothing = 0.1f;
        [SerializeField] private float _rotationSmoothing = 0.1f;
        [SerializeField] private bool _useSmoothing = false;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private bool _hasTarget;

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

            // Use Director's MainCameraTransform property
            _cameraTransform = dir.MainCameraTransform;
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;

            if (_cameraTransform == null)
                return;

            _bound = true;
        }

        private void Unbind()
        {
            _ctl = null;
            _stage = null;
            _cameraTransform = null;
            _bound = false;
            _hasTarget = false;
        }

        private void LateUpdate()
        {
            if (_ctl == null || _stage == null)
                BindIfPossible();

            if (!_hasTarget || _cameraTransform == null)
                return;

            if (_useSmoothing)
            {
                _cameraTransform.localPosition = Vector3.Lerp(
                    _cameraTransform.localPosition,
                    _targetPosition,
                    Time.deltaTime / _positionSmoothing);

                _cameraTransform.localRotation = Quaternion.Slerp(
                    _cameraTransform.localRotation,
                    _targetRotation,
                    Time.deltaTime / _rotationSmoothing);
            }
            else
            {
                _cameraTransform.localPosition = _targetPosition;
                _cameraTransform.localRotation = _targetRotation;
            }
        }

        /// <summary>
        /// Called by LiveTimelineControl when camera position is updated.
        /// This provides the camera position from the timeline data.
        /// </summary>
        public void ApplyCameraPosition(Vector3 position, Quaternion rotation)
        {
            _targetPosition = position;
            _targetRotation = rotation;
            _hasTarget = true;

            if (!_useSmoothing && _cameraTransform != null)
            {
                _cameraTransform.localPosition = position;
                _cameraTransform.localRotation = rotation;
            }
        }
    }
}
