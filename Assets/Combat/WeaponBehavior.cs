// WeaponBehavior.cs
// Görev: Her silah tipine özel akıllı davranış tanımlar.
// BattleRobot hangi silahı ne zaman kullanacağını buradan öğrenir.

using System.Collections.Generic;
using UnityEngine;

public class WeaponBehavior : MonoBehaviour
{
    [Header("EMP Ayarları")]
    // EMP önce vur, sonra Sword saldırsın

    [Header("Roket Ayarları")]
    [SerializeField] private float rocketMinClusterSize  = 2f;
    // Kaç düşman kümeleşince roket ateşle

    [Header("Kalkan Ayarları")]
    [SerializeField] private float shieldActivateHPRatio = 0.5f;
    // HP yüzde kaçta kalkan aktifleştir

    private BattleRobot      owner;
    private ShieldController shield;

    private void Awake()
    {
        owner  = GetComponent<BattleRobot>();
        shield = GetComponent<ShieldController>();
    }

    /// <summary>
    /// Mevcut duruma göre en uygun silahı seçer.
    /// BattleRobot.GetBestWeapon() yerine bu kullanılır.
    /// </summary>
    public WeaponData SelectWeapon(BattleRobot target, float distance,
                                   List<BattleRobot> nearbyEnemies)
    {
        if (owner.StatSheet == null) return null;

        WeaponData sword  = GetWeapon(WeaponCategory.Melee);
        WeaponData laser  = GetWeapon(WeaponCategory.Ranged);
        WeaponData rocket = GetWeapon(WeaponCategory.AOE);
        WeaponData emp    = GetWeapon(WeaponCategory.Debuff);
        WeaponData shield = GetWeapon(WeaponCategory.Defensive);

        // EMP Önceliği: hedef dondurulmamışsa ve menzildeyse önce EMP
        if (emp != null && distance <= emp.effectiveRange)
        {
            EMPEffect targetEMP = target?.GetComponent<EMPEffect>();
            if (targetEMP != null && !targetEMP.IsFrozen && IsWeaponReady(emp))
                return emp;
        }

        // Roket Önceliği: birden fazla düşman kümeleşmişse
        if (rocket != null && distance <= rocket.effectiveRange)
        {
            int clusterCount = CountEnemiesInRadius(
                target?.transform.position ?? transform.position,
                rocket.aoeRadius, nearbyEnemies);

            if (clusterCount >= rocketMinClusterSize && IsWeaponReady(rocket))
                return rocket;
        }

        // Melee: yakın mesafedeyse Sword
        if (sword != null && distance <= sword.effectiveRange && IsWeaponReady(sword))
            return sword;

        // Ranged: orta-uzak mesafede Laser
        if (laser != null && distance <= laser.effectiveRange && IsWeaponReady(laser))
            return laser;

        // Roket: tek hedef olsa bile menzildeyse kullan
        if (rocket != null && distance <= rocket.effectiveRange && IsWeaponReady(rocket))
            return rocket;

        // Menzil dışı — en uzun menzilli silah
        return GetLongestRangeWeapon();
    }

    /// <summary>Kalkan aktifleştirme zamanı mı?</summary>
    public bool ShouldActivateShield()
    {
        if (shield == null) return false;

        float hpRatio = owner.MaxHP > 0
            ? (float)owner.CurrentHP / owner.MaxHP : 1f;

        return hpRatio <= shieldActivateHPRatio && !shield.IsActive;
    }

    // ── Yardımcı Metodlar ────────────────────────────────────────────────

    private WeaponData GetWeapon(WeaponCategory cat)
{
    if (owner == null || owner.StatSheet == null) return null;
    foreach (WeaponData w in owner.StatSheet.equippedWeapons)
        if (w != null && w.category == cat) return w;
    return null;
}

    private WeaponData GetLongestRangeWeapon()
    {
        WeaponData longest  = null;
        float      maxRange = 0f;

        if (owner.StatSheet == null) return null;

        foreach (WeaponData w in owner.StatSheet.equippedWeapons)
        {
            if (w != null && w.effectiveRange > maxRange)
            {
                maxRange = w.effectiveRange;
                longest  = w;
            }
        }

        return longest;
    }

    private bool IsWeaponReady(WeaponData weapon)
{
    if (weapon == null) return false;
    
    // BattleRobot'taki lastAttackTimes dictionary'sinden oku
    return owner.IsWeaponReady(weapon);
}

    private int CountEnemiesInRadius(Vector3 center, float radius,
                                      List<BattleRobot> enemies)
    {
        int count = 0;
        foreach (BattleRobot e in enemies)
        {
            if (e == null || e.IsDead) continue;
            if (Vector3.Distance(center, e.transform.position) <= radius)
                count++;
        }
        return count;
    }
}