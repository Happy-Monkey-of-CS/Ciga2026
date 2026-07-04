using Ciga.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossTrapDamageSetup
{
    private const string BossPrefabPath = "Assets/Prefabs/Boss.prefab";
    private const float DefaultTrapDamage = 35f;

    [MenuItem("Tools/Ciga/Apply Boss Trap Damage")]
    public static void ApplyBossTrapDamage()
    {
        bool changed = false;

        BossController2D sceneBoss = Object.FindFirstObjectByType<BossController2D>();
        if (sceneBoss != null)
        {
            changed |= SetTrapDamage(sceneBoss);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(sceneBoss.gameObject.scene);
                EditorSceneManager.SaveScene(sceneBoss.gameObject.scene);
            }
        }

        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        if (bossPrefab != null)
        {
            BossController2D prefabBoss = bossPrefab.GetComponent<BossController2D>();
            if (prefabBoss != null && SetTrapDamage(prefabBoss))
            {
                EditorUtility.SetDirty(prefabBoss);
                PrefabUtility.SavePrefabAsset(bossPrefab);
                changed = true;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(changed
            ? "Boss trap damage applied to current scene Boss and Boss prefab where available."
            : "Boss trap damage was already configured.");
    }

    private static bool SetTrapDamage(BossController2D boss)
    {
        SerializedObject serialized = new SerializedObject(boss);
        SerializedProperty trapDamage = serialized.FindProperty("trapDamage");
        if (trapDamage == null)
        {
            return false;
        }

        trapDamage.floatValue = DefaultTrapDamage;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
