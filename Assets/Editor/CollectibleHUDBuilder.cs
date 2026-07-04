using Ciga.Demo;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.Editor
{
    /// <summary>
    /// Creates the CollectibleHUD prefab at Assets/Prefabs/CollectibleHUD.prefab.
    /// The HUD shows a collectible counter (icon + "X / Y" text) anchored to the
    /// top-right corner. Can be shown/hidden via UnityEvents or script calls.
    /// Run via Tools → Ciga → Create Collectible HUD Prefab.
    /// </summary>
    public static class CollectibleHUDBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/CollectibleHUD.prefab";
        private const string IconSpritePath = "Assets/Demo/CollectibleIcon.png";

        private static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.08f, 0.78f);
        private static readonly Color IconColor = new Color(1f, 0.88f, 0.25f, 1f);     // gold
        private static readonly Color TextColor = new Color(0.95f, 0.94f, 0.9f, 1f);   // off-white

        [MenuItem("Tools/Ciga/Create Collectible HUD Prefab", priority = 120)]
        public static void CreatePrefab()
        {
            EnsureFolders();
            EnsureIconSprite();

            // Clean up existing
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            // ---- Build the prefab hierarchy ----
            GameObject root = new GameObject("CollectibleHUD");

            // Canvas
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // Panel (top-right container)
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-24f, -20f);
            panelRect.sizeDelta = new Vector2(180f, 52f);

            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = PanelColor;

            // Make the background a rounded-ish look via a simple dark sprite
            // If no sprite is set, Unity's default white sprite gives a solid rect
            Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (knobSprite != null)
            {
                panelBg.sprite = knobSprite;
            }

            // Horizontal layout group for icon + text
            HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            layout.padding = new RectOffset(14, 16, 0, 0);

            // Icon
            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(panel.transform, false);

            RectTransform iconRect = icon.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(36f, 36f);

            Image iconImage = icon.AddComponent<Image>();
            Sprite collectibleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconSpritePath);
            iconImage.sprite = collectibleSprite != null ? collectibleSprite : knobSprite;
            iconImage.color = IconColor;
            iconImage.preserveAspect = true;

            // Counter text
            GameObject counter = new GameObject("CounterText");
            counter.transform.SetParent(panel.transform, false);

            RectTransform counterRect = counter.AddComponent<RectTransform>();
            counterRect.sizeDelta = new Vector2(100f, 36f);

            Text counterText = counter.AddComponent<Text>();
            counterText.text = "0 / 3";
            counterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            counterText.fontSize = 26;
            counterText.fontStyle = FontStyle.Bold;
            counterText.color = TextColor;
            counterText.alignment = TextAnchor.MiddleLeft;
            counterText.raycastTarget = false;

            // Add shadow for readability
            Shadow shadow = counter.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);

            // ---- The HUD script ----
            CollectibleHUD hud = root.AddComponent<CollectibleHUD>();
            SerializedObject serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("counterText").objectReferenceValue = counterText;
            serializedHud.FindProperty("iconImage").objectReferenceValue = iconImage;
            serializedHud.FindProperty("panelRoot").objectReferenceValue = panel;
            serializedHud.FindProperty("counterFormat").stringValue = "{0} / {1}";
            serializedHud.FindProperty("hideWhenZero").boolValue = false;
            serializedHud.FindProperty("animateOnCollect").boolValue = true;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CollectibleHUDBuilder] Prefab created at {PrefabPath}. " +
                      "Drop it into any scene — it auto-connects to CollectionManager2D.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Demo"))
            {
                AssetDatabase.CreateFolder("Assets", "Demo");
            }
        }

        private static void EnsureIconSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(IconSpritePath) != null)
            {
                return;
            }

            // Generate a simple gold circle icon
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color gold = new Color(1f, 0.88f, 0.25f, 1f);
            Color goldEdge = new Color(0.85f, 0.65f, 0.1f, 1f);
            float radius = size / 2f - 1f;
            float cx = size / 2f, cy = size / 2f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius - 2f)
                    {
                        // Inner gold fill
                        pixels[y * size + x] = gold;
                    }
                    else if (dist <= radius)
                    {
                        // Edge ring
                        float t = (dist - (radius - 2f)) / 2f;
                        pixels[y * size + x] = Color.Lerp(gold, goldEdge, t);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            System.IO.File.WriteAllBytes(IconSpritePath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(IconSpritePath);

            // Set texture import settings
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(IconSpritePath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64f;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            Object.DestroyImmediate(tex);
        }
    }
}
