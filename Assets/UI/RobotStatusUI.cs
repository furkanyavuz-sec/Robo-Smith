// RobotStatusUI.cs
// Görev: Şasideki mevcut parça durumunu gösterir.
// HP, ATK, SPD, DEF değerleri + takılı silahlar + aktif sinerji

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RobotStatusUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private RobotChassis chassis;

    [Header("Stat Metinleri")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI atkText;
    [SerializeField] private TextMeshProUGUI spdText;
    [SerializeField] private TextMeshProUGUI defText;

    [Header("Silah Slotları")]
    [SerializeField] private TextMeshProUGUI[] weaponSlotTexts;  // 3 slot

    [Header("Sinerji")]
    [SerializeField] private TextMeshProUGUI synergyText;
    [SerializeField] private Image           synergyIcon;

    [Header("Zırh")]
    [SerializeField] private TextMeshProUGUI armorText;

    [Header("Güncelleme Aralığı")]
    [SerializeField] private float updateInterval = 0.2f;

    [Header("Referanslar")]
    [SerializeField] private RobotChassis[] chassisList;  // Her iki şasiyi bağla

    private float updateTimer = 0f;

    private void Update()
{
    updateTimer -= Time.deltaTime;
    if (updateTimer > 0f) return;
    updateTimer = updateInterval;

    // En güçlü şasiyi bul (HP + ATK + SPD + DEF toplamı)
    RobotChassis strongest = null;
    int          maxPower  = -1;

    foreach (RobotChassis c in chassisList)
    {
        if (c == null) continue;
        RobotStatSheet s = c.StatSheet;
        int power = s.HP + s.ATK + s.SPD + s.DEF;

        if (power > maxPower)
        {
            maxPower  = power;
            strongest = c;
        }
    }

    if (strongest == null) return;
    RefreshUI(strongest.StatSheet, strongest.EquippedArmor);
}

    private void RefreshUI(RobotStatSheet sheet, ArmorType armor)
{
    if (sheet == null) return;

    if (hpText  != null) hpText.text  = $"HP:  {sheet.HP}";
    if (atkText != null) atkText.text = $"ATK: {sheet.ATK}";
    if (spdText != null) spdText.text = $"SPD: {sheet.SPD}";
    if (defText != null) defText.text = $"DEF: {sheet.DEF}";

    for (int i = 0; i < weaponSlotTexts.Length; i++)
    {
        if (weaponSlotTexts[i] == null) continue;

        if (i < sheet.weaponCount && sheet.equippedWeapons[i] != null)
        {
            WeaponData w = sheet.equippedWeapons[i];

            // Sonraki seviye için gereken malzemeyi göster —
            // bu bilgi daha önce sadece konsol logundaydı
            UpgradeLevel next = WeaponUpgradeSystem.GetNextLevel(w);
            string hint = next != null
                ? $"  →  {next.requiredAmount}x {MaterialShortName(next.requiredMaterial)}"
                : "";

            weaponSlotTexts[i].text  = $"{w.weaponName} [{w.UpgradeStatus}]{hint}";
            weaponSlotTexts[i].color = w.IsMaxLevel
                ? new Color(1f, 0.85f, 0.3f)   // MAX: altın
                : Color.white;
        }
        else if (i == 0 && sheet.weaponCount == 0)
        {
            // Hiç silah yok — arenada savaşamaz, oyuncuyu uyar
            weaponSlotTexts[i].text  = "⚠ Silah tak! (Silah Atölyesi)";
            weaponSlotTexts[i].color = new Color(1f, 0.4f, 0.3f);
        }
        else
        {
            weaponSlotTexts[i].text  = $"Bos Yuva {i + 1}";
            weaponSlotTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    if (armorText != null)
    {
        armorText.text  = armor == ArmorType.None
            ? "Zırh: Yok"
            : $"Zırh: {ArmorShortName(armor)}  ({ArmorResistanceTable.GetDescription(armor)})";
        armorText.color = armor == ArmorType.None ? Color.grey : Color.white;
    }

    if (synergyText != null)
    {
        synergyText.text  = sheet.activeSynergy == SynergyBonus.None
            ? "Sinerji: Yok"
            : $"Sinerji: {sheet.activeSynergy}";
        synergyText.color = sheet.activeSynergy == SynergyBonus.None
            ? Color.grey
            : Color.yellow;
    }
}

    // ── İsim Yardımcıları ────────────────────────────────────────────────

    private static string MaterialShortName(ItemType type) => type switch
    {
        ItemType.SteelPlate => "Plaka",
        ItemType.PlasmaCore => "Plazma",
        ItemType.Microchip  => "Çip",
        _                   => type.ToString()
    };

    private static string ArmorShortName(ArmorType armor) => armor switch
    {
        ArmorType.HeavyPlate    => "Ağır Plaka",
        ArmorType.ReactiveArmor => "Reaktif Zırh",
        ArmorType.EnergyShield  => "Enerji Kalkanı",
        ArmorType.EMPResistance => "EMP Direnci",
        _                       => "Yok"
    };
}