using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Fully automated wave spawner. No hand-authored wave lists - waves are generated from
/// each EnemyData's spawnDifficulty plus this spawner's difficulty/count curves, with
/// randomness in which enemies get picked and when they fire.
///
/// How difficulty maps to waves: an enemy becomes eligible to spawn once
/// startingDifficulty + (currentWave - 1) * difficultyPerWave >= enemyData.spawnDifficulty.
/// Example with the default difficultyPerWave of 0.1: spawnDifficulty 1.0 -> eligible from
/// wave 1. spawnDifficulty 1.3 -> eligible from wave 4 (1.0 + 3*0.1 = 1.3).
///
/// Enemies that share the exact same spawnDifficulty value spawn back-to-back with no gap
/// between them ("at the same time"); the min/max spawn interval only applies between
/// picks of different difficulty.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Setup")]
    public Transform spawnPoint;          // defaults to this transform if left empty
    [Tooltip("Enemies spawn at a random point within this radius of spawnPoint.")]
    public float spawnAreaRadius = 3f;
    [Tooltip("Every enemy type this spawner is allowed to pick from.")]
    public List<EnemyData> enemyPool = new List<EnemyData>();

    [Header("Difficulty Scaling")]
    public float startingDifficulty = 1f;
    [Tooltip("How much the difficulty ceiling rises per wave. 0.1 means +0.1 every wave.")]
    public float difficultyPerWave = 0.1f;

    [Header("Enemy Count Scaling")]
    public int baseEnemyCount = 5;
    [Tooltip("Extra enemies added per wave (rounded). 1.5 means wave 5 has roughly 6 more enemies than wave 1.")]
    public float enemyCountGrowthPerWave = 1.5f;

    [Header("Spawn Timing")]
    [Tooltip("Random delay range between spawning enemies of DIFFERENT difficulty. Same-difficulty picks spawn together with no delay.")]
    public float minSpawnInterval = 0.4f;
    public float maxSpawnInterval = 1.2f;
    public float delayBetweenWaves = 5f;

    [Header("Behaviour")]
    [Tooltip("On: waves chain automatically (delayBetweenWaves pause in between), including the first one. Off: nothing spawns until you call StartNextWave() (e.g. from a UI button) - gives you a calm gap before wave 1 and full control over pacing after.")]
    public bool autoStartWaves = false;
    [Tooltip("0 = run forever. Otherwise stop after this many waves.")]
    public int maxWaves = 0;

    [Header("Gizmos")]
    public bool showGizmos = true;
    public bool alwaysShowGizmos = true;
    public Color gizmoColor = new Color(0f, 1f, 0.4f); // green

    [Header("Read-only (visible in Play mode)")]
    public int currentWave = 0;

    public System.Action<int> OnWaveStarted;
    public System.Action<int> OnWaveCleared;
    public System.Action OnAllWavesComplete;

    private int enemiesAliveThisWave = 0;
    private Transform enemiesContainer;
    private bool waveRequested = false;

    private void Awake()
    {
        if (spawnPoint == null) spawnPoint = transform;

        // Enemies spawn under an "Enemies" child of this same GameObject, creating it if
        // it doesn't already exist (e.g. if you set one up by hand in the Hierarchy).
        Transform existing = transform.Find("Enemies");
        if (existing != null)
        {
            enemiesContainer = existing;
        }
        else
        {
            GameObject containerGO = new GameObject("Enemies");
            containerGO.transform.SetParent(transform, false);
            enemiesContainer = containerGO.transform;
        }
    }

    private void Start()
    {
        StartCoroutine(RunForever());
    }

    /// <summary>
    /// Call this (e.g. from a UI "Start Wave" button) to begin the next wave when
    /// autoStartWaves is off. Has no effect if autoStartWaves is on, since waves already
    /// chain on their own in that mode.
    /// </summary>
    public void StartNextWave()
    {
        waveRequested = true;
    }

    private IEnumerator RunForever()
    {
        while (maxWaves <= 0 || currentWave < maxWaves)
        {
            if (!autoStartWaves)
            {
                waveRequested = false;
                yield return new WaitUntil(() => waveRequested);
            }

            currentWave++;
            yield return StartCoroutine(RunWave(currentWave));
            OnWaveCleared?.Invoke(currentWave);

            if (autoStartWaves)
            {
                yield return new WaitForSeconds(delayBetweenWaves);
            }
        }
        OnAllWavesComplete?.Invoke();
    }

    private IEnumerator RunWave(int waveNumber)
    {
        List<EnemyData> queue = BuildSpawnQueue(waveNumber);
        enemiesAliveThisWave = queue.Count;

        OnWaveStarted?.Invoke(waveNumber);

        for (int i = 0; i < queue.Count; i++)
        {
            SpawnEnemy(queue[i]);

            bool nextIsSameDifficulty = i + 1 < queue.Count &&
                Mathf.Approximately(queue[i + 1].spawnDifficulty, queue[i].spawnDifficulty);

            // Same-difficulty picks fire together with no gap; only wait between different tiers.
            if (!nextIsSameDifficulty)
            {
                yield return new WaitForSeconds(Random.Range(minSpawnInterval, maxSpawnInterval));
            }
        }

        // Wait until every enemy from this wave is dead or has reached the base.
        yield return new WaitUntil(() => enemiesAliveThisWave <= 0);
    }

    /// <summary>
    /// Picks which enemies spawn this wave: filters the pool down to whatever's unlocked at
    /// this wave's difficulty ceiling, then randomly (weighted toward harder enemies as they
    /// unlock) fills a queue of enemyCount picks.
    /// </summary>
    private List<EnemyData> BuildSpawnQueue(int waveNumber)
    {
        float maxDifficulty = startingDifficulty + (waveNumber - 1) * difficultyPerWave;
        List<EnemyData> eligible = enemyPool.FindAll(e => e != null && e.spawnDifficulty <= maxDifficulty);

        if (eligible.Count == 0)
        {
            Debug.LogWarning($"EnemySpawner: no enemies in the pool have spawnDifficulty <= {maxDifficulty:F2} " +
                              $"for wave {waveNumber}. Add a lower-difficulty EnemyData or raise difficultyPerWave.");
            return new List<EnemyData>();
        }

        int enemyCount = Mathf.Max(1, baseEnemyCount + Mathf.RoundToInt((waveNumber - 1) * enemyCountGrowthPerWave));

        List<EnemyData> queue = new List<EnemyData>(enemyCount);
        for (int i = 0; i < enemyCount; i++)
        {
            queue.Add(PickWeightedRandomEnemy(eligible));
        }

        return queue;
    }

    /// <summary>
    /// Weighted random pick where higher spawnDifficulty = more weight, so newly-unlocked
    /// harder enemies gradually become more common instead of everything staying uniform.
    /// </summary>
    private EnemyData PickWeightedRandomEnemy(List<EnemyData> eligible)
    {
        float totalWeight = 0f;
        foreach (EnemyData e in eligible) totalWeight += Mathf.Max(e.spawnDifficulty, 0.01f);

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (EnemyData e in eligible)
        {
            cumulative += Mathf.Max(e.spawnDifficulty, 0.01f);
            if (roll <= cumulative) return e;
        }
        return eligible[eligible.Count - 1]; // float rounding fallback
    }

    private void SpawnEnemy(EnemyData data)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning("EnemySpawner: an EnemyData in the pool is missing its prefab.");
            return;
        }

        GameObject go = Instantiate(data.prefab, GetRandomSpawnPosition(), spawnPoint.rotation, enemiesContainer);
        Enemy enemyComponent = go.GetComponent<Enemy>();

        if (enemyComponent == null)
        {
            Debug.LogError($"Spawned prefab for {data.enemyName} has no Enemy component attached.");
            Destroy(go);
            return;
        }

        enemyComponent.Initialize(data);

        // OnRemoved fires for both kills and reach-base, so this is the single source of truth
        // for "this wave's enemy count just went down by one".
        enemyComponent.OnRemoved += _ => enemiesAliveThisWave--;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnAreaRadius;
        return spawnPoint.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
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
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = gizmoColor;
        DrawXZCircle(origin.position, spawnAreaRadius);
    }

    // Flat circle in the XZ plane (rather than a wire sphere) since that's the shape
    // GetRandomSpawnPosition actually samples from.
    private void DrawXZCircle(Vector3 center, float radius, int segments = 32)
    {
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    public void ToggleAutoWave()
    {
        autoStartWaves = !autoStartWaves;
    }
}