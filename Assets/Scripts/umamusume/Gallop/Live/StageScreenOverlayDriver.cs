using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    public class StageScreenOverlayDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private bool _bound;

        public Gallop.ImageEffect.ScreenOverlay Overlay { get; private set; }

        private void Awake()
        {
            Overlay = new Gallop.ImageEffect.ScreenOverlay();
        }

        private void OnEnable()
        {
            BindIfPossible();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void LateUpdate()
        {
            if (_ctl == null)
                BindIfPossible();
        }

        private void BindIfPossible()
        {
            var dir = Director.instance;
            _ctl = dir ? dir._liveTimelineControl : null;
            if (_ctl == null || _bound)
                return;

            _bound = true;
            _ctl.OnUpdatePostFilm += OnPostFilm;
        }

        private void Unbind()
        {
            if (_ctl != null && _bound)
                _ctl.OnUpdatePostFilm -= OnPostFilm;
            _ctl = null;
            _bound = false;
        }

        private void OnPostFilm(LiveTimelineKeyPostFilmData data, ref PostFilmUpdateInfo info, float currentLiveTime)
        {
            if (Overlay == null)
                return;

            var target = ChooseOverlay(data, info);
            if (target == null)
                return;

            MapToOverlay(target, info);
        }

        private Gallop.ImageEffect.ScreenOverlay.Overlay ChooseOverlay(
            LiveTimelineKeyPostFilmData data,
            PostFilmUpdateInfo info)
        {
            // route uv-movie frames to the secondary composite layer (filmPass2nd) and
            // keep color/vignette post-films on the primary layer so the two stack cleanly.
            if (info.layerMode == LiveTimelineKeyPostFilmData.LayerMode.UVMovie)
                return Overlay.Overlay2;
            return Overlay.Overlay1;
        }

        private static void MapToOverlay(
            Gallop.ImageEffect.ScreenOverlay.Overlay target,
            PostFilmUpdateInfo info)
        {
            target.postFilmMode = (Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode)info.filmMode;
            target.postFilmPower = info.filmPower;
            target.depthPower = info.depthPower;
            target.DepthClip = info.DepthClip;
            target.postFilmOffsetParam = info.filmOffsetParam;
            target.postFilmOptionParam = info.filmOptionParam;
            target.postFilmColor0 = info.color0;
            target.postFilmColor1 = info.color1;
            target.postFilmColor2 = info.color2;
            target.postFilmColor3 = info.color3;
            target.layerMode = (Gallop.ImageEffect.ScreenOverlay.Overlay.LayerMode)info.layerMode;
            target.movieResId = info.movieResId;
            target.colorBlend = (Gallop.ImageEffect.ScreenOverlay.Overlay.ColorBlend)info.colorBlend;
            target.colorBlendFactor = info.colorBlendFactor;
            target.SetRollAngle(info.RollAngle);
            target.SetScale(info.FilmScale);
            target.IsEnableDepth = info.DepthClip <= 1f && info.DepthClip > 0f;
        }
    }
}