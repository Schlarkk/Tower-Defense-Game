using UnityEngine;

/// <summary>
/// Attach to the research facility object enemies are trying to destroy.
/// Enemies find this via the "target" reference passed in by the spawner.
/// </summary>
public class ResearchFacility : MonoBehaviour
{
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public System.Action<float, float> OnHealthChanged; // (current, max) - hook UI here
    public System.Action OnFacilityDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            OnFacilityDestroyed?.Invoke();
            // TODO later: trigger your game-over screen / GameManager state change here.
            Debug.Log("Research Facility destroyed. Game over.");
        }
    }
}