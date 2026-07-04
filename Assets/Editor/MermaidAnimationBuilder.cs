using System.Collections.Generic;
using System.IO;
using Ciga.Demo;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ciga.Editor
{
    /// <summary>
    /// Builds the mermaid Animator Controller from PNG sprites in
    /// Assets/Sprites/Mermaid and assigns it to the player.
    /// Run via Tools → Build Mermaid Animations.
    /// </summary>
    public static class MermaidAnimationBuilder
    {
        private const string SpriteDir = "Assets/Sprites/Mermaid";
        private const string AnimDir  = "Assets/Animations/Mermaid";
        private const string ControllerPath = "Assets/Animations/Mermaid/Mermaid.controller";

        private const float FrameRate = 10f;

        [MenuItem("Tools/Build Mermaid Animations", priority = 120)]
        public static void Build()
        {
            // Ensure directories
            if (!Directory.Exists(AnimDir)) Directory.CreateDirectory(AnimDir);

            // Refresh asset DB to ensure the copied PNGs are imported
            AssetDatabase.Refresh();

            // Configure sprite import settings
            ConfigureSpriteImports();

            // Create animation clips
            AnimationClip idleClip     = CreateClip("Idle",     new[] { "Idle_1", "Idle_2", "Idle_3", "Idle_4" });
            AnimationClip runClip      = CreateClip("Run",      new[] { "Run_1", "Run_2", "Run_3", "Run_4" });
            AnimationClip jumpClip     = CreateClip("Jump",     new[] { "Jump_1", "Jump_2", "Jump_3", "Jump_4" });
            AnimationClip wallSlideClip= CreateClip("WallSlide",new[] { "WallSlide_1", "WallSlide_2" });
            AnimationClip climbClip    = CreateClip("Climb",    new[] { "Climb_1", "Climb_2" });
            AnimationClip wallJumpClip = CreateClip("WallJump", new[] { "WallJump_1", "WallJump_2", "WallJump_3" });
            AnimationClip deathClip    = CreateClip("Death",    new[] { "Die_1", "Die_2", "Die_3" });
            AnimationClip attack1Clip  = CreateClip("Attack1",  new[] { "AttackH_1", "AttackH_2", "AttackH_3", "AttackH_4" });
            AnimationClip attack2Clip  = CreateClip("Attack2",  new[] { "AttackU_1", "AttackU_2", "AttackU_3" });
            AnimationClip attack3Clip  = CreateClip("Attack3",  new[] { "AttackD_1", "AttackD_2", "AttackD_3", "AttackD_4" });

            // Build controller
            AnimatorController controller = BuildController(
                idleClip, runClip, jumpClip, wallSlideClip, climbClip,
                wallJumpClip, deathClip, attack1Clip, attack2Clip, attack3Clip);

            // Assign to player
            PlayerController2D player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player != null)
            {
                Animator animator = player.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = controller;
                    EditorUtility.SetDirty(animator);
                    Debug.Log("[MermaidAnim] Controller assigned to player.");
                }

                // Assign anchor indicator sprite (shown at aim target, not as player replacement)
                // Also clear old aim-replacement sprites so no residual anchor shows on the player
                SerializedObject pso = new SerializedObject(player);
                Sprite anchorIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/AnchorIcon.png");
                pso.FindProperty("grappleAimSprite").objectReferenceValue = null;
                pso.FindProperty("strikeAimSprite").objectReferenceValue = null;
                if (anchorIcon != null)
                {
                    pso.FindProperty("anchorSprite").objectReferenceValue = anchorIcon;
                }
                pso.ApplyModifiedProperties();
                EditorUtility.SetDirty(player);
                Debug.Log("[MermaidAnim] Anchor indicator assigned, aim sprites cleared.");
            }
            else
            {
                Debug.LogWarning("[MermaidAnim] No PlayerController2D found in scene.");
            }

            // Import chain texture and update rope material
            ConfigureChainTexture();
            UpdateRopeMaterial();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MermaidAnim] Done!");
        }

        private static void ConfigureChainTexture()
        {
            string chainPath = $"{SpriteDir}/Chain.png";
            TextureImporter importer = AssetImporter.GetAtPath(chainPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            Debug.Log("[MermaidAnim] Chain texture configured.");
        }

        private static void UpdateRopeMaterial()
        {
            // Replace the rope material's texture with the chain
            string matPath = "Assets/Demo/GrappleRopeMaterial.mat";
            Material ropeMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (ropeMat == null)
            {
                Debug.LogWarning("[MermaidAnim] GrappleRopeMaterial.mat not found.");
                return;
            }

            Texture2D chainTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpriteDir}/Chain.png");
            if (chainTex != null)
            {
                ropeMat.mainTexture = chainTex;
                EditorUtility.SetDirty(ropeMat);
                Debug.Log("[MermaidAnim] Rope material updated with chain texture.");
            }
        }

        private static void ConfigureSpriteImports()
        {
            string[] pngs = Directory.GetFiles(SpriteDir, "*.png");
            foreach (string png in pngs)
            {
                string assetPath = png.Replace("\\", "/");
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (importer.filterMode != FilterMode.Bilinear)
                {
                    importer.filterMode = FilterMode.Bilinear;
                    changed = true;
                }
                if (importer.spritePixelsPerUnit != 300)
                {
                    importer.spritePixelsPerUnit = 300;
                    changed = true;
                }

                // Set pivot to bottom center via TextureImporterSettings
                TextureImporterSettings tis = new TextureImporterSettings();
                importer.ReadTextureSettings(tis);
                if (tis.spriteAlignment != (int)SpriteAlignment.BottomCenter)
                {
                    tis.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                    importer.SetTextureSettings(tis);
                    changed = true;
                }

                if (changed) importer.SaveAndReimport();
            }
        }

        private static AnimationClip CreateClip(string name, string[] spriteNames)
        {
            string path = $"{AnimDir}/{name}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;

            if (isNew) clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = FrameRate;

            // Build sprite keyframes
            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            List<ObjectReferenceKeyframe> keyframes = new List<ObjectReferenceKeyframe>();
            float timePerFrame = 1f / FrameRate;
            for (int i = 0; i < spriteNames.Length; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{spriteNames[i]}.png");
                if (sprite == null)
                {
                    Debug.LogWarning($"[MermaidAnim] Missing sprite: {spriteNames[i]}");
                    continue;
                }
                keyframes.Add(new ObjectReferenceKeyframe { time = i * timePerFrame, value = sprite });
            }
            clip.frameRate = FrameRate;

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());

            if (isNew)
                AssetDatabase.CreateAsset(clip, path);
            else
                EditorUtility.SetDirty(clip);

            return clip;
        }

        private static AnimatorController BuildController(
            AnimationClip idle, AnimationClip run, AnimationClip jump,
            AnimationClip wallSlide, AnimationClip climb, AnimationClip wallJump,
            AnimationClip death, AnimationClip attack1, AnimationClip attack2, AnimationClip attack3)
        {
            // Delete existing
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine root = ctrl.layers[0].stateMachine;

            // Parameters matching PlayerController2D
            ctrl.AddParameter("AnimState", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Grounded",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("AirSpeedY", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Jump",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("WallSlide", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Block",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Death",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack1",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack2",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack3",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("noBlood",   AnimatorControllerParameterType.Bool);

            // --- States ---
            AnimatorState idleState     = root.AddState("Idle",     new Vector2(0,    0));
            AnimatorState runState      = root.AddState("Run",      new Vector2(200,  0));
            AnimatorState jumpState     = root.AddState("Jump",     new Vector2(0,    100));
            AnimatorState fallState     = root.AddState("Fall",     new Vector2(200,  100));
            AnimatorState wallSlideState= root.AddState("WallSlide",new Vector2(400,  100));
            AnimatorState climbState    = root.AddState("Climb",    new Vector2(400,  0));
            AnimatorState wallJumpState = root.AddState("WallJump", new Vector2(600,  0));
            AnimatorState deathState    = root.AddState("Death",    new Vector2(600,  200));
            AnimatorState attack1State  = root.AddState("Attack1",  new Vector2(800,  0));
            AnimatorState attack2State  = root.AddState("Attack2",  new Vector2(800,  100));
            AnimatorState attack3State  = root.AddState("Attack3",  new Vector2(800,  200));

            AssignClip(idleState, idle);
            AssignClip(runState, run);
            AssignClip(jumpState, jump);
            AssignClip(fallState, jump);  // fall reuses jump animation
            AssignClip(wallSlideState, wallSlide);
            AssignClip(climbState, climb);
            AssignClip(wallJumpState, wallJump);
            AssignClip(deathState, death);
            AssignClip(attack1State, attack1);
            AssignClip(attack2State, attack2);
            AssignClip(attack3State, attack3);

            root.defaultState = idleState;

            // --- Transitions ---

            // Idle ↔ Run (based on AnimState int)
            root.AddEntryTransition(idleState);
            AddCondition(Transition(idleState, runState), AnimatorConditionMode.Equals, 1, "AnimState");
            AddCondition(Transition(runState, idleState), AnimatorConditionMode.Equals, 0, "AnimState");

            // Idle/Run → Jump (Jump trigger)
            AddCondition(Transition(idleState, jumpState), AnimatorConditionMode.If, 0, "Jump");
            AddCondition(Transition(runState, jumpState), AnimatorConditionMode.If, 0, "Jump");
            AddCondition(Transition(wallSlideState, wallJumpState), AnimatorConditionMode.If, 0, "Jump");

            // Jump → Fall (AirSpeedY < 0)
            { var t = Transition(jumpState, fallState); t.AddCondition(AnimatorConditionMode.Less, 0, "AirSpeedY"); t.hasExitTime = false; }

            // Fall → Idle/Run (Grounded)
            AddCondition(Transition(fallState, idleState), AnimatorConditionMode.If, 0, "Grounded");
            AddCondition(Transition(wallJumpState, idleState), AnimatorConditionMode.If, 0, "Grounded");

            // WallSlide ↔ any (WallSlide bool)
            AddCondition(TransitionAnyTo(root, wallSlideState), AnimatorConditionMode.If, 0, "WallSlide");
            { var t = Transition(wallSlideState, fallState); t.AddCondition(AnimatorConditionMode.IfNot, 0, "WallSlide"); t.hasExitTime = false; }

            // Climb (Block trigger)
            AddCondition(TransitionAnyTo(root, climbState), AnimatorConditionMode.If, 0, "Block");
            Transition(climbState, idleState).hasExitTime = true;

            // Death (Death trigger)
            AddCondition(TransitionAnyTo(root, deathState), AnimatorConditionMode.If, 0, "Death");

            // Attacks (Attack1/2/3 triggers)
            AddCondition(TransitionAnyTo(root, attack1State), AnimatorConditionMode.If, 0, "Attack1");
            AddCondition(TransitionAnyTo(root, attack2State), AnimatorConditionMode.If, 0, "Attack2");
            AddCondition(TransitionAnyTo(root, attack3State), AnimatorConditionMode.If, 0, "Attack3");
            // Return to idle after attack
            Transition(attack1State, idleState).hasExitTime = true;
            Transition(attack2State, idleState).hasExitTime = true;
            Transition(attack3State, idleState).hasExitTime = true;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static void AssignClip(AnimatorState state, AnimationClip clip)
        {
            state.motion = clip;
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
        {
            return from.AddTransition(to);
        }

        private static AnimatorStateTransition TransitionAnyTo(AnimatorStateMachine root, AnimatorState to)
        {
            return root.AddAnyStateTransition(to);
        }

        private static void AddCondition(AnimatorStateTransition t, AnimatorConditionMode mode, float threshold, string param)
        {
            t.AddCondition(mode, threshold, param);
        }

        [MenuItem("Tools/Build Mermaid Animations", validate = true)]
        private static bool ValidateBuild()
        {
            return Directory.Exists(SpriteDir);
        }
    }
}
