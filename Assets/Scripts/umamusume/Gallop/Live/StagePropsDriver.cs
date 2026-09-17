using System;
using System.Collections.Generic;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>
    /// Viewer-side driver for character props (microphone, vocal speakers).
    /// Attaches props to character's hand based on propsDataGroup.
    /// </summary>
    public class StagePropsDriver : MonoBehaviour
    {
        private LiveTimelineControl _ctl;
        private StageController _stage;
        private bool _bound;

        [Header("Props Settings")]
        [SerializeField] private bool _enablePropsDriver = true;
        [SerializeField] private bool _verboseLog = true;

        // props instances found on the stage, keyed by joint so timeline color and
        // visibility updates can address them without scene searches each frame.
        private static readonly Dictionary<string, List<Renderer>> _propsRenderersByJoint =
            new Dictionary<string, List<Renderer>>();

        // registers a prop's renderers under its attach joint; called when the
        // evaluator instances a prop so per-frame color events can find it.
        public static void RegisterPropRenderers(string jointName, Renderer[] renderers)
        {
            if (string.IsNullOrEmpty(jointName) || renderers == null || renderers.Length == 0)
                return;

            if (!_propsRenderersByJoint.TryGetValue(jointName, out var list))
            {
                list = new List<Renderer>();
                _propsRenderersByJoint[jointName] = list;
            }

            foreach (var r in renderers)
            {
                if (r != null && !list.Contains(r))
                    list.Add(r);
            }
        }

        // applies a timeline props color/visibility update to every registered prop
        // renderer; null-safe when no props have registered yet.
        public static void ApplyPropsColor(Color color, bool rendererEnable)
        {
            foreach (var kv in _propsRenderersByJoint)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    var r = list[i];
                    if (r == null)
                        continue;
                    r.enabled = rendererEnable;
                    r.material.color = color;
                }
            }
        }

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

            _bound = true;
            TryAttachProps();
        }

        private void Unbind()
        {
            _ctl = null;
            _stage = null;
            _bound = false;
        }

        private void LateUpdate()
        {
            if (_ctl == null || _stage == null)
                BindIfPossible();
        }

        private void TryAttachProps()
        {
            if (!_enablePropsDriver)
                return;

            var data = _ctl?.data;
            if (data == null)
                return;

            // Check props settings
            if (data.propsSettings == null || data.propsSettings.propsDataGroup == null)
            {
                if (_verboseLog)
                    Debug.Log("[StagePropsDriver] No props settings found.");
                return;
            }

            foreach (var propGroup in data.propsSettings.propsDataGroup)
            {
                if (propGroup == null) continue;

                if (_verboseLog)
                    Debug.Log($"[StagePropsDriver] Props: {propGroup.propsName} isChara={propGroup.isCharaProps} joints={propGroup.attachJointNameCount}");

                // Look for microphone-related props
                if (propGroup.propsName != null &&
                    (propGroup.propsName.IndexOf("mic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     propGroup.propsName.IndexOf("000", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    AttachMicToCharacter(propGroup);
                }
            }

            // Also try to find vocal speaker objects
            FindAndAttachVocalSpeakers();
        }

        private void AttachMicToCharacter(LiveTimelinePropsSettings.PropsDataGroup propGroup)
        {
            if (propGroup.attachJointNames == null || propGroup.attachJointNameCount == 0)
                return;

            // Find the first character
            var dir = Director.instance;
            if (dir == null || dir.CharaContainerScript == null || dir.CharaContainerScript.Count == 0)
                return;

            var chara = dir.CharaContainerScript[0];
            if (chara == null)
                return;

            // Find hand bone
            Transform handBone = FindHandBone(chara.transform);
            if (handBone == null)
            {
                if (_verboseLog)
                    Debug.LogWarning("[StagePropsDriver] Hand bone not found.");
                return;
            }

            // Create a simple mic placeholder if no mic instance exists
            CreateMicPlaceholder(handBone);
        }

        private void FindAndAttachVocalSpeakers()
        {
            // Find vocal speaker objects in the scene
            var allObjects = FindObjectsOfType<GameObject>(true);
            foreach (var obj in allObjects)
            {
                if (obj == null) continue;

                string name = obj.name ?? "";
                if (name.IndexOf("vocal_speaker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("shadow_vocal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Found a vocal speaker - attach it to a character
                    AttachVocalSpeaker(obj);
                }
            }
        }

        private void AttachVocalSpeaker(GameObject speaker)
        {
            if (speaker == null) return;

            // Find the first character
            var dir = Director.instance;
            if (dir == null || dir.CharaContainerScript == null || dir.CharaContainerScript.Count == 0)
                return;

            var chara = dir.CharaContainerScript[0];
            if (chara == null) return;

            // Find hand bone
            Transform handBone = FindHandBone(chara.transform);
            if (handBone == null) return;

            // Attach the vocal speaker to the hand
            speaker.transform.SetParent(handBone, false);
            speaker.transform.localPosition = new Vector3(0, 0.1f, 0.05f);
            speaker.transform.localRotation = Quaternion.Euler(0, 0, 90);

            if (_verboseLog)
                Debug.Log($"[StagePropsDriver] Attached vocal speaker to {handBone.name}");
        }

        private void CreateMicPlaceholder(Transform handBone)
        {
            // Check if mic already exists
            var existingMic = handBone.Find("Mic_Prop");
            if (existingMic != null) return;

            var micInstance = new GameObject("Mic_Prop");
            micInstance.transform.SetParent(handBone, false);
            micInstance.transform.localPosition = new Vector3(0, 0.1f, 0.05f);
            micInstance.transform.localRotation = Quaternion.Euler(0, 0, 90);

            // Add a simple cylinder as mic body
            var micBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            micBody.name = "Mic_Body";
            micBody.transform.SetParent(micInstance.transform, false);
            micBody.transform.localScale = new Vector3(0.02f, 0.08f, 0.02f);
            micBody.transform.localPosition = Vector3.zero;
            micBody.transform.localRotation = Quaternion.identity;

            // Add a sphere as mic head
            var micHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            micHead.name = "Mic_Head";
            micHead.transform.SetParent(micInstance.transform, false);
            micHead.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
            micHead.transform.localPosition = new Vector3(0, 0.08f, 0);
            micHead.transform.localRotation = Quaternion.identity;

            if (_verboseLog)
                Debug.Log($"[StagePropsDriver] Created mic placeholder at {handBone.name}");
        }

        private Transform FindHandBone(Transform charaTransform)
        {
            if (charaTransform == null)
                return null;

            // Try to find hand bone by common naming conventions
            string[] handBoneNames = {
                "Hand_L", "Hand_R", "LeftHand", "RightHand",
                "J_Bip_L_Hand", "J_Bip_R_Hand", "mixamorig:LeftHand", "mixamorig:RightHand",
                "Hand_Attach"
            };

            foreach (var name in handBoneNames)
            {
                Transform bone = FindChildRecursive(charaTransform, name);
                if (bone != null)
                    return bone;
            }

            // Fallback: find any transform with "hand" in the name
            return FindChildByKeyword(charaTransform, "hand");
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name != null &&
                    child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private Transform FindChildByKeyword(Transform parent, string keyword)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name != null &&
                    child.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;

                Transform found = FindChildByKeyword(child, keyword);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
