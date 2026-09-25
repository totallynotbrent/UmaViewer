using System;
using System.Collections.Generic;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// Drives the audience track: instances each entry's crowd prefab from the
    /// common bundles, applies the authored transform + cyalume tint, and
    /// switches the crowd AnimationClip per key. Also plays every authored
    /// stage Animation so static stages animate their props.
    /// </summary>
    public class StageAudienceAnimator : MonoBehaviour
    {
        private StageController _stage;
        private LiveTimelineControl _ctl;
        private bool _stageClipsStarted;
        private bool _bound;

        // crowd prefab instances per timeline entry, and the clip cache per
        // audience prefab name so repeated entries share one loaded bundle.
        private readonly Dictionary<string, GameObject> _crowdPrefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<int, GameObject> _crowdInstances = new Dictionary<int, GameObject>();

        private void LateUpdate()
        {
            if (_stage == null)
                _stage = GetComponent<StageController>() ?? FindObjectOfType<StageController>();
            if (_ctl == null)
            {
                var dir = Director.instance;
                _ctl = dir ? dir._liveTimelineControl : null;
                if (_ctl != null && !_bound)
                {
                    _ctl.OnUpdateAudience += OnAudienceUpdate;
                    _bound = true;
                }
            }

            if (_stage == null)
                return;

            if (!_stageClipsStarted)
            {
                _stageClipsStarted = true;
                int clipsPlayed = 0;
                var animators = _stage.GetComponentsInChildren<Animation>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    var anim = animators[i];
                    if (anim == null || anim.clip == null)
                        continue;
                    anim.Play(anim.clip.name);
                    clipsPlayed++;
                }
                Director.FileLog($"[audience] started {clipsPlayed} stage animations");

                // one-shot census of the authored audience tracks so a silent
                // skip is visible: name, key count, and binding state.
                var wsSheet = _ctl != null ? _ctl.GetMainLiveSheet() : null;
                if (wsSheet == null || wsSheet.audienceList == null || wsSheet.audienceList.Count == 0)
                {
                    Director.FileLog("[audience] census: no audienceList on sheet");
                }
                else
                {
                    var sb = new System.Text.StringBuilder("[audience] census:");
                    for (int i = 0; i < wsSheet.audienceList.Count; i++)
                    {
                        var entry = wsSheet.audienceList[i];
                        sb.Append($" [{i}] name='{entry?.name}' keys={entry?.keys?.Count ?? -1}");
                    }
                    Director.FileLog(sb.ToString());
                }
            }
        }

        private void OnDestroy()
        {
            if (_ctl != null && _bound)
            {
                _ctl.OnUpdateAudience -= OnAudienceUpdate;
                _bound = false;
            }
        }

        // one instance per timeline entry; the prefab name lives on the entry.
        private void OnAudienceUpdate(
            LiveTimelineAudienceData entry,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            int animationIndex,
            float animationSpeed)
        {
            if (entry == null || string.IsNullOrEmpty(entry.name))
                return;

            var dir = Director.instance;
            if (dir == null)
                return;

            int instanceKey = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(entry);
            if (!_crowdInstances.TryGetValue(instanceKey, out var instance) || instance == null)
            {
                GameObject prefab = ResolveCrowdPrefab(entry.name);
                if (prefab == null)
                    return;
                instance = Instantiate(prefab, dir.transform);
                instance.name = $"Audience_{entry._objectIndex}_{entry.name}";
                _crowdInstances[instanceKey] = instance;
            }

            instance.SetActive(true);
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;
            instance.transform.localScale = scale;

            // the crowd clips ship in the same bundle as the prefab; play the
            // animationIndex-th clip when the track selects one.
            if (animationIndex >= 0)
                PlayCrowdClip(instance, animationIndex, animationSpeed);
        }

        private GameObject ResolveCrowdPrefab(string prefabName)
        {
            if (_crowdPrefabs.TryGetValue(prefabName, out var cached))
                return cached;

            var main = UmaViewerMain.Instance;
            if (main == null)
                return null;

            // crowd prefabs live under 3d/env/live/common/cyalume_audience/<name> in
            // the manifest; try the exact folder first, then any key ending with the name.
            string bundleKey = $"3d/env/live/common/cyalume_audience/{prefabName}";
            if (!main.AbList.TryGetValue(bundleKey, out var entry))
            {
                foreach (var kv in main.AbList)
                {
                    if (kv.Key.EndsWith(prefabName, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = kv.Value;
                        break;
                    }
                }
            }

            if (entry == null)
            {
                Director.FileLog($"[audience] crowd prefab not found for '{prefabName}'");
                return null;
            }

            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry);
            if (bundle == null)
                return null;

            GameObject prefab = bundle.LoadAsset<GameObject>(prefabName);
            if (prefab == null)
            {
                foreach (GameObject go in bundle.LoadAllAssets<GameObject>())
                {
                    if (go != null) { prefab = go; break; }
                }
            }

            if (prefab != null)
                _crowdPrefabs[prefabName] = prefab;
            return prefab;
        }

        private static void PlayCrowdClip(GameObject instance, int animationIndex, float speed)
        {
            var anim = instance.GetComponent<Animation>() ?? instance.GetComponentInChildren<Animation>(true);
            if (anim == null)
                return;

            // AnimationClip ordering inside the bundle is load-order; collect
            // them once and index into it like the game's animationSetting.
            var clips = new List<AnimationClip>();
            foreach (AnimationState state in anim)
                clips.Add(state.clip);

            if (clips.Count == 0)
                return;

            int idx = Mathf.Clamp(animationIndex, 0, clips.Count - 1);
            if (speed > 0f)
                anim[clips[idx].name].speed = speed;
            anim.Play(clips[idx].name);
        }
    }
}
