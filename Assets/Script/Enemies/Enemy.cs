using UnityEngine;

/// <summary>
/// Attach to every enemy prefab. No pathfinding - moves in a straight line toward its
/// current target, which is either the research facility or a tower that showed up
/// inside its detection radius.
///
/// Target rules once an enemy has locked onto a tower:
/// - Tower moves out of attack range while being hit  -> enemy stops attacking and chases it.
/// - Tower gets further than detection radius + leeway -> enemy gives up and heads for the
///   facility again (rescanning for other towers on the way).
/// - Tower destroyed (by anyone)                       -> enemy rescans / heads for the facility.
/// All distances are measured flat (X/Z only), matching how enemies actually move.
///
/// Requires:
/// - The Research Facility GameObject to be tagged "ResearchFacility"
/// - Tower colliders to be on the layer assigned to towerLayerMask below
/// </summary>
public class Enemy : MonoBehaviour
{
    public EnemyData data;

    [Tooltip("Layer that tower colliders live on - used for the detection-radius scan.")]
    public LayerMask towerLayerMask;

    [Header("Gizmos")]
    public bool showGizmos = true;
    [Tooltip("If on, gizmos draw all the time. If off, they only draw while this object is selected in the Hierarchy.")]
    public bool alwaysShowGizmos = false;
    public Color detectionGizmoColor = Color.cyan;
    public Color attackRangeGizmoColor = new Color(1f, 0.5f, 0f); // orange
    [Tooltip("The 'give up' radius: detection radius + range leeway.")]
    public Color loseTargetGizmoColor = new Color(0.6f, 0.6f, 0.6f); // grey

    private enum State { SeekingBase, ChasingTower, AttackingTower }
    private State state = State.SeekingBase;

    private float currentHealth;
    private Transform baseTransform;
    private ResearchFacility facility;

    private Tower currentTower;
    private float attackCooldown;
    private HitFlashEffect hitFlash;

    private const string BaseTag = "ResearchFacility";

    /// <summary>Current health as a 0-1 fraction, e.g. for a health bar UI.</summary>
    public float CurrentHealthPercent => data != null ? currentHealth / data.maxHealth : 0f;

    // Fired when this enemy dies from damage (not from reaching the base).
    public System.Action<Enemy> OnKilled;

    // Fired whenever this enemy is removed from play for ANY reason (killed OR reached base).
    public System.Action<Enemy> OnRemoved;

    private void Awake()
    {
        hitFlash = GetComponent<HitFlashEffect>();

        GameObject baseObj = GameObject.FindGameObjectWithTag(BaseTag);
        if (baseObj != null)
        {
            baseTransform = baseObj.transform;
            facility = baseObj.GetComponent<ResearchFacility>();
            if (facility == null)
                Debug.LogWarning($"Object tagged '{BaseTag}' has no ResearchFacility component.");
        }
        else
        {
            Debug.LogError($"No object tagged '{BaseTag}' found in the scene - enemies have nowhere to go.");
        }
    }

    private void OnDestroy()
    {
        ClearTower(); // makes sure we never leave a dangling subscription on a tower that outlives us
    }

    /// <summary>Call this right after Instantiate() to configure the enemy before it starts moving.</summary>
    public void Initialize(EnemyData enemyData)
    {
        data = enemyData;
        currentHealth = data.maxHealth;
        state = State.SeekingBase;
        ScanForTower(); // in case one's already sitting inside the radius at spawn
    }

    private void Update()
    {
        if (data == null) return;

        switch (state)
        {
            case State.SeekingBase:
                UpdateSeeking();
                break;
            case State.ChasingTower:
                UpdateChasing();
                break;
            case State.AttackingTower:
                UpdateAttacking();
                break;
        }
    }

    // No tower in range: walk straight at the base, checking the gizmo sphere every frame.
    private void UpdateSeeking()
    {
        ScanForTower();
        if (state != State.SeekingBase) return; // scan may have just locked onto a tower

        if (baseTransform == null) return;

        MoveToward(baseTransform.position);

        if (FlatDistanceTo(baseTransform.position) <= data.attackRange)
        {
            ReachBase();
        }
    }

    // Locked onto a tower, walking to it.
    private void UpdateChasing()
    {
        if (currentTower == null)
        {
            state = State.SeekingBase; // safety net, shouldn't normally hit this
            return;
        }

        // Tower slipped out of sight (e.g. the player dragged it away) - give up on it.
        if (FlatDistanceTo(currentTower.transform.position) > LoseTargetRadius)
        {
            DropTower();
            return;
        }

        MoveToward(currentTower.transform.position);

        if (FlatDistanceTo(currentTower.transform.position) <= data.attackRange)
        {
            state = State.AttackingTower;
            attackCooldown = 0f;
        }
    }

    // Standing next to a tower, dealing damage on a tick.
    private void UpdateAttacking()
    {
        if (currentTower == null)
        {
            state = State.SeekingBase;
            ScanForTower();
            return;
        }

        // Tower is no longer within reach - stop swinging at thin air and go after it again.
        // (UpdateChasing decides next frame whether it's still close enough to be worth chasing.)
        if (FlatDistanceTo(currentTower.transform.position) > data.attackRange + data.rangeLeeway)
        {
            state = State.ChasingTower;
            return;
        }

        attackCooldown -= Time.deltaTime;
        if (attackCooldown <= 0f)
        {
            currentTower.TakeDamage(data.attackDamage);
            attackCooldown = data.attackInterval;
        }
    }

    /// <summary>
    /// Looks for a tower within data.detectionRadius. Highest towerData.priority wins
    /// (a tank tower with high priority gets targeted over closer-but-lower-priority towers);
    /// distance is only the tie-breaker between towers of equal priority.
    /// </summary>
    private void ScanForTower()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, data.detectionRadius, towerLayerMask);
        if (hits.Length == 0) return;

        Tower best = null;
        int bestPriority = int.MinValue;
        float bestDistance = float.PositiveInfinity;

        foreach (Collider hit in hits)
        {
            Tower tower = hit.GetComponent<Tower>();
            if (tower == null) continue;

            // OverlapSphere counts a tower if any part of its collider touches the sphere, but
            // the give-up check measures to its centre - skip towers whose centre is outside
            // the radius so we never lock onto something we'd drop again straight away.
            float dist = FlatDistanceTo(tower.transform.position);
            if (dist > data.detectionRadius) continue;

            int priority = tower.Priority;

            bool isBetterTarget = priority > bestPriority ||
                                   (priority == bestPriority && dist < bestDistance);

            if (isBetterTarget)
            {
                bestPriority = priority;
                bestDistance = dist;
                best = tower;
            }
        }

        if (best != null) LockOntoTower(best);
    }

    private void LockOntoTower(Tower tower)
    {
        ClearTower(); // never hold two subscriptions at once
        currentTower = tower;
        currentTower.OnDestroyed += HandleTowerDestroyed;
        state = State.ChasingTower;
    }

    // Lets go of the current tower and goes back to heading for the facility (rescanning as it walks).
    private void DropTower()
    {
        ClearTower();
        state = State.SeekingBase;
    }

    // Unsubscribes from the current tower and forgets it. Safe to call when there isn't one.
    private void ClearTower()
    {
        if (currentTower != null) currentTower.OnDestroyed -= HandleTowerDestroyed;
        currentTower = null;
    }

    // Wired to the tower's OnDestroyed event - fires however the tower died (us or anyone else).
    private void HandleTowerDestroyed(Tower tower)
    {
        if (currentTower == tower)
        {
            ClearTower();
            state = State.SeekingBase; // rescans automatically next Update
        }
    }

    /// <summary>How far a locked-on tower can get before the enemy gives up on it.</summary>
    private float LoseTargetRadius => data.detectionRadius + data.rangeLeeway;

    // Distance on the ground plane only. Enemies move on X/Z and keep their own height, so a
    // tower pivot sitting higher or lower than the enemy shouldn't change whether it's "in range".
    private float FlatDistanceTo(Vector3 worldPos)
    {
        Vector3 offset = worldPos - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private void MoveToward(Vector3 worldPos)
    {
        Vector3 destination = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        transform.position = Vector3.MoveTowards(transform.position, destination, data.moveSpeed * Time.deltaTime);

        Vector3 direction = destination - transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        hitFlash?.Flash();
        if (currentHealth <= 0f) Die();
    }

    private void Die()
    {
        ClearTower();
        OnKilled?.Invoke(this);
        CurrencyManager.Instance?.AddCurrency(data.killReward);
        RemoveSelf();
    }

    private void ReachBase()
    {
        if (facility != null) facility.TakeDamage(data.damageToBase);
        RemoveSelf();
    }

    private void RemoveSelf()
    {
        OnRemoved?.Invoke(this);
        Destroy(gameObject);
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
        if (data == null) return;
        Gizmos.color = detectionGizmoColor;
        Gizmos.DrawWireSphere(transform.position, data.detectionRadius);
        Gizmos.color = attackRangeGizmoColor;
        Gizmos.DrawWireSphere(transform.position, data.attackRange);
        Gizmos.color = loseTargetGizmoColor;
        Gizmos.DrawWireSphere(transform.position, LoseTargetRadius);
    }
}