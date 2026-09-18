using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Plays the stage audience's own AnimationClips once the stage is live so
    /// the crowd claps and sways instead of standing frozen.
    /// </summary>
    public class StageAudienceAnimator : MonoBehaviour
    {
        private StageController _stage;
        private bool _started;

        private void LateUpdate()
        {
            if (_started)
                return;

            if (_stage == null)
                _stage = GetComponent<StageController>() ?? FindObjectOfType<StageController>();

            if (_stage == null)
                return;

            _started = true;

            int clipsPlayed = 0;
            var animators = _stage.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                var anim = animators[i];
                if (anim == null || anim.clip == null)
                    continue;

                // play every authored stage clip: audience rigs, wave flags, beams.
                anim.Play(anim.clip.name);
                clipsPlayed++;
            }

            Director.FileLog($"[audience] started {clipsPlayed} stage animations");
        }
    }
}
