using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Ciga.Demo;

namespace Ciga.Editor
{
    /// <summary>
    /// One-click tool that auto-assigns audio clips from Assets/Audio/ to
    /// PlayerController2D, Enemy2D, and TrapDeathZone2D components on the
    /// scene instance and prefabs.
    /// </summary>
    public static class AudioAutoSetup
    {
        [MenuItem("Tools/Setup All Audio", priority = 100)]
        public static void SetupAll()
        {
            EnsureAudioManager();
            SetupPlayerAudio();
            SetupEnemyAudio();
            SetupTrapAudio();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AudioAutoSetup] All audio clips assigned.");
        }

        private static void EnsureAudioManager()
        {
            AudioManager2D existing = Object.FindFirstObjectByType<AudioManager2D>();
            if (existing != null)
            {
                Debug.Log("[AudioAutoSetup] AudioManager already exists in scene.");
                return;
            }

            GameObject go = new GameObject("AudioManager");
            go.AddComponent<AudioManager2D>();
            Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");
            Debug.Log("[AudioAutoSetup] Created AudioManager GameObject in scene.");
        }

        // ---- scene / prefab helpers ---------------------------------------------------

        private static T FindSceneComponent<T>() where T : Component
        {
            T component = Object.FindFirstObjectByType<T>();
            if (component == null)
            {
                Debug.LogWarning($"[AudioAutoSetup] No {typeof(T).Name} found in scene.");
            }
            return component;
        }

        private static T FindPrefabComponent<T>(string prefabName) where T : Component
        {
            string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                T component = prefab.GetComponent<T>();
                if (component != null) return component;
            }

            Debug.LogWarning($"[AudioAutoSetup] No prefab with {typeof(T).Name} found for '{prefabName}'.");
            return null;
        }

        // ---- audio clip loader -------------------------------------------------------

        private static AudioClip LoadClip(string fileName)
        {
            string[] guids = AssetDatabase.FindAssets($"{fileName} t:AudioClip");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Assets/Audio"))
                {
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }
            Debug.LogWarning($"[AudioAutoSetup] Clip not found: {fileName}");
            return null;
        }

        // ---- player ------------------------------------------------------------------

        private static readonly Dictionary<string, string> PlayerFieldMap = new Dictionary<string, string>
        {
            { "Player_Jump",            "jumpClip" },
            { "Player_Land",            "landClip" },
            { "Player_Attack",          "attackClip" },
            { "Player_AttackHit",       "attackHitClip" },
            { "Player_Death",           "deathClip" },
            { "Player_Footsteps",       "footstepsLoopClip" },
            { "Player_WallSlide",       "wallSlideLoopClip" },
            { "Player_WallJump",        "wallJumpClip" },
            { "Player_GrappleAimStart", "grappleAimStartClip" },
            { "Player_GrappleFire",     "grappleFireClip" },
            { "Player_GrappleConnect",  "grappleConnectClip" },
            { "Player_GrapplePullSelf", "grapplePullSelfLoopClip" },
            { "Player_GrapplePullObject","grapplePullObjectLoopClip" },
            { "Player_GrappleLand",     "grappleLandClip" },
            { "Player_GrappleClimb",    "grappleClimbClip" },
            { "Player_StrikeAimStart",  "strikeAimStartClip" },
            { "Player_StrikeFire",      "strikeFireClip" },
            { "Player_StrikeObject",    "strikeObjectLoopClip" },
            { "Player_StrikeImpact",    "strikeImpactClip" },
        };

        public static void SetupPlayerAudio()
        {
            PlayerController2D player = FindSceneComponent<PlayerController2D>();
            if (player == null) return;

            SerializedObject so = new SerializedObject(player);
            int assigned = 0;

            foreach (KeyValuePair<string, string> kvp in PlayerFieldMap)
            {
                AudioClip clip = LoadClip(kvp.Key);
                if (clip == null) continue;

                SerializedProperty prop = so.FindProperty(kvp.Value);
                if (prop != null)
                {
                    prop.objectReferenceValue = clip;
                    assigned++;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(player);
            Debug.Log($"[AudioAutoSetup] Player: {assigned}/{PlayerFieldMap.Count} clips assigned.");
        }

        // ---- enemy -------------------------------------------------------------------

        public static void SetupEnemyAudio()
        {
            Enemy2D enemy = FindPrefabComponent<Enemy2D>("Enemy");
            if (enemy == null)
            {
                Debug.LogWarning("[AudioAutoSetup] Enemy prefab not found. Looking for scene instance...");
                enemy = FindSceneComponent<Enemy2D>();
            }

            if (enemy == null) return;

            SerializedObject so = new SerializedObject(enemy);
            int assigned = 0;

            AudioClip defeatClip = LoadClip("Enemy_Defeat");
            if (defeatClip != null) { so.FindProperty("defeatClip").objectReferenceValue = defeatClip; assigned++; }
            AudioClip fallClip = LoadClip("Enemy_Fall");
            if (fallClip != null) { so.FindProperty("fallClip").objectReferenceValue = fallClip; assigned++; }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemy);
            Debug.Log($"[AudioAutoSetup] Enemy: {assigned}/2 clips assigned.");
        }

        // ---- trap --------------------------------------------------------------------

        public static void SetupTrapAudio()
        {
            TrapDeathZone2D trap = FindPrefabComponent<TrapDeathZone2D>("Trap");
            if (trap == null)
            {
                Debug.LogWarning("[AudioAutoSetup] Trap prefab not found. Looking for scene instance...");
                trap = FindSceneComponent<TrapDeathZone2D>();
            }

            if (trap == null) return;

            SerializedObject so = new SerializedObject(trap);
            AudioClip clip = LoadClip("Trap_Trigger");
            if (clip != null)
            {
                so.FindProperty("triggerClip").objectReferenceValue = clip;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(trap);
                Debug.Log("[AudioAutoSetup] Trap: 1/1 clip assigned.");
            }
        }
    }
}
