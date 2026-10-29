using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to every tower prefab (alongside Tower.cs). Handles click-to-select
/// (shows range + highlights the tower) and click-and-drag to reposition it.
///
/// Requires:
/// - A Collider on this GameObject (towers already need one for Tower.cs's targeting).
///   Keep colliders off the child model objects, otherwise clicking a model counts as
///   clicking "something else" and deselects the tower.
/// - Ground geometry on the layer assigned to groundLayerMask.
/// - Tower colliders on the layer assigned to towerLayerMask (used to block drops onto
///   other towers - this can be the same "Towers" layer used elsewhere).
/// - The scene camera tagged "MainCamera".
/// - An EventSystem in the scene (any UI Canvas creates one) so clicks on UI don't
///   count as clicks on the world.
/// </summary>
[RequireComponent(typeof(Tower))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Outline))]
public class TowerInteractable : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Layer the ground/floor is on. The tower follows wherever this raycasts to while dragging.")]
    public LayerMask groundLayerMask;
    [Tooltip("Layer towers are on. Used to block dropping this tower on top of another.")]
    public LayerMask towerLayerMask;
    [Tooltip("Radius checked against towerLayerMask before allowing a move - roughly the tower's footprint.")]
    public float placementCheckRadius = 0.5f;
    public float maxRaycastDistance = 200f;

    [Header("Selection Visuals")]
    public Color rangeIndicatorColor = new Color(0.2f, 0.8f, 1f, 0.6f);
    public Color highlightTint = new Color(1f, 1f, 0.4f);
    [Tooltip("Color property name on your shader: '_Color' for Built-in/Standard, '_BaseColor' for URP Lit.")]
    public string colorPropertyName = "_BaseColor";

    // Only one tower can be selected/dragged at a time - clicking a new one deselects the last.
    private static TowerInteractable currentlySelected;
    public static TowerInteractable CurrentlySelected => currentlySelected;

    private Tower tower;
    private Collider towerCollider;
    private Renderer highlightRenderer;
    private MaterialPropertyBlock propBlock;
    private Color originalColor;
    private LineRenderer rangeIndicator;
    private Camera cam;

    private Outline Outline;

    private void Awake()
    {
        tower = GetComponent<Tower>();
        towerCollider = GetComponent<Collider>();
        propBlock = new MaterialPropertyBlock();
        cam = Camera.main;

        highlightRenderer = FindVisualRenderer();
        CacheOriginalColor();

        CreateRangeIndicator();
        tower.OnUpgraded += HandleUpgraded;

        Outline = GetComponent<Outline>();
    }

    private void OnDestroy()
    {
        if (tower != null) tower.OnUpgraded -= HandleUpgraded;
        if (currentlySelected == this) currentlySelected = null;
    }

    // Upgrading swaps which model is visible, so the highlight has to move to the new model,
    // and the range can change between tiers - refresh the ring if it's currently visible.
    private void HandleUpgraded(int newTier)
    {
        RefreshHighlightRenderer();
        if (rangeIndicator != null && rangeIndicator.enabled) UpdateRangeIndicatorPoints();
    }

    private void Update()
    {
        // Only the currently selected tower bothers checking - everyone else skips this entirely.
        if (currentlySelected != this || !Input.GetMouseButtonDown(0)) return;

        // A click on the info panel / upgrade button isn't a click "elsewhere" in the world.
        // Without this, pressing the upgrade button deselected the tower and hid the panel
        // before the button could register the click.
        if (IsPointerOverUI()) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // OnMouseDown already runs before Update this same frame, so if this click landed on
        // this tower, hit.collider will be towerCollider and we leave it selected. Anything
        // else (ground, an enemy, empty space) means the click was elsewhere - deselect.
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
        {
            if (hit.collider != towerCollider) Deselect();
        }
        else
        {
            Deselect();
        }
    }

    // Unity calls this automatically on left-click, for any object with a Collider under the cursor.
    private void OnMouseDown()
    {
        // Unity fires this even when UI is covering the tower, so skip those clicks.
        if (IsPointerOverUI()) return;
        SelectThisTower();
    }

    /// <summary>
    /// Selects this tower the same way clicking it does (deselects whatever was selected
    /// before, shows the range ring, highlights it). Public so other systems - e.g.
    /// TowerBuildManager, to show the range the instant a new tower is bought - can call it.
    /// </summary>
    public void SelectThisTower()
    {
        if (currentlySelected != null && currentlySelected != this)
        {
            currentlySelected.Deselect();
        }
        currentlySelected = this;
        Select();
    }

    // Called every frame the left mouse button is held down, starting from the object that
    // received OnMouseDown - this is what makes "hold and drag" work.
    private void OnMouseDrag()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit groundHit, maxRaycastDistance, groundLayerMask)) return;

        // Keep the tower's current height - only follow the cursor on the ground plane (X/Z).
        Vector3 candidate = new Vector3(groundHit.point.x, transform.position.y, groundHit.point.z);

        if (IsPositionClear(candidate))
        {
            transform.position = candidate;
            if (rangeIndicator.enabled) UpdateRangeIndicatorPoints(); // keep the ring centered while moving
        }
        // If blocked by another tower, we simply don't move this frame - the tower "sticks"
        // at its last valid spot until you drag the cursor somewhere open again.
    }

    private void OnMouseUp()
    {
        // Selection stays active after dropping (deselect-on-click-elsewhere is handled in
        // Update() above, not here) - so you can keep repositioning without reselecting.
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPositionClear(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, placementCheckRadius, towerLayerMask);
        foreach (Collider hit in hits)
        {
            if (hit != towerCollider) return false; // some other tower occupies that spot
        }
        return true;
    }

    private void Select()
    {
        UpdateRangeIndicatorPoints();
        rangeIndicator.enabled = true;
        SetHighlighted(true);
    }

    public void Deselect()
    {
        if (rangeIndicator != null) rangeIndicator.enabled = false;
        SetHighlighted(false);
        if (currentlySelected == this) currentlySelected = null;
    }

    private void SetHighlighted(bool on)
    {
        if (highlightRenderer == null) return;
        highlightRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(colorPropertyName, on ? highlightTint : originalColor);
        highlightRenderer.SetPropertyBlock(propBlock);
        Outline.enabled = on;
        Outline.OutlineColor = Color.white;

    }

    /// <summary>
    /// Finds the first ACTIVE model renderer under this tower. GetComponentsInChildren skips
    /// inactive children, so after a model swap this returns the new tier's model. LineRenderers
    /// are skipped so the range ring is never mistaken for the tower's model.
    /// </summary>
    private Renderer FindVisualRenderer()
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
        if (highlightRenderer != null && highlightRenderer.sharedMaterial != null
            && highlightRenderer.sharedMaterial.HasProperty(colorPropertyName))
        {
            originalColor = highlightRenderer.sharedMaterial.GetColor(colorPropertyName);
        }
        else
        {
            originalColor = Color.white;
        }
    }

    // Called after an upgrade, once the new model is active: moves the highlight over to it.
    private void RefreshHighlightRenderer()
    {
        Renderer previous = highlightRenderer;

        highlightRenderer = FindVisualRenderer();
        CacheOriginalColor();

        // Clear any leftover tint on the old model so it's clean if it's ever shown again.
        if (previous != null && previous != highlightRenderer) previous.SetPropertyBlock(null);

        // Re-apply (or clear) the highlight on the new model depending on selection state.
        SetHighlighted(currentlySelected == this);
    }

    private void CreateRangeIndicator()
    {
        GameObject go = new GameObject("RangeIndicator");
        go.transform.SetParent(transform, false);

        rangeIndicator = go.AddComponent<LineRenderer>();
        rangeIndicator.useWorldSpace = true; // world-space points so a scaled tower doesn't shrink the ring
        rangeIndicator.loop = true;
        rangeIndicator.widthMultiplier = 0.08f;
        rangeIndicator.material = new Material(Shader.Find("Sprites/Default")); // built-in, no asset needed
        rangeIndicator.startColor = rangeIndicator.endColor = rangeIndicatorColor;
        rangeIndicator.enabled = false;
    }

    private void UpdateRangeIndicatorPoints()
    {
        if (rangeIndicator == null || tower == null || tower.towerData == null) return;

        const int segments = 48;
        float range = tower.towerData.range;
        Vector3 center = transform.position;

        rangeIndicator.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * range, 0.05f, Mathf.Sin(angle) * range);
            rangeIndicator.SetPosition(i, center + offset);
        }
    }
}