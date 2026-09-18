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
                Director.FileLog($"{PROP_LOG_TAG} propsSettings empty or missing");
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

                // Stage props carry CharaPosition conditions listing the slots they
                // appear at; spawn one instance per slot instead of testing all
                // conditions against a single slot.
                List<int> targetSlots = ResolveTargetSlots(group, charaContainers);
                if (targetSlots == null || targetSlots.Count == 0)
                {
                    skipped++;
                    continue;
                }

                bool anyAttached = false;
                for (int slot = 0; slot < targetSlots.Count; slot++)
                {
                    if (AttachProp(group, charaContainers, targetSlots[slot]))
                        anyAttached = true;
                    else
                        skipped++;
                }
                if (anyAttached)
                    attached++;
            }

            if (attached > 0 || skipped > 0)
                Director.FileLog($"{PROP_LOG_TAG} props evaluated: attached={attached} skipped={skipped}");
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

            // chara props reference their exact prefab by major/minor id; stage
            // dressing falls back to the common prop bundle by propsName code.
            UmaDatabaseEntry entry = null;
            string prefabName = null;
            if (group.isCharaProps && group.charaPropsMajorId > 0)
            {
                string kind = group.IsRichProp ? "richprop"
                    : group.IsToonProp ? "toonprop"
                    : "prop";
                string prefix = group.IsRichProp ? "pfb_rich_prop"
                    : group.IsToonProp ? "pfb_toon_prop"
                    : "pfb_chr_prop";
                int major = group.charaPropsMajorId;
                int minor = group.charaPropsMinorId;
                for (int v = minor; v >= 0 && entry == null; v--)
                {
                    prefabName = $"{prefix}{major}_{v:00}";
                    // bundle keys follow 3d/chara/<kind>/prop<major>_<minor>/<prefabName>;
                    // rich/toon dirs share the same prop<major> folder shape.
                    string bundleKey = $"3d/chara/{kind}/prop{major}_{v:00}/{prefabName}";
                    if (main.AbList.TryGetValue(bundleKey, out var exact))
                    {
                        entry = exact;
                    }
                    else
                    {
                        // fall back to any AbList key ending in the prefab name.
                        foreach (var kv in main.AbList)
                        {
                            if (kv.Key.EndsWith(prefabName, StringComparison.OrdinalIgnoreCase))
                            {
                                entry = kv.Value;
                                break;
                            }
                        }
                    }
                }
            }

            if (entry == null && !string.IsNullOrEmpty(group.propsName))
                entry = FindPropEntry(main.AbList, group.propsName);

            if (entry == null)
            {
                Director.FileLog($"{PROP_LOG_TAG} prop bundle not found for '{group.propsName}'");
                return false;
            }

            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry);
            if (bundle == null)
            {
                Director.FileLog($"{PROP_LOG_TAG} failed to load bundle for '{group.propsName}'");
                return false;
            }

            GameObject[] prefabs = bundle.LoadAllAssets<GameObject>();
            if (prefabs == null || prefabs.Length == 0)
            {
                Director.FileLog($"{PROP_LOG_TAG} no prefab in bundle for '{group.propsName}'");
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

            // the joint list enumerates candidate anchors; prefer the _loc
            // locator the game authors the mic for, then the right hand, so a
            // mic lands in one hand only.
            Transform joint = null;
            string jointName = null;
            string[] orderedCandidates = OrderAttachCandidates(group.attachJointNames, attachCount);
            for (int j = 0; j < orderedCandidates.Length; j++)
            {
                string candidate = orderedCandidates[j];
                if (string.IsNullOrEmpty(candidate))
                    continue;
                Transform found = ResolveJoint(charaContainers, charaIndex, candidate);
                if (found != null)
                {
                    joint = found;
                    jointName = candidate;
                    break;
                }
            }

            if (joint == null)
            {
                Director.FileLog($"{PROP_LOG_TAG} no attach joint found for '{group.propsName}' (charaIndex={charaIndex})");
                return false;
            }

            var attachedGo = UnityEngine.Object.Instantiate(prefab, joint);
            attachedGo.name = $"Prop_{group.propsName}_{jointName}";
            attachedGo.transform.localPosition = Vector3.zero;
            attachedGo.transform.localRotation = Quaternion.identity;
            attachedGo.transform.localScale = Vector3.one;

            // stage-dressing bundles carry both a handheld "mic" and a "standmic"
            // under one root; a chara prop IS the handheld item, so only strip
            // the stand (and its lights) for stage dressing instances.
            if (!group.isCharaProps)
            {
                foreach (Transform child in attachedGo.GetComponentsInChildren<Transform>(true))
                {
                    if (child == attachedGo.transform)
                        continue;
                    if (child.name == "standmic" || child.name.EndsWith("_light") || child.name.StartsWith("light"))
                    {
                        UnityEngine.Object.Destroy(child.gameObject);
                        break;
                    }
                }
            }

            StagePropsDriver.RegisterPropRenderers(jointName, attachedGo.GetComponentsInChildren<Renderer>());
            return true;
        }

        /// <summary>
        /// Decide which character slots a prop group targets. Chara props keep
        /// their resolved slot; stage props collect every CharaPosition value the
        /// conditions name. Returns null when nothing matches.
        /// </summary>
        private static List<int> ResolveTargetSlots(
            LiveTimelinePropsSettings.PropsDataGroup group,
            List<UmaContainerCharacter> charaContainers)
        {
            if (group.propsConditionGroup == null || group.propsConditionGroup.Length == 0)
            {
                // no conditions: stage prop attaches once, chara props at slot 0.
                return group.isCharaProps
                    ? new List<int> { 0 }
                    : new List<int> { -1 };
            }

            var slots = new List<int>();
            if (group.isCharaProps)
            {
                for (int g = 0; g < group.propsConditionGroup.Length; g++)
                {
                    var condGroup = group.propsConditionGroup[g];
                    if (condGroup == null)
                        continue;
                    for (int c = 0; c < (condGroup.propsConditionData?.Length ?? 0); c++)
                    {
                        var cond = condGroup.propsConditionData[c];
                        if (cond?.Type == LiveTimelinePropsSettings.PropsConditionType.CharaPosition)
                        {
                            int slotIndex = cond.Value;
                            if (slotIndex >= 0 && slotIndex < charaContainers.Count && !slots.Contains(slotIndex))
                                slots.Add(slotIndex);
                        }
                    }
                }
                if (slots.Count == 0 && (charaContainers?.Count ?? 0) > 0)
                    slots.Add(0);
                return slots;
            }

            // stage prop: collect every named slot; none named means attach once.
            for (int g = 0; g < group.propsConditionGroup.Length; g++)
            {
                var condGroup = group.propsConditionGroup[g];
                if (condGroup == null)
                    continue;
                for (int c = 0; c < (condGroup.propsConditionData?.Length ?? 0); c++)
                {
                    var cond = condGroup.propsConditionData[c];
                    if (cond?.Type == LiveTimelinePropsSettings.PropsConditionType.CharaPosition &&
                        cond.Value >= 0 && cond.Value < charaContainers.Count &&
                        !slots.Contains(cond.Value))
                        slots.Add(cond.Value);
                }
            }
            if (slots.Count == 0)
                slots.Add(-1);
            return slots;
        }

        /// <summary>
        /// Reorder the group's candidate joints so _loc locators and the right
        /// hand win; the game's authored order puts both hands in the list.
        /// </summary>
        private static string[] OrderAttachCandidates(string[] attachJointNames, int attachCount)
        {
            var candidates = new List<string>(attachCount);
            // a handheld mic anchors at the rig's Mic_Attach_00 bone; prefer it
            // over the generic hand joints when the group lists it.
            bool hasMicAnchor = false;
            for (int i = 0; i < attachCount; i++)
            {
                string n = attachJointNames[i];
                if (!string.IsNullOrEmpty(n) && n.StartsWith("Mic_Attach", StringComparison.OrdinalIgnoreCase))
                {
                    hasMicAnchor = true;
                    break;
                }
            }

            for (int i = 0; i < attachCount; i++)
            {
                string name = attachJointNames[i];
                if (string.IsNullOrEmpty(name))
                    continue;

                bool priority = hasMicAnchor
                    ? name.StartsWith("Mic_Attach", StringComparison.OrdinalIgnoreCase) && !name.EndsWith("_loc", StringComparison.OrdinalIgnoreCase)
                    : name.EndsWith("_loc", StringComparison.OrdinalIgnoreCase) ||
                      name.EndsWith("_R", StringComparison.OrdinalIgnoreCase);

                if (priority)
                    candidates.Insert(0, name);
                else
                    candidates.Add(name);
            }
            return candidates.ToArray();
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

            // stage props live in 3d/env/live/common/prop/pfb_env_live_cmn_propNNN
            // keyed by the bare number the cut data carries (e.g. "001").
            string padded = propsName.PadLeft(3, '0');
            string direct = $"3d/env/live/common/prop/pfb_env_live_cmn_prop{padded}";
            if (abList.TryGetValue(direct, out var exact))
                return exact;

            // fall back to any key whose file name ends with the padded number.
            string fileName = "pfb_env_live_cmn_prop" + padded;
            foreach (var kv in abList)
            {
                string keyFile = System.IO.Path.GetFileName(kv.Key);
                if (string.Equals(keyFile, fileName, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            // last resort: name contains the segment anywhere.
            foreach (var kv in abList)
            {
                if (kv.Key.IndexOf("pfb_env_live_cmn_prop" + padded, StringComparison.OrdinalIgnoreCase) >= 0)
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

        // timeline attach event: snaps an already-instanced prop to the named joint
        // with the timeline's offset; used when the propsAttach track fires.
        public static void AttachToJoint(string jointName, Vector3 offsetPosition)
        {
            if (string.IsNullOrEmpty(jointName))
                return;

            var director = Director.instance;
            if (director == null || director.CharaContainerScript == null || director.CharaContainerScript.Count == 0)
                return;

            // find any prop already instanced under this joint name first
            for (int i = 0; i < director.CharaContainerScript.Count; i++)
            {
                var container = director.CharaContainerScript[i];
                if (container == null)
                    continue;

                Transform joint = ResolveJoint(new List<UmaContainerCharacter> { container }, i, jointName);
                if (joint == null)
                    continue;

                foreach (Transform child in joint)
                {
                    if (child.name.StartsWith("Prop_", StringComparison.Ordinal))
                    {
                        child.localPosition = offsetPosition;
                        return;
                    }
                }
            }
        }
    }
}
