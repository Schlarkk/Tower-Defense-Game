using UnityEngine;
using TMPro;

/// <summary>
/// attach to a TextMeshPro text to show the player's current currency, live-updating whenever
/// CurrencyManager's balance changes. Optional convenience - not required for the
/// currency/placement system to function.
/// </summary>
public class CurrencyDisplay : MonoBehaviour
{
    public TMP_Text label;
    public string prefix = "$";

    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged += Refresh;
            Refresh(CurrencyManager.Instance.CurrentCurrency);
        }
        else
        {
            Debug.LogWarning("CurrencyDisplay: no CurrencyManager found in the scene.");
        }
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged -= Refresh;
        }
    }

    private void Refresh(int amount)
    {
        if (label != null) label.text = prefix + amount;
    }
}