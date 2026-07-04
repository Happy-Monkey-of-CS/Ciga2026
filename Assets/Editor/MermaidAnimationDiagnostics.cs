using System.Text;
using UnityEditor;
using UnityEngine;

public static class MermaidAnimationDiagnostics
{
    private const string RunClipPath = "Assets/Demo/Mermaid/Clips/Mermaid_Run.anim";

    [MenuItem("Tools/Ciga/Diagnostics/Sample Mermaid Run On Demo Player")]
    public static void SampleMermaidRunOnDemoPlayer()
    {
        GameObject player = GameObject.Find("Player");
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunClipPath);

        if (player == null || clip == null)
        {
            Debug.LogError($"Mermaid animation diagnostic failed. Player found: {player != null}, clip found: {clip != null}");
            return;
        }

        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            Debug.LogError("Mermaid animation diagnostic failed. Player has no SpriteRenderer.");
            return;
        }

        Undo.RecordObject(renderer, "Sample Mermaid Run Animation");
        Sprite original = renderer.sprite;
        StringBuilder report = new StringBuilder();
        report.AppendLine("Mermaid_Run sample result on scene Player:");

        float[] times = { 0f, 0.09f, 0.18f, 0.27f, 0.36f };
        foreach (float time in times)
        {
            clip.SampleAnimation(player, time);
            report.AppendLine($"{time:0.00}s -> {(renderer.sprite != null ? renderer.sprite.name : "null")}");
        }

        renderer.sprite = original;
        EditorUtility.SetDirty(renderer);
        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Ciga/Diagnostics/Sample Mermaid Animator Run State")]
    public static void SampleMermaidAnimatorRunState()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("Mermaid animator diagnostic failed. Player was not found.");
            return;
        }

        Animator animator = player.GetComponent<Animator>();
        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
        if (animator == null || renderer == null)
        {
            Debug.LogError($"Mermaid animator diagnostic failed. Animator found: {animator != null}, SpriteRenderer found: {renderer != null}");
            return;
        }

        Undo.RecordObject(renderer, "Sample Mermaid Animator Run State");
        Sprite original = renderer.sprite;
        StringBuilder report = new StringBuilder();
        report.AppendLine("Mermaid Animator Run state sample result on scene Player:");
        report.AppendLine($"Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}");

        animator.Rebind();
        animator.Update(0f);
        animator.Play("Run", 0, 0f);

        float[] deltas = { 0f, 0.09f, 0.09f, 0.09f, 0.09f };
        float total = 0f;
        foreach (float delta in deltas)
        {
            total += delta;
            animator.Update(delta);
            report.AppendLine($"{total:0.00}s -> state {animator.GetCurrentAnimatorStateInfo(0).shortNameHash}, sprite {(renderer.sprite != null ? renderer.sprite.name : "null")}");
        }

        renderer.sprite = original;
        animator.Rebind();
        EditorUtility.SetDirty(renderer);
        Debug.Log(report.ToString());
    }
}
