using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Ciga.Demo
{
    /// <summary>
    /// Simple main menu — cover image background + Start / Quit buttons.
    /// Put on a GameObject in an empty scene. Creates its own Canvas UI.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [Header("Cover Image (drag 封面.jpg here)")]
        [SerializeField] private Sprite coverSprite;

        [Header("Scene to load on Start")]
        [SerializeField] private string gameScene = "demo";

        private void Start()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            // EventSystem — required for button clicks in builds
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            // Canvas
            var cGo = new GameObject("MenuCanvas");
            cGo.transform.SetParent(transform);
            var canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var cs = cGo.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cGo.AddComponent<GraphicRaycaster>();

            // Background image
            var bgGo = new GameObject("CoverBG");
            bgGo.transform.SetParent(cGo.transform, false);
            var bg = bgGo.AddComponent<Image>();
            if (coverSprite != null) bg.sprite = coverSprite;
            bg.color = Color.white;
            var bgr = bg.rectTransform;
            bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
            bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;

            // Button panel (bottom-center)
            var btnPanel = new GameObject("ButtonPanel");
            btnPanel.transform.SetParent(cGo.transform, false);
            var bpRect = btnPanel.AddComponent<RectTransform>();
            bpRect.anchorMin = new Vector2(0.3f, 0.1f);
            bpRect.anchorMax = new Vector2(0.7f, 0.4f);
            bpRect.offsetMin = Vector2.zero; bpRect.offsetMax = Vector2.zero;
            var vlg = btnPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;

            // Start button
            MakeButton(btnPanel.transform, "开始游戏", () =>
            {
                SceneManager.LoadScene(gameScene);
            });

            // Quit button
            MakeButton(btnPanel.transform, "退出游戏", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.08f, 0.15f, 0.85f);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            // Hover color
            var colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.12f, 0.2f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.25f, 0.4f, 0.9f);
            colors.pressedColor = new Color(0.08f, 0.06f, 0.1f, 0.9f);
            btn.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 70);

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var txt = labelGo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 32;
            txt.color = new Color(0.9f, 0.85f, 0.7f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;
            var tr = txt.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        }
    }
}
