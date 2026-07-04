using Ciga.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utilities for quickly adding void zones and black camera background
/// to existing scenes without rebuilding them from scratch.
/// </summary>
public static class SceneSetupUtilities
{
    private const string VoidContainerName = "Void Zones";

    [MenuItem("Tools/Ciga/Add Void Zones to Scene")]
    public static void AddVoidZonesToScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("No active scene found. Open a scene first.");
            return;
        }

        // Remove existing void zones if any
        RemoveVoidZones(scene);

        // Find or create a parent container
        GameObject world = GameObject.Find("World");
        Transform parent = world != null ? world.transform : null;

        if (parent == null)
        {
            GameObject container = new GameObject("World");
            parent = container.transform;
        }

        // Create void zone container
        GameObject voidContainer = new GameObject(VoidContainerName);
        voidContainer.transform.SetParent(parent);

        // Bottom void zone — catches player falling off the map
        CreateVoidZone("Void Bottom", new Vector2(4f, -7f), new Vector2(50f, 3f), voidContainer.transform);

        // Left void zone — catches player falling behind the scrolling view
        CreateVoidZone("Void Left", new Vector2(-14f, 0f), new Vector2(3f, 30f), voidContainer.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Void zones added to scene. Player will be teleported back to last safe ground position when falling into the void.");
    }

    [MenuItem("Tools/Ciga/Set Camera Background to Black")]
    public static void SetCameraBackgroundToBlack()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            // Try to find by tag
            GameObject cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObj != null)
            {
                mainCamera = cameraObj.GetComponent<Camera>();
            }
        }

        if (mainCamera == null)
        {
            Debug.LogError("No camera found in scene. Make sure there is a camera tagged 'MainCamera'.");
            return;
        }

        Undo.RecordObject(mainCamera, "Set Camera Background");
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.black;
        EditorUtility.SetDirty(mainCamera);

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Camera background set to black. Areas without images/sprites will now appear black.");
    }

    [MenuItem("Tools/Ciga/Full Void Setup (Background + Zones)")]
    public static void FullVoidSetup()
    {
        SetCameraBackgroundToBlack();
        AddVoidZonesToScene();
        Debug.Log("Full void setup complete: black background + void teleport zones.");
    }

    private static void RemoveVoidZones(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Transform existingVoid = rootObject.transform.Find(VoidContainerName);
            if (existingVoid != null)
            {
                Object.DestroyImmediate(existingVoid.gameObject);
            }

            // Also check under "World" container
            if (rootObject.name == "World")
            {
                Transform worldVoid = rootObject.transform.Find(VoidContainerName);
                if (worldVoid != null)
                {
                    Object.DestroyImmediate(worldVoid.gameObject);
                }
            }
        }
    }

    private static void CreateVoidZone(string name, Vector2 position, Vector2 size, Transform parent)
    {
        GameObject voidZone = new GameObject(name);
        voidZone.transform.SetParent(parent);
        voidZone.transform.position = new Vector3(position.x, position.y, 0f);

        BoxCollider2D collider = voidZone.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = size;

        voidZone.AddComponent<VoidZone2D>();
    }
}
