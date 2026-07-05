using System.Collections.Generic;
using System.IO;
using Ciga.Demo;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MermaidEnemyAnimationBuilder
{
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string SpriteFolder = "Assets/Sprites/Mermaid/Enemy";
    private const string OutputFolder = "Assets/Demo/Enemy";
    private const string ClipFolder = "Assets/Demo/Enemy/Clips";
    private const string ControllerPath = "Assets/Demo/Enemy/MermaidEnemyAnimator.controller";

    [MenuItem("Tools/Ciga/Apply Mermaid Enemy Animations")]
    public static void ApplyMermaidEnemyAnimations()
    {
        RuntimeAnimatorController controller = CreateOrUpdateController();
        Sprite defaultSprite = LoadSprite("敌人向右站立/敌人向右站立1");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Enemy prefab not found at {EnemyPrefabPath}.");
            return;
        }

        try
        {
            SpriteRenderer renderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = defaultSprite;
                renderer.color = Color.white;
                EditorUtility.SetDirty(renderer);
            }

            Animator animator = prefabRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = prefabRoot.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
            Debug.Log("Applied Mermaid enemy animations to Enemy prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static RuntimeAnimatorController CreateOrUpdateController()
    {
        EnsureFolder(OutputFolder);
        EnsureFolder(ClipFolder);
        EnsureEnemyTextureImportSettings();

        AnimationClip rightIdle = CreateClip("Enemy_RightIdle", true, 0.18f, "敌人向右站立/敌人向右站立1", "敌人向右站立/敌人向右站立2");
        AnimationClip leftIdle = CreateClip("Enemy_LeftIdle", true, 0.18f, "敌人向左站立/敌人向左站立1", "敌人向左站立/敌人向左站立2");
        AnimationClip rightRun = CreateClip("Enemy_RightRun", true, 0.10f, "敌人向右跑/敌人右跑1", "敌人向右跑/敌人右跑2", "敌人向右跑/敌人右跑3", "敌人向右跑/敌人右跑4");
        AnimationClip leftRun = CreateClip("Enemy_LeftRun", true, 0.10f, "敌人向左跑/敌人左跑1", "敌人向左跑/敌人左跑2", "敌人向左跑/敌人左跑3", "敌人向左跑/敌人左跑4");

        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Direction",
            type = AnimatorControllerParameterType.Int,
            defaultInt = 1,
        });

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState rightIdleState = AddState(stateMachine, "RightIdle", rightIdle, 160f, 80f);
        AnimatorState rightRunState = AddState(stateMachine, "RightRun", rightRun, 390f, 80f);
        AnimatorState leftIdleState = AddState(stateMachine, "LeftIdle", leftIdle, 160f, 240f);
        AnimatorState leftRunState = AddState(stateMachine, "LeftRun", leftRun, 390f, 240f);
        stateMachine.defaultState = rightIdleState;

        AddTransition(rightIdleState, rightRunState, true, 1);
        AddTransition(rightRunState, rightIdleState, false, 1);
        AddTransition(leftIdleState, leftRunState, true, -1);
        AddTransition(leftRunState, leftIdleState, false, -1);
        AddDirectionTransition(rightIdleState, leftIdleState, false, -1);
        AddDirectionTransition(rightRunState, leftRunState, true, -1);
        AddDirectionTransition(leftIdleState, rightIdleState, false, 1);
        AddDirectionTransition(leftRunState, rightRunState, true, 1);
        AddTransition(rightIdleState, leftRunState, true, -1);
        AddTransition(leftIdleState, rightRunState, true, 1);
        AddTransition(rightRunState, leftIdleState, false, -1);
        AddTransition(leftRunState, rightIdleState, false, 1);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip CreateClip(string clipName, bool loop, float frameDuration, params string[] spriteRelativePaths)
    {
        string clipPath = $"{ClipFolder}/{clipName}.anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(clipPath);
        }

        AnimationClip clip = new AnimationClip
        {
            frameRate = Mathf.RoundToInt(1f / Mathf.Max(0.01f, frameDuration))
        };
        AssetDatabase.CreateAsset(clip, clipPath);
        AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceUpdate);

        List<ObjectReferenceKeyframe> frames = new List<ObjectReferenceKeyframe>();
        for (int i = 0; i < spriteRelativePaths.Length; i++)
        {
            Sprite sprite = LoadSprite(spriteRelativePaths[i]);
            if (sprite == null)
            {
                Debug.LogWarning($"Enemy sprite not found: {spriteRelativePaths[i]}");
                continue;
            }

            frames.Add(new ObjectReferenceKeyframe
            {
                time = i * frameDuration,
                value = sprite,
            });
        }

        if (frames.Count > 0)
        {
            frames.Add(new ObjectReferenceKeyframe
            {
                time = frames.Count * frameDuration,
                value = loop ? frames[0].value : frames[frames.Count - 1].value,
            });
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.startTime = 0f;
        settings.stopTime = frames.Count > 0 ? frames[frames.Count - 1].time : frameDuration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite",
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, frames.ToArray());
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    private static Sprite LoadSprite(string relativePathWithoutExtension)
    {
        string path = $"{SpriteFolder}/{relativePathWithoutExtension}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite childSprite)
            {
                return childSprite;
            }
        }

        return null;
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, float x, float y)
    {
        AnimatorState state = stateMachine.AddState(name, new Vector3(x, y, 0f));
        state.motion = motion;
        return state;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, bool moving, int direction)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.03f;
        transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsMoving");
        transition.AddCondition(AnimatorConditionMode.Equals, direction, "Direction");
    }

    private static void AddDirectionTransition(AnimatorState from, AnimatorState to, bool moving, int direction)
    {
        AddTransition(from, to, moving, direction);
    }

    private static void EnsureEnemyTextureImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 300f))
            {
                importer.spritePixelsPerUnit = 300f;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
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
