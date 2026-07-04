using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Ciga.Demo
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class OutlineHighlight2D : MonoBehaviour
    {
        [Tooltip("Color used by the generated outline sprites.")]
        [SerializeField] private Color outlineColor = new Color(1f, 0.9f, 0.2f, 1f);

        [Tooltip("World-space offset used to create the outline thickness.")]
        [SerializeField, Min(0.001f)] private float outlineThickness = 0.08f;

        [Tooltip("Show the outline automatically when this object is enabled.")]
        [SerializeField] private bool highlightedOnStart;

        private readonly List<GameObject> outlineObjects = new List<GameObject>();
        private bool isHighlighted;
#if UNITY_EDITOR
        private bool editorRefreshQueued;
#endif

        public void SetStyle(Color color, float thickness)
        {
            outlineColor = color;
            outlineThickness = Mathf.Max(0.001f, thickness);

            if (isHighlighted)
            {
                Hide();
                Show();
            }
        }

        public void SetHighlightedOnStart(bool value)
        {
            highlightedOnStart = value;
        }

        public void Show()
        {
            if (isHighlighted && HasGeneratedOutlines())
            {
                return;
            }

            RemoveGeneratedOutlines();

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || IsGeneratedOutline(renderer.gameObject))
                {
                    continue;
                }

                CreateOutlineCopies(renderer);
            }

            isHighlighted = outlineObjects.Count > 0;
        }

        public void Hide()
        {
            RemoveGeneratedOutlines();
            outlineObjects.Clear();
            isHighlighted = false;
        }

        private void Start()
        {
            if (highlightedOnStart)
            {
                Show();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && highlightedOnStart)
            {
                Show();
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnValidate()
        {
            outlineThickness = Mathf.Max(0.001f, outlineThickness);

#if UNITY_EDITOR
            QueueEditorRefresh();
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorRefresh()
        {
            if (Application.isPlaying || editorRefreshQueued)
            {
                return;
            }

            editorRefreshQueued = true;
            EditorApplication.delayCall += RefreshInEditor;
        }

        private void RefreshInEditor()
        {
            editorRefreshQueued = false;
            if (this == null)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                Hide();
                return;
            }

            if (highlightedOnStart || HasGeneratedOutlines())
            {
                Show();
            }
        }
#endif

        private void RemoveGeneratedOutlines()
        {
            for (int i = outlineObjects.Count - 1; i >= 0; i--)
            {
                DestroyObject(outlineObjects[i]);
            }

            outlineObjects.Clear();

            List<GameObject> generatedChildren = new List<GameObject>();
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null && IsGeneratedOutline(renderer.gameObject))
                {
                    generatedChildren.Add(renderer.gameObject);
                }
            }

            foreach (GameObject child in generatedChildren)
            {
                DestroyObject(child);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private bool HasGeneratedOutlines()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null && IsGeneratedOutline(renderer.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private void CreateOutlineCopies(SpriteRenderer source)
        {
            Vector3[] offsets =
            {
                new Vector3(outlineThickness, 0f, 0f),
                new Vector3(-outlineThickness, 0f, 0f),
                new Vector3(0f, outlineThickness, 0f),
                new Vector3(0f, -outlineThickness, 0f),
                new Vector3(outlineThickness, outlineThickness, 0f),
                new Vector3(outlineThickness, -outlineThickness, 0f),
                new Vector3(-outlineThickness, outlineThickness, 0f),
                new Vector3(-outlineThickness, -outlineThickness, 0f),
            };

            foreach (Vector3 offset in offsets)
            {
                GameObject outline = new GameObject($"{source.name} Outline");
                outline.transform.SetParent(source.transform, false);
                outline.transform.localPosition = offset;
                outline.transform.localRotation = Quaternion.identity;
                outline.transform.localScale = Vector3.one;

                SpriteRenderer copy = outline.AddComponent<SpriteRenderer>();
                copy.sprite = source.sprite;
                copy.drawMode = source.drawMode;
                copy.size = source.size;
                copy.tileMode = source.tileMode;
                copy.maskInteraction = source.maskInteraction;
                copy.flipX = source.flipX;
                copy.flipY = source.flipY;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder - 1;
                copy.sharedMaterial = CreateSilhouetteMaterial(source);
                copy.color = Color.white;

                outlineObjects.Add(outline);
            }
        }

        private Material CreateSilhouetteMaterial(SpriteRenderer source)
        {
            Shader silhouetteShader = Resources.Load<Shader>("Shaders/SpriteSolidSilhouette");
            if (silhouetteShader == null)
            {
                silhouetteShader = Shader.Find("Ciga/SpriteSolidSilhouette");
            }

            if (silhouetteShader == null)
            {
                Debug.LogWarning("Ciga/SpriteSolidSilhouette shader was not found. Falling back to tinted sprite outline.", this);
                Material fallback = source.sharedMaterial != null
                    ? new Material(source.sharedMaterial)
                    : new Material(Shader.Find("Sprites/Default"));
                fallback.color = outlineColor;
                return fallback;
            }

            Material material = new Material(silhouetteShader);
            material.name = $"{source.name} Solid Outline";
            material.SetColor("_Color", outlineColor);
            return material;
        }

        private static bool IsGeneratedOutline(GameObject candidate)
        {
            return candidate.name.EndsWith(" Outline");
        }
    }
}
