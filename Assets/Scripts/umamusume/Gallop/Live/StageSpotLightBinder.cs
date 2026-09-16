using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    // IL2CPP recovery proved there is no dedicated spotlight key data type; spotlights
    // are per-song stage fixtures that this binder instances onto the stage root.
    public class StageSpotLightBinder : MonoBehaviour
    {
        public void Bind(StageController stage, LiveTimelineData data, GameObject overrideParent = null)
        {
            if (stage == null || data == null)
                return;

            if (data.spotLightPrefabNames == null || data.spotLightPrefabNames.Length == 0)
                return;

            Transform parent = ResolveParent(stage, data.spotLightParentName, overrideParent);
            if (parent == null)
            {
                Debug.Log("[spotlight] no parent transform found to host spotlight fixtures");
                return;
            }

            foreach (string name in data.spotLightPrefabNames)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                try
                {
                    BindOne(stage, name, parent);
                }
                catch (Exception e)
                {
                    Debug.Log($"[spotlight] failed to instance '{name}': {e.Message}");
                }
            }
        }

        private static void BindOne(StageController stage, string name, Transform parent)
        {
            UmaDatabaseEntry entry = ResolveEntry(name);
            if (entry == null)
            {
                LogUnresolved(name);
                return;
            }

            GameObject prefab = LoadPrefab(name, entry);
            if (prefab == null)
            {
                LogUnresolved(name);
                return;
            }

            GameObject instance = Instantiate(prefab, parent);
            instance.name = prefab.name;
            RegisterInStage(stage, instance.name, instance);

            Debug.Log($"[spotlight] instanced fixture '{instance.name}' from '{name}' under '{parent.name}'");
        }

        private static void LogUnresolved(string name)
        {
            Debug.Log($"[spotlight] could not resolve spotlight fixture asset '{name}'");
        }

        // first an exact name match in the meta manifest, then a wildcard endswith
        // fallback because per-song cut data may carry a bare asset name.
        private static UmaDatabaseEntry ResolveEntry(string name)
        {
            var entries = UmaDatabaseController.Instance?.MetaEntries;
            if (entries == null)
                return null;

            if (entries.TryGetValue(name, out UmaDatabaseEntry exact) && exact != null)
                return exact;

            string bare = name.Substring(name.LastIndexOf('/') + 1);
            if (string.IsNullOrEmpty(bare) || string.Equals(bare, name, StringComparison.OrdinalIgnoreCase))
                return null;

            foreach (var kv in entries)
            {
                string key = kv.Key;
                if (string.IsNullOrEmpty(key))
                    continue;

                string keyBare = key.Substring(key.LastIndexOf('/') + 1);
                if (string.Equals(keyBare, bare, StringComparison.OrdinalIgnoreCase) ||
                    key.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Value != null)
                        return kv.Value;
                }
            }

            return null;
        }

        private static GameObject LoadPrefab(string name, UmaDatabaseEntry entry)
        {
            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry, neverUnload: true, isRecursive: true);
            if (bundle == null)
                return null;

            string bare = name.Substring(name.LastIndexOf('/') + 1);

            foreach (string assetName in bundle.GetAllAssetNames())
            {
                if (assetName.IndexOf(bare, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    GameObject prefab = bundle.LoadAsset<GameObject>(assetName);
                    if (prefab != null)
                        return prefab;
                }
            }

            foreach (GameObject go in bundle.LoadAllAssets<GameObject>())
            {
                if (go != null && go.name.IndexOf(bare, StringComparison.OrdinalIgnoreCase) >= 0)
                    return go;
            }

            return null;
        }

        // spot light fixtures should sit under the stage parent named by the cut data,
        // falling back to the stage root (overrideParent wins when explicitly supplied).
        private static Transform ResolveParent(StageController stage, string parentName, GameObject overrideParent)
        {
            if (overrideParent != null)
                return overrideParent.transform;

            if (stage == null)
                return null;

            if (!string.IsNullOrEmpty(parentName))
            {
                Transform[] all = stage.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in all)
                {
                    if (t != null && string.Equals(t.name, parentName, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }

            return stage.transform;
        }

        // register the runtime fixture so StageBlinkLightDriver's StageObjectMap
        // name lookup finds it and drives its blinklight renderers.
        private static void RegisterInStage(StageController stage, string instanceName, GameObject instance)
        {
            if (stage == null || instance == null || string.IsNullOrEmpty(instanceName))
                return;

            if (stage.StageObjectMap != null)
                stage.StageObjectMap[instanceName] = instance;
        }
    }
}