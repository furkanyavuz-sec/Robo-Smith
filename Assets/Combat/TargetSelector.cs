// TargetSelector.cs
// Görev: Utility AI ile en uygun hedefi seçer.
// Her düşman için puan hesaplar, en yüksek puanlıyı döndürür.
// Puan kriterleri: mesafe, HP, tehdit, takım önceliği

using System.Collections.Generic;
using UnityEngine;

public class TargetSelector : MonoBehaviour
{
    [Header("Puan Ağırlıkları")]
    [SerializeField] private float weightDistance  = 0.4f;  // Yakın düşman öncelikli
    [SerializeField] private float weightLowHP     = 0.3f;  // Zayıf düşman öncelikli
    [SerializeField] private float weightThreat    = 0.2f;  // Tehlikeli düşman öncelikli
    [SerializeField] private float weightTeamFocus = 0.1f;  // Takım odağı bonusu

    [Header("Güncelleme Aralığı")]
    [SerializeField] private float updateInterval = 0.5f;   // Her 0.5sn yeniden hesapla

    private BattleRobot owner;
    private RobotMemory  memory;
    private float        updateTimer = 0f;
    private BattleRobot  cachedTarget = null;

    public BattleRobot CurrentTarget => cachedTarget;

    private void Awake()
    {
        owner  = GetComponent<BattleRobot>();
        memory = GetComponent<RobotMemory>();
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer > 0f) return;
        updateTimer = updateInterval;

        cachedTarget = SelectBestTarget();
    }

    private BattleRobot SelectBestTarget()
    {
        List<BattleRobot> enemies = ArenaManager.Instance?.GetEnemiesOf(owner.TeamID);
        if (enemies == null || enemies.Count == 0) return null;

        BattleRobot bestTarget = null;
        float       bestScore  = float.MinValue;

        // Takımın ortak hedefi var mı?
        BattleRobot teamFocus = TeamCoordinator.Instance?.GetTeamFocusTarget(owner.TeamID);

        foreach (BattleRobot enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            float score = CalculateScore(enemy, teamFocus);

            if (score > bestScore)
            {
                bestScore  = score;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    private float CalculateScore(BattleRobot enemy, BattleRobot teamFocus)
    {
        float score = 0f;

        // 1. Mesafe puanı: yakın = yüksek puan (0-1 arası)
        float maxRange = 30f;
        float dist     = Vector3.Distance(transform.position, enemy.transform.position);
        float distScore = 1f - Mathf.Clamp01(dist / maxRange);
        score += distScore * weightDistance;

        // 2. Düşük HP puanı: zayıf düşman = yüksek puan
        float hpRatio  = enemy.MaxHP > 0 ? (float)enemy.CurrentHP / enemy.MaxHP : 1f;
        float hpScore  = 1f - hpRatio;  // HP az = puan yüksek
        score += hpScore * weightLowHP;

        // 3. Tehdit puanı: bize çok hasar veren = yüksek puan
        float threatScore = 0f;
        if (memory != null)
        {
            float dmgFrom = memory.GetDamageFrom(enemy);
            threatScore   = Mathf.Clamp01(dmgFrom / 200f); // 200 hasar = max tehdit
        }
        score += threatScore * weightThreat;

        // 4. Takım odağı bonusu: takım bu düşmana odaklanıyorsa bonus
        if (teamFocus != null && teamFocus == enemy)
            score += weightTeamFocus;

        return score;
    }

    /// <summary>Acil durum: belirli bir düşmana odaklan.</summary>
    public void ForceTarget(BattleRobot target)
    {
        cachedTarget = target;
        updateTimer  = updateInterval; // Bir sonraki hesaplamayı ertele
    }
}