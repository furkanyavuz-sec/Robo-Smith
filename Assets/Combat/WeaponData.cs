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
    public int        upgradeProgress  = 0;   // Sonraki seviye için teslim edilen malzeme

    public const int MAX_UPGRADE = 5;
    public bool   IsMaxLevel     => upgradeLevel >= MAX_UPGRADE;
    public string UpgradeStatus  => IsMaxLevel ? "MAX" : $"Lv{upgradeLevel}/{MAX_UPGRADE}";

    // WeaponData.cs içinde bu metodu bul ve değerleri değiştir:

public static WeaponData Create(ItemType type) => type switch
{
    ItemType.Sword  => new WeaponData { weaponName="Kilic",  sourceItem=type,
                       category=WeaponCategory.Melee,
                       damage=60,              // ← 80'den 60'a düşürdük (HP yüksek olduğu için)
                       attackCooldown=1.2f, 
                       effectiveRange=1.8f },

    ItemType.Laser  => new WeaponData { weaponName="Lazer",  sourceItem=type,
                       category=WeaponCategory.Ranged,
                       damage=35,              // ← 45'ten 35'e düşürdük
                       attackCooldown=0.8f, 
                       effectiveRange=12f },

    ItemType.Rocket => new WeaponData { weaponName="Roket",  sourceItem=type,
                       category=WeaponCategory.AOE,
                       damage=50,              // ← 60'tan 50'ye düşürdük
                       attackCooldown=3.0f, 
                       effectiveRange=10f,
                       aoeRadius=4f },

    ItemType.Shield => new WeaponData { weaponName="Kalkan", sourceItem=type,
                       category=WeaponCategory.Defensive,
                       damage=15,              // ← 20'den 15'e düşürdük
                       reflectRatio=0.30f },

    ItemType.EMP    => new WeaponData { weaponName="EMP",    sourceItem=type,
                       category=WeaponCategory.Debuff,
                       damage=10,              // ← 15'ten 10'a düşürdük
                       attackCooldown=5.0f, 
                       effectiveRange=6f,
                       debuffDuration=3f,   
                       debuffAmount=0.5f },

    _ => null
};
}