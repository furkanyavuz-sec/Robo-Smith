// ArmorType.cs
// Görev: Zırh tiplerini ve hangi silaha karşı güçlü/zayıf
//        olduklarını tanımlar.
// Oyuncu RobotChassis'e SteelPlate getirdiğinde zırh tipini seçer.
// Seçim UI'dan gelir (Hafta 7) — şimdilik Inspector'dan.

using UnityEngine;
using System.Collections.Generic;

public enum ArmorType
{
    None,
    HeavyPlate,     // Melee'ye karşı güçlü  (+%50), Ranged'a normal
    ReactiveArmor,  // Roket'e karşı güçlü   (+%50), Melee'ye normal  
    EnergyShield,   // Lazer'e karşı güçlü   (+%50), AOE'ye normal
    EMPResistance,  // EMP'ye karşı güçlü    (+%50), Lazer'e normal
}

[System.Serializable]
public class ArmorResistanceTable
{
    // Her zırh tipi için direnç çarpanları
    // 1.0 = normal, 0.5 = yarı hasar al, 1.5 = fazla hasar al
    public static float GetResistance(ArmorType armor, WeaponCategory attackerWeapon)
    {
        return armor switch
        {
            ArmorType.HeavyPlate => attackerWeapon switch
            {
                WeaponCategory.Melee    => 0.50f,  // Güçlü: yarı hasar
                WeaponCategory.AOE      => 1.30f,  // Zayıf: %30 fazla hasar
                _                       => 1.00f   // Normal
            },

            ArmorType.ReactiveArmor => attackerWeapon switch
            {
                WeaponCategory.AOE      => 0.50f,  // Güçlü
                WeaponCategory.Melee    => 1.30f,  // Zayıf
                _                       => 1.00f
            },

            ArmorType.EnergyShield => attackerWeapon switch
            {
                WeaponCategory.Ranged   => 0.50f,  // Güçlü (Lazer dahil)
                WeaponCategory.Debuff   => 1.30f,  // Zayıf
                _                       => 1.00f
            },

            ArmorType.EMPResistance => attackerWeapon switch
            {
                WeaponCategory.Debuff   => 0.50f,  // Güçlü
                WeaponCategory.Ranged   => 1.30f,  // Zayıf
                _                       => 1.00f
            },

            _ => 1.00f  // ArmorType.None
        };
    }

    public static string GetDescription(ArmorType armor) => armor switch
    {
        ArmorType.HeavyPlate    => "Melee'ye güçlü | Rokete zayıf",
        ArmorType.ReactiveArmor => "Rokete güçlü  | Melee'ye zayıf",
        ArmorType.EnergyShield  => "Lazere güçlü  | EMP'ye zayıf",
        ArmorType.EMPResistance => "EMP'ye güçlü  | Lazere zayıf",
        _                       => "Zırh yok"
    };
}