// WeaponUpgradeSystem.cs
// Görev: Şasiye takılı bir silahı hammadde ile 5 seviyeye kadar geliştirir.
// Silah şasiye takılınca buraya referans kaydedilir.
// Oyuncu hammadde getirince RequiredMaterial kontrolü yapılır,
// eşleşirse UpgradeWeapon() çağrılır.

using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class UpgradeLevel
{
    public int   level;
    public int   damageBonus;      // Bu seviyede eklenen hasar
    public float cooldownReduction; // Bu seviyede azalan bekleme süresi
    public float rangeBonus;       // Bu seviyede eklenen menzil
    public ItemType requiredMaterial; // Bu seviyeye çıkmak için gereken hammadde
    public int   requiredAmount;   // Kaç adet gerekli
}

public static class WeaponUpgradeSystem
{
    // ── Upgrade Tablosu ──────────────────────────────────────────────────
    // Her silah tipi için 5 seviye tanımı
    private static readonly Dictionary<ItemType, List<UpgradeLevel>> UpgradeTable =
        new()
        {
            [ItemType.Sword] = new List<UpgradeLevel>
            {
                new() { level=1, damageBonus=20, cooldownReduction=0.1f, rangeBonus=0.1f,
                        requiredMaterial=ItemType.SteelPlate,  requiredAmount=1 },
                new() { level=2, damageBonus=30, cooldownReduction=0.1f, rangeBonus=0.1f,
                        requiredMaterial=ItemType.SteelPlate,  requiredAmount=2 },
                new() { level=3, damageBonus=40, cooldownReduction=0.2f, rangeBonus=0.2f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=1 },
                new() { level=4, damageBonus=55, cooldownReduction=0.2f, rangeBonus=0.2f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=2 },
                new() { level=5, damageBonus=80, cooldownReduction=0.3f, rangeBonus=0.3f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=1 },
            },

            [ItemType.Laser] = new List<UpgradeLevel>
            {
                new() { level=1, damageBonus=15, cooldownReduction=0.15f, rangeBonus=1f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=1 },
                new() { level=2, damageBonus=20, cooldownReduction=0.15f, rangeBonus=1f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=2 },
                new() { level=3, damageBonus=30, cooldownReduction=0.20f, rangeBonus=2f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=1 },
                new() { level=4, damageBonus=40, cooldownReduction=0.20f, rangeBonus=2f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=2 },
                new() { level=5, damageBonus=60, cooldownReduction=0.30f, rangeBonus=3f,
                        requiredMaterial=ItemType.SteelPlate,  requiredAmount=2 },
            },

            [ItemType.Rocket] = new List<UpgradeLevel>
            {
                new() { level=1, damageBonus=25, cooldownReduction=0.2f, rangeBonus=0.5f,
                        requiredMaterial=ItemType.SteelPlate,  requiredAmount=2 },
                new() { level=2, damageBonus=35, cooldownReduction=0.2f, rangeBonus=0.5f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=1 },
                new() { level=3, damageBonus=50, cooldownReduction=0.3f, rangeBonus=1f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=2 },
                new() { level=4, damageBonus=65, cooldownReduction=0.3f, rangeBonus=1f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=1 },
                new() { level=5, damageBonus=90, cooldownReduction=0.4f, rangeBonus=2f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=2 },
            },

            [ItemType.Shield] = new List<UpgradeLevel>
            {
                new() { level=1, damageBonus=5,  cooldownReduction=0f, rangeBonus=0f,
                        requiredMaterial=ItemType.SteelPlate,  requiredAmount=2 },
                new() { level=2, damageBonus=10, cooldownReduction=0f, rangeBonus=0f,
                        requiredMaterial=ItemType.SteelPlate,  requiredAmount=3 },
                new() { level=3, damageBonus=15, cooldownReduction=0f, rangeBonus=0f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=1 },
                new() { level=4, damageBonus=20, cooldownReduction=0f, rangeBonus=0f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=2 },
                new() { level=5, damageBonus=30, cooldownReduction=0f, rangeBonus=0f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=2 },
            },

            [ItemType.EMP] = new List<UpgradeLevel>
            {
                new() { level=1, damageBonus=5,  cooldownReduction=0.3f, rangeBonus=0.5f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=1 },
                new() { level=2, damageBonus=10, cooldownReduction=0.3f, rangeBonus=0.5f,
                        requiredMaterial=ItemType.Microchip,   requiredAmount=2 },
                new() { level=3, damageBonus=15, cooldownReduction=0.5f, rangeBonus=1f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=1 },
                new() { level=4, damageBonus=20, cooldownReduction=0.5f, rangeBonus=1f,
                        requiredMaterial=ItemType.PlasmaCore,  requiredAmount=2 },
                new() { level=5, damageBonus=30, cooldownReduction=0.7f, rangeBonus=2f,
                        requiredMaterial=ItemType.SteelPlate,  requiredAmount=2 },
            },
        };

    public const int MAX_LEVEL = 5;

    /// <summary>
    /// Bir sonraki seviye için gereken hammaddeyi döndürür.
    /// Null → zaten max seviyede.
    /// </summary>
    public static UpgradeLevel GetNextLevel(WeaponData weapon)
    {
        if (weapon == null) return null;
        if (!UpgradeTable.TryGetValue(weapon.sourceItem, out var levels)) return null;

        int nextIndex = weapon.upgradeLevel; // 0-based index
        if (nextIndex >= levels.Count) return null;

        return levels[nextIndex];
    }

    /// <summary>
    /// Silahı bir seviye yükseltir, statlarını günceller.
    /// </summary>
    public static bool TryUpgrade(WeaponData weapon, ItemType incomingMaterial)
    {
        UpgradeLevel next = GetNextLevel(weapon);
        if (next == null)
        {
            Debug.Log($"[Upgrade] {weapon.weaponName} zaten maksimum seviyede!");
            return false;
        }

        if (next.requiredMaterial != incomingMaterial)
        {
            Debug.Log($"[Upgrade] {weapon.weaponName} Seviye {weapon.upgradeLevel + 1} için " +
                      $"{next.requiredMaterial} gerekli, " +
                      $"{incomingMaterial} getirildi.");
            return false;
        }

        // Upgrade uygula
        weapon.damage         += next.damageBonus;
        weapon.attackCooldown  = Mathf.Max(0.2f,
                                 weapon.attackCooldown - next.cooldownReduction);
        weapon.effectiveRange += next.rangeBonus;
        weapon.upgradeLevel++;

        Debug.Log($"<color=orange>[Upgrade] ⬆️ {weapon.weaponName} " +
                  $"Seviye {weapon.upgradeLevel}! " +
                  $"ATK:{weapon.damage} | " +
                  $"CD:{weapon.attackCooldown:F1}s | " +
                  $"Menzil:{weapon.effectiveRange:F1}</color>");
        return true;
    }
}