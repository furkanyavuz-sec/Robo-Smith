// ItemType.cs — Tam genişletilmiş versiyon

public enum ItemType
{
    // ── Garaj Ham Maddeleri (SupplyBin'den) ─────────────────────────────
    Iron,           // Demir    → SteelPlate (Zırh)
    RawPlasma,      // Plazma   → PlasmaCore (Silah ATK)
    Circuit,        // Devre    → Microchip  (Hız)

    // ── İşlenmiş Ürünler (Processor çıktısı) ────────────────────────────
    SteelPlate,     // Çelik Plaka  → Zırh: HP +random(30-50)
    PlasmaCore,     // Plazma Çekirdeği → ATK +random(30-50)
    Microchip,      // Mikroçip → SPD +random(30-50)

    // ── Orta Bölge Ham Maddeleri (ScrapyardStation'dan) ─────────────────
    ScrapMetal,     // Hurda Metal  → Kılıç
    CrystalShard,   // Kristal Kıymık → Lazer
    RocketFuel,     // Roket Yakıtı → Roket
    ShieldAlloy,    // Kalkan Alaşımı → Kalkan
    EMPCore,        // EMP Çekirdeği → EMP

    // ── Silahlar (WeaponCraftStation çıktısı — robota monte edilir) ─────
    Sword,          // Kılıç   — Melee, yüksek ATK
    Laser,          // Lazer   — Ranged, orta ATK + hızlı ateş
    Rocket,         // Roket   — AOE hasar
    Shield,         // Kalkan  — DEF + hasar yansıtma
    EMP,            // EMP     — Düşman robotunu yavaşlatır (Debuff)

    // ── Modüller (AssemblyStation çıktısı — 2 farklı işlenmiş üründen) ──
    RepairModule,       // Plaka + Çip     → arenada saniyede HP yeniler
    OverdriveModule,    // Plaka + Plazma  → düşük HP'de hasar artar
    TargetingComputer,  // Plazma + Çip    → bekleme süresi ↓, menzil ↑
}

// Item'ın hangi kategoriye girdiğini hızlıca sorgulamak için
public static class ItemTypeExtensions
{
    public static bool IsRawMaterial(this ItemType t) =>
        t == ItemType.Iron      ||
        t == ItemType.RawPlasma ||
        t == ItemType.Circuit;

    public static bool IsScrapyardMaterial(this ItemType t) =>
        t == ItemType.ScrapMetal   ||
        t == ItemType.CrystalShard ||
        t == ItemType.RocketFuel   ||
        t == ItemType.ShieldAlloy  ||
        t == ItemType.EMPCore;

    public static bool IsWeapon(this ItemType t) =>
        t == ItemType.Sword  ||
        t == ItemType.Laser  ||
        t == ItemType.Rocket ||
        t == ItemType.Shield ||
        t == ItemType.EMP;

    public static bool IsProcessed(this ItemType t) =>
        t == ItemType.SteelPlate ||
        t == ItemType.PlasmaCore ||
        t == ItemType.Microchip;

    public static bool IsModule(this ItemType t) =>
        t == ItemType.RepairModule     ||
        t == ItemType.OverdriveModule  ||
        t == ItemType.TargetingComputer;
}