using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Randomly scatters prefabs around the world origin (0, 0, 0). Each one lands at a random
/// point in a ring on the X/Z plane (between minRadius and maxRadius), with its height picked
/// between minY and maxY, optionally a random rotation around the Y axis, and optionally a
/// random uniform size (same scale on all three axes).
/// Add to an empty GameObject, fill in the prefab list, and press Play (or right-click the
/// component header > "Scatter Now" to do it in the editor without entering Play mode).
///
/// All variables are private; [SerializeField] is what keeps them editable in the Inspector.
/// Spawned prefabs are parented under this GameObject to keep the Hierarchy tidy.
/// </summary>
public class PrefabScatterer : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Each spawn picks one of these at random. Empty slots are skipped.")]
    [SerializeField] private List<GameObject> prefabs = new List<GameObject>();
    [Tooltip("How many prefabs to place in total.")]
    [SerializeField] private int count = 50;

    [Header("Area")]
    [Tooltip("Prefabs never land closer than this to (0, 0, 0). 0 = they can land right at the centre.")]
    [SerializeField] private float minRadius = 0f;
    [Tooltip("Prefabs never land further than this from (0, 0, 0) on the X/Z plane. Spread is even across the ring's area, not bunched toward the centre.")]
    [SerializeField] private float maxRadius = 20f;
    [Tooltip("Lowest height a prefab can be placed at.")]
    [SerializeField] private float minY = -0.05f;
    [Tooltip("Highest height a prefab can be placed at.")]
    [SerializeField] private float maxY = 0.1f;

    [Header("Rotation")]
    [Tooltip("On: each prefab gets a random rotation around the Y axis (0-360 degrees). Off: prefabs keep their default rotation.")]
    [SerializeField] private bool randomYRotation = true;

    [Header("Size")]
    [Tooltip("On: each prefab gets a random uniform size (the same multiplier on X, Y and Z). Off: prefabs keep their own scale.")]
    [SerializeField] private bool randomSize = true;
    [Tooltip("Smallest size multiplier. 1 = the prefab's own size, 0.5 = half size.")]
    [SerializeField] private float minSize = 0.8f;
    [Tooltip("Largest size multiplier. 1 = the prefab's own size, 2 = double size.")]
    [SerializeField] private float maxSize = 1.2f;

    [Header("Behaviour")]
    [Tooltip("On: scatters automatically when the scene starts. Off: nothing happens until you use the 'Scatter Now' context menu.")]
    [SerializeField] private bool scatterOnStart = true;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(0.4f, 0.8f, 1f);

    private void Start()
    {
        if (scatterOnStart) Scatter();
    }

    [ContextMenu("Scatter Now")]
    private void Scatter()
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning("PrefabScatterer: the prefab list is empty - nothing to place.");
            return;
        }

        if (minRadius > maxRadius)
        {
            Debug.LogWarning("PrefabScatterer: minRadius is larger than maxRadius - swapping them for this run.");
        }

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
            if (prefab == null) continue;

            GameObject instance = Instantiate(prefab, GetRandomPosition(), GetRotation(prefab), transform);
            if (randomSize) instance.transform.localScale = prefab.transform.localScale * GetRandomSize();
        }
    }

    private Vector3 GetRandomPosition()
    {
        float low = Mathf.Max(0f, Mathf.Min(minRadius, maxRadius));
        float high = Mathf.Max(0f, Mathf.Max(minRadius, maxRadius));

        // Picking the distance from the SQUARE of the radii keeps the spread even across the
        // ring's area - picking it straight from the radii would bunch prefabs near the inner edge.
        float distance = Mathf.Sqrt(Random.Range(low * low, high * high));
        float angle = Random.Range(0f, Mathf.PI * 2f);

        float y = Random.Range(minY, maxY);
        return new Vector3(Mathf.Cos(angle) * distance, y, Mathf.Sin(angle) * distance);
    }

    // One number used for all three axes, so the prefab grows/shrinks without getting squashed.
    // Min/max are sorted first so mixed-up Inspector values still work.
    private float GetRandomSize()
    {
        float low = Mathf.Max(0.01f, Mathf.Min(minSize, maxSize));
        float high = Mathf.Max(0.01f, Mathf.Max(minSize, maxSize));
        return Random.Range(low, high);
    }

    private Quaternion GetRotation(GameObject prefab)
    {
        if (!randomYRotation) return prefab.transform.rotation; // keep the prefab's own rotation
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = gizmoColor;
        DrawXZCircle(Vector3.zero, maxRadius);
        if (minRadius > 0f) DrawXZCircle(Vector3.zero, minRadius);
    }

    // Flat circle in the XZ plane - the same shape GetRandomPosition samples from.
    private void DrawXZCircle(Vector3 center, float circleRadius, int segments = 48)
    {
        Vector3 prevPoint = center + new Vector3(circleRadius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}