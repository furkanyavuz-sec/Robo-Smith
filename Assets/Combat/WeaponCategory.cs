// WeaponCategory.cs
// Görev: Tüm scriptlerin erişebildiği merkezi silah kategorisi tanımı.
// ArmorType.cs ve WeaponData.cs ikisi de buradan okur.

public enum WeaponCategory
{
    Melee,
    Ranged,
    AOE,
    Defensive,
    Debuff
}