using System.Collections.Generic;
using System.IO;
using Ciga.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HighlightTestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/gaoguang.unity";
    private static readonly Color PreviewHighlightColor = new Color(1f, 0.9f, 0.2f, 1f);

    private static readonly string[] IncludedFolders =
    {
        "Assets/Prefabs/Steps",
        "Assets/Prefabs/TallWalls",
        "Assets/Prefabs/Traps",
        "Assets/Prefabs/Doors",
    };

    private static readonly string[] IncludedSinglePrefabs =
    {
        "Assets/Prefabs/Enemy.prefab",
    };

    [MenuItem("Tools/Ciga/Build Gaoguang Scene")]
    public static void BuildGaoguangScene()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = new GameObject("Highlight Test Prefabs");
        GameObject labels = new GameObject("Labels");
        labels.transform.SetParent(root.transform);

        List<string> prefabPaths = CollectPrefabPaths();
        prefabPaths.Sort(ComparePrefabPaths);

        float startX = -12f;
        float startY = 7f;
        float columnSpacing = 8f;
        float rowSpacing = 5.5f;
        int columns = 4;

        for (int i = 0; i < prefabPaths.Count; i++)
        {
            string path = prefabPaths[i];
            int row = i / columns;
            int column = i % columns;
            Vector3 position = new Vector3(startX + column * columnSpacing, startY - row * rowSpacing, 0f);

            GameObject instance = InstantiatePrefab(path, position, root.transform);
            if (instance == null)
            {
                continue;
            }

            ApplyCurrentGameplayHighlightPreview(instance);
            CreateLabel(Path.GetFileNameWithoutExtension(path), position + Vector3.down * 2.2f, labels.transform);
        }

        CreateReferenceGrid(root.transform);
        CreateCamera();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = root;
        Debug.Log($"Gaoguang highlight test scene created at {ScenePath}. Prefab count: {prefabPaths.Count}");
    }

    private static List<string> CollectPrefabPaths()
    {
        List<string> paths = new List<string>();

        foreach (string folder in IncludedFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path))
                {
                    paths.Add(path);
                }
            }
        }

        foreach (string path in IncludedSinglePrefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static int ComparePrefabPaths(string left, string right)
    {
        int categoryCompare = GetCategoryOrder(left).CompareTo(GetCategoryOrder(right));
        if (categoryCompare != 0)
        {
            return categoryCompare;
        }

        return EditorUtility.NaturalCompare(left, right);
    }

    private static int GetCategoryOrder(string path)
    {
        if (path.Contains("/Steps/")) return 0;
        if (path.Contains("/TallWalls/")) return 1;
        if (path.Contains("/Traps/")) return 2;
        if (path.Contains("/Doors/")) return 3;
        if (path.EndsWith("/Enemy.prefab")) return 4;
        return 99;
    }

    private static GameObject InstantiatePrefab(string prefabPath, Vector3 position, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = Path.GetFileNameWithoutExtension(prefabPath);
        instance.transform.SetParent(parent);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;
        return instance;
    }

    private static void ApplyCurrentGameplayHighlightPreview(GameObject instance)
    {
        OutlineHighlight2D outline = instance.GetComponent<OutlineHighlight2D>();
        if (outline == null)
        {
            outline = instance.AddComponent<OutlineHighlight2D>();
        }

        outline.SetStyle(PreviewHighlightColor, 0.12f);
        outline.SetHighlightedOnStart(true);
        outline.Show();
        EditorUtility.SetDirty(outline);
    }

    private static void CreateLabel(string text, Vector3 position, Transform parent)
    {
        GameObject label = new GameObject($"{text} Label");
        label.transform.SetParent(parent);
        label.transform.position = position;

        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = 0.18f;
        mesh.fontSize = 42;
        mesh.color = new Color(0.08f, 0.1f, 0.12f, 1f);
    }

    private static void CreateReferenceGrid(Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(floor.GetComponent<BoxCollider>());
        floor.name = "Reference Floor";
        floor.transform.SetParent(parent);
        floor.transform.position = new Vector3(0f, -8f, 1f);
        floor.transform.localScale = new Vector3(24f, 0.08f, 1f);

        Renderer renderer = floor.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial("Gaoguang Reference Floor", new Color(0.7f, 0.74f, 0.78f, 1f));
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 11f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.82f, 0.88f, 0.94f, 1f);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, -3f, -10f);
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.name = name;
        material.color = color;
        return material;
    }
}
