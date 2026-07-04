using UnityEngine;

namespace Ciga.Demo
{
    /// <summary>
    /// Ensures the main camera renders black in areas without any sprites/background tiles.
    /// Attach to any GameObject in the scene (or the camera itself).
    /// </summary>
    public sealed class CameraBackgroundSetter2D : MonoBehaviour
    {
        [Tooltip("The background color for areas without images. Default is black.")]
        [SerializeField] private Color voidColor = Color.black;

        private void Start()
        {
            ApplyBackgroundColor();
        }

        private void OnEnable()
        {
            ApplyBackgroundColor();
        }

        private void ApplyBackgroundColor()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[CameraBackgroundSetter2D] No main camera found.", this);
                return;
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = voidColor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Apply immediately in editor for preview
            if (Application.isPlaying)
            {
                ApplyBackgroundColor();
            }
        }
#endif
    }
}
