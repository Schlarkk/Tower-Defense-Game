using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Put this on the root of your tower info UI panel. It shows/hides automatically based on
/// TowerInteractable.CurrentlySelected, displays the selected tower's live stats, and
/// drives the upgrade button.
///
/// While the mouse hovers the upgrade button, every stat label switches to the stats the
/// tower WOULD have after upgrading, and turns green. Move the mouse away and everything
/// goes back to the current stats and the labels' original colors.
///
/// Also add UpgradeHoverTrigger.cs to the same GameObject as Upgrade Button and assign
/// this panel to it - that's what makes the hover preview work.
/// </summary>
public class TowerInfoPanel : MonoBehaviour
{
    [Header("Panel Root")]
    [Tooltip("The GameObject to show/hide. Defaults to this GameObject if left empty.")]
    public GameObject panelRoot;

    [Header("Stat Labels")]
    public TMP_Text towerNameLabel;
    public TMP_Text damageLabel;
    public TMP_Text rangeLabel;
    public TMP_Text fireRateLabel;
    public TMP_Text priorityLabel;
    public TMP_Text healthLabel;

    [Header("Upgrade")]
    public Button upgradeButton;
    [Tooltip("Shows e.g. 'Upgrade - 50' or 'Max Tier'.")]
    public TMP_Text upgradeButtonLabel;

    [Header("Hover Preview")]
    [Tooltip("Color every stat label turns while hovering the upgrade button.")]
    public Color previewColor = Color.green;

    private Tower currentTower;
    private bool isPreviewing;

    // Each label's own color, remembered at startup so un-hovering restores it exactly
    // (instead of assuming everything was white).
    private readonly Dictionary<TMP_Text, Color> originalColors = new Dictionary<TMP_Text, Color>();

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        if (upgradeButton != null) upgradeButton.onClick.AddListener(HandleUpgradeClicked);

        CacheOriginalColor(towerNameLabel);
        CacheOriginalColor(damageLabel);
        CacheOriginalColor(rangeLabel);
        CacheOriginalColor(fireRateLabel);
        CacheOriginalColor(priorityLabel);
        CacheOriginalColor(healthLabel);

        panelRoot.SetActive(false);
    }

    private void CacheOriginalColor(TMP_Text label)
    {
        if (label != null) originalColors[label] = label.color;
    }

    private void Update()
    {
        TowerInteractable selected = TowerInteractable.CurrentlySelected;
        Tower selectedTower = selected != null ? selected.GetComponent<Tower>() : null;

        // Unity's overridden null-check handles a destroyed tower correctly here too, so this
        // also covers "selected tower got destroyed by enemies while its panel was open".
        if (selectedTower != currentTower)
        {
            SwitchTower(selectedTower);
        }
    }

    private void SwitchTower(Tower newTower)
    {
        if (currentTower != null) currentTower.OnUpgraded -= HandleTowerUpgraded;

        currentTower = newTower;
        isPreviewing = false; // a hover from the previous tower shouldn't carry over

        if (currentTower != null)
        {
            currentTower.OnUpgraded += HandleTowerUpgraded;
            panelRoot.SetActive(true);
            RefreshStats();
        }
        else
        {
            panelRoot.SetActive(false);
        }
    }

    // If the mouse is still over the button after clicking it, the preview simply moves on to
    // the NEXT upgrade (or drops back to normal if that was the last one) - see RefreshStats().
    private void HandleTowerUpgraded(int newTier)
    {
        RefreshStats();
    }

    private void RefreshStats()
    {
        if (currentTower == null || currentTower.towerData == null) return;

        // Previewing only makes sense if there's actually something to upgrade to.
        bool showPreview = isPreviewing && currentTower.HasNextUpgrade;
        TowerData data = showPreview ? currentTower.NextTierData : currentTower.towerData;

        SetLabel(towerNameLabel, data.towerName, showPreview);
        SetLabel(damageLabel, $"Damage: {data.damage:F1}", showPreview);
        SetLabel(rangeLabel, $"Range: {data.range:F1}", showPreview);
        SetLabel(fireRateLabel, $"Fire Rate: {data.fireRate:F2}/s", showPreview);
        SetLabel(priorityLabel, $"Priority: {data.priority}", showPreview);

        string healthText;
        if (showPreview)
        {
            // Upgrading fully heals (see Tower.TryUpgrade), so the preview shows a full bar.
            healthText = $"Health: {data.maxHealth:F0}/{data.maxHealth:F0}";
        }
        else
        {
            int current = Mathf.RoundToInt(currentTower.CurrentHealthPercent * data.maxHealth);
            healthText = $"Health: {current}/{data.maxHealth:F0}";
        }
        SetLabel(healthLabel, healthText, showPreview);

        RefreshUpgradeButton();
    }

    // Sets a label's text and color in one go: green while previewing, its own original color otherwise.
    private void SetLabel(TMP_Text label, string text, bool preview)
    {
        if (label == null) return;

        label.text = text;

        if (preview)
        {
            label.color = previewColor;
        }
        else if (originalColors.TryGetValue(label, out Color original))
        {
            label.color = original;
        }
    }

    private void RefreshUpgradeButton()
    {
        if (upgradeButton == null) return;

        bool hasNext = currentTower.HasNextUpgrade;
        upgradeButton.interactable = hasNext;

        if (upgradeButtonLabel != null)
        {
            upgradeButtonLabel.text = hasNext ? $"Upgrade - {currentTower.NextUpgrade.upgradeCost}" : "Max Tier";
        }
    }

    private void HandleUpgradeClicked()
    {
        if (currentTower == null) return;
        currentTower.TryUpgrade(); // RefreshStats() runs automatically via the OnUpgraded subscription above
    }

    /// <summary>Called by UpgradeHoverTrigger when the mouse enters the upgrade button.</summary>
    public void ShowUpgradePreview()
    {
        if (currentTower == null || !currentTower.HasNextUpgrade) return;

        isPreviewing = true;
        RefreshStats();
    }

    /// <summary>Called by UpgradeHoverTrigger when the mouse exits the upgrade button.</summary>
    public void HideUpgradePreview()
    {
        isPreviewing = false;
        RefreshStats();
    }
}