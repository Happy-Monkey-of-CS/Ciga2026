using Ciga.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TeleportExperimentSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/chuansong.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string DemoGroundTag = "DemoGround";

    [MenuItem("Tools/Ciga/Build Chuansong Scene")]
    public static void BuildChuansongScene()
    {
        EnsureTag(DemoGroundTag);

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject world = new GameObject("World");
        GameObject platforms = new GameObject("Platforms");
        platforms.transform.SetParent(world.transform);
        GameObject teleporters = new GameObject("Teleporters");
        teleporters.transform.SetParent(world.transform);

        GameObject player = CreatePlayer();
        CreateGround("Ground", new Vector2(0f, -2f), new Vector2(22f, 1f), new Color(0.28f, 0.34f, 0.42f), platforms.transform);
        CreateGround("Upper Landing", new Vector2(5.5f, 1f), new Vector2(5f, 0.5f), new Color(0.32f, 0.48f, 0.38f), platforms.transform);

        Transform destination = CreateDestinationMarker(new Vector2(5.5f, 1.75f), teleporters.transform);
        CreateTeleportZone("Teleport Square", new Vector2(-1.2f, -1.18f), new Vector2(1.25f, 0.65f), destination, teleporters.transform);

        CreateCamera(player.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
        Debug.Log($"Chuansong scene created at {ScenePath}.");
    }

    private static GameObject CreatePlayer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject player;
        if (prefab != null)
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Player";
        }
        else
        {
            player = new GameObject("Player");
            player.AddComponent<SpriteRenderer>();
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<BoxCollider2D>();
            player.AddComponent<PlayerController2D>();
        }

        player.tag = "Player";
        player.layer = 0;
        player.transform.position = new Vector3(-7f, -0.8f, 0f);

        PlayerController2D controller = player.GetComponent<PlayerController2D>();
        if (controller != null)
        {
            SerializedObject serialized = new SerializedObject(controller);
            Set(serialized, "wrapAtMapEdges", false);
            Set(serialized, "groundMask", new LayerMask { value = 1 });
            Set(serialized, "grappleMask", new LayerMask { value = 1 });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        return player;
    }

    private static GameObject CreateGround(string name, Vector2 position, Vector2 scale, Color color, Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(ground.GetComponent<BoxCollider>());
        ground.name = name;
        ground.tag = DemoGroundTag;
        ground.layer = 0;
        ground.transform.SetParent(parent);
        ground.transform.position = new Vector3(position.x, position.y, 0f);
        ground.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        Renderer renderer = ground.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateColorMaterial($"{name} Material", color);

        BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        return ground;
    }

    private static Transform CreateDestinationMarker(Vector2 position, Transform parent)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(marker.GetComponent<SphereCollider>());
        marker.name = "Teleport Destination";
        marker.transform.SetParent(parent);
        marker.transform.position = new Vector3(position.x, position.y, 0f);
        marker.transform.localScale = Vector3.one * 0.35f;

        Renderer renderer = marker.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateColorMaterial("Teleport Destination Material", new Color(0.2f, 0.95f, 1f, 1f));
        return marker.transform;
    }

    private static GameObject CreateTeleportZone(string name, Vector2 position, Vector2 scale, Transform destination, Transform parent)
    {
        GameObject zone = new GameObject(name);
        zone.tag = DemoGroundTag;
        zone.layer = 0;
        zone.transform.SetParent(parent);
        zone.transform.position = new Vector3(position.x, position.y, 0f);
        zone.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        MeshFilter meshFilter = zone.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshRenderer renderer = zone.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateColorMaterial("Teleport Square Material", new Color(0.1f, 0.8f, 1f, 0.75f));

        BoxCollider2D collider = zone.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        TeleportZone2D teleport = zone.AddComponent<TeleportZone2D>();
        SerializedObject serialized = new SerializedObject(teleport);
        Set(serialized, "targetDestination", destination);
        Set(serialized, "worldOffset", Vector2.zero);
        Set(serialized, "requiredTag", "");
        Set(serialized, "onlyPlayer", true);
        Set(serialized, "preserveVelocity", true);
        Set(serialized, "cooldown", 0.3f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return zone;
    }

    private static void CreateCamera(Transform player)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.47f, 0.72f, 0.95f);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0.5f, -10f);

        CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
        follow.SetTarget(player);
    }

    private static Material CreateColorMaterial(string name, Color color)
    {
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.name = name;
        material.color = color;
        return material;
    }

    private static void Set(SerializedObject serialized, string propertyName, object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        switch (value)
        {
            case bool boolValue:
                property.boolValue = boolValue;
                break;
            case float floatValue:
                property.floatValue = floatValue;
                break;
            case string stringValue:
                property.stringValue = stringValue;
                break;
            case Vector2 vector2Value:
                property.vector2Value = vector2Value;
                break;
            case LayerMask layerMask:
                property.intValue = layerMask.value;
                break;
            case Object objectValue:
                property.objectReferenceValue = objectValue;
                break;
        }
    }

    private static void EnsureTag(string tag)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag)
            {
                return;
            }
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }
}
