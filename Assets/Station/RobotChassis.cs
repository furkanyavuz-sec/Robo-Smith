// RobotChassis.cs — Upgrade + ArmorType entegrasyonu

using UnityEngine;

public class RobotChassis : BaseStation
{
    public enum ChassisState { Idle, Assembling }
    public enum InteractMode { AddToArmor, UpgradeWeapon, }

    

    [Header("Stat Döngüleri")]
    [SerializeField] private int platePerCycle  = 3;
    [SerializeField] private int plasmaPerCycle = 4;
    [SerializeField] private int chipPerCycle   = 3;

    [Header("Silah Yuvası")]
    [SerializeField] private int maxWeaponSlots = 3;

    [Header("Zırh Seçimi")]
    [SerializeField] private ArmorType selectedArmor = ArmorType.HeavyPlate;

    [Header("Durum Takibi")]
    [SerializeField] private int currentPlates  = 0;
    [SerializeField] private int currentPlasmas = 0;
    [SerializeField] private int currentChips   = 0;

    private RobotStatSheet statSheet = new RobotStatSheet();

    public RobotStatSheet StatSheet    => statSheet;
    public ArmorType      EquippedArmor => selectedArmor;
    public ChassisState   CurrentState { get; private set; } = ChassisState.Idle;

    private void Awake() => ResetChassis();

    // ── BaseStation Sözleşmesi ───────────────────────────────────────────

    public override bool CanInteract(PlayerInteraction player)
    {
        if (player.HeldObject == null) return false;
        if (!player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
            return false;

        // İşlenmiş hammadde — upgrade olarak kullanılabilir mi?
        if (item.Type.IsProcessed())
        {
            // Takılı silahlardan biri bu hammaddeyle upgrade olabiliyorsa kabul et
            return CanUpgradeAnyWeapon(item.Type) || CanInstallStatPart(item.Type);
        }

        // Silah item'ı — boş yuva var mı?
        if (item.Type.IsWeapon())
            return statSheet.weaponCount < maxWeaponSlots;

        // Modül — yuva boşsa kabul (robot başına 1 modül)
        if (item.Type.IsModule())
            return statSheet.equippedModule == ModuleType.None;

        return false;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item);
        GameObject obj = player.HeldObject;
        player.ForceDropFromStation();
        Destroy(obj);

        CurrentState = ChassisState.Assembling;

        if (item.Type.IsWeapon())
        {
            InstallWeapon(item.Type);
        }
        else if (item.Type.IsModule())
        {
            InstallModule(item.Type);
        }
        else if (item.Type.IsProcessed())
        {
            // Önce upgrade dene, olmuyorsa stat olarak ekle
            if (!TryUpgradeWeapon(item.Type))
                InstallStatPart(item.Type);
        }

        SynergySystem.Evaluate(statSheet);
        ChassisPreviewBuilder.Rebuild(this);   // Holografik taslağı güncelle
        Debug.Log($"[RobotChassis] → {statSheet}");
    }

    private void InstallModule(ItemType type)
    {
        statSheet.equippedModule = ModuleCatalog.FromItem(type);
        Debug.Log($"<color=cyan>🔧 {ModuleCatalog.TrName(statSheet.equippedModule)} " +
                  $"takıldı! ({ModuleCatalog.Description(statSheet.equippedModule)})</color>");
    }

// ── Bunları ekle ────────────────────────────────────────────────────────

public bool CanInteractArmor(PlayerInteraction player)
{
    if (player.HeldObject == null) return false;
    if (!player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
        return false;

    if (item.Type.IsWeapon())    return statSheet.weaponCount < maxWeaponSlots;
    if (item.Type.IsModule())    return statSheet.equippedModule == ModuleType.None;
    if (item.Type.IsProcessed()) return CanInstallStatPart(item.Type);
    return false;
}

public bool CanInteractUpgrade(PlayerInteraction player)
{
    if (player.HeldObject == null) return false;
    if (!player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
        return false;
    if (!item.Type.IsProcessed()) return false;
    return CanUpgradeAnyWeapon(item.Type);
}

public void InteractWithMode(PlayerInteraction player, InteractMode mode)
{
    if (player.HeldObject == null) return;
    if (!player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item)) return;

    GameObject obj = player.HeldObject;
    CurrentState = ChassisState.Assembling;

    switch (mode)
    {
        case InteractMode.AddToArmor:
            if (!CanInteractArmor(player)) return;
            player.ForceDropFromStation();
            Destroy(obj);

            if      (item.Type.IsWeapon()) InstallWeapon(item.Type);
            else if (item.Type.IsModule()) InstallModule(item.Type);
            else                           InstallStatPart(item.Type);
            break;

        case InteractMode.UpgradeWeapon:
            if (!CanInteractUpgrade(player)) return;
            player.ForceDropFromStation();
            Destroy(obj);
            TryUpgradeWeapon(item.Type);
            break;
    }

    SynergySystem.Evaluate(statSheet);
    ChassisPreviewBuilder.Rebuild(this);   // Holografik taslağı güncelle
    Debug.Log($"[RobotChassis] → {statSheet}");
}

    // ── Stat Parçaları ───────────────────────────────────────────────────

    private bool CanInstallStatPart(ItemType type) => type switch
    {
        ItemType.SteelPlate => currentPlates  < platePerCycle,
        ItemType.PlasmaCore => currentPlasmas < plasmaPerCycle,
        ItemType.Microchip  => currentChips   < chipPerCycle,
        _                   => false
    };

    private void InstallStatPart(ItemType type)
    {
        StatRoller.ApplyStat(type, statSheet);

        switch (type)
        {
            case ItemType.SteelPlate:
                currentPlates++;
                if (currentPlates >= platePerCycle) { currentPlates = 0;
                    Debug.Log("<color=cyan>Plaka döngüsü tamamlandı!</color>"); }
                break;
            case ItemType.PlasmaCore:
                currentPlasmas++;
                if (currentPlasmas >= plasmaPerCycle) { currentPlasmas = 0;
                    Debug.Log("<color=cyan>Plazma döngüsü tamamlandı!</color>"); }
                break;
            case ItemType.Microchip:
                currentChips++;
                if (currentChips >= chipPerCycle) { currentChips = 0;
                    Debug.Log("<color=cyan>Çip döngüsü tamamlandı!</color>"); }
                break;
        }
    }

    // ── Silah Kurulumu ───────────────────────────────────────────────────

    private void InstallWeapon(ItemType type)
    {
        WeaponData weapon = WeaponData.Create(type);
        if (weapon == null) return;

        statSheet.equippedWeapons[statSheet.weaponCount] = weapon;
        statSheet.weaponCount++;

        StatRoller.ApplyStat(type, statSheet);

        // Sonraki upgrade için ne gerektiğini haber ver
        UpgradeLevel next = WeaponUpgradeSystem.GetNextLevel(weapon);
        if (next != null)
            Debug.Log($"<color=orange>⚔️ {weapon.weaponName} takıldı! " +
                      $"Lv1'e çıkmak için {next.requiredAmount}x " +
                      $"{next.requiredMaterial} getir.</color>");
    }

    // ── Silah Upgrade ────────────────────────────────────────────────────

    private bool CanUpgradeAnyWeapon(ItemType material)
    {
        for (int i = 0; i < statSheet.weaponCount; i++)
        {
            WeaponData w = statSheet.equippedWeapons[i];
            if (w == null || w.IsMaxLevel) continue;

            UpgradeLevel next = WeaponUpgradeSystem.GetNextLevel(w);
            if (next != null && next.requiredMaterial == material)
                return true;
        }
        return false;
    }

    private bool TryUpgradeWeapon(ItemType material)
    {
        for (int i = 0; i < statSheet.weaponCount; i++)
        {
            WeaponData w = statSheet.equippedWeapons[i];
            if (w == null || w.IsMaxLevel) continue;

            if (WeaponUpgradeSystem.TryUpgrade(w, material))
            {
                // Upgrade sonrası bir sonraki seviyeyi logla
                UpgradeLevel next = WeaponUpgradeSystem.GetNextLevel(w);
                if (next != null)
                    Debug.Log($"Sonraki seviye için: {next.requiredAmount}x " +
                              $"{next.requiredMaterial}");
                else
                    Debug.Log($"<color=gold>{w.weaponName} MAX seviyeye ulaştı!</color>");

                return true;
            }
        }
        return false;
    }

    // ── Zırh Seçimi (UI'dan çağrılır — Hafta 7) ─────────────────────────

    public void SetArmor(ArmorType armor)
    {
        selectedArmor = armor;
        Debug.Log($"[RobotChassis] Zırh seçildi: {armor} | " +
                  $"{ArmorResistanceTable.GetDescription(armor)}");
    }

    public void ResetChassis()
    {
        statSheet.Reset();
        currentPlates = currentPlasmas = currentChips = 0;
        CurrentState  = ChassisState.Idle;
    }

    // ── MP Faz 2B: Client aynası ─────────────────────────────────────────

    /// <summary>ChassisSync server tarafında sayaçları örneklemek için okur.</summary>
    public (int plates, int plasmas, int chips) PartCounts =>
        (currentPlates, currentPlasmas, currentChips);

    /// <summary>
    /// MP client: server'dan gelen şasi durumunu yerel kopyaya uygular.
    /// RobotStatusUI, hologram, zırh paneli ve ipuçları bu kopyadan okur —
    /// hepsi değişiklik gerektirmeden doğru veriyle çalışır.
    /// </summary>
    public void ApplyNetworkMirror(ChassisSync.State s)
    {
        statSheet.HP  = s.hp;
        statSheet.ATK = s.atk;
        statSheet.SPD = s.spd;
        statSheet.DEF = s.def;

        statSheet.equippedModule = (ModuleType)s.module;
        statSheet.activeSynergy  = (SynergyBonus)s.synergy;
        selectedArmor            = (ArmorType)s.armor;

        currentPlates  = s.plates;
        currentPlasmas = s.plasmas;
        currentChips   = s.chips;

        // Silah yuvalarını yeniden kur (görüntü amaçlı — savaş verisi server'da)
        statSheet.equippedWeapons = new WeaponData[3];
        statSheet.weaponCount     = 0;

        MirrorWeapon(s.w0Type, s.w0Lvl, s.w0Prog);
        MirrorWeapon(s.w1Type, s.w1Lvl, s.w1Prog);
        MirrorWeapon(s.w2Type, s.w2Lvl, s.w2Prog);

        ChassisPreviewBuilder.Rebuild(this);   // Hologram client'ta da güncellensin
    }

    private void MirrorWeapon(int type, int level, int progress)
    {
        if (type < 0) return;

        WeaponData w = WeaponData.Create((ItemType)type);
        if (w == null) return;

        w.upgradeLevel    = level;
        w.upgradeProgress = progress;

        statSheet.equippedWeapons[statSheet.weaponCount] = w;
        statSheet.weaponCount++;
    }
}