using UnityEngine;

/// <summary>
/// Handles tower placement. BeginPlacement(towerData) - called by a UI button - spawns a
/// preview of that tower that follows the mouse across the ground; left-click confirms
/// placement (spending currency) if the spot is clear of other towers, right-click or
/// Escape cancels. Add this once to an empty GameObject in your scene.
/// </summary>
public class TowerBuildManager : MonoBehaviour
{
    public static TowerBuildManager Instance { get; private set; }

    [Header("Placement")]
    [Tooltip("Layer the ground/floor is on - the preview follows wherever this raycasts to.")]
    public LayerMask groundLayerMask;
    [Tooltip("Layer towers are on - used to block placing on top of another tower.")]
    public LayerMask towerLayerMask;
    [Tooltip("Radius checked against towerLayerMask before allowing placement - roughly the tower's footprint.")]
    public float placementCheckRadius = 0.5f;
    [Tooltip("Height added on the Y axis when placing a tower. Raise this if towers sit inside the ground (depends on where your tower prefab's pivot is). Dragging a placed tower keeps this height.")]
    public float placementHeightOffset = 0f;
    public float maxRaycastDistance = 200f;

    [Header("Preview Tint")]
    public Color validTint = new Color(0.4f, 1f, 0.4f);
    public Color invalidTint = new Color(1f, 0.4f, 0.4f);
    [Tooltip("Color property name on your shader: '_Color' for Built-in/Standard, '_BaseColor' for URP Lit.")]
    public string colorPropertyName = "_BaseColor";

    private GameObject previewInstance;
    private TowerData pendingTowerData;
    private Renderer previewRenderer;
    private MaterialPropertyBlock propBlock;
    private Camera cam;
    private bool isPlacing;
    private bool lastPositionValid;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple TowerBuildManager instances found in the scene - keeping the first one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        propBlock = new MaterialPropertyBlock();
        cam = Camera.main;
    }

    /// <summary>Call this from a UI button's OnClick to start placing a tower of this type.</summary>
    public void BeginPlacement(TowerData towerData)
    {
        if (towerData == null || towerData.towerPrefab == null)
        {
            Debug.LogWarning("TowerBuildManager: TowerData is missing its towerPrefab.");
            return;
        }

        CancelPlacement(); // clear out any placement already in progress

        pendingTowerData = towerData;
        previewInstance = Instantiate(towerData.towerPrefab);
        SetPreviewMode(previewInstance, true);
        previewRenderer = previewInstance.GetComponentInChildren<Renderer>();
        isPlacing = true;
    }

    /// <summary>Destroys the in-progress preview without spending anything.</summary>
    public void CancelPlacement()
    {
        if (previewInstance != null) Destroy(previewInstance);
        previewInstance = null;
        pendingTowerData = null;
        previewRenderer = null;
        isPlacing = false;
    }

    private void Update()
    {
        if (!isPlacing) return;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayerMask))
        {
            Vector3 placePos = hit.point + Vector3.up * placementHeightOffset;
            previewInstance.transform.position = placePos;
            lastPositionValid = IsPositionClear(placePos);
            Tint(lastPositionValid ? validTint : invalidTint);
        }
        // If the raycast misses the ground this frame (cursor off the play area), the preview
        // just stays put at its last position rather than snapping somewhere odd.

        if (Input.GetMouseButtonDown(0))
        {
            TryConfirmPlacement();
        }
    }

    private void TryConfirmPlacement()
    {
        if (!lastPositionValid)
        {
            Debug.Log("Can't place here - blocked by another tower.");
            return;
        }

        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpend(pendingTowerData.cost))
        {
            Debug.Log("Not enough currency to place this tower.");
            return;
        }

        SetPreviewMode(previewInstance, false);
        ClearTint();

        // Select it immediately so its range shows right away, no re-click needed.
        TowerInteractable interactable = previewInstance.GetComponent<TowerInteractable>();
        if (interactable != null) interactable.SelectThisTower();

        // Hand off to the now-real tower - stop tracking it as a preview.
        previewInstance = null;
        pendingTowerData = null;
        previewRenderer = null;
        isPlacing = false;
    }

    private bool IsPositionClear(Vector3 position)
    {
        // The preview's own collider is disabled while placing, so any hit here is a real tower.
        Collider[] hits = Physics.OverlapSphere(position, placementCheckRadius, towerLayerMask);
        return hits.Length == 0;
    }

    private void SetPreviewMode(GameObject instance, bool isPreview)
    {
        Tower tower = instance.GetComponent<Tower>();
        if (tower != null) tower.enabled = !isPreview;

        TowerInteractable interactable = instance.GetComponent<TowerInteractable>();
        if (interactable != null) interactable.enabled = !isPreview;

        Collider col = instance.GetComponent<Collider>();
        if (col != null) col.enabled = !isPreview;
    }

    private void Tint(Color color)
    {
        if (previewRenderer == null) return;
        previewRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(colorPropertyName, color);
        previewRenderer.SetPropertyBlock(propBlock);
    }

    private void ClearTint()
    {
        if (previewRenderer == null) return;
        previewRenderer.SetPropertyBlock(new MaterialPropertyBlock()); // resets to the material's real color
    }
}