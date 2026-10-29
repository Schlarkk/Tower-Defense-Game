using UnityEngine;

/// <summary>
/// Defines the stats for one enemy type (e.g. "Class 4 Entity", "Class 4-2 Entity").
/// Create one asset per class/sub-class combo: right-click in Project > Create > TD > Enemy Data.
/// Keeping this as data (not code) means your 10 classes x 5 sub-classes don't need 50 scripts,
/// just 50 assets that all use the same Enemy.cs behaviour.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "TD/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Class 1 Entity";
    [Tooltip("Main class, 1-10 (or use 11 for Class X if you want it to sort above 10).")]
    public int enemyClass = 1;
    [Tooltip("Sub-class 0 = base entity, 1-5 = variants that buff/change it.")]
    public int subClass = 0;

    [Header("Spawning")]
    [Tooltip("Controls which wave this enemy starts appearing on. See EnemySpawner's Difficulty Per Wave setting for the exact wave math.")]
    public float spawnDifficulty = 1f;

    [Header("Stats")]
    public float maxHealth = 10f;
    public float moveSpeed = 3.5f;
    [Tooltip("Damage dealt to the research facility if this enemy reaches it.")]
    public float damageToBase = 1f;
    [Tooltip("Currency granted to the player when this enemy is killed.")]
    public int killReward = 1;

    [Header("Tower targeting")]
    [Tooltip("Towers inside this radius can be picked as a target (checked at spawn, and whenever the enemy has no target). A locked-on tower that ends up further away than this (plus Range Leeway) is dropped, and the enemy heads for the facility again.")]
    public float detectionRadius = 5f;
    [Tooltip("Damage dealt to a tower per attack tick while in range of it.")]
    public float attackDamage = 2f;
    [Tooltip("Seconds between attack ticks against a tower.")]
    public float attackInterval = 1f;
    [Tooltip("How close the enemy needs to be to a tower before it stops moving and starts attacking.")]
    public float attackRange = 1.5f;
    [Tooltip("Extra wiggle room before the enemy gives up on a tower it's locked onto. It stops chasing once the tower is further than Detection Radius + this, and stops attacking (and goes back to chasing) once the tower is further than Attack Range + this. Without it, an enemy right at the edge of a range would flicker between states.")]
    public float rangeLeeway = 1f;

    [Header("Visual/Prefab")]
    [Tooltip("The prefab that gets spawned; must have an Enemy component + NavMeshAgent on it.")]
    public GameObject prefab;
}