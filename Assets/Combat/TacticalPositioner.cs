// TacticalPositioner.cs
// Görev: Robotun taktiksel konumlanmasını yönetir.
// - Engel arkasına saklanma
// - Flanklama hareketi
// - Takım arkadaşının yanında konumlanma

using UnityEngine;
using UnityEngine.AI;

public class TacticalPositioner : MonoBehaviour
{
    [Header("Konumlanma Ayarları")]
    [SerializeField] private float flankAngle       = 90f;   // Flanklama açısı
    [SerializeField] private float coverSearchRadius = 8f;   // Engel arama yarıçapı
    [SerializeField] private float minCoverDistance  = 3f;   // Minimum engel mesafesi
    [SerializeField] private LayerMask obstacleLayer;        // Engel layer

    private NavMeshAgent agent;
    private BattleRobot  owner;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        owner = GetComponent<BattleRobot>();
    }

    // ── Flanklama ─────────────────────────────────────────────────────────

    /// <summary>
    /// Hedefe yandan yaklaş — düz çizgi yerine 90 derece açıyla.
    /// Melee robotlar için ideal.
    /// </summary>
    public Vector3 GetFlankPosition(BattleRobot target)
    {
        if (target == null) return transform.position;

        Vector3 toTarget  = (target.transform.position - transform.position).normalized;
        Vector3 flankDir  = Quaternion.Euler(0, flankAngle, 0) * toTarget;
        Vector3 flankPos  = target.transform.position + flankDir * 3f;

        // NavMesh üzerinde geçerli nokta bul
        if (NavMesh.SamplePosition(flankPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return hit.position;

        return target.transform.position;
    }

    // ── Engel Arkası ──────────────────────────────────────────────────────

    /// <summary>
    /// En yakın engelin arkasına saklan.
    /// Ranged robotlar hasar alınca buraya çekilir.
    /// </summary>
    public Vector3 GetCoverPosition(BattleRobot threat)
    {
        if (threat == null) return transform.position;

        Collider[] obstacles = Physics.OverlapSphere(
            transform.position, coverSearchRadius, obstacleLayer);

        Vector3 bestCover    = transform.position;
        float   bestScore    = float.MinValue;

        foreach (Collider obstacle in obstacles)
        {
            // Engelin tehditten uzak tarafını bul
            Vector3 coverDir = (obstacle.transform.position -
                                threat.transform.position).normalized;
            Vector3 coverPos = obstacle.transform.position + coverDir * minCoverDistance;

            if (!NavMesh.SamplePosition(coverPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;

            // Puan: engel bizi tehditten koruyor mu?
            float distFromThreat = Vector3.Distance(hit.position, threat.transform.position);
            float distToUs       = Vector3.Distance(transform.position, hit.position);

            // Uzak engel + yakın bize = iyi sığınak
            float score = distFromThreat - distToUs * 0.5f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCover = hit.position;
            }
        }

        return bestCover;
    }

    // ── Takım Konumlanması ────────────────────────────────────────────────

    /// <summary>
    /// Takım arkadaşının yanında konumlan — kümeleşmeyi önle.
    /// </summary>
    public Vector3 GetSupportPosition(BattleRobot ally)
    {
        if (ally == null) return transform.position;

        // Müttefikten 3 birim uzakta, hedefe bakan yön
        Vector3 offset  = Random.insideUnitSphere * 3f;
        offset.y        = 0f;
        Vector3 suppPos = ally.transform.position + offset;

        if (NavMesh.SamplePosition(suppPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    /// <summary>Hedefe olan açıyı döndürür (flanklama kontrolü için).</summary>
    public float GetAngleToTarget(BattleRobot target)
    {
        if (target == null) return 0f;

        Vector3 toTarget = (target.transform.position - transform.position).normalized;
        return Vector3.Angle(transform.forward, toTarget);
    }
}