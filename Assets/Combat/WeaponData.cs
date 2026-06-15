// WeaponData.cs

using UnityEngine;

[System.Serializable]
public class WeaponData
{
    public string         weaponName;
    public ItemType       sourceItem;
    public WeaponCategory category;

    public int        damage           = 0;
    public float      attackCooldown   = 1.5f;
    public float      effectiveRange   = 3f;
    public float      aoeRadius        = 0f;
    public float      debuffDuration   = 0f;
    public float      debuffAmount     = 0f;
    public float      reflectRatio     = 0f;
    public GameObject projectilePrefab = null;
    public int        upgradeLevel     = 0;

    public const int MAX_UPGRADE = 5;
    public bool   IsMaxLevel     => upgradeLevel >= MAX_UPGRADE;
    public string UpgradeStatus  => IsMaxLevel ? "MAX" : $"Lv{upgradeLevel}/{MAX_UPGRADE}";

    public static WeaponData Create(ItemType type) => type switch
    {
        ItemType.Sword  => new WeaponData { weaponName="Kılıç",  sourceItem=type,
                           category=WeaponCategory.Melee,
                           damage=80, attackCooldown=1.2f, effectiveRange=1.8f },
        ItemType.Laser  => new WeaponData { weaponName="Lazer",  sourceItem=type,
                           category=WeaponCategory.Ranged,
                           damage=45, attackCooldown=0.8f, effectiveRange=12f },
        ItemType.Rocket => new WeaponData { weaponName="Roket",  sourceItem=type,
                           category=WeaponCategory.AOE,
                           damage=60, attackCooldown=3.0f, effectiveRange=10f, aoeRadius=4f },
        ItemType.Shield => new WeaponData { weaponName="Kalkan", sourceItem=type,
                           category=WeaponCategory.Defensive,
                           damage=20, reflectRatio=0.30f },
        ItemType.EMP    => new WeaponData { weaponName="EMP",    sourceItem=type,
                           category=WeaponCategory.Debuff,
                           damage=15, attackCooldown=5.0f, effectiveRange=6f,
                           debuffDuration=3f, debuffAmount=0.5f },
        _               => null
    };
}