// RobotStatSheet.cs
// Görev: Parça eklenince kaç stat kazanılacağını hesaplar.
// Garaj ürünleri → random aralık (min-max arası %20 fark)
// Scrapyard ürünleri → sabit yüksek değer
// Sinerji bonusları → SynergySystem tarafından eklenir

using UnityEngine;

[System.Serializable]
public class RobotStatSheet
{
    // ── Temel Statlar ────────────────────────────────────────────────────
    public int HP      = 0;
    public int ATK     = 0;
    public int SPD     = 0;
    public int DEF     = 0;    // Kalkan parçasından gelir

    // ── Silah Yuvaları (max 3) ───────────────────────────────────────────
    public WeaponData[] equippedWeapons = new WeaponData[3];
    public int          weaponCount     = 0;

    // ── Modül Yuvası (1 adet) ────────────────────────────────────────────
    public ModuleType equippedModule = ModuleType.None;

    // ── Sinerji ─────────────────────────────────────────────────────────
    public SynergyBonus activeSynergy = SynergyBonus.None;

    public void Reset()
    {
        HP  = ATK = SPD = DEF = 0;
        weaponCount  = 0;
        activeSynergy = SynergyBonus.None;
        equippedWeapons = new WeaponData[3];
        equippedModule  = ModuleType.None;
    }

    public override string ToString() =>
        $"HP:{HP} ATK:{ATK} SPD:{SPD} DEF:{DEF} " +
        $"Silah:{weaponCount} Modül:{equippedModule} Sinerji:{activeSynergy}";
}

// ── Stat Hesaplama Fabrikası ─────────────────────────────────────────────
public static class StatRoller
{
    // Garaj ürünleri: min ile max arasında random
    // max = min * 1.20 → tam olarak %20 fark
    private const float VARIANCE = 0.20f;

    public static int RollGaragestat(int baseMin)
    {
        int baseMax = Mathf.RoundToInt(baseMin * (1f + VARIANCE));
        return Random.Range(baseMin, baseMax + 1);
    }

    // Scrapyard ürünleri: sabit yüksek değer, varyasyon yok
    public static int FixedScrapyardStat(int fixedValue) => fixedValue;

    /// <summary>
    /// ItemType'a göre doğru stat'ı hesaplar ve RobotStatSheet'e ekler.
    /// </summary>
    public static void ApplyStat(ItemType type, RobotStatSheet sheet)
    {
        switch (type)
        {
            // ── Garaj İşlenmiş Ürünleri (random) ─────────────────────────
            case ItemType.SteelPlate:
                sheet.HP  += RollGaragestat(30);   // 30-36 arası
                Debug.Log($"<color=yellow>Çelik Plaka → HP +{sheet.HP} " +
                          $"(random)</color>");
                break;

            case ItemType.PlasmaCore:
                sheet.ATK += RollGaragestat(30);
                Debug.Log($"<color=magenta>Plazma Çekirdeği → ATK +{sheet.ATK} " +
                          $"(random)</color>");
                break;

            case ItemType.Microchip:
                sheet.SPD += RollGaragestat(30);
                Debug.Log($"<color=cyan>Mikroçip → SPD +{sheet.SPD} " +
                          $"(random)</color>");
                break;

            // ── Scrapyard Silah Bileşenleri (sabit yüksek) ───────────────
            // Bunlar doğrudan stat vermez, WeaponData olarak eklenir
            // WeaponCraftStation halleder — burası sadece ham madde değil
            // işlenmiş silah item'ları için:
            case ItemType.Sword:
                sheet.ATK += FixedScrapyardStat(80);
                Debug.Log($"<color=red>Kılıç monte edildi → ATK +80</color>");
                break;

            case ItemType.Shield:
                sheet.DEF += FixedScrapyardStat(70);
                Debug.Log($"<color=blue>Kalkan monte edildi → DEF +70</color>");
                break;
        }
    }
}