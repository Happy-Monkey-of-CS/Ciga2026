using Ciga.Demo;
using UnityEditor;
using UnityEngine;

namespace Ciga.Editor
{
    /// <summary>
    /// Creates a CollectionManager in the current scene and wires up
    /// collectibles and audio clips automatically.
    /// Run via Tools → Setup Collection Manager.
    /// </summary>
    public static class CollectionManagerSetup
    {
        [MenuItem("Tools/Setup Collection Manager", priority = 112)]
        public static void Setup()
        {
            // 1. Create or find the CollectionManager
            CollectionManager2D manager = Object.FindFirstObjectByType<CollectionManager2D>();
            if (manager == null)
            {
                GameObject go = new GameObject("CollectionManager");
                manager = go.AddComponent<CollectionManager2D>();
                Undo.RegisterCreatedObjectUndo(go, "Create CollectionManager");
                Debug.Log("[CollectionSetup] Created CollectionManager.");
            }
            else
            {
                Debug.Log("[CollectionSetup] CollectionManager already exists in scene.");
            }

            // 2. Wire up audio
            SerializedObject so = new SerializedObject(manager);

            AudioClip allCollected = LoadClip("AllCollected");
            if (allCollected != null)
            {
                so.FindProperty("allCollectedClip").objectReferenceValue = allCollected;
            }

            so.ApplyModifiedProperties();

            // 3. Wire up collect clip on all Collectible2D in scene
            AudioClip pickup = LoadClip("Collectible_Pickup");
            if (pickup != null)
            {
                Collectible2D[] collectibles = Object.FindObjectsByType<Collectible2D>(FindObjectsSortMode.None);
                foreach (Collectible2D c in collectibles)
                {
                    SerializedObject cso = new SerializedObject(c);
                    cso.FindProperty("collectClip").objectReferenceValue = pickup;
                    cso.ApplyModifiedProperties();
                    EditorUtility.SetDirty(c);
                }
                Debug.Log($"[CollectionSetup] Assigned pickup sound to {collectibles.Length} collectibles.");
            }

            // 4. Try to assign boss prefab from Assets/Prefabs/Boss.prefab
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss.prefab");
            if (bossPrefab != null)
            {
                so.FindProperty("bossPrefab").objectReferenceValue = bossPrefab;
                so.ApplyModifiedProperties();
                Debug.Log("[CollectionSetup] Boss prefab assigned.");
            }
            else
            {
                Debug.LogWarning("[CollectionSetup] Boss.prefab not found. Run Tools → Extract Boss Prefab first, then assign it manually.");
            }

            // 5. Boss spawn point defaults to a few units ahead of the player
            PlayerController2D player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player != null)
            {
                GameObject spawnPoint = new GameObject("BossSpawnPoint");
                spawnPoint.transform.position = player.transform.position + Vector3.right * 10f + Vector3.up * 2f;
                Undo.RegisterCreatedObjectUndo(spawnPoint, "Create BossSpawnPoint");

                so.FindProperty("bossSpawnPoint").objectReferenceValue = spawnPoint.transform;
                so.ApplyModifiedProperties();
                Debug.Log("[CollectionSetup] Boss spawn point created.");
            }

            EditorUtility.SetDirty(manager);
            Debug.Log("[CollectionSetup] Done! Assign Total Needed and adjust spawn point as needed.");
        }

        private static AudioClip LoadClip(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:AudioClip");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Assets/Audio"))
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            Debug.LogWarning($"[CollectionSetup] Audio clip not found: {name}");
            return null;
        }
    }
}
