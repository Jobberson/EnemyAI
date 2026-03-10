#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SnogTools.AI.Editor
{
    public static class TagLayerUtility
    {
        private const string TagManagerAsset = "ProjectSettings/TagManager.asset";

        public static int EnsureLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return -1;

            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
                return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(TagManagerAsset)[0]);
            var layersProp = tagManager.FindProperty("layers");

            // user layers are 8..31
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    return LayerMask.NameToLayer(layerName);
                }
                if (sp.stringValue == layerName)
                {
                    return i;
                }
            }

            Debug.LogWarning($"[TagLayerUtility] Could not create layer '{layerName}' (no empty slots).");
            return -1;
        }

        public static bool TagExists(string tag)
        {
            try
            {
                return UnityEditorInternal.InternalEditorUtility.tags != null &&
                       System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, tag) >= 0;
            }
            catch
            {
                // Fallback via TagManager asset
                var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(TagManagerAsset)[0]);
                var tagsProp = tagManager.FindProperty("tags");
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                        return true;
                }
                return false;
            }
        }

        public static void EnsureTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            if (TagExists(tag))
                return;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(TagManagerAsset)[0]);
            var tagsProp = tagManager.FindProperty("tags");

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            var sp = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
            sp.stringValue = tag;

            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        public static void AssignLayer(GameObject go, string layerName, bool includeChildren = true)
        {
            int li = LayerMask.NameToLayer(layerName);
            if (li < 0)
                return;

            go.layer = li;
            if (includeChildren)
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = li;
                }
            }
        }
    }
}
#endif