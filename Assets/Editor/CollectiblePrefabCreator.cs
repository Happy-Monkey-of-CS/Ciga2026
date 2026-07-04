using Ciga.Demo;
using UnityEditor;
using UnityEngine;

namespace Ciga.Editor
{
    /// <summary>
    /// Creates a Collectible prefab at Assets/Prefabs/Collectible.prefab
    /// with a default sprite, trigger collider, and Collectible2D component.
    /// Run via Tools → Create Collectible Prefab.
    /// </summary>
    public static class CollectiblePrefabCreator
    {
        private const string PrefabPath = "Assets/Prefabs/Collectible.prefab";

        [MenuItem("Tools/Create Collectible Prefab", priority = 111)]
        public static void Create()
        {
            // Ensure directory
            if (!System.IO.Directory.Exists("Assets/Prefabs"))
                System.IO.Directory.CreateDirectory("Assets/Prefabs");

            // Delete existing if present
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            // Build the GameObject
            GameObject go = new GameObject("Collectible");

            // SpriteRenderer with a default white circle sprite
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (sr.sprite == null)
            {
                // Fallback: create a small placeholder texture
                sr.sprite = CreatePlaceholderSprite();
            }
            sr.color = new Color(1f, 0.85f, 0.2f, 1f); // gold

            // Circle trigger
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.35f;

            // The collectible component
            go.AddComponent<Collectible2D>();

            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CollectiblePrefabCreator] Prefab created at {PrefabPath}");
        }

        private static Sprite CreatePlaceholderSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color gold = new Color(1f, 0.85f, 0.2f, 1f);
            Color[] pixels = new Color[size * size];
            float radius = size / 2f;
            float cx = radius, cy = radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    pixels[y * size + x] = (dx * dx + dy * dy) <= (radius * radius)
                        ? gold
                        : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            string path = "Assets/Demo/CollectibleIcon.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            Object.DestroyImmediate(tex);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
