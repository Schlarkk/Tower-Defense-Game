using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to the same GameObject as your upgrade Button (alongside TowerInfoPanel's
/// button reference). Forwards pointer enter/exit so TowerInfoPanel can show/hide the
/// "here's what you'll get" preview on hover.
/// </summary>
public class UpgradeHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TowerInfoPanel infoPanel;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (infoPanel != null) infoPanel.ShowUpgradePreview();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (infoPanel != null) infoPanel.HideUpgradePreview();
    }
}