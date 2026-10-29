using UnityEngine;
using System.Collections;

/// <summary>
/// Drop this on anything with a Renderer (tower or enemy) to give it a brief color
/// flash when it takes damage. Call Flash() from TakeDamage(). Uses a
/// MaterialPropertyBlock so it doesn't create extra material instances.
///
/// If the object swaps which child model is active (e.g. a tower upgrading and showing its
/// next model), Flash() notices the old renderer is hidden and switches to the active one.
/// </summary>
[RequireComponent(typeof(Outline))]
public class HitFlashEffect : MonoBehaviour
{
    [Tooltip("Renderer to flash. Auto-found on this object/children if left empty.")]
    public Renderer targetRenderer;
    public Color flashColor = Color.red;
    public float flashDuration = 0.2f;
    [Tooltip("Color property name on your shader: '_Color' for Built-in/Standard, '_BaseColor' for URP Lit.")]
    public string colorPropertyName = "_BaseColor";

    private MaterialPropertyBlock propBlock;
    private Color originalColor;
    private Coroutine flashRoutine;

    private Outline outline;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        if (targetRenderer == null) targetRenderer = FindRenderer();
        CacheOriginalColor();

        outline = GetComponent<Outline>();
    }

    /// <summary>Call this whenever the object takes damage.</summary>
    public void Flash()
    {
        // The cached renderer may belong to a model that's been hidden since (tower upgrade) -
        // flashing it would be invisible, so re-find the currently active one first.
        if (targetRenderer == null || !targetRenderer.gameObject.activeInHierarchy)
        {
            RefreshRenderer();
        }

        if (targetRenderer == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        outline.OutlineColor = Color.red;
        outline.enabled = true;

        yield return new WaitForSeconds(flashDuration);

        outline.enabled = false;
        flashRoutine = null;
    }

    private void SetColor(Color color)
    {
        targetRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(colorPropertyName, color);
        targetRenderer.SetPropertyBlock(propBlock);
    }

    /// <summary>
    /// Finds the first active model renderer on this object or its children. Skips
    /// LineRenderers so a tower's range ring is never picked as the thing to flash.
    /// </summary>
    private Renderer FindRenderer()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer) continue;
            return r;
        }
        return null;
    }

    private void CacheOriginalColor()
    {
        if (targetRenderer != null && targetRenderer.sharedMaterial != null
            && targetRenderer.sharedMaterial.HasProperty(colorPropertyName))
        {
            originalColor = targetRenderer.sharedMaterial.GetColor(colorPropertyName);
        }
        else
        {
            originalColor = Color.white;
        }
    }

    private void RefreshRenderer()
    {
        targetRenderer = FindRenderer();
        CacheOriginalColor();
    }
}