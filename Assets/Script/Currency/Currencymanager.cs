using UnityEngine;

/// <summary>
/// Tracks the player's currency. Add once to an empty GameObject in your scene (e.g.
/// "GameManager" or its own "CurrencyManager" object). Enemies award currency automatically
/// on death via their EnemyData.killReward - see Enemy.cs's Die() method.
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    public int startingCurrency = 100;

    private int currentCurrency;
    public int CurrentCurrency => currentCurrency;

    /// <summary>Fires whenever currency changes, passing the new total. Hook UI up to this.</summary>
    public System.Action<int> OnCurrencyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple CurrencyManager instances found in the scene - keeping the first one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentCurrency = startingCurrency;
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        currentCurrency += amount;
        OnCurrencyChanged?.Invoke(currentCurrency);
    }

    /// <summary>Attempts to spend currency. Returns false (and spends nothing) if you can't afford it.</summary>
    public bool TrySpend(int amount)
    {
        if (amount < 0 || currentCurrency < amount) return false;
        currentCurrency -= amount;
        OnCurrencyChanged?.Invoke(currentCurrency);
        return true;
    }
}