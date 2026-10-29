using UnityEngine;

/// <summary>
/// Defines the stats for one tower/upgrade tier (e.g. "Standard 01 Turret", "Standard 01 (Rifle Path) Tier 2").
/// Create one asset per tier of each upgrade branch: Project > Create > TD > Tower Data.
/// </summary>
[CreateAssetMenu(fileName = "NewTowerData", menuName = "TD/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    public string towerName = "Standard 01 Turret";

    [Header("Stats")]
    public float range = 8f;
    public float fireRate = 1f;      // shots per second
    public float damage = 5f;
    public float projectileSpeed = 20f;
    [Tooltip("How much damage this tower can take from enemies before being destroyed.")]
    public float maxHealth = 30f;
    [Tooltip("Higher priority towers get targeted by enemies over lower priority ones within detection range, regardless of which is closer. Give a tank tower a high value to draw enemies toward it and away from your other towers.")]
    public int priority = 0;

    [Header("Shop")]
    [Tooltip("How much currency this tower costs to place.")]
    public int cost = 50;
    [Tooltip("The prefab to instantiate when this tower is bought/placed. Should have Tower.cs (with this asset assigned), TowerInteractable.cs, and a Collider.")]
    public GameObject towerPrefab;

    [Header("Prefab")]
    [Tooltip("Optional: leave empty if this tower uses hitscan instead of a projectile.")]
    public GameObject projectilePrefab;
}