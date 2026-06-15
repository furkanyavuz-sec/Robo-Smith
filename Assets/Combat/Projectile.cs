// Projectile.cs — Temiz versiyon

using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Mermi Ayarları")]
    [SerializeField] private float      moveSpeed = 12f;
    [SerializeField] private float      lifetime  = 4f;
    [SerializeField] private GameObject hitVFX;

    private int       damage;
    private Transform target;
    private int       ownerTeamID;
    private bool      hasHit = false;

    public void Initialize(int shooterTeamID, Transform targetTransform, int dmg)
    {
        ownerTeamID = shooterTeamID;
        target      = targetTransform;
        damage      = dmg;

        if (TryGetComponent<Collider>(out Collider col))
            col.isTrigger = true;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (hasHit) return;
        if (target == null) { Destroy(gameObject); return; }

        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.forward   = direction;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        BattleRobot hit = other.GetComponentInParent<BattleRobot>();
        if (hit == null || hit.TeamID == ownerTeamID) return;

        hasHit = true;
        hit.TakeDamage(damage, WeaponCategory.Ranged);

        if (hitVFX != null)
            Instantiate(hitVFX, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}