using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to a UI Button. Assign a TowerData asset in the Inspector - clicking the button
/// starts placement mode for that tower via TowerBuildManager. Optionally assign costLabel
/// to auto-display the price on the button.
/// </summary>
[RequireComponent(typeof(Button))]
public class TowerBuildButton : MonoBehaviour
{
    public TowerData towerData;
    [Tooltip("Optional - shows the tower's cost. Leave empty to skip.")]
    public TMP_Text costLabel;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(HandleClick);

        if (costLabel != null && towerData != null)
        {
            costLabel.text = "$" + towerData.cost.ToString();
        }
    }

    private void HandleClick()
    {
        if (TowerBuildManager.Instance == null)
        {
            Debug.LogWarning("TowerBuildButton: no TowerBuildManager found in the scene.");
            return;
        }
        TowerBuildManager.Instance.BeginPlacement(towerData);
    }
}