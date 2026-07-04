using System.IO;
using Ciga.Demo;
using UnityEditor;
using UnityEngine;

namespace Ciga.Editor
{
    /// <summary>
    /// Open boss.unity first, then run Tools → Extract Boss Prefab.
    /// This creates Assets/Prefabs/Boss.prefab from the Boss GameObject
    /// in the currently open scene.
    /// </summary>
    public static class BossPrefabExtractor
    {
        private const string PrefabOutputPath = "Assets/Prefabs/Boss.prefab";
        private const string MaterialOutputPath = "Assets/Demo/Boss/BossAbilityLineMaterial.mat";

        [MenuItem("Tools/Extract Boss Prefab", priority = 110)]
        public static void ExtractBossPrefab()
        {
            // Find the boss in the currently open scene
            BossController2D bossController = Object.FindFirstObjectByType<BossController2D>();
            if (bossController == null)
            {
                Debug.LogError("[ExtractBossPrefab] No BossController2D found in the current scene. Open boss.unity first.");
                return;
            }

            GameObject bossObject = bossController.gameObject;

            // Ensure directories exist
            if (!Directory.Exists("Assets/Prefabs"))
                Directory.CreateDirectory("Assets/Prefabs");
            string matDir = Path.GetDirectoryName(MaterialOutputPath);
            if (!string.IsNullOrEmpty(matDir) && !Directory.Exists(matDir))
                Directory.CreateDirectory(matDir);

            // Extract LineRenderer material if it's embedded in the scene
            LineRenderer bossLine = bossObject.GetComponent<LineRenderer>();
            if (bossLine != null && bossLine.sharedMaterial != null)
            {
                Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialOutputPath);
                if (existing == null)
                {
                    Material newMat = new Material(bossLine.sharedMaterial);
                    AssetDatabase.CreateAsset(newMat, MaterialOutputPath);
                    bossLine.sharedMaterial = newMat;
                    Debug.Log($"[ExtractBossPrefab] Created material: {MaterialOutputPath}");
                }
                else
                {
                    bossLine.sharedMaterial = existing;
                }
            }

            // Clear scene-specific player reference before saving prefab
            SerializedObject so = new SerializedObject(bossController);
            so.FindProperty("player").objectReferenceValue = null;
            so.ApplyModifiedProperties();

            // Create / overwrite prefab
            PrefabUtility.SaveAsPrefabAsset(bossObject, PrefabOutputPath);
            Debug.Log($"[ExtractBossPrefab] Prefab saved: {PrefabOutputPath}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ExtractBossPrefab] Done. Drop Boss.prefab into any scene and assign the Player reference.");
        }
    }
}
