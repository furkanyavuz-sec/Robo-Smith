// RocketProjectile.cs
// Görev: Hedefe doğru uçar, çarpınca AOE patlama yapar.
// Projectile.cs'den ayrı tutuldu çünkü AOE mantığı farklı:
// tek hedefe değil, patlama yarıçapındaki HERKESe hasar verir.

using UnityEngine;

public class RocketProjectile : MonoBehaviour
{
    [Header("Uçuş")]
    [SerializeField] private float moveSpeed  = 8f;
    [SerializeField] private float lifetime   = 6f;

    [Header("Patlama")]
    [SerializeField] private GameObject explosionVFX;  // Patlama efekti prefabı

    private float     aoeRadius;
    private int       damage;
    private int       ownerTeamID;
    private Transform target;
    private bool      hasExploded = false;

    public void Initialize(int teamID, Transform targetTransform, int dmg, float radius)
    {
        ownerTeamID = teamID;
        target      = targetTransform;
        damage      = dmg;
        aoeRadius   = radius;

        if (TryGetComponent<Collider>(out Collider col))
            col.isTrigger = true;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (hasExploded) return;

        if (target == null) { Explode(transform.position); return; }

        // Hedefe doğru uç
        Vector3 dir = (target.position - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
        transform.forward   = dir;

        // Hedefe çok yaklaştıysa patlat
        if (Vector3.Distance(transform.position, target.position) < 0.5f)
            Explode(transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;

        BattleRobot hit = other.GetComponentInParent<BattleRobot>();
        if (hit == null || hit.TeamID == ownerTeamID) return;

        Explode(transform.position);
    }

    private void Explode(Vector3 center)
    {
        hasExploded = true;

        // Patlama efekti — prefab yoksa koddan mini patlama
        if (explosionVFX != null)
            Instantiate(explosionVFX, center, Quaternion.identity);
        else
            DeathExplosion.SmallBlast(center, new Color(0.95f, 0.45f, 0.15f));

        // AOE hasar: yarıçap içindeki tüm düşmanlara
        Collider[] hits = Physics.OverlapSphere(center, aoeRadius);
        foreach (Collider col in hits)
        {
            BattleRobot robot = col.GetComponentInParent<BattleRobot>();
            if (robot == null || robot.TeamID == ownerTeamID) continue;

            // Merkeze uzaklığa göre hasar azalır (falloff)
            float dist        = Vector3.Distance(center, robot.transform.position);
            float falloff     = 1f - Mathf.Clamp01(dist / aoeRadius);
            int   finalDamage = Mathf.RoundToInt(damage * falloff);

            robot.TakeDamage(finalDamage, WeaponCategory.AOE);

            Debug.Log($"<color=orange>[Roket] AOE hasar: {finalDamage} " +
                      $"(mesafe: {dist:F1})</color>");
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, aoeRadius);
    }
}