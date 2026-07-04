using UnityEditor;
using UnityEngine;

namespace Ciga.Editor
{
    /// <summary>
    /// Fixes import settings for illustration PNGs so they display crisp and clear.
    /// Run via Tools → Fix Illustration Imports.
    /// </summary>
    public static class FixIllustrationImport
    {
        [MenuItem("Tools/Fix Illustration Imports", priority = 121)]
        public static void Fix()
        {
            FixFile("Assets/插画4.png");
            Debug.Log("[FixIllustration] Done.");
        }

        private static void FixFile(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[FixIllustration] Not found: {path}");
                return;
            }

            bool changed = false;

            // Keep as Sprite so SpriteRenderer can display it
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            // No size limit
            if (importer.maxTextureSize != 8192)
            {
                importer.maxTextureSize = 8192;
                changed = true;
            }

            // Smooth filtering
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            // High quality — no crunched compression artifacts
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            if (changed || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.SaveAndReimport();
                Debug.Log($"[FixIllustration] Fixed {path} → Default, 8192, Bilinear, Uncompressed");
            }
            else
            {
                Debug.Log($"[FixIllustration] {path} already optimal.");
            }
        }
    }
}
