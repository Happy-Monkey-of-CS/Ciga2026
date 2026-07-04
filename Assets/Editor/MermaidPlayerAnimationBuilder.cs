using System.Collections.Generic;
using System.IO;
using Ciga.Demo;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using RuntimeAnimatorController = UnityEngine.RuntimeAnimatorController;

public static class MermaidPlayerAnimationBuilder
{
    public const string ControllerPath = "Assets/Demo/Mermaid/MermaidPlayerAnimator.controller";
    public const string MermaidFolder = "Assets/Sprites/Mermaid";
    public const string ChainTexturePath = "Assets/Sprites/Mermaid/Chain.png";
    public const string AnchorIconPath = "Assets/Sprites/Mermaid/AnchorIcon.png";
    public const string DemoScenePath = "Assets/Scenes/demo.unity";

    private const string ClipFolder = "Assets/Demo/Mermaid/Clips";
    private const string DeathFallbackClipPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_Death.anim";
    private const string MermaidChainMaterialPath = "Assets/Demo/Mermaid/MermaidChainMaterial.mat";

    [MenuItem("Tools/Ciga/Apply Mermaid Player To Demo Scene")]
    public static void ApplyMermaidPlayerToDemoScene()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("No Player object found in demo scene. Mermaid player visuals were not applied.");
            return;
        }

        ApplyMermaidPlayerVisuals(player);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        Debug.Log("Applied Mermaid player visuals to existing demo scene without rebuilding level content.");
    }

    [MenuItem("Tools/Ciga/Replace Demo Player With Mermaid Player")]
    public static void ReplaceDemoPlayerWithMermaidPlayer()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        GameObject oldPlayer = GameObject.Find("Player");
        if (oldPlayer == null)
        {
            Debug.LogWarning("No Player object found in demo scene. A new Mermaid Player will be created at origin.");
        }

        Vector3 position = oldPlayer != null ? oldPlayer.transform.position : Vector3.zero;
        Quaternion rotation = oldPlayer != null ? oldPlayer.transform.rotation : Quaternion.identity;
        Vector3 scale = oldPlayer != null ? oldPlayer.transform.localScale : Vector3.one;
        int layer = oldPlayer != null ? oldPlayer.layer : 2;
        string tag = oldPlayer != null ? oldPlayer.tag : "Player";

        SpriteRenderer oldRenderer = oldPlayer != null ? oldPlayer.GetComponent<SpriteRenderer>() : null;
        Rigidbody2D oldBody = oldPlayer != null ? oldPlayer.GetComponent<Rigidbody2D>() : null;
        Collider2D oldCollider = oldPlayer != null ? oldPlayer.GetComponent<Collider2D>() : null;
        LineRenderer oldLine = oldPlayer != null ? oldPlayer.GetComponent<LineRenderer>() : null;
        PlayerController2D oldController = oldPlayer != null ? oldPlayer.GetComponent<PlayerController2D>() : null;

        GameObject player = CreateMermaidPlayer(position, rotation, scale, layer, tag, oldRenderer, oldBody, oldCollider, oldLine, oldController);
        if (oldPlayer != null)
        {
            Object.DestroyImmediate(oldPlayer);
        }

        player.name = "Player";
        RebindCamerasToPlayer(player.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        Selection.activeGameObject = player;
        Debug.Log("Replaced demo Player with a newly-created Mermaid Player at the same position. Other scene objects were left untouched.");
    }

    public static RuntimeAnimatorController CreateOrUpdateController()
    {
        EnsureFolder("Assets/Demo");
        EnsureFolder("Assets/Demo/Mermaid");
        EnsureFolder(ClipFolder);
        EnsureMermaidTextureImportSettings();

        AnimationClip idle = CreateClip("Mermaid_Idle", true, 0.12f, "Idle_1", "Idle_2", "Idle_3", "Idle_4");
        AnimationClip run = CreateClip("Mermaid_Run", true, 0.09f, "Run_1", "Run_2", "Run_3", "Run_4");
        AnimationClip jump = CreateClip("Mermaid_Jump", true, 0.10f, "Jump_1", "Jump_2", "Jump_3", "Jump_4");
        AnimationClip wallSlide = CreateClip("Mermaid_WallSlide", true, 0.14f, "WallSlide_1", "WallSlide_2");
        AnimationClip climb = CreateClip("Mermaid_Climb", false, 0.16f, "Climb_1", "Climb_2");
        AnimationClip pullObject = CreateClip("Mermaid_PullObject", true, 0.12f, "WallJump_1", "WallJump_2", "WallJump_3");
        AnimationClip attack = CreateClip("Mermaid_AttackH", false, 0.08f, "AttackH_1", "AttackH_2", "AttackH_3", "AttackH_4");
        AnimationClip death = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathFallbackClipPath);

        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("AnimState", AnimatorControllerParameterType.Int);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("AirSpeedY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("WallSlide", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Block", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("noBlood", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack3", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("PullObject", AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "PreviewMode",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = true
        });

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = AddState(stateMachine, "Idle", idle, 180f, 80f);
        AnimatorState runState = AddState(stateMachine, "Run", run, 420f, 80f);
        AnimatorState jumpState = AddState(stateMachine, "Jump", jump, 300f, 250f);
        AnimatorState wallSlideState = AddState(stateMachine, "WallSlide", wallSlide, 60f, 250f);
        AnimatorState climbState = AddState(stateMachine, "Climb", climb, 540f, 250f);
        AnimatorState pullState = AddState(stateMachine, "PullObject", pullObject, 720f, 250f);
        AnimatorState attackState = AddState(stateMachine, "AttackH", attack, 540f, -80f);
        AnimatorState deathState = AddState(stateMachine, "Death", death != null ? death : idle, 780f, -80f);
        stateMachine.defaultState = idleState;

        AddIntTransition(idleState, runState, "AnimState", true, 0.04f);
        AddIntTransition(runState, idleState, "AnimState", false, 0.04f);

        AddAnyTriggerTransition(stateMachine, jumpState, "Jump");
        AddAnyTriggerTransition(stateMachine, climbState, "Block");
        AddAnyTriggerTransition(stateMachine, deathState, "Death");
        AddAnyTriggerTransition(stateMachine, attackState, "Attack1");
        AddAnyTriggerTransition(stateMachine, attackState, "Attack2");
        AddAnyTriggerTransition(stateMachine, attackState, "Attack3");
        AddAnyBoolTransition(stateMachine, wallSlideState, "WallSlide", true);
        AddAnyBoolTransition(stateMachine, pullState, "PullObject", true);

        AddExitByAnimState(jumpState, idleState, runState, true);
        AddExitByAnimState(climbState, idleState, runState, true);
        AddExitByAnimState(attackState, idleState, runState, true);
        AddBoolTransition(wallSlideState, idleState, "WallSlide", false, 0.05f);
        AddBoolTransition(pullState, idleState, "PullObject", false, 0.05f);
        AddBoolTransition(pullState, runState, "PullObject", false, 0.05f, "AnimState", true);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    public static void ApplyMermaidPlayerVisuals(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        RuntimeAnimatorController controller = CreateOrUpdateController();
        Sprite idleSprite = LoadSprite("Idle_1");
        Material chainMaterial = CreateChainMaterial(MermaidChainMaterialPath);

        SpriteRenderer spriteRenderer = player.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }

        Animator animator = player.GetComponent<Animator>();
        if (animator != null && controller != null)
        {
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        LineRenderer line = player.GetComponent<LineRenderer>();
        if (line != null && chainMaterial != null)
        {
            line.material = chainMaterial;
            line.textureMode = LineTextureMode.Tile;
        }

        MonoBehaviour playerController = player.GetComponent("PlayerController2D") as MonoBehaviour;
        if (playerController != null && idleSprite != null)
        {
            SerializedObject serialized = new SerializedObject(playerController);
            SerializedProperty grappleAimSprite = serialized.FindProperty("grappleAimSprite");
            if (grappleAimSprite != null)
            {
                grappleAimSprite.objectReferenceValue = idleSprite;
            }

            SerializedProperty strikeAimSprite = serialized.FindProperty("strikeAimSprite");
            if (strikeAimSprite != null)
            {
                strikeAimSprite.objectReferenceValue = idleSprite;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameObject CreateMermaidPlayer(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        int layer,
        string tag,
        SpriteRenderer oldRenderer,
        Rigidbody2D oldBody,
        Collider2D oldCollider,
        LineRenderer oldLine,
        PlayerController2D oldController)
    {
        RuntimeAnimatorController controller = CreateOrUpdateController();
        Sprite idleSprite = LoadSprite("Idle_1");
        Material chainMaterial = CreateChainMaterial(MermaidChainMaterialPath);

        GameObject player = new GameObject("Player");
        player.transform.SetPositionAndRotation(position, rotation);
        player.transform.localScale = scale;
        player.layer = layer;
        if (!string.IsNullOrEmpty(tag))
        {
            player.tag = tag;
        }

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        if (oldRenderer != null)
        {
            EditorUtility.CopySerialized(oldRenderer, renderer);
        }

        renderer.sprite = idleSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = oldRenderer != null ? oldRenderer.sortingOrder : 10;

        Animator animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        if (oldBody != null)
        {
            EditorUtility.CopySerialized(oldBody, body);
        }
        else
        {
            body.freezeRotation = true;
            body.gravityScale = 3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        CopyOrCreateCollider(player, oldCollider);

        LineRenderer line = player.AddComponent<LineRenderer>();
        if (oldLine != null)
        {
            EditorUtility.CopySerialized(oldLine, line);
        }
        else
        {
            line.positionCount = 2;
            line.enabled = false;
            line.useWorldSpace = true;
            line.startWidth = 0.06f;
            line.endWidth = 0.035f;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.sortingOrder = 20;
        }

        line.material = chainMaterial;
        line.textureMode = LineTextureMode.Tile;

        PlayerController2D playerController = player.AddComponent<PlayerController2D>();
        if (oldController != null)
        {
            EditorUtility.CopySerialized(oldController, playerController);
        }

        ApplyMermaidPlayerVisuals(player);
        return player;
    }

    private static void CopyOrCreateCollider(GameObject player, Collider2D oldCollider)
    {
        if (oldCollider is BoxCollider2D oldBox)
        {
            BoxCollider2D box = player.AddComponent<BoxCollider2D>();
            EditorUtility.CopySerialized(oldBox, box);
            return;
        }

        if (oldCollider is CapsuleCollider2D oldCapsule)
        {
            CapsuleCollider2D capsule = player.AddComponent<CapsuleCollider2D>();
            EditorUtility.CopySerialized(oldCapsule, capsule);
            return;
        }

        BoxCollider2D fallback = player.AddComponent<BoxCollider2D>();
        fallback.offset = new Vector2(0f, 0.66f);
        fallback.size = new Vector2(0.73f, 1.2f);
    }

    private static void RebindCamerasToPlayer(Transform player)
    {
        CameraFollow2D[] cameras = Object.FindObjectsByType<CameraFollow2D>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].SetTarget(player);
                EditorUtility.SetDirty(cameras[i]);
            }
        }
    }

    public static Sprite LoadSprite(string spriteName)
    {
        string path = $"{MermaidFolder}/{spriteName}.png";
        Sprite directSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (directSprite != null)
        {
            return directSprite;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                return sprite;
            }
        }

        return null;
    }

    public static Material CreateChainMaterial(string materialPath)
    {
        string parent = Path.GetDirectoryName(materialPath)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
        }

        Texture2D chain = AssetDatabase.LoadAssetAtPath<Texture2D>(ChainTexturePath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.shader = Shader.Find("Sprites/Default");
        material.mainTexture = chain;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static AnimationClip CreateClip(string clipName, bool loop, float frameDuration, params string[] spriteNames)
    {
        string clipPath = $"{ClipFolder}/{clipName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip != null)
        {
            AssetDatabase.DeleteAsset(clipPath);
        }

        clip = new AnimationClip
        {
            frameRate = Mathf.RoundToInt(1f / Mathf.Max(0.01f, frameDuration))
        };
        AssetDatabase.CreateAsset(clip, clipPath);

        List<ObjectReferenceKeyframe> frames = new List<ObjectReferenceKeyframe>();
        for (int i = 0; i < spriteNames.Length; i++)
        {
            Sprite sprite = LoadSprite(spriteNames[i]);
            if (sprite == null)
            {
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
        else
        {
            Debug.LogWarning($"Mermaid animation clip {clipName} was generated without frames. Check sprites in {MermaidFolder}.");
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
        AddRuntimeCondition(transition);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddIntTransition(AnimatorState from, AnimatorState to, string parameter, bool equalsOne, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        AddRuntimeCondition(transition);
        transition.AddCondition(equalsOne ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual, 1f, parameter);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value, float duration, string intParameter, bool intEqualsOne)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        AddRuntimeCondition(transition);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        transition.AddCondition(intEqualsOne ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual, 1f, intParameter);
    }

    private static void AddAnyTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState to, string trigger)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.02f;
        transition.canTransitionToSelf = false;
        AddRuntimeCondition(transition);
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void AddAnyBoolTransition(AnimatorStateMachine stateMachine, AnimatorState to, string parameter, bool value)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.03f;
        transition.canTransitionToSelf = false;
        AddRuntimeCondition(transition);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddExitByAnimState(AnimatorState from, AnimatorState idle, AnimatorState run, bool waitForExit = false)
    {
        AnimatorStateTransition toIdle = from.AddTransition(idle);
        toIdle.hasExitTime = waitForExit;
        toIdle.exitTime = waitForExit ? 0.9f : 0f;
        toIdle.duration = 0.04f;
        AddRuntimeCondition(toIdle);
        toIdle.AddCondition(AnimatorConditionMode.NotEqual, 1f, "AnimState");

        AnimatorStateTransition toRun = from.AddTransition(run);
        toRun.hasExitTime = waitForExit;
        toRun.exitTime = waitForExit ? 0.9f : 0f;
        toRun.duration = 0.04f;
        AddRuntimeCondition(toRun);
        toRun.AddCondition(AnimatorConditionMode.Equals, 1f, "AnimState");
    }

    private static void AddRuntimeCondition(AnimatorStateTransition transition)
    {
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "PreviewMode");
    }

    private static void EnsureMermaidTextureImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { MermaidFolder });
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
