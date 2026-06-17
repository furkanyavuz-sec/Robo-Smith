// RobotMemory.cs
// Görev: Robotun savaş boyunca edindiği bilgileri saklar.
// - Kimden ne kadar hasar aldı
// - Hangi düşman en tehlikeli
// - Son hasar alınan zaman
// BattleRobot ve TargetSelector bu veriyi okur.

using System.Collections.Generic;
using UnityEngine;

public class RobotMemory : MonoBehaviour
{
    [Header("Bellek Ayarları")]
    [SerializeField] private float memoryDuration = 10f;  // Kaç saniye hatırla // En fazla kaç düşman hatırla

    // Hasar kaydı: düşman → toplam hasar
    private Dictionary<BattleRobot, float> damageReceived = new();
    private Dictionary<BattleRobot, float> lastDamageTime = new();

    // Son hasar alınan zaman
    public float LastDamageTime  { get; private set; } = -999f;
    public float TotalDamageReceived { get; private set; } = 0f;

    private void Update()
    {
        CleanOldMemories();
    }

    /// <summary>Hasar alındığında BattleRobot çağırır.</summary>
    public void RecordDamage(BattleRobot attacker, int damage)
    {
        if (attacker == null) return;

        if (!damageReceived.ContainsKey(attacker))
            damageReceived[attacker] = 0f;

        damageReceived[attacker] += damage;
        lastDamageTime[attacker]  = Time.time;
        LastDamageTime            = Time.time;
        TotalDamageReceived      += damage;
    }

    /// <summary>En tehlikeli düşmanı döndürür (en fazla hasar veren).</summary>
    public BattleRobot GetMostDangerousEnemy()
    {
        BattleRobot mostDangerous = null;
        float       maxDamage     = 0f;

        foreach (var kvp in damageReceived)
        {
            if (kvp.Key == null || kvp.Key.IsDead) continue;
            if (kvp.Value > maxDamage)
            {
                maxDamage     = kvp.Value;
                mostDangerous = kvp.Key;
            }
        }

        return mostDangerous;
    }

    /// <summary>Belirli bir düşmandan ne kadar hasar alındı?</summary>
    public float GetDamageFrom(BattleRobot enemy)
    {
        return damageReceived.TryGetValue(enemy, out float dmg) ? dmg : 0f;
    }

    /// <summary>Son X saniyede hasar alındı mı?</summary>
    public bool WasRecentlyAttacked(float seconds = 3f)
    {
        return Time.time - LastDamageTime < seconds;
    }

    /// <summary>Süresi geçmiş bellekleri temizle.</summary>
    private void CleanOldMemories()
    {
        List<BattleRobot> toRemove = new();

        foreach (var kvp in lastDamageTime)
        {
            if (Time.time - kvp.Value > memoryDuration)
                toRemove.Add(kvp.Key);
        }

        foreach (var robot in toRemove)
        {
            damageReceived.Remove(robot);
            lastDamageTime.Remove(robot);
        }
    }

    public void Reset()
    {
        damageReceived.Clear();
        lastDamageTime.Clear();
        LastDamageTime       = -999f;
        TotalDamageReceived  = 0f;
    }
}