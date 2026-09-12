using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// Phase 4 (S5): LiveTimelinePropsSettings evaluator.
    ///
    /// Iterates propsSettings.propsDataGroup, evaluates PropsConditionGroup conditions
    /// (Default / CharaPosition / CharaId / DressId + satisfiesAllConditions + IsInvalid),
    /// resolves attachJointNames against the character's bone hierarchy, and instantiates
    /// the matching prop prefab from UmaViewerMain.AbList onto the resolved joint.
    ///
    /// Fixes the "特别周手里少了话筒🎤" bug: without this evaluator, no code ever reads
    /// propsDataGroup[*].attachJointNames, so hand-held props never attach.
    /// </summary>
    public static class LivePropsEvaluator
    {
        private const string PROP_LOG_TAG = "[LivePropsEvaluator]";

        /// <summary>
        /// Evaluate all props for the loaded live. Call once from Director.InitializeTimeline
        /// after character containers and locators are set up.
        /// </summary>
        public static void EvaluateAndAttach(
            LiveTimelineData timelineData,
            List<UmaContainerCharacter> charaContainers,
            int[] motionSequenceIndices)
        {
            if (timelineData == null)
                return;

            var settings = timelineData.propsSettings;
            if (settings == null || settings.propsDataGroup == null || settings.propsDataGroup.Length == 0)
            {
                Debug.LogWarning($"{PROP_LOG_TAG} propsSettings empty or missing");
                return;
            }

            int attached = 0;
            int skipped = 0;

            int groupCount = settings.propsDataGroup.Length;
            for (int i = 0; i < groupCount; i++)
            {
                var group = settings.propsDataGroup[i];
                if (group == null || string.IsNullOrEmpty(group.propsName))
                {
                    skipped++;
                    continue;
                }

                // Resolve target character index (chara props attach per-chara; stage props attach once).
                int charaIndex = -1;
                if (group.isCharaProps && !ResolveCharaIndex(group, charaContainers, motionSequenceIndices, ref charaIndex))
                {
                    skipped++;
                    continue;
                }

                if (!ConditionsSatisfied(group, charaContainers, charaIndex))
                {
                    skipped++;
                    continue;
                }

                if (AttachProp(group, charaContainers, charaIndex))
                    attached++;
                else
                    skipped++;
            }

            if (attached > 0 || skipped > 0)
                Debug.Log($"{PROP_LOG_TAG} props evaluated: attached={attached} skipped={skipped}");
        }

        /// <summary>
        /// Instantiate the prop prefab for a group and parent it to the resolved joint(s).
        /// Stage props (isCharaProps == false) parent to the Director transform.
        /// </summary>
        private static bool AttachProp(
            LiveTimelinePropsSettings.PropsDataGroup group,
            List<UmaContainerCharacter> charaContainers,
            int charaIndex)
        {
            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return false;

            // Find the prop bundle entry: match by propsName against AbList keys.
            // Prop bundles are typed UmaFileType.prop in the manifest (e.g. 3d/prop/...).
            UmaDatabaseEntry entry = FindPropEntry(main.AbList, group.propsName);
            if (entry == null)
            {
                Debug.LogWarning($"{PROP_LOG_TAG} prop bundle not found for '{group.propsName}'");
                return false;
            }

            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry);
            if (bundle == null)
            {
                Debug.LogWarning($"{PROP_LOG_TAG} failed to load bundle for '{group.propsName}'");
                return false;
            }

            GameObject[] prefabs = bundle.LoadAllAssets<GameObject>();
            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogWarning($"{PROP_LOG_TAG} no prefab in bundle for '{group.propsName}'");
                return false;
            }

            // Prefer exact-name prefab, else first.
            GameObject prefab = prefabs.FirstOrDefault(p => p != null && p.name == group.propsName)
                ?? prefabs.FirstOrDefault(p => p != null);
            if (prefab == null)
                return false;

            int attachCount = group.attachJointNames?.Length ?? 0;

            if (attachCount <= 0)
            {
                // No joint specified: stage-level prop, parent to Director.
                var director = Director.instance;
                if (director == null) return false;
                var go = UnityEngine.Object.Instantiate(prefab, director.transform);
                go.name = $"Prop_{group.propsName}";
                return true;
            }

            bool anyAttached = false;
            for (int j = 0; j < attachCount; j++)
            {
                string jointName = group.attachJointNames[j];
                if (string.IsNullOrEmpty(jointName))
                    continue;

                Transform joint = ResolveJoint(charaContainers, charaIndex, jointName);
                if (joint == null)
                {
                    Debug.LogWarning($"{PROP_LOG_TAG} joint '{jointName}' not found for '{group.propsName}' (charaIndex={charaIndex})");
                    continue;
                }

                var go = UnityEngine.Object.Instantiate(prefab, joint);
                go.name = $"Prop_{group.propsName}_{jointName}";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                anyAttached = true;
            }

            return anyAttached;
        }

        /// <summary>
        /// Locate the prop bundle entry in AbList by propsName.
        /// Prefers entries typed UmaFileType.prop; falls back to name matching
        /// (propsName may omit the directory prefix the manifest key carries).
        /// </summary>
        private static UmaDatabaseEntry FindPropEntry(Dictionary<string, UmaDatabaseEntry> abList, string propsName)
        {
            if (string.IsNullOrEmpty(propsName))
                return null;

            // Exact key match first.
            if (abList.TryGetValue(propsName, out var exact))
                return exact;

            string fileName = System.IO.Path.GetFileName(propsName);

            // Typed pass: only entries whose manifest type is item (props are filed
            // under the item type in the current manifest) or whose key contains prop.
            foreach (var kv in abList)
            {
                if (kv.Value.Type != UmaFileType.item)
                    continue;
                string keyFile = System.IO.Path.GetFileName(kv.Key);
                if (string.Equals(keyFile, fileName, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            // Typed fuzzy pass: key contains the propsName segment.
            foreach (var kv in abList)
            {
                if (kv.Value.Type == UmaFileType.item &&
                    kv.Key.IndexOf(propsName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            }

            // Last resort: untyped name match (older manifests may not tag props).
            foreach (var kv in abList)
            {
                string keyFile = System.IO.Path.GetFileName(kv.Key);
                if (string.Equals(keyFile, fileName, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            return null;
        }

        /// <summary>
        /// Resolve which character index a chara-props group targets.
        /// Uses motionSequenceIndices when present (position → sequence index mapping),
        /// otherwise defaults to index 0 when only one character is loaded.
        /// </summary>
        private static bool ResolveCharaIndex(
            LiveTimelinePropsSettings.PropsDataGroup group,
            List<UmaContainerCharacter> charaContainers,
            int[] motionSequenceIndices,
            ref int charaIndex)
        {
            if (charaContainers == null || charaContainers.Count == 0)
                return false;

            // Single-character live: props belong to the only chara.
            if (charaContainers.Count == 1)
            {
                charaIndex = 0;
                return true;
            }

            // Multi-chara: condition groups may pin a CharaPosition.
            int? pinned = TryGetConditionValue(group, LiveTimelinePropsSettings.PropsConditionType.CharaPosition);
            if (pinned.HasValue && pinned.Value >= 0 && pinned.Value < charaContainers.Count)
            {
                charaIndex = pinned.Value;
                return true;
            }

            // CharaId condition: match against loaded containers.
            int? charaId = TryGetConditionValue(group, LiveTimelinePropsSettings.PropsConditionType.CharaId);
            if (charaId.HasValue)
            {
                for (int i = 0; i < charaContainers.Count; i++)
                {
                    var entry = charaContainers[i]?.CharaEntry;
                    if (entry != null && entry.Id == charaId.Value)
                    {
                        charaIndex = i;
                        return true;
                    }
                }
            }

            // Default: first chara (official default for unconditioned chara props).
            charaIndex = 0;
            return true;
        }

        /// <summary>
        /// Evaluate all condition groups for a props entry.
        /// Official semantics: group.satisfiesAllConditions means every condition in the
        /// group must pass; multiple groups are OR'd; IsInvalid groups are skipped;
        /// Default-type conditions always pass.
        /// </summary>
        private static bool ConditionsSatisfied(
            LiveTimelinePropsSettings.PropsDataGroup group,
            List<UmaContainerCharacter> charaContainers,
            int charaIndex)
        {
            if (group.propsConditionGroup == null || group.propsConditionGroup.Length == 0)
                return true; // no conditions = always attach

            int groupCount = group.propsConditionGroup.Length;
            bool anyGroupValid = false;

            for (int g = 0; g < groupCount; g++)
            {
                var condGroup = group.propsConditionGroup[g];
                if (condGroup == null || condGroup.IsInvalid)
                    continue;

                anyGroupValid = true;

                int condCount = condGroup.propsConditionData?.Length ?? 0;

                bool allPass = true;
                for (int c = 0; c < condCount; c++)
                {
                    var cond = condGroup.propsConditionData[c];
                    if (cond == null)
                        continue;

                    if (!ConditionPasses(cond, charaContainers, charaIndex))
                    {
                        allPass = false;
                        break;
                    }
                }

                bool groupResult = condGroup.satisfiesAllConditions ? allPass : (condCount > 0 && !allPass ? AnyPass(condGroup, condCount, charaContainers, charaIndex) : allPass);
                if (groupResult)
                    return true;
            }

            // All groups invalid/empty → treat as unconditional.
            return !anyGroupValid;
        }

        private static bool AnyPass(
            LiveTimelinePropsSettings.PropsConditionGroup condGroup,
            int condCount,
            List<UmaContainerCharacter> charaContainers,
            int charaIndex)
        {
            for (int c = 0; c < condCount; c++)
            {
                var cond = condGroup.propsConditionData[c];
                if (cond != null && ConditionPasses(cond, charaContainers, charaIndex))
                    return true;
            }
            return false;
        }

        private static bool ConditionPasses(
            LiveTimelinePropsSettings.PropsConditionData cond,
            List<UmaContainerCharacter> charaContainers,
            int charaIndex)
        {
            switch (cond.Type)
            {
                case LiveTimelinePropsSettings.PropsConditionType.Default:
                    return true;

                case LiveTimelinePropsSettings.PropsConditionType.CharaPosition:
                    return charaIndex >= 0 && charaIndex == cond.Value;

                case LiveTimelinePropsSettings.PropsConditionType.CharaId:
                {
                    if (charaIndex < 0 || charaIndex >= charaContainers.Count)
                        return false;
                    var entry = charaContainers[charaIndex]?.CharaEntry;
                    return entry != null && entry.Id == cond.Value;
                }

                case LiveTimelinePropsSettings.PropsConditionType.DressId:
                {
                    if (charaIndex < 0 || charaIndex >= charaContainers.Count)
                        return false;
                    var container = charaContainers[charaIndex];
                    var entry = container?.CharaEntry;
                    if (entry == null)
                        return false;
                    // DressId compares against the loaded costume id (CharaEntry carries it).
                    return entry.Id == cond.Value || (container.VarCostumeIdShort == cond.Value.ToString());
                }

                default:
                    return true;
            }
        }

        private static int? TryGetConditionValue(
            LiveTimelinePropsSettings.PropsDataGroup group,
            LiveTimelinePropsSettings.PropsConditionType type)
        {
            if (group.propsConditionGroup == null)
                return null;

            for (int g = 0; g < group.propsConditionGroup.Length; g++)
            {
                var condGroup = group.propsConditionGroup[g];
                if (condGroup == null || condGroup.IsInvalid || condGroup.propsConditionData == null)
                    continue;

                for (int c = 0; c < condGroup.propsConditionData.Length; c++)
                {
                    var cond = condGroup.propsConditionData[c];
                    if (cond != null && cond.Type == type)
                        return cond.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Resolve a joint/bone Transform by name on the target character.
        /// Searches the full bone hierarchy under the container transform.
        /// </summary>
        private static Transform ResolveJoint(
            List<UmaContainerCharacter> charaContainers,
            int charaIndex,
            string jointName)
        {
            if (charaIndex < 0 || charaIndex >= charaContainers.Count)
                return null;

            var container = charaContainers[charaIndex];
            if (container == null)
                return null;

            // Fast path: direct child search by name through the whole hierarchy.
            var t = container.transform.Find(jointName);
            if (t != null)
                return t;

            // Deep search.
            foreach (Transform child in container.transform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == jointName)
                    return child;
            }

            return null;
        }
    }
}
