// RobotModule.cs
// Görev: Modül sistemi — tarifler, arena etkileri ve isimler.
// Modüller Montaj İstasyonu'nda İKİ FARKLI işlenmiş üründen üretilir,
// şasiye takılır (robot başına 1 yuva), arenada pasif etki verir.
//   Plaka + Çip    → Onarım Modülü        (saniyede HP)
//   Plaka + Plazma → Aşırı Yükleme Modülü (düşük HP'de +hasar)
//   Plazma + Çip   → Hedefleme Bilgisayarı (bekleme ↓, menzil ↑)

public enum ModuleType
{
    None,
    Repair,     // Onarım Modülü
    Overdrive,  // Aşırı Yükleme
    Targeting,  // Hedefleme Bilgisayarı
}

public static class ModuleCatalog
{
    // ── Denge Sabitleri ──────────────────────────────────────────────────
    public const float RepairPerSecond       = 3f;     // Onarım: +3 HP/sn
    public const float OverdriveThreshold    = 0.40f;  // HP %40 altında...
    public const float OverdriveBonus        = 0.40f;  // ...hasar +%40
    public const float TargetingCooldownMult = 0.80f;  // Bekleme süresi -%20
    public const float TargetingRangeMult    = 1.15f;  // Menzil +%15

    // ── Tarifler ─────────────────────────────────────────────────────────

    /// <summary>
    /// İki işlenmiş ürün geçerli bir modül tarifi oluşturuyor mu?
    /// Sıra önemsiz. Geçersizse ModuleType.None döner.
    /// </summary>
    public static ModuleType GetRecipeResult(ItemType a, ItemType b)
    {
        if (a == b) return ModuleType.None;   // İki FARKLI ürün şart

        bool Has(ItemType x) => a == x || b == x;

        if (Has(ItemType.SteelPlate) && Has(ItemType.Microchip))
            return ModuleType.Repair;

        if (Has(ItemType.SteelPlate) && Has(ItemType.PlasmaCore))
            return ModuleType.Overdrive;

        if (Has(ItemType.PlasmaCore) && Has(ItemType.Microchip))
            return ModuleType.Targeting;

        return ModuleType.None;
    }

    // ── Dönüşümler ───────────────────────────────────────────────────────

    public static ModuleType FromItem(ItemType t) => t switch
    {
        ItemType.RepairModule      => ModuleType.Repair,
        ItemType.OverdriveModule   => ModuleType.Overdrive,
        ItemType.TargetingComputer => ModuleType.Targeting,
        _                          => ModuleType.None
    };

    public static ItemType ToItem(ModuleType m) => m switch
    {
        ModuleType.Repair    => ItemType.RepairModule,
        ModuleType.Overdrive => ItemType.OverdriveModule,
        ModuleType.Targeting => ItemType.TargetingComputer,
        _                    => ItemType.Iron   // None için anlamsız — çağırma
    };

    public static string TrName(ModuleType m) => m switch
    {
        ModuleType.Repair    => "Onarım Modülü",
        ModuleType.Overdrive => "Aşırı Yükleme",
        ModuleType.Targeting => "Hedefleme Bilgisayarı",
        _                    => "Yok"
    };

    public static string Description(ModuleType m) => m switch
    {
        ModuleType.Repair    => "Arenada saniyede +3 HP yeniler",
        ModuleType.Overdrive => "HP %40'ın altında hasar +%40",
        ModuleType.Targeting => "Bekleme süresi -%20, menzil +%15",
        _                    => ""
    };
}
