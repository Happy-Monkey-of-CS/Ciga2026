using System.IO;
using Ciga.Demo;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DarkMermaidBossPrefabBuilder
{
    private const string BossPrefabPath = "Assets/Prefabs/Boss.prefab";
    private const string BossControllerPath = "Assets/Demo/Boss/DarkMermaidBossAnimator.controller";
    private const string MermaidClipFolder = "Assets/Demo/Mermaid/Clips";

    private static readonly Color DarkMermaidColor = new Color(0.22f, 0.14f, 0.28f, 1f);

    [MenuItem("Tools/Ciga/Apply Dark Mermaid Boss Prefab")]
    public static void ApplyDarkMermaidBossPrefab()
    {
        MermaidPlayerAnimationBuilder.CreateOrUpdateController();
        RuntimeAnimatorController controller = CreateOrUpdateBossController();
        Sprite idleSprite = MermaidPlayerAnimationBuilder.LoadSprite("Idle_1");
        Sprite chainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MermaidPlayerAnimationBuilder.ChainTexturePath);
        Sprite anchorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MermaidPlayerAnimationBuilder.AnchorIconPath);

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Boss prefab not found at {BossPrefabPath}.");
            return;
        }

        try
        {
            SpriteRenderer renderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                SerializedObject rendererSerialized = new SerializedObject(renderer);
                SerializedProperty spriteProperty = rendererSerialized.FindProperty("m_Sprite");
                if (spriteProperty != null)
                {
                    spriteProperty.objectReferenceValue = idleSprite;
                }

                rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.color = DarkMermaidColor;
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 11);
                EditorUtility.SetDirty(renderer);
            }

            Animator animator = prefabRoot.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);
            }

            BossController2D bossController = prefabRoot.GetComponent<BossController2D>();
            if (bossController != null)
            {
                SerializedObject serializedBoss = new SerializedObject(bossController);
                Set(serializedBoss, "abilityRopeSprite", chainSprite);
                Set(serializedBoss, "abilityAnchorSprite", anchorSprite);
                Set(serializedBoss, "abilityRopeWidth", 0.12f);
                Set(serializedBoss, "abilityRopeSegmentLength", 0.18f);
                Set(serializedBoss, "abilityRopeOriginOffset", new Vector2(0.15f, 0.65f));
                Set(serializedBoss, "abilityAnchorScale", 1f);
                serializedBoss.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bossController);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, BossPrefabPath);
            Debug.Log("Applied dark Mermaid visuals and animations to Boss prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static RuntimeAnimatorController CreateOrUpdateBossController()
    {
        EnsureFolder("Assets/Demo");
        EnsureFolder("Assets/Demo/Boss");

        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(BossControllerPath);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(BossControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(BossControllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimationClip idle = LoadClip("Mermaid_Idle");
        AnimationClip run = LoadClip("Mermaid_Run");
        AnimationClip attack = LoadClip("Mermaid_AttackH");
        AnimationClip death = LoadClip("Mermaid_Death");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = AddState(stateMachine, "Idle", idle, 160f, 80f);
        AnimatorState runState = AddState(stateMachine, "Run", run, 390f, 80f);
        AnimatorState attackState = AddState(stateMachine, "Attack", attack, 390f, -80f);
        AnimatorState deathState = AddState(stateMachine, "Death", death, 620f, -80f);
        stateMachine.defaultState = idleState;

        AddBoolTransition(idleState, runState, "IsMoving", true, 0.05f);
        AddBoolTransition(runState, idleState, "IsMoving", false, 0.05f);
        AddAnyTriggerTransition(stateMachine, attackState, "Attack", 0.02f);
        AddAnyTriggerTransition(stateMachine, deathState, "Death", 0.02f);
        AddExitByMoving(attackState, idleState, runState);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void Set(SerializedObject serialized, string propertyName, Vector2 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
        }
    }

    private static AnimationClip LoadClip(string name)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{MermaidClipFolder}/{name}.anim");
        if (clip == null)
        {
            Debug.LogWarning($"Missing Mermaid clip: {name}");
        }

        return clip;
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, float x, float y)
    {
        AnimatorState state = stateMachine.AddState(name, new Vector3(x, y, 0f));
        state.motion = motion;
        return state;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddAnyTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState to, string trigger, float duration)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void AddExitByMoving(AnimatorState from, AnimatorState idleState, AnimatorState runState)
    {
        AnimatorStateTransition toIdle = from.AddTransition(idleState);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 0.9f;
        toIdle.duration = 0.04f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition toRun = from.AddTransition(runState);
        toRun.hasExitTime = true;
        toRun.exitTime = 0.9f;
        toRun.duration = 0.04f;
        toRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
