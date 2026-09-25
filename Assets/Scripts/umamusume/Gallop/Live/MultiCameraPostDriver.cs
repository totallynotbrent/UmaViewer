using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Evaluates the per-multicamera post-effect tracks and routes the active
    /// camera's keys through the same consumers the global tracks use. The game
    /// applies these inside each multiCamera's render; the viewer shows one
    /// switcher-selected camera, so the group whose MultiCameraNo matches the
    /// active camera drives the shared post state.
    /// </summary>
    public class MultiCameraPostDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private bool _censusLogged;

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

            // the switcher selects one of the timeline cameras; multicamera post
            // groups number the multiCamera slots directly.
            int active = dir.activeMultiCameraIndex;
            bool any = EvaluateTracks(sheet, active);
            if (any && !_censusLogged)
            {
                _censusLogged = true;
                Director.FileLog($"[multicampost] active camera {active} driving shared post state");
            }
        }

        // returns true when any track for the active camera produced a key this frame.
        private bool EvaluateTracks(LiveTimelineWorkSheet sheet, int active)
        {
            bool any = false;
            any |= ApplyBloomDiffusion(sheet, active);
            any |= ApplyDof(sheet, active);
            any |= ApplyTransmittedLight(sheet, active);
            any |= ApplyTiltShift(sheet, active);
            any |= ApplyRadialBlur(sheet, active);
            any |= ApplyColorCorrection(sheet, active);
            any |= ApplyPostFilm(sheet, active);
            return any;
        }

        // find the group for the active camera and its current key pair.
        private static bool FindGroup<T>(
            List<T> groups,
            int active,
            out ILiveTimelineKeyDataList keys)
            where T : class, IMultiCameraGroup
        {
            keys = null;
            if (groups == null)
                return false;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null || g.MultiCameraNo != active)
                    continue;
                if (!(g is ILiveTimelineGroupData named) || (keys = named.GetKeyList()) == null)
                    continue;
                return true;
            }
            return false;
        }

        private bool ApplyBloomDiffusion(LiveTimelineWorkSheet sheet, int active)
        {
            if (!FindGroup(sheet.postEffectBloomDiffusionMultiCameraKeys, active, out var keys))
                return false;
            LiveTimelineControl.FindTimelineKey(out var curBase, out var nextBase, keys, _ctl.currentFrame);
            var cur = curBase as LiveTimelineKeyMultiCameraPostEffectBloomDiffusionData;
            if (cur == null)
                return false;
            var next = nextBase as LiveTimelineKeyMultiCameraPostEffectBloomDiffusionData;

            var info = new PostEffectUpdateInfo_BloomDiffusion
            {
                IsEnabledBloom = true,
                IsEnabledDiffusion = true,
                bloomDofWeight = cur.bloomDofWeight,
                threshold = cur.threshold,
                intensity = cur.intensity,
                BloomBlurSize = cur.BloomBlurSize,
                BloomBlendMode = (Gallop.ImageEffect.DofDiffusionBloomOverlayParam.BloomScreenBlendMode)cur.BloomBlendMode,
                diffusionBlurSize = cur.diffusionBlurSize,
                diffusionBright = cur.diffusionBright,
                diffusionThreshold = cur.diffusionThreshold,
                diffusionSaturation = cur.diffusionSaturation,
                diffusionContrast = cur.diffusionContrast,
            };
            if (next != null && next.IsInterpolateKey())
            {
                float t = LiveTimelineControl.CalculateInterpolationValue(cur, next, _ctl.currentFrame);
                info.bloomDofWeight = Mathf.Lerp(cur.bloomDofWeight, next.bloomDofWeight, t);
                info.threshold = Mathf.Lerp(cur.threshold, next.threshold, t);
                info.intensity = Mathf.Lerp(cur.intensity, next.intensity, t);
                info.BloomBlurSize = Mathf.Lerp(cur.BloomBlurSize, next.BloomBlurSize, t);
                info.diffusionBlurSize = Mathf.Lerp(cur.diffusionBlurSize, next.diffusionBlurSize, t);
                info.diffusionBright = Mathf.Lerp(cur.diffusionBright, next.diffusionBright, t);
                info.diffusionThreshold = Mathf.Lerp(cur.diffusionThreshold, next.diffusionThreshold, t);
                info.diffusionSaturation = Mathf.Lerp(cur.diffusionSaturation, next.diffusionSaturation, t);
                info.diffusionContrast = Mathf.Lerp(cur.diffusionContrast, next.diffusionContrast, t);
            }
            _ctl.RaiseMultiCameraBloomDiffusion(info);
            return true;
        }

        private bool ApplyDof(LiveTimelineWorkSheet sheet, int active)
        {
            if (!FindGroup(sheet.postEffectDOFMultiCameraKeys, active, out var keys))
                return false;
            LiveTimelineControl.FindTimelineKey(out var curBase, out _, keys, _ctl.currentFrame);
            if (!(curBase is LiveTimelineKeyMultiCameraPostEffectDOFData cur))
                return false;

            var info = new PostEffectUpdateInfo_DOF
            {
                isValid = true,
                forcalSize = cur.forcalSize,
                blurSpread = cur.blurSpread,
                charactor = cur.charactor,
                dofBlurType = (Gallop.ImageEffect.DofDiffusionBloomOverlayParam.DofDiffusionBloomType)cur.dofBlurType,
                dofQuality = cur.dofQuality,
                dofForegroundSize = cur.dofForegroundSize,
                dofFocalPoint = cur.dofFocalPoint,
                dofSmoothness = cur.dofSmoothness,
                BallBlurPowerFactor = cur.BallBlurPowerFactor,
                BallBlurBrightnessThreshhold = cur.BallBlurBrightnessThreshhold,
                BallBlurBrightnessIntensity = cur.BallBlurBrightnessIntensity,
                BallBlurSpread = cur.BallBlurSpread,
            };
            _ctl.RaiseMultiCameraDof(info);
            return true;
        }

        private bool ApplyTransmittedLight(LiveTimelineWorkSheet sheet, int active)
        {
            if (!FindGroup(sheet.MultiCameraTransmittedLightDataList, active, out var keys))
                return false;
            LiveTimelineControl.FindTimelineKey(out var curBase, out _, keys, _ctl.currentFrame);
            if (!(curBase is LiveTimelineKeyMultiCameraTransmittedLightData cur))
                return false;

            var info = new TransmittedLightUpdateInfo
            {
                isValid = true,
                IsEnabled = true,
                Iterations = cur.Iterations,
                Intensity = cur.Intensity,
                Threshold = cur.Threshold,
                BlurSpread = cur.BlurSpread,
                BlendMode = cur.BlendMode,
            };
            _ctl.RaiseMultiCameraTransmittedLight(info);
            return true;
        }

        private bool ApplyTiltShift(LiveTimelineWorkSheet sheet, int active)
        {
            if (!FindGroup(sheet.multiCameraTiltShiftDataLists, active, out var keys))
                return false;
            LiveTimelineControl.FindTimelineKey(out var curBase, out _, keys, _ctl.currentFrame);
            if (!(curBase is LiveTimelineKeyMultiCameraTiltShiftData cur))
                return false;

            var info = new TiltShiftUpdateInfo
            {
                isValid = true,
                mode = cur.mode,
                quality = cur.quality,
                blurArea = cur.blurArea,
                maxBlurSize = cur.maxBlurSize,
                downsample = cur.downsample,
                roll = cur.roll,
            };
            _ctl.RaiseMultiCameraTiltShift(info);
            return true;
        }

        private bool ApplyRadialBlur(LiveTimelineWorkSheet sheet, int active)
        {
            if (!FindGroup(sheet.multiCameraRadialBlurDataLists, active, out var keys))
                return false;
            LiveTimelineControl.FindTimelineKey(out var curBase, out _, keys, _ctl.currentFrame);
            if (!(curBase is LiveTimelineKeyMultiCameraRadialBlurData cur))
                return false;

            var info = new RadialBlurUpdateInfo
            {
                isValid = true,
                moveBlurType = cur.moveBlurType,
                radialBlurDownsample = cur.radialBlurDownsample,
                radialBlurStartArea = cur.radialBlurStartArea,
                radialBlurEndArea = cur.radialBlurEndArea,
                radialBlurPower = cur.radialBlurPower,
                radialBlurIteration = cur.radialBlurIteration,
                radialBlurRollEulerAngles = cur.radialBlurRollEulerAngles,
                depthPowerFront = cur.depthPowerFront,
                depthPowerBack = cur.depthPowerBack,
            };
            _ctl.RaiseMultiCameraRadialBlur(info);
            return true;
        }

        // the multicamera postfilm tracks mirror the global postfilm keys; v1 only
        // bridges them when the global sheet has no postfilm track so the film
        // state is never applied twice in one frame.
        private bool ApplyPostFilm(LiveTimelineWorkSheet sheet, int active)
        {
            if (sheet.postFilmKeys != null && sheet.postFilmKeys.Count > 0)
                return false;
            for (int track = 0; track < 3; track++)
            {
                var groups = track == 0 ? sheet.postFilm1MultiCameraKeys
                    : track == 1 ? sheet.postFilm2MultiCameraKeys
                    : sheet.postFilm3MultiCameraKeys;
                if (groups == null)
                    continue;
                for (int g = 0; g < groups.Count; g++)
                {
                    var grp = groups[g];
                    if (grp == null || grp.MultiCameraNo != active || grp.keys == null)
                        continue;
                    LiveTimelineControl.FindTimelineKey(
                        out var curBase, out _, grp.keys, _ctl.currentFrame);
                    if (!(curBase is LiveTimelineKeyMultiCameraPostFilmData cur))
                        continue;

                    // build a synthetic global key so the existing consumer reads
                    // the authored attribute bits and layer fields.
                    var key = new LiveTimelineKeyPostFilmData
                    {
                        frame = cur.frame,
                        attribute = cur.attribute,
                        filmMode = (PostFilmMode)cur.filmMode,
                        colorType = (PostColorType)cur.colorType,
                        filmPower = cur.filmPower,
                        filmOffsetParam = cur.filmOffsetParam,
                        filmOptionParam = cur.filmOptionParam,
                        color0 = cur.color0,
                        color1 = cur.color1,
                        color2 = cur.color2,
                        color3 = cur.color3,
                        depthPower = cur.depthPower,
                        DepthClip = cur.DepthClip,
                        RollAngle = cur.RollAngle,
                        FilmScale = cur.FilmScale,
                        layerMode = (LiveTimelineKeyPostFilmData.LayerMode)cur.layerMode,
                        movieResId = cur.movieResId,
                        movieFrameOffset = cur.movieFrameOffset,
                        movieSpeed = cur.movieSpeed,
                        colorBlend = (LiveTimelineKeyPostFilmData.ColorBlend)cur.colorBlend,
                        colorBlendFactor = cur.colorBlendFactor,
                        loopType = (LiveTimelineKeyLoopType)cur.loopType,
                        loopCount = cur.loopCount,
                        loopExecutedCount = cur.loopExecutedCount,
                        loopIntervalFrame = cur.loopIntervalFrame,
                        isPasteLoopUnit = cur.isPasteLoopUnit,
                        isChangeLoopInterpolate = cur.isChangeLoopInterpolate,
                    };
                    _ctl.RaiseMultiCameraPostFilm(key, track);
                    return true;
                }
            }
            return false;
        }

        private bool ApplyColorCorrection(LiveTimelineWorkSheet sheet, int active)
        {
            if (!FindGroup(sheet.multiCameraColorCorrectionDataLists, active, out var keys))
                return false;
            LiveTimelineControl.FindTimelineKey(out var curBase, out _, keys, _ctl.currentFrame);
            if (!(curBase is LiveTimelineKeyMultiCameraColorCorrectionData cur))
                return false;

            if (!cur.enable)
                return false;
            _ctl.RaiseMultiCameraStageGrade(cur.saturation);
            return true;
        }
    }
}
