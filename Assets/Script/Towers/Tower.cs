using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// One upgrade in a tower's upgrade chain. The tower's STARTING tier is simply the TowerData
/// assigned on the Tower component itself, so this list only holds the upgrades bought after
/// that - first entry = first upgrade, and so on. There is no entry for the starting tier.
/// </summary>
[System.Serializable]
public class TowerUpgradeTier
{
    [Tooltip("Just a label for logs - 'Upgrade 1', 'Rifle Path 1', whatever fits your naming.")]
    public string tierName = "Upgrade 1";
    [Tooltip("Stats for this tier. Leave empty to keep whatever TowerData the previous tier was using.")]
    public TowerData data;
    [Tooltip("Currency cost to upgrade INTO this tier from the previous one.")]
    public int upgradeCost = 0;
}

public enum TargetingMode
{
    First,      // furthest along the path / closest to the base
    Last,       // closest to the spawn point
    Strongest,  // highest current health
    Closest     // closest to this tower physically
}

/// <summary>
/// Attach to every tower prefab. Assign a TowerData asset to configure its stats.
/// Swap towerData at runtime (e.g. when upgrading) to change stats without changing the GameObject.
///
/// Prefab layout for upgradeable towers (one prefab, no separate prefab per tier):
///   TowerRoot            (Tower, TowerInteractable, HitFlashEffect, the Collider)
///   +-- Models           (empty GameObject - the "modelRoot")
///       +-- Tier0Model   (child 0 - the starting look)
///       +-- Tier1Model   (child 1 - the look after the first upgrade)
///       +-- ...
/// Upgrading advances the tier, swaps towerData, and shows only the matching child of Models.
/// Keep colliders on the root only - not on the model children.
/// </summary>
public class Tower : MonoBehaviour
{
    public TowerData towerData;
    public TargetingMode targetingMode = TargetingMode.First;

    [Tooltip("Where projectiles spawn from / where the tower visually aims. Defaults to this transform.")]
    public Transform firePoint;

    [Tooltip("Layer mask for enemies, so OverlapSphere only picks them up.")]
    public LayerMask enemyLayerMask;

    [Header("Upgrades")]
    [Tooltip("Empty parent that holds one child per tier model: child 0 = starting model, child 1 = after the first upgrade, etc. Auto-found if you name it 'Models'. Leave empty (and don't have a 'Models' child) for towers whose look never changes.")]
    public Transform modelRoot;
    [Tooltip("One entry per upgrade, in the order they're bought. The starting tier is the TowerData assigned above - don't add an entry for it. If there are more upgrades than models, the last model stays shown.")]
    public List<TowerUpgradeTier> upgradeTiers = new List<TowerUpgradeTier>();

    [Header("Gizmos")]
    public bool showGizmos = true;
    [Tooltip("If on, gizmos draw all the time. If off, they only draw while this object is selected in the Hierarchy.")]
    public bool alwaysShowGizmos = false;
    public Color rangeGizmoColor = Color.yellow;

    private float fireCooldown = 0f;
    [SerializeField] private GameObject muzzleLight;
    [SerializeField] private ParticleSystem muzzleParticle;
    private Enemy currentTarget;
    private float currentHealth;
    private HitFlashEffect hitFlash;

    private Coroutine muzzleFlash;

    private Animator animator;
    [SerializeField] private GameObject anim;

    /// <summary>Fired when this tower is destroyed by enemy attacks. Enemies attacking it use this to know when to move on.</summary>
    public System.Action<Tower> OnDestroyed;

    /// <summary>Current health as a 0-1 fraction, for a health bar UI if you want one.</summary>
    public float CurrentHealthPercent => towerData != null ? currentHealth / towerData.maxHealth : 0f;

    /// <summary>Shortcut to towerData.priority, used by Enemy's tower-targeting scan.</summary>
    public int Priority => towerData != null ? towerData.priority : 0;

    // 0 = starting tier, 1 = after the first upgrade, and so on.
    private int currentTier = 0;
    public int CurrentTier => currentTier;
    public bool HasNextUpgrade => currentTier < upgradeTiers.Count;
    public TowerUpgradeTier NextUpgrade => HasNextUpgrade ? upgradeTiers[currentTier] : null;

    /// <summary>
    /// The TowerData this tower WILL have after its next upgrade (used by the info panel's
    /// hover preview). Falls back to the current data if the next tier doesn't override it,
    /// and is null if there's no next upgrade.
    /// </summary>
    public TowerData NextTierData
    {
        get
        {
            if (!HasNextUpgrade) return null;
            TowerData overrideData = NextUpgrade.data;
            return overrideData != null ? overrideData : towerData;
        }
    }

    /// <summary>Fires after a successful upgrade, passing the new tier index.</summary>
    public System.Action<int> OnUpgraded;

    private void Awake()
    {
        if (firePoint == null) firePoint = transform;
        if (modelRoot == null) modelRoot = transform.Find("Models");
        if (towerData != null) currentHealth = towerData.maxHealth;
        hitFlash = GetComponent<HitFlashEffect>();
        animator = anim.GetComponent<Animator>();
        ApplyModelForCurrentTier();
    }

    /// <summary>
    /// Spends currency and advances to the next upgrade: swaps towerData (if that upgrade
    /// has one assigned), fully heals, and shows the matching model. Returns false (spending
    /// nothing) if there's no next upgrade or you can't afford it.
    /// </summary>
    public bool TryUpgrade()
    {
        if (!HasNextUpgrade) return false;

        TowerUpgradeTier next = upgradeTiers[currentTier];

        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpend(next.upgradeCost))
        {
            Debug.Log($"Not enough currency to upgrade to {next.tierName}.");
            return false;
        }

        currentTier++;

        if (next.data != null) towerData = next.data;

        // Full heal on upgrade - the info panel's hover preview assumes this, so if you change
        // it to carry over a % instead, update the health line in TowerInfoPanel.RefreshStats too.
        if (towerData != null) currentHealth = towerData.maxHealth;

        ApplyModelForCurrentTier(); // model first, THEN the event, so listeners see the new model
        OnUpgraded?.Invoke(currentTier);
        return true;
    }

    // Shows the model child matching the current tier and hides the rest. If there are fewer
    // models than tiers, the last model stays up (handy for stat-only upgrades).
    private void ApplyModelForCurrentTier()
    {
        if (modelRoot == null) return;

        if (modelRoot.childCount == 0)
        {
            Debug.LogWarning($"{name}: modelRoot '{modelRoot.name}' has no child models to swap between.");
            return;
        }

        int modelIndex = Mathf.Min(currentTier, modelRoot.childCount - 1);
        for (int i = 0; i < modelRoot.childCount; i++)
        {
            modelRoot.GetChild(i).gameObject.SetActive(i == modelIndex);
        }
    }

    /// <summary>Call this whenever an enemy attacks the tower.</summary>
    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return; // already destroyed, ignore further hits this frame

        currentHealth -= amount;
        hitFlash?.Flash();
        animator.SetTrigger("Hit");
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            OnDestroyed?.Invoke(this);
            // TODO later: play a destruction VFX/removal animation before actually removing the
            // GameObject if you want that instead of an instant Destroy.
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (towerData == null) return;

        fireCooldown -= Time.deltaTime;

        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            currentTarget = FindTarget();
        }

        if (currentTarget != null)
        {
            AimAt(currentTarget.transform.position);

            if (fireCooldown <= 0f)
            {
                Fire(currentTarget);
                fireCooldown = 1f / Mathf.Max(towerData.fireRate, 0.01f);
            }
        }
    }

    private bool IsValidTarget(Enemy enemy)
    {
        if (enemy == null) return false;
        float dist = Vector3.Distance(transform.position, enemy.transform.position);
        return dist <= towerData.range;
    }

    private Enemy FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, towerData.range, enemyLayerMask);
        if (hits.Length == 0) return null;

        Enemy best = null;
        float bestScore = float.NegativeInfinity;

        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null) continue;

            float score = ScoreForTargeting(enemy);
            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    private float ScoreForTargeting(Enemy enemy)
    {
        switch (targetingMode)
        {
            case TargetingMode.Closest:
                return -Vector3.Distance(transform.position, enemy.transform.position);

            case TargetingMode.Strongest:
                // NOTE: requires Enemy to expose current health publicly - see note below.
                return enemy.CurrentHealthPercent;

            case TargetingMode.First:
            case TargetingMode.Last:
            default:
                // NOTE: proper First/Last needs "distance remaining along path", which requires
                // NavMeshAgent.remainingDistance on the enemy. Placeholder uses raw distance to the
                // base transform for now - swap in real path-distance once your Enemy exposes it.
                float distToBase = Vector3.Distance(enemy.transform.position, FindBasePosition());
                return targetingMode == TargetingMode.First ? -distToBase : distToBase;
        }
    }

    private Vector3 FindBasePosition()
    {
        ResearchFacility facility = FindObjectOfType<ResearchFacility>();
        return facility != null ? facility.transform.position : transform.position;
    }

    private void AimAt(Vector3 worldPos)
    {
        Vector3 direction = worldPos - transform.position;
        direction.y = 0f; // keep the tower upright; remove this line for full 3D aiming
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void Fire(Enemy target)
    {
        muzzleFlash = StartCoroutine(MuzzleFlash());
        muzzleParticle.Play();


        if (towerData.projectilePrefab != null)
        {
            GameObject projGO = Instantiate(towerData.projectilePrefab, firePoint.position, firePoint.rotation);
            Projectile proj = projGO.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Launch(target, towerData.damage, towerData.projectileSpeed);
                animator.SetTrigger("Shoot");
            }
        }
        else
        {
            // Hitscan fallback: instant damage, no projectile prefab needed.
            target.TakeDamage(towerData.damage);
            animator.SetTrigger("Shoot");
        }
    }


    private IEnumerator MuzzleFlash()
    {
        muzzleLight.SetActive(true);

        yield return new WaitForSeconds(0.2f);

        muzzleLight.SetActive(false);
        muzzleFlash = null;
    }

    private void OnDrawGizmos()
    {
        if (showGizmos && alwaysShowGizmos) DrawGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (showGizmos && !alwaysShowGizmos) DrawGizmos();
    }

    private void DrawGizmos()
    {
        if (towerData == null) return;
        Gizmos.color = rangeGizmoColor;
        Gizmos.DrawWireSphere(transform.position, towerData.range);
    }
}