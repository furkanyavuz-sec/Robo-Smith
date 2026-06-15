// SynergySystem.cs
// Görev: RobotStatSheet'teki değerlere bakarak aktif sinerjiyi belirler
//        ve bonus statları uygular.
// Sinerji öncelik sırası: Efsanevi > Nadir > Normal

using UnityEngine;

public enum SynergyBonus
{
    None,
    HeavyWarrior,   // Zırh(HP) + Hız(SPD) yüksek → Ağır Savaşçı
    Aggressor,      // Silah(ATK) + Hız(SPD) yüksek → Baskıncı
    Champion,       // Zırh(HP) + Silah(ATK) yüksek → Kahraman
    Juggernaut,     // HP + ATK + DEF hepsi yüksek → Juggernaut (Efsanevi)
    Phantom,        // ATK + SPD + EMP silahı var → Hayalet (Nadir)
}

public static class SynergySystem
{
    // Sinerji eşik değerleri
    private const int HIGH_STAT    = 80;   // "Yüksek" sayılmak için minimum
    private const int VERY_HIGH    = 150;  // Efsanevi sinerji için

    /// <summary>
    /// Mevcut stat sheet'e göre en güçlü aktif sinerjiyi hesaplar
    /// ve bonus statları uygular. Her seferinde sıfırdan hesaplanır.
    /// </summary>
    public static void Evaluate(RobotStatSheet sheet)
    {
        // Önceki sinerji bonusunu temizle — sıfırdan hesapla
        RemovePreviousBonus(sheet);

        SynergyBonus best = DetermineBestSynergy(sheet);
        sheet.activeSynergy = best;

        if (best == SynergyBonus.None) return;

        ApplyBonus(best, sheet);

        Debug.Log($"<color=green>⚡ Sinerji Aktif: {best} → {sheet}</color>");
    }

    private static SynergyBonus DetermineBestSynergy(RobotStatSheet sheet)
    {
        bool highHP  = sheet.HP  >= HIGH_STAT;
        bool highATK = sheet.ATK >= HIGH_STAT;
        bool highSPD = sheet.SPD >= HIGH_STAT;
        bool highDEF = sheet.DEF >= HIGH_STAT;
        bool hasEMP  = HasWeapon(sheet, ItemType.EMP);

        bool veryHighHP  = sheet.HP  >= VERY_HIGH;
        bool veryHighATK = sheet.ATK >= VERY_HIGH;
        bool veryHighDEF = sheet.DEF >= VERY_HIGH;

        // ── Efsanevi (en yüksek öncelik) ────────────────────────────────
        if (veryHighHP && veryHighATK && veryHighDEF)
            return SynergyBonus.Juggernaut;

        if (highATK && highSPD && hasEMP)
            return SynergyBonus.Phantom;

        // ── Nadir ────────────────────────────────────────────────────────
        if (highHP && highATK && highDEF)
            return SynergyBonus.Champion;

        // ── Normal ───────────────────────────────────────────────────────
        if (highHP && highSPD)
            return SynergyBonus.HeavyWarrior;

        if (highATK && highSPD)
            return SynergyBonus.Aggressor;

        return SynergyBonus.None;
    }

    private static void ApplyBonus(SynergyBonus synergy, RobotStatSheet sheet)
    {
        switch (synergy)
        {
            case SynergyBonus.HeavyWarrior:
                sheet.HP  += 40;
                sheet.DEF += 20;
                Debug.Log("Ağır Savaşçı: HP+40, DEF+20");
                break;

            case SynergyBonus.Aggressor:
                sheet.ATK += 35;
                sheet.SPD += 25;
                Debug.Log("Baskıncı: ATK+35, SPD+25");
                break;

            case SynergyBonus.Champion:
                sheet.HP  += 30;
                sheet.ATK += 30;
                sheet.DEF += 30;
                Debug.Log("Kahraman: HP+30, ATK+30, DEF+30");
                break;

            case SynergyBonus.Juggernaut:
                sheet.HP  += 80;
                sheet.ATK += 60;
                sheet.DEF += 60;
                Debug.Log("⚔️ JUGGERNAUT: HP+80, ATK+60, DEF+60");
                break;

            case SynergyBonus.Phantom:
                sheet.ATK += 50;
                sheet.SPD += 50;
                Debug.Log("👻 Hayalet: ATK+50, SPD+50");
                break;
        }
    }

    // Önceki sinerji bonusunu geri al (yeniden hesaplama için)
    private static void RemovePreviousBonus(RobotStatSheet sheet)
    {
        switch (sheet.activeSynergy)
        {
            case SynergyBonus.HeavyWarrior:
                sheet.HP  -= 40; sheet.DEF -= 20; break;
            case SynergyBonus.Aggressor:
                sheet.ATK -= 35; sheet.SPD -= 25; break;
            case SynergyBonus.Champion:
                sheet.HP  -= 30; sheet.ATK -= 30; sheet.DEF -= 30; break;
            case SynergyBonus.Juggernaut:
                sheet.HP  -= 80; sheet.ATK -= 60; sheet.DEF -= 60; break;
            case SynergyBonus.Phantom:
                sheet.ATK -= 50; sheet.SPD -= 50; break;
        }
    }

    private static bool HasWeapon(RobotStatSheet sheet, ItemType type)
    {
        foreach (WeaponData w in sheet.equippedWeapons)
            if (w != null && w.sourceItem == type) return true;
        return false;
    }
}