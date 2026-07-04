using Ciga.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossPlayerPrefabSync
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string BossScenePath = "Assets/Scenes/boss.unity";

    [MenuItem("Tools/Ciga/Apply Mermaid Player To Prefab And Boss Scene")]
    public static void ApplyMermaidPlayerToPrefabAndBossScene()
    {
        ApplyToPlayerPrefab();
        ApplyToBossScene();
        AssetDatabase.SaveAssets();
        Debug.Log("Applied Mermaid player animation and rope visuals to Player prefab and boss scene.");
    }

    private static void ApplyToPlayerPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Player prefab not found at {PlayerPrefabPath}.");
            return;
        }

        MermaidPlayerAnimationBuilder.ApplyMermaidPlayerVisuals(prefab);
        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
    }

    private static void ApplyToBossScene()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("No Player object found in boss scene.");
            return;
        }

        MermaidPlayerAnimationBuilder.ApplyMermaidPlayerVisuals(player);
        RebindBossToPlayer(player.GetComponent<PlayerController2D>());
        RebindCameraToPlayer(player.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, BossScenePath);
    }

    private static void RebindBossToPlayer(PlayerController2D player)
    {
        if (player == null)
        {
            return;
        }

        BossController2D boss = Object.FindFirstObjectByType<BossController2D>();
        if (boss == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(boss);
        SerializedProperty playerProperty = serialized.FindProperty("player");
        if (playerProperty != null)
        {
            playerProperty.objectReferenceValue = player;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boss);
        }
    }

    private static void RebindCameraToPlayer(Transform player)
    {
        CameraFollow2D cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(player);
            EditorUtility.SetDirty(cameraFollow);
        }
    }
}
