// WeaponSlot.cs
// Görev: Bir robottaki tek bir silah yuvasını tanımlar.
// BattleRobot birden fazla WeaponSlot barındırabilir.
// Mesafeye göre hangisinin kullanılacağına BattleRobot karar verir:
//   Ranged → uzakta kullan + kaçarak ateş et
//   Melee  → yakında kullan

using UnityEngine;

public enum WeaponType { Melee, Ranged }

[System.Serializable]
public class WeaponSlot
{
    [Header("Silah Kimliği")]
    public string     weaponName   = "Silah";
    public WeaponType weaponType   = WeaponType.Melee;

    [Header("İstatistikler")]
    public int   baseDamage        = 20;
    public float attackCooldown    = 1.5f;   // Saniye
    public float effectiveRange    = 3f;     // Bu silah için ideal mesafe

    [Header("Ranged — Mermi")]
    public GameObject projectilePrefab;      // Sadece Ranged için
    public Transform  firePoint;             // Mermi çıkış noktası

    [Header("Melee — Vuruş Alanı")]
    public float meleeRadius       = 1.2f;   // OverlapSphere yarıçapı

    // Runtime
    [HideInInspector] public float lastAttackTime = -999f;

    public bool IsReady => Time.time - lastAttackTime >= attackCooldown;

    /// <summary>Overtime çarpanı uygulanmış hasar.</summary>
    public int GetDamage()
    {
        float mult = MatchData.Instance != null
                   ? MatchData.Instance.OvertimeDamageMultiplier
                   : 1f;
        return Mathf.RoundToInt(baseDamage * mult);
    }
}