using Ciga.Demo;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RuntimeAnimatorController = UnityEngine.RuntimeAnimatorController;

public static class BossSceneBuilder
{
    private readonly struct StepMovementStepConfig
    {
        public readonly StepMover2D.StepMovementAction Action;
        public readonly float Duration;
        public readonly float SpeedMultiplier;

        public StepMovementStepConfig(StepMover2D.StepMovementAction action, float duration, float speedMultiplier = 1f)
        {
            Action = action;
            Duration = duration;
            SpeedMultiplier = speedMultiplier;
        }
    }

    private readonly struct EnemyMovementStepConfig
    {
        public readonly Enemy2D.EnemyMovementAction Action;
        public readonly float Duration;
        public readonly float SpeedMultiplier;

        public EnemyMovementStepConfig(Enemy2D.EnemyMovementAction action, float duration, float speedMultiplier = 1f)
        {
            Action = action;
            Duration = duration;
            SpeedMultiplier = speedMultiplier;
        }
    }

    private const string ScenePath = "Assets/Scenes/boss.unity";
    private const string WhiteSpritePath = "Assets/Demo/WhitePixelTexture.asset";
    private const string NoFrictionMaterialPath = "Assets/Demo/NoFriction2D.physicsMaterial2D";
    private const string RopeMaterialPath = "Assets/Demo/BossAbilityLineMaterial.mat";
    private const string StepPrefabPath = "Assets/Prefabs/Step.prefab";
    private const string TallWallPrefabPath = "Assets/Prefabs/TallWall.prefab";
    private const string TrapPrefabPath = "Assets/Prefabs/Trap.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string HeroKnightSpritePath = "Assets/Hero Knight - Pixel Art/Sprites/HeroKnight.png";
    private const string HeroKnightControllerPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_AnimController.controller";
    private const string BossAnimatorControllerPath = "Assets/Demo/Boss/BossAnimator.controller";
    private const string BossIdleClipPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_Idle.anim";
    private const string BossRunClipPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_Run.anim";
    private const string BossAttackClipPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_Attack1.anim";
    private const string BossDeathClipPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_Death.anim";
    private const int GroundLayer = 0;
    private const int PlayerLayer = 2;
    private const string GroundTag = "DemoGround";
    private const string StepTag = "GrappleStep";
    private const string WallTag = "GrappleWall";
    private const string TrapTag = "Trap";
    private const string EnemyTag = "Enemy";

    [MenuItem("Tools/Ciga/Build Boss Scene")]
    public static void BuildBossScene()
    {
        EnsureFolders();
        EnsureTags();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "boss";

        Sprite whiteSprite = CreateSprite(WhiteSpritePath);
        PhysicsMaterial2D noFrictionMaterial = CreateNoFrictionMaterial();
        Material lineMaterial = CreateLineMaterial();
        RuntimeAnimatorController heroController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroKnightControllerPath);
        RuntimeAnimatorController bossController = CreateBossAnimatorController();
        Sprite heroSprite = LoadHeroSprite();

        GameObject world = new GameObject("World");
        GameObject platforms = new GameObject("Platforms");
        platforms.transform.SetParent(world.transform);
        GameObject traps = new GameObject("Traps");
        traps.transform.SetParent(world.transform);
        GameObject enemies = new GameObject("Enemies");
        enemies.transform.SetParent(world.transform);
        GameObject bossTools = new GameObject("Boss Tools");
        bossTools.transform.SetParent(world.transform);

        CreateBackground(whiteSprite, world.transform);
        CreateGround("Ground_Runway", new Vector2(42f, -2.4f), new Vector2(116f, 1f), new Color(0.18f, 0.42f, 0.24f), whiteSprite, noFrictionMaterial, platforms.transform);

        CreateStep("Step_Start_01", new Vector2(-1.5f, -0.95f), new Vector2(3.2f, 0.45f), platforms.transform, 0.45f,
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveRightForDuration, 1.8f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.StopForDuration, 0.6f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveLeftForDuration, 1.8f));
        CreateStep("Step_Start_02", new Vector2(4.2f, -0.45f), new Vector2(2.6f, 0.45f), platforms.transform, 0f);
        CreateStep("Step_Chase_01", new Vector2(9.5f, 0.35f), new Vector2(3.4f, 0.45f), platforms.transform, 0.55f,
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveUpForDuration, 1.2f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveDownForDuration, 1.2f));
        CreateStep("Step_Chase_02", new Vector2(15.2f, 1.05f), new Vector2(2.8f, 0.45f), platforms.transform, 0.75f,
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveLeftForDuration, 1.4f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.StopForDuration, 0.5f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveRightForDuration, 1.4f));
        CreateStep("Step_Chase_03", new Vector2(21.5f, 1.85f), new Vector2(3.1f, 0.45f), platforms.transform, 0f);
        CreateStep("Step_Chase_04", new Vector2(28.4f, 0.8f), new Vector2(3.5f, 0.45f), platforms.transform, 0.65f,
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveDownForDuration, 1f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveUpForDuration, 1f));
        CreateStep("Step_Chase_05", new Vector2(36f, -0.15f), new Vector2(3.1f, 0.45f), platforms.transform, 0f);
        CreateStep("Step_Chase_06", new Vector2(43.2f, 0.95f), new Vector2(3.8f, 0.45f), platforms.transform, 0.7f,
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveRightForDuration, 1.3f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveLeftForDuration, 1.3f));
        CreateStep("Step_Chase_07", new Vector2(51f, 1.75f), new Vector2(2.8f, 0.45f), platforms.transform, 0f);
        CreateStep("Step_Chase_08", new Vector2(60f, 0.6f), new Vector2(4.2f, 0.45f), platforms.transform, 0.55f,
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveUpForDuration, 1.1f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveDownForDuration, 1.1f));
        CreateStep("Step_Chase_09", new Vector2(70f, 1.3f), new Vector2(3.3f, 0.45f), platforms.transform, 0f);
        CreateStep("Step_Chase_10", new Vector2(82f, 0.15f), new Vector2(4.6f, 0.45f), platforms.transform, 0.75f,
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveRightForDuration, 1.5f),
            new StepMovementStepConfig(StepMover2D.StepMovementAction.MoveLeftForDuration, 1.5f));

        CreateStep("Boss_Ammo_Step_01", new Vector2(12.8f, -1.1f), new Vector2(1.7f, 0.42f), bossTools.transform, 0f);
        CreateStep("Boss_Ammo_Step_02", new Vector2(25.8f, -1.15f), new Vector2(1.9f, 0.42f), bossTools.transform, 0f);
        CreateStep("Boss_Ammo_Step_03", new Vector2(39.4f, -0.9f), new Vector2(1.8f, 0.42f), bossTools.transform, 0f);
        CreateStep("Boss_Ammo_Step_04", new Vector2(55.8f, -1.05f), new Vector2(2f, 0.42f), bossTools.transform, 0f);
        CreateStep("Boss_Ammo_Step_05", new Vector2(74.5f, -1f), new Vector2(2.2f, 0.42f), bossTools.transform, 0f);

        CreatePrefabInstance(TrapPrefabPath, "Trap_01", new Vector2(7.2f, -1.86f), new Vector2(1.3f, 0.55f), new Color(0.95f, 0.08f, 0.06f), traps.transform);
        CreatePrefabInstance(TrapPrefabPath, "Trap_02", new Vector2(18.5f, -1.86f), new Vector2(1.5f, 0.55f), new Color(0.95f, 0.08f, 0.06f), traps.transform);
        CreatePrefabInstance(TrapPrefabPath, "Trap_03", new Vector2(33.8f, -1.86f), new Vector2(1.6f, 0.55f), new Color(0.95f, 0.08f, 0.06f), traps.transform);
        CreatePrefabInstance(TrapPrefabPath, "Trap_04", new Vector2(49.5f, -1.86f), new Vector2(1.5f, 0.55f), new Color(0.95f, 0.08f, 0.06f), traps.transform);
        CreatePrefabInstance(TrapPrefabPath, "Trap_05", new Vector2(68.8f, -1.86f), new Vector2(1.8f, 0.55f), new Color(0.95f, 0.08f, 0.06f), traps.transform);

        CreateEnemy("Enemy_Ground_01", new Vector2(6f, -1.42f), enemies.transform, 0.8f,
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveRightForDuration, 1.2f),
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.StopForDuration, 0.7f),
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveLeftForDuration, 1.2f));
        CreateEnemy("Enemy_Step_01", new Vector2(15.2f, 1.7f), enemies.transform, 0.9f,
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveLeftUntilEdge, 0f),
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.StopForDuration, 0.8f),
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveRightUntilEdge, 0f));
        CreateEnemy("Enemy_Step_02", new Vector2(43.2f, 1.6f), enemies.transform, 0.85f,
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveRightForDuration, 1.4f),
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveLeftForDuration, 1.4f));
        CreateEnemy("Enemy_Step_03", new Vector2(70f, 2f), enemies.transform, 0.9f,
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveLeftUntilEdge, 0f),
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.StopForDuration, 0.6f),
            new EnemyMovementStepConfig(Enemy2D.EnemyMovementAction.MoveRightUntilEdge, 0f));

        GameObject player = CreatePlayer(heroSprite, heroController, noFrictionMaterial, lineMaterial);
        GameObject boss = CreateBoss(heroSprite, bossController, noFrictionMaterial, lineMaterial, player.GetComponent<PlayerController2D>());
        CreateCamera(player.transform);
        CreateInstructions();

        Selection.activeGameObject = boss;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Boss scene created at {ScenePath}. Open it and press Play.");
    }

    private static GameObject CreatePlayer(Sprite sprite, RuntimeAnimatorController controller, PhysicsMaterial2D material, Material lineMaterial)
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.layer = PlayerLayer;
        player.transform.position = new Vector3(-7.2f, -1.15f, 0f);

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 10;

        Animator animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 3f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.offset = new Vector2(0f, 0.66f);
        collider.size = new Vector2(0.73f, 1.2f);
        collider.sharedMaterial = material;

        LineRenderer line = player.AddComponent<LineRenderer>();
        ConfigureLine(line, lineMaterial, 0.05f, 14);

        PlayerController2D controller2D = player.AddComponent<PlayerController2D>();
        SerializedObject serialized = new SerializedObject(controller2D);
        Set(serialized, "autoRunSpeed", 4f);
        Set(serialized, "wrapAtMapEdges", false);
        Set(serialized, "wrapLeftX", -9.5f);
        Set(serialized, "wrapRightX", 100f);
        Set(serialized, "grappleAimRadius", 6.2f);
        Set(serialized, "strikeAimRadius", 2.5f);
        Set(serialized, "groundMask", new LayerMask { value = 1 << GroundLayer });
        Set(serialized, "grappleMask", new LayerMask { value = 1 << GroundLayer });
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    private static GameObject CreateBoss(Sprite sprite, RuntimeAnimatorController controller, PhysicsMaterial2D material, Material lineMaterial, PlayerController2D player)
    {
        GameObject boss = new GameObject("Boss");
        boss.layer = GroundLayer;
        boss.transform.position = new Vector3(-12.6f, -1.1f, 0f);
        boss.transform.localScale = new Vector3(1.35f, 1.35f, 1f);

        SpriteRenderer renderer = boss.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 0.45f, 0.45f);
        renderer.sortingOrder = 11;

        Animator animator = boss.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Rigidbody2D body = boss.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 3f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = boss.AddComponent<BoxCollider2D>();
        collider.offset = new Vector2(0f, 0.66f);
        collider.size = new Vector2(0.82f, 1.3f);
        collider.sharedMaterial = material;

        LineRenderer line = boss.AddComponent<LineRenderer>();
        ConfigureLine(line, lineMaterial, 0.08f, 16);

        BossController2D bossController = boss.AddComponent<BossController2D>();
        SerializedObject serialized = new SerializedObject(bossController);
        Set(serialized, "player", player);
        Set(serialized, "bossAbilityMask", new LayerMask { value = 1 << GroundLayer });
        Set(serialized, "grappleRange", 9f);
        Set(serialized, "objectAbilityRange", 8f);
        Set(serialized, "moveSpeed", 4.35f);
        Set(serialized, "jumpForce", 11.5f);
        Set(serialized, "gravityScale", 3f);
        Set(serialized, "maxFallSpeed", 12f);
        Set(serialized, "playerRunSpeedEstimate", 4f);
        Set(serialized, "desiredChaseDistance", 3.4f);
        Set(serialized, "catchUpDistance", 7f);
        Set(serialized, "catchUpSpeedBonus", 3.2f);
        Set(serialized, "maxChaseSpeed", 8.8f);
        Set(serialized, "burstCatchUpDistance", 9.8f);
        Set(serialized, "burstSpeed", 11.2f);
        Set(serialized, "burstDuration", 0.55f);
        Set(serialized, "burstCooldown", 2.1f);
        Set(serialized, "emergencyRepositionGap", 17f);
        Set(serialized, "emergencyRepositionDistance", 5.6f);
        Set(serialized, "maxHealth", 100f);
        Set(serialized, "playerAttackDamage", 20f);
        Set(serialized, "struckStepDamage", 35f);
        Set(serialized, "deathDestroyDelay", 1.2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return boss;
    }

    private static GameObject CreateStep(string name, Vector2 position, Vector2 scale, Transform parent, float moveSpeed, params StepMovementStepConfig[] movement)
    {
        GameObject step = CreatePrefabInstance(StepPrefabPath, name, position, scale, new Color(0.32f, 0.62f, 0.32f), parent);
        ConfigureStepMovement(step, moveSpeed, movement);
        return step;
    }

    private static GameObject CreateEnemy(string name, Vector2 position, Transform parent, float moveSpeed, params EnemyMovementStepConfig[] movement)
    {
        GameObject enemy = CreatePrefabInstance(EnemyPrefabPath, name, position, new Vector2(0.75f, 0.95f), new Color(0.55f, 0.12f, 0.82f), parent);
        Enemy2D enemy2D = enemy.GetComponent<Enemy2D>();
        if (enemy2D == null)
        {
            return enemy;
        }

        SerializedObject serialized = new SerializedObject(enemy2D);
        Set(serialized, "moveSpeed", moveSpeed);
        SerializedProperty plan = serialized.FindProperty("movementPlan");
        if (plan != null)
        {
            plan.arraySize = movement.Length;
            for (int i = 0; i < movement.Length; i++)
            {
                SerializedProperty item = plan.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("action").enumValueIndex = (int)movement[i].Action;
                item.FindPropertyRelative("duration").floatValue = movement[i].Duration;
                item.FindPropertyRelative("speedMultiplier").floatValue = movement[i].SpeedMultiplier;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return enemy;
    }

    private static void ConfigureStepMovement(GameObject stepObject, float moveSpeed, params StepMovementStepConfig[] movement)
    {
        StepMover2D mover = stepObject.GetComponent<StepMover2D>();
        if (mover == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(mover);
        Set(serialized, "moveSpeed", moveSpeed);
        SerializedProperty plan = serialized.FindProperty("movementPlan");
        if (plan != null)
        {
            plan.arraySize = movement.Length;
            for (int i = 0; i < movement.Length; i++)
            {
                SerializedProperty item = plan.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("action").enumValueIndex = (int)movement[i].Action;
                item.FindPropertyRelative("duration").floatValue = movement[i].Duration;
                item.FindPropertyRelative("speedMultiplier").floatValue = movement[i].SpeedMultiplier;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreatePrefabInstance(string prefabPath, string name, Vector2 position, Vector2 scale, Color color, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject instance = prefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
            : new GameObject(name);

        instance.name = name;
        instance.transform.SetParent(parent);
        instance.transform.position = new Vector3(position.x, position.y, 0f);
        instance.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
            renderer.sortingOrder = instance.CompareTag(TrapTag) ? 6 : 4;
        }

        Collider2D collider = instance.GetComponent<Collider2D>();
        if (collider != null && collider.sharedMaterial == null)
        {
            collider.sharedMaterial = CreateNoFrictionMaterial();
        }

        return instance;
    }

    private static void CreateGround(string name, Vector2 position, Vector2 scale, Color color, Sprite sprite, PhysicsMaterial2D material, Transform parent)
    {
        GameObject ground = CreateSpriteObject(name, sprite, position, scale, color);
        ground.tag = GroundTag;
        ground.layer = GroundLayer;
        ground.transform.SetParent(parent);
        BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
        collider.sharedMaterial = material;
        Rigidbody2D body = ground.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
    }

    private static void CreateBackground(Sprite sprite, Transform parent)
    {
        GameObject back = CreateSpriteObject("Boss Chase Backdrop", sprite, new Vector2(42f, 1.2f), new Vector2(124f, 14f), new Color(0.11f, 0.15f, 0.22f));
        back.transform.SetParent(parent);
        back.GetComponent<SpriteRenderer>().sortingOrder = -20;

        for (int i = 0; i < 18; i++)
        {
            float x = -16f + i * 6.4f;
            GameObject pillar = CreateSpriteObject($"Background Pillar {i + 1}", sprite, new Vector2(x, -0.2f), new Vector2(0.6f, 6.5f), new Color(0.18f, 0.2f, 0.28f));
            pillar.transform.SetParent(parent);
            pillar.GetComponent<SpriteRenderer>().sortingOrder = -15;
        }

        GameObject foreground = CreateSpriteObject("Foreground Framing Layer", sprite, new Vector2(42f, -3.15f), new Vector2(124f, 0.8f), new Color(0.04f, 0.05f, 0.07f, 0.95f));
        foreground.transform.SetParent(parent);
        foreground.GetComponent<SpriteRenderer>().sortingOrder = 30;
    }

    private static GameObject CreateCamera(Transform target)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(-2.5f, 0.3f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.2f;
        camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
        follow.SetTarget(target);
        return cameraObject;
    }

    private static void CreateInstructions()
    {
        GameObject textObject = new GameObject("Boss Scene Notes");
        textObject.transform.position = new Vector3(-8.8f, 4.1f, 0f);
        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = "Boss chase prototype: keep running right while the boss follows, grapples forward, and throws steps into the route.";
        text.characterSize = 0.28f;
        text.anchor = TextAnchor.MiddleLeft;
        text.color = new Color(0.88f, 0.92f, 1f);
    }

    private static GameObject CreateSpriteObject(string name, Sprite sprite, Vector2 position, Vector2 scale, Color color)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = new Vector3(position.x, position.y, 0f);
        gameObject.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        return gameObject;
    }

    private static void ConfigureLine(LineRenderer line, Material material, float width, int order)
    {
        line.enabled = false;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.textureMode = LineTextureMode.Tile;
        line.alignment = LineAlignment.View;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.sortingOrder = order;
        line.material = material;
    }

    private static Sprite CreateSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, assetPath);
        AssetDatabase.SaveAssets();

        string texturePath = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 1f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static Sprite LoadHeroSprite()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(HeroKnightSpritePath).OfType<Sprite>().ToArray();
        if (sprites.Length > 0)
        {
            return sprites[0];
        }

        return CreateSprite(WhiteSpritePath);
    }

    private static PhysicsMaterial2D CreateNoFrictionMaterial()
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionMaterialPath);
        if (material != null)
        {
            return material;
        }

        material = new PhysicsMaterial2D("NoFriction2D")
        {
            friction = 0f,
            bounciness = 0f,
        };
        AssetDatabase.CreateAsset(material, NoFrictionMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material CreateLineMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Sprites/Default"))
        {
            color = Color.white,
        };
        AssetDatabase.CreateAsset(material, RopeMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static RuntimeAnimatorController CreateBossAnimatorController()
    {
        EnsureFolder("Assets/Demo/Boss");

        UnityEditor.Animations.AnimatorController controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(BossAnimatorControllerPath);
        if (controller != null)
        {
            AssetDatabase.DeleteAsset(BossAnimatorControllerPath);
        }

        controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(BossAnimatorControllerPath);
        controller.AddParameter("IsMoving", UnityEngine.AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", UnityEngine.AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", UnityEngine.AnimatorControllerParameterType.Trigger);

        UnityEditor.Animations.AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;

        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BossIdleClipPath);
        AnimationClip runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BossRunClipPath);
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BossAttackClipPath);
        AnimationClip deathClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BossDeathClipPath);

        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(240f, 80f, 0f));
        idleState.motion = idleClip;
        AnimatorState runState = stateMachine.AddState("Run", new Vector3(480f, 80f, 0f));
        runState.motion = runClip;
        AnimatorState attackState = stateMachine.AddState("Attack", new Vector3(360f, 220f, 0f));
        attackState.motion = attackClip;
        AnimatorState deathState = stateMachine.AddState("Death", new Vector3(600f, 220f, 0f));
        deathState.motion = deathClip;

        stateMachine.defaultState = idleState;

        AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.05f;
        idleToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.05f;
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition idleToAttack = idleState.AddTransition(attackState);
        ConfigureAttackTransition(idleToAttack);
        AnimatorStateTransition runToAttack = runState.AddTransition(attackState);
        ConfigureAttackTransition(runToAttack);

        AnimatorStateTransition idleToDeath = idleState.AddTransition(deathState);
        ConfigureDeathTransition(idleToDeath);
        AnimatorStateTransition runToDeath = runState.AddTransition(deathState);
        ConfigureDeathTransition(runToDeath);
        AnimatorStateTransition attackToDeath = attackState.AddTransition(deathState);
        ConfigureDeathTransition(attackToDeath);

        AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.92f;
        attackToIdle.duration = 0.05f;
        attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition attackToRun = attackState.AddTransition(runState);
        attackToRun.hasExitTime = true;
        attackToRun.exitTime = 0.92f;
        attackToRun.duration = 0.05f;
        attackToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void ConfigureAttackTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = 0.02f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
    }

    private static void ConfigureDeathTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = 0.02f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Death");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Demo");
        EnsureFolder("Assets/Demo/Boss");
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

    private static void EnsureTags()
    {
        EnsureTag(GroundTag);
        EnsureTag(StepTag);
        EnsureTag(WallTag);
        EnsureTag(TrapTag);
        EnsureTag(EnemyTag);
    }

    private static void EnsureTag(string tag)
    {
        if (InternalEditorUtility.tags.Contains(tag))
        {
            return;
        }

        InternalEditorUtility.AddTag(tag);
    }

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void Set(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void Set(SerializedObject serialized, string propertyName, LayerMask value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value.value;
        }
    }
}
