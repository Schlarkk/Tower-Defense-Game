using UnityEngine;

/// <summary>
/// Attach to a projectile prefab. Homes in on its target and deals damage on contact.
/// If the target dies before the projectile arrives, the projectile just disappears
/// (no wasted damage on a dead/gone enemy).
/// </summary>
public class Projectile : MonoBehaviour
{
    private Enemy target;
    private float damage;
    private float speed;

    [Tooltip("Distance at which the projectile counts as 'hit'.")]
    public float hitDistance = 0.3f;

    [Tooltip("Safety timeout so projectiles never live forever if something goes wrong.")]
    public float maxLifetime = 5f;

    public void Launch(Enemy targetEnemy, float dmg, float projSpeed)
    {
        target = targetEnemy;
        damage = dmg;
        speed = projSpeed;
        Destroy(gameObject, maxLifetime);
    }

    private void Update()
    {
        if (target == null)
        {
            // Target died or was removed before we arrived - no hit, just clean up.
            Destroy(gameObject);
            return;
        }

        Vector3 targetPos = target.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
        transform.LookAt(targetPos);

        if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}