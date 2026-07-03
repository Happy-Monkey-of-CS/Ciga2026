using Ciga.Demo;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RuntimeAnimatorController = UnityEngine.RuntimeAnimatorController;

public static class DemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/demo.unity";
    private const string NoFrictionMaterialPath = "Assets/Demo/NoFriction2D.physicsMaterial2D";
    private const string HeroKnightSpritePath = "Assets/Hero Knight - Pixel Art/Sprites/HeroKnight.png";
    private const string HeroKnightControllerPath = "Assets/Hero Knight - Pixel Art/Animations/HeroKnight_AnimController.controller";
    private const int GroundLayer = 0;
    private const int PlayerLayer = 2;
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

        CreateBackground(whiteSprite, world.transform);
        CreatePlatform("Ground", new Vector2(0f, -2.4f), new Vector2(20f, 1f), new Color(0.2f, 0.5f, 0.25f), whiteSprite, noFrictionMaterial, platforms.transform);
        CreatePlatform("Step_01", new Vector2(5.2f, -0.8f), new Vector2(3f, 0.45f), new Color(0.32f, 0.62f, 0.32f), whiteSprite, noFrictionMaterial, platforms.transform);
        CreatePlatform("Step_02", new Vector2(9.2f, 0.5f), new Vector2(3.2f, 0.45f), new Color(0.32f, 0.62f, 0.32f), whiteSprite, noFrictionMaterial, platforms.transform);
        CreatePlatform("Step_03", new Vector2(14f, -0.2f), new Vector2(4f, 0.45f), new Color(0.32f, 0.62f, 0.32f), whiteSprite, noFrictionMaterial, platforms.transform);
        CreateTrap("Trap_01", new Vector2(-1.2f, -1.85f), new Vector2(0.9f, 0.55f), whiteSprite, traps.transform);
        CreateTrap("Trap_02", new Vector2(7.2f, -2f), new Vector2(1.2f, 0.45f), whiteSprite, traps.transform);
        CreateTrap("Trap_03", new Vector2(12.1f, -1.86f), new Vector2(1.4f, 0.55f), whiteSprite, traps.transform);

        GameObject player = CreatePlayer(whiteSprite, noFrictionMaterial);
        GameObject cameraObject = CreateCamera(player.transform);

        CreateSun(whiteSprite, world.transform);
        CreateInstructions();

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log($"Demo scene created at {ScenePath}. Open it and press Play. Move with A/D or Left/Right, jump with Space.");

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
        CreateFolderIfMissing("Assets", "Scenes");
        CreateFolderIfMissing("Assets", "Scripts");
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

    private static void CreateBackground(Sprite sprite, Transform parent)
    {
        GameObject background = new GameObject("Scrolling Background");
        background.transform.SetParent(parent);

        CreateBackgroundLayer("Sky", sprite, background.transform, new Vector2(0f, 1.5f), new Vector2(24f, 12f), new Color(0.42f, 0.74f, 0.95f), -20, 0.35f);
        CreateBackgroundLayer("Far Hills", sprite, background.transform, new Vector2(0f, -1.6f), new Vector2(24f, 2.1f), new Color(0.35f, 0.58f, 0.45f), -12, 0.8f);
        CreateBackgroundLayer("Near Hills", sprite, background.transform, new Vector2(0f, -1.95f), new Vector2(24f, 1.4f), new Color(0.25f, 0.48f, 0.35f), -10, 1.3f);
    }

    private static void CreateBackgroundLayer(string name, Sprite sprite, Transform parent, Vector2 position, Vector2 tileScale, Color color, int sortingOrder, float scrollSpeed)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent);
        layer.transform.position = new Vector3(position.x, position.y, 0f);

        for (int i = 0; i < 3; i++)
        {
            GameObject tile = CreateSpriteObject($"{name} Tile {i + 1}", sprite, Vector2.zero, tileScale, color);
            tile.transform.SetParent(layer.transform, false);
            tile.transform.localPosition = new Vector3((i - 1) * tileScale.x, 0f, 0f);
            tile.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
        }

        LoopingBackground2D looper = layer.AddComponent<LoopingBackground2D>();
        SerializedObject serializedLooper = new SerializedObject(looper);
        serializedLooper.FindProperty("scrollSpeed").floatValue = scrollSpeed;
        serializedLooper.FindProperty("tileWidth").floatValue = tileScale.x;
        serializedLooper.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreatePlatform(string name, Vector2 position, Vector2 scale, Color color, Sprite sprite, PhysicsMaterial2D material, Transform parent)
    {
        GameObject platform = CreateSpriteObject(name, sprite, position, scale, color);
        platform.layer = GroundLayer;
        platform.transform.SetParent(parent);
        platform.GetComponent<SpriteRenderer>().sortingOrder = 0;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.sharedMaterial = material;
        return platform;
    }

    private static GameObject CreateTrap(string name, Vector2 position, Vector2 scale, Sprite sprite, Transform parent)
    {
        GameObject trap = CreateSpriteObject(name, sprite, position, scale, new Color(0.95f, 0.08f, 0.06f));
        trap.transform.SetParent(parent);
        trap.GetComponent<SpriteRenderer>().sortingOrder = 2;

        BoxCollider2D collider = trap.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.isTrigger = true;

        trap.AddComponent<TrapDeathZone2D>();
        return trap;
    }

    private static GameObject CreatePlayer(Sprite sprite, PhysicsMaterial2D material)
    {
        GameObject player = new GameObject("Player");
        player.layer = PlayerLayer;
        player.transform.position = new Vector3(-7f, -1.28f, 0f);

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadHeroKnightPreviewSprite(sprite);
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

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.offset = new Vector2(0f, 0.662f);
        collider.size = new Vector2(0.73f, 1.2f);
        collider.sharedMaterial = material;

        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("moveSpeed").floatValue = 7f;
        serializedController.FindProperty("jumpForce").floatValue = 14f;
        serializedController.FindProperty("groundNormalThreshold").floatValue = 0.65f;
        serializedController.FindProperty("groundMask").FindPropertyRelative("m_Bits").intValue = 1 << GroundLayer;
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
        text.text = "A/D or Left/Right: Move    Space: Jump    Left Click: Attack    E: Death";
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
