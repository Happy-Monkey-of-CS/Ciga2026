using Ciga.Demo;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RuntimeAnimatorController = UnityEngine.RuntimeAnimatorController;

public static class DemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/demo.unity";
    private const string NoFrictionMaterialPath = "Assets/Demo/NoFriction2D.physicsMaterial2D";
    private const string GrappleRopeTexturePath = "Assets/Demo/GrappleRopeTexture.asset";
    private const string GrappleRopeMaterialPath = "Assets/Demo/GrappleRopeMaterial.mat";
    private const string StepPrefabPath = "Assets/Prefabs/Step.prefab";
    private const string TallWallPrefabPath = "Assets/Prefabs/TallWall.prefab";
    private const string TrapPrefabPath = "Assets/Prefabs/Trap.prefab";
    private const string HeroKnightSpritePath = "Assets/Hero Knight - Pixel Art/Sprites/HeroKnight.png";
    private const long HeroKnightHurtAimSpriteLocalId = 21300090;
    private const string HeroKnightControllerPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_AnimController.controller";
    private const int GroundLayer = 0;
    private const int PlayerLayer = 2;
    private const string GroundTag = "DemoGround";
    private const string StepTag = "GrappleStep";
    private const string WallTag = "GrappleWall";
    private const string TrapTag = "Trap";
    private const float DefaultPlayerAutoRunSpeed = 4f;
    private const float DefaultWrapLeftX = -9.5f;
    private const float DefaultWrapRightX = 21f;
    private const string AutoBuildSessionKey = "Ciga.DemoSceneBuilder.AutoBuildAttempted";

    [InitializeOnLoadMethod]
    private static void AutoBuildWhenMissing()
    {
        if (SessionState.GetBool(AutoBuildSessionKey, false) || File.Exists(ScenePath))
        {
            return;
        }

        SessionState.SetBool(AutoBuildSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isCompiling && !File.Exists(ScenePath))
            {
                BuildDemoScene();
            }
        };
    }

    [MenuItem("Tools/Ciga/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        EnsureFolders();
        EnsureTags();

        Scene scene = GetOrCreateDemoScene();
        EditorSceneManager.SetActiveScene(scene);
        if (string.IsNullOrEmpty(scene.path))
        {
            scene.name = "demo";
        }
        ClearScene(scene);

        GameObject world = new GameObject("World");
        GameObject platforms = new GameObject("Platforms");
        platforms.transform.SetParent(world.transform);
        GameObject traps = new GameObject("Traps");
        traps.transform.SetParent(world.transform);

        Material spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        Sprite whiteSprite = CreateSprite("Assets/Demo/WhitePixelTexture.asset");
        PhysicsMaterial2D noFrictionMaterial = CreateNoFrictionMaterial();
        Material grappleRopeMaterial = CreateGrappleRopeMaterial();
        EnsureDemoPrefabs(whiteSprite, noFrictionMaterial);

        CreateBackground(whiteSprite, world.transform);
        CreateGround("Ground", new Vector2(4f, -2.4f), new Vector2(34f, 1f), new Color(0.2f, 0.5f, 0.25f), whiteSprite, noFrictionMaterial, platforms.transform);
        CreatePrefabInstance(StepPrefabPath, "Step_01", new Vector2(5.2f, -0.8f), new Vector2(3f, 0.45f), new Color(0.32f, 0.62f, 0.32f), platforms.transform);
        CreatePrefabInstance(StepPrefabPath, "Step_02", new Vector2(9.2f, 0.5f), new Vector2(3.2f, 0.45f), new Color(0.32f, 0.62f, 0.32f), platforms.transform);
        CreatePrefabInstance(StepPrefabPath, "Step_03", new Vector2(14f, -0.2f), new Vector2(4f, 0.45f), new Color(0.32f, 0.62f, 0.32f), platforms.transform);
        CreatePrefabInstance(TallWallPrefabPath, "Tall Wall", new Vector2(17.4f, 0.7f), new Vector2(0.8f, 5.2f), new Color(0.42f, 0.45f, 0.48f), platforms.transform);
        CreatePrefabInstance(TrapPrefabPath, "Trap_01", new Vector2(-1.2f, -1.85f), new Vector2(0.9f, 0.55f), new Color(0.95f, 0.08f, 0.06f), traps.transform);
        CreatePrefabInstance(TrapPrefabPath, "Trap_02", new Vector2(7.2f, -2f), new Vector2(1.2f, 0.45f), new Color(0.95f, 0.08f, 0.06f), traps.transform);
        CreatePrefabInstance(TrapPrefabPath, "Trap_03", new Vector2(12.1f, -1.86f), new Vector2(1.4f, 0.55f), new Color(0.95f, 0.08f, 0.06f), traps.transform);

        GameObject player = CreatePlayer(whiteSprite, noFrictionMaterial, grappleRopeMaterial);
        GameObject cameraObject = CreateCamera(player.transform);

        CreateSun(whiteSprite, world.transform);
        CreateInstructions();

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log($"Demo scene created at {ScenePath}. Open it and press Play. Auto run is enabled; jump with Space.");

        Object.DestroyImmediate(spriteMaterial);
        _ = cameraObject;
    }

    private static Scene GetOrCreateDemoScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.path == ScenePath)
            {
                return loadedScene;
            }
        }

        if (File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
    }

    private static void ClearScene(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(rootObject);
        }

        CloseTemporaryDemoScenes(scene);
    }

    private static void CloseTemporaryDemoScenes(Scene sceneToKeep)
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene != sceneToKeep && loadedScene.name == "demo" && string.IsNullOrEmpty(loadedScene.path))
            {
                EditorSceneManager.CloseScene(loadedScene, true);
            }
        }
    }

    private static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets", "Demo");
        CreateFolderIfMissing("Assets", "Prefabs");
        CreateFolderIfMissing("Assets", "Scenes");
        CreateFolderIfMissing("Assets", "Scripts");
    }

    private static void EnsureTags()
    {
        AddTagIfMissing(GroundTag);
        AddTagIfMissing(StepTag);
        AddTagIfMissing(WallTag);
        AddTagIfMissing(TrapTag);
    }

    private static void AddTagIfMissing(string tag)
    {
        if (System.Array.IndexOf(InternalEditorUtility.tags, tag) < 0)
        {
            InternalEditorUtility.AddTag(tag);
        }
    }

    private static void CreateFolderIfMissing(string parent, string folder)
    {
        string path = $"{parent}/{folder}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static Sprite CreateSprite(string assetPath)
    {
        Object[] existingAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object asset in existingAssets)
        {
            if (asset is Sprite existingSprite)
            {
                return existingSprite;
            }
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "WhitePixelTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "WhitePixelSprite";

        AssetDatabase.CreateAsset(texture, assetPath);
        AssetDatabase.AddObjectToAsset(sprite, texture);
        AssetDatabase.SaveAssets();
        return sprite;
    }

    private static PhysicsMaterial2D CreateNoFrictionMaterial()
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionMaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("NoFriction2D");
            AssetDatabase.CreateAsset(material, NoFrictionMaterialPath);
        }

        material.friction = 0f;
        material.bounciness = 0f;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material CreateGrappleRopeMaterial()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrappleRopeTexturePath);
        if (texture == null)
        {
            texture = new Texture2D(16, 4, TextureFormat.RGBA32, false);
            texture.name = "GrappleRopeTexture";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;

            Color dark = new Color(0.08f, 0.08f, 0.08f, 1f);
            Color light = new Color(0.72f, 0.72f, 0.68f, 1f);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    bool stripe = ((x / 4) + y) % 2 == 0;
                    texture.SetPixel(x, y, stripe ? dark : light);
                }
            }

            texture.Apply();
            AssetDatabase.CreateAsset(texture, GrappleRopeTexturePath);
        }
        else
        {
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            EditorUtility.SetDirty(texture);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(GrappleRopeMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            material.name = "GrappleRopeMaterial";
            AssetDatabase.CreateAsset(material, GrappleRopeMaterialPath);
        }

        material.shader = Shader.Find("Sprites/Default");
        material.mainTexture = texture;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void EnsureDemoPrefabs(Sprite sprite, PhysicsMaterial2D material)
    {
        CreatePlatformPrefab(StepPrefabPath, "Step", StepTag, new Color(0.32f, 0.62f, 0.32f), sprite, material);
        CreatePlatformPrefab(TallWallPrefabPath, "TallWall", WallTag, new Color(0.42f, 0.45f, 0.48f), sprite, material);
        CreateTrapPrefab(sprite);
        AssetDatabase.SaveAssets();
    }

    private static void CreatePlatformPrefab(string prefabPath, string name, string tag, Color color, Sprite sprite, PhysicsMaterial2D material)
    {
        GameObject template = CreateSpriteObject(name, sprite, Vector2.zero, Vector2.one, color);
        template.layer = GroundLayer;
        template.tag = tag;
        template.GetComponent<SpriteRenderer>().sortingOrder = 0;

        BoxCollider2D collider = template.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.sharedMaterial = material;

        PrefabUtility.SaveAsPrefabAsset(template, prefabPath);
        Object.DestroyImmediate(template);
    }

    private static void CreateTrapPrefab(Sprite sprite)
    {
        GameObject template = CreateSpriteObject("Trap", sprite, Vector2.zero, Vector2.one, new Color(0.95f, 0.08f, 0.06f));
        template.tag = TrapTag;
        template.GetComponent<SpriteRenderer>().sortingOrder = 2;

        BoxCollider2D collider = template.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.isTrigger = true;
        template.AddComponent<TrapDeathZone2D>();

        PrefabUtility.SaveAsPrefabAsset(template, TrapPrefabPath);
        Object.DestroyImmediate(template);
    }

    private static void CreateBackground(Sprite sprite, Transform parent)
    {
        GameObject background = new GameObject("Scrolling Background");
        background.transform.SetParent(parent);

        CreateBackgroundLayer("Sky", sprite, background.transform, new Vector2(4f, 1.5f), new Vector2(34f, 12f), new Color(0.42f, 0.74f, 0.95f), -20);
        CreateBackgroundLayer("Far Hills", sprite, background.transform, new Vector2(4f, -1.6f), new Vector2(34f, 2.1f), new Color(0.35f, 0.58f, 0.45f), -12);
        CreateBackgroundLayer("Near Hills", sprite, background.transform, new Vector2(4f, -1.95f), new Vector2(34f, 1.4f), new Color(0.25f, 0.48f, 0.35f), -10);
    }

    private static void CreateBackgroundLayer(string name, Sprite sprite, Transform parent, Vector2 position, Vector2 tileScale, Color color, int sortingOrder)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent);
        layer.transform.position = new Vector3(position.x, position.y, 0f);

        GameObject tile = CreateSpriteObject($"{name} Tile", sprite, Vector2.zero, tileScale, color);
        tile.transform.SetParent(layer.transform, false);
        tile.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
    }

    private static GameObject CreateGround(string name, Vector2 position, Vector2 scale, Color color, Sprite sprite, PhysicsMaterial2D material, Transform parent)
    {
        GameObject platform = CreateSpriteObject(name, sprite, position, scale, color);
        platform.layer = GroundLayer;
        platform.tag = GroundTag;
        platform.transform.SetParent(parent);
        platform.GetComponent<SpriteRenderer>().sortingOrder = 0;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.sharedMaterial = material;
        return platform;
    }

    private static GameObject CreatePrefabInstance(string prefabPath, string name, Vector2 position, Vector2 scale, Color color, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            Debug.LogError($"Could not instantiate prefab at {prefabPath}.");
            return null;
        }

        instance.name = name;
        instance.transform.SetParent(parent);
        instance.transform.position = new Vector3(position.x, position.y, 0f);
        instance.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
        }

        return instance;
    }

    private static GameObject CreatePlayer(Sprite sprite, PhysicsMaterial2D material, Material grappleRopeMaterial)
    {
        GameObject player = new GameObject("Player");
        player.layer = PlayerLayer;
        player.transform.position = new Vector3(-7f, -1.28f, 0f);

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        Sprite previewSprite = LoadHeroKnightPreviewSprite(sprite);
        renderer.sprite = previewSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 10;

        player.transform.localScale = Vector3.one;

        Animator animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroKnightControllerPath);
        animator.applyRootMotion = false;

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        body.gravityScale = 3f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        LineRenderer grappleLine = player.AddComponent<LineRenderer>();
        grappleLine.positionCount = 2;
        grappleLine.enabled = false;
        grappleLine.useWorldSpace = true;
        grappleLine.startWidth = 0.06f;
        grappleLine.endWidth = 0.035f;
        grappleLine.numCapVertices = 2;
        grappleLine.alignment = LineAlignment.View;
        grappleLine.textureMode = LineTextureMode.Tile;
        grappleLine.sortingOrder = 20;
        grappleLine.material = grappleRopeMaterial;
        grappleLine.startColor = Color.white;
        grappleLine.endColor = Color.white;

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.offset = new Vector2(0f, 0.662f);
        collider.size = new Vector2(0.73f, 1.2f);
        collider.sharedMaterial = material;

        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("autoRunSpeed").floatValue = DefaultPlayerAutoRunSpeed;
        serializedController.FindProperty("jumpForce").floatValue = 14f;
        serializedController.FindProperty("wrapAtMapEdges").boolValue = true;
        serializedController.FindProperty("wrapLeftX").floatValue = DefaultWrapLeftX;
        serializedController.FindProperty("wrapRightX").floatValue = DefaultWrapRightX;
        serializedController.FindProperty("wallSlideFallSpeedMultiplier").floatValue = 0.35f;
        serializedController.FindProperty("grappleAimRadius").floatValue = 5f;
        serializedController.FindProperty("grappleAimMoveSpeedMultiplier").floatValue = 0.15f;
        serializedController.FindProperty("grappleAimSprite").objectReferenceValue = LoadHeroKnightAimSprite(previewSprite);
        serializedController.FindProperty("groundNormalThreshold").floatValue = 0.65f;
        serializedController.FindProperty("grapplePullSpeed").floatValue = 14f;
        serializedController.FindProperty("grappleStopDistance").floatValue = 0.65f;
        serializedController.FindProperty("grappleClimbAnimationDuration").floatValue = 0.45f;
        serializedController.FindProperty("groundMask").FindPropertyRelative("m_Bits").intValue = 1 << GroundLayer;
        serializedController.FindProperty("grappleMask").FindPropertyRelative("m_Bits").intValue = 1 << GroundLayer;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    private static Sprite LoadHeroKnightPreviewSprite(Sprite fallback)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(HeroKnightSpritePath);
        foreach (Object asset in assets)
        {
            if (asset is Sprite heroSprite)
            {
                return heroSprite;
            }
        }

        return fallback;
    }

    private static Sprite LoadHeroKnightAimSprite(Sprite fallback)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(HeroKnightSpritePath);
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long localId)
                && localId == HeroKnightHurtAimSpriteLocalId)
            {
                return sprite;
            }
        }

        return fallback;
    }

    private static GameObject CreateCamera(Transform player)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(-4.5f, 0.8f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4.2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.42f, 0.74f, 0.95f);

        AudioListener listener = cameraObject.AddComponent<AudioListener>();
        listener.enabled = true;

        CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
        follow.SetTarget(player);
        return cameraObject;
    }

    private static void CreateSun(Sprite sprite, Transform parent)
    {
        GameObject sun = CreateSpriteObject("Sun", sprite, new Vector2(-7f, 3.3f), new Vector2(1.1f, 1.1f), new Color(1f, 0.86f, 0.28f));
        sun.transform.SetParent(parent);
        sun.GetComponent<SpriteRenderer>().sortingOrder = -5;
    }

    private static void CreateInstructions()
    {
        GameObject instructions = new GameObject("Controls Note");
        instructions.transform.position = new Vector3(-6.8f, 2.35f, 0f);

        TextMesh text = instructions.AddComponent<TextMesh>();
        text.text = "Auto Run    Space: Jump    J: Attack    Hold Left Click: Aim Grapple    E: Death";
        text.fontSize = 42;
        text.characterSize = 0.08f;
        text.anchor = TextAnchor.MiddleLeft;
        text.color = new Color(0.08f, 0.14f, 0.18f);
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

}
