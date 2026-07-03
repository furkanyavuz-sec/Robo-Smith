// DirectorAI.cs
// Görev: Singleplayer'da rakip takımın otomatik üretimini simüle eder.
// Zorluk → MatchData'dan okunur:
//   Easy   → %50  hız (baseInterval × 2.0)
//   Normal → %100 hız (baseInterval × 1.0)
//   Hard   → %150 hız (baseInterval × 0.67)
// Her üretim döngüsünde sırayla SteelPlate → PlasmaCore üretir,
// tamamlanınca MatchData'ya rakip robot olarak kaydeder.

using UnityEngine;

public class DirectorAI : MonoBehaviour
{
    [Header("Üretim Ayarları (Normal Zorluk Baz)")]
    [SerializeField] private float basePlateInterval  = 8f;   // Normal'de 8sn/plaka
    [SerializeField] private float basePlasmaInterval = 12f;  // Normal'de 12sn/plazma
    [SerializeField] private float baseChipInterval   = 10f;  // Normal'de 10sn/çip

    [Header("Şasi Hedefleri (Rakip robot başına)")]
    [SerializeField] private int platesPerRobot  = 3;
    [SerializeField] private int plasmasPerRobot = 1;
    [SerializeField] private int chipsPerRobot   = 3;
    [SerializeField] private int maxRobots       = 3;

    [Header("İstatistik Başına Bonus (Oyuncuyla aynı)")]
    // Oyuncu karşılığı: döngü başına 3-4 parça × ~33 ortalama stat
    [SerializeField] private int hpPerPlateCycle    = 100;   // 3 plaka  ≈ +100 HP
    [SerializeField] private int atkPerPlasmaCycle  = 130;   // 4 plazma ≈ +130 ATK
    [SerializeField] private int spdPerChipCycle    = 100;   // 3 çip    ≈ +100 SPD

    // ── Zorluk Tablosu (KOD İÇİNDE — tek denge noktası) ──────────────────
    // Inspector'daki eski seri değerler dengeyi etkilemesin diye buradadır.
    // speedMult: üretim aralığı çarpanı (büyük = yavaş rakip)
    // statMult:  robot statlarına çarpan  weaponCount: silah sayısı
    private struct DifficultyTuning
    {
        public float speedMult;
        public int   maxRobots;
        public int   weaponCount;
        public float statMult;
        public bool  moduleOnLastRobot;
        public bool  moduleOnAll;
    }

    private static DifficultyTuning GetTuning(Difficulty d) => d switch
    {
        Difficulty.Easy => new DifficultyTuning
        {   // Öğrenme modu: 2 zayıf robot, çok yavaş üretim, modül yok
            speedMult = 2.6f, maxRobots = 2, weaponCount = 2, statMult = 0.70f
        },
        Difficulty.Hard => new DifficultyTuning
        {   // Eski "Normal" hız + tam stat + 3 silah + son robota modül
            speedMult = 1.0f, maxRobots = 3, weaponCount = 3, statMult = 1.0f,
            moduleOnLastRobot = true
        },
        _ => new DifficultyTuning
        {   // Normal: %50 yavaş üretim, %15 stat kesintisi, modül yok
            speedMult = 1.5f, maxRobots = 3, weaponCount = 2, statMult = 0.85f
        },
    };

    private DifficultyTuning tuning;

    // ── Dahili Durum ─────────────────────────────────────────────────────
    private float     plateTimer     = 0f;
    private float     plasmaTimer    = 0f;
    private float     chipTimer      = 0f;
    private float     difficultyMult = 1f;

    private int       platesBuilt    = 0;
    private int       plasmasBuilt   = 0;
    private int       chipsBuilt     = 0;
    private int       robotsBuilt    = 0;

    private RobotStats currentRobotStats = new RobotStats();

    private bool isActive = false;

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    private void Start()
    {
        // Zorluk ayarını kod tablosundan al
        Difficulty diff = MatchData.Instance != null
                        ? MatchData.Instance.SelectedDifficulty
                        : Difficulty.Normal;

        tuning         = GetTuning(diff);
        difficultyMult = tuning.speedMult;
        maxRobots      = tuning.maxRobots;   // Inspector değerini tablo yönetir

        // Timer'ları zorluğa göre ayarla
        plateTimer  = basePlateInterval  * difficultyMult;
        plasmaTimer = basePlasmaInterval * difficultyMult;
        chipTimer   = baseChipInterval   * difficultyMult;

        isActive = true;

        Debug.Log($"[DirectorAI] Zorluk: {diff} | Hız ×{1f / tuning.speedMult:F2} | " +
                  $"Robot: {tuning.maxRobots} | Silah: {tuning.weaponCount} | " +
                  $"Stat ×{tuning.statMult:F2}");

        //Debug.Log($"[DirectorAI] Başlatıldı. Zorluk: {diff} | " +
          //        $"Plaka aralığı: {basePlateInterval * difficultyMult:F1}s | " +
          //        $"Plazma aralığı: {basePlasmaInterval * difficultyMult:F1}s");
    }

    private void Update()
    {
        if (!isActive) return;
        if (GameManager.Instance?.CurrentPhase != GamePhase.Preparation) return;
        if (robotsBuilt >= maxRobots) return;

        plateTimer  -= Time.deltaTime;
        plasmaTimer -= Time.deltaTime;
        chipTimer   -= Time.deltaTime;

        if (plateTimer <= 0f)
        {
            ProducePlate();
            plateTimer = basePlateInterval * difficultyMult;
        }

        if (plasmaTimer <= 0f)
        {
            ProducePlasma();
            plasmaTimer = basePlasmaInterval * difficultyMult;
        }

        if (chipTimer <= 0f)
        {
            ProduceChip();
            chipTimer = baseChipInterval * difficultyMult;
        }
    }

    // ── Üretim Metodları ─────────────────────────────────────────────────

    private void ProducePlate()
    {
        platesBuilt++;
        //Debug.Log($"<color=yellow>[DirectorAI] Çelik Plaka üretildi " +
          //        $"({platesBuilt}/{platesPerRobot})</color>");

        if (platesBuilt >= platesPerRobot)
        {
            currentRobotStats.maxHP += hpPerPlateCycle;
            platesBuilt = 0;
            //Debug.Log($"<color=cyan>[DirectorAI] Plaka döngüsü tamamlandı! " +
              //        $"HP +{hpPerPlateCycle}</color>");

            TryCompleteRobot();
        }
    }

    private void ProducePlasma()
    {
        plasmasBuilt++;
        //Debug.Log($"<color=magenta>[DirectorAI] Plazma Çekirdeği üretildi " +
                 // $"({plasmasBuilt}/{plasmasPerRobot})</color>");

        if (plasmasBuilt >= plasmasPerRobot)
        {
            currentRobotStats.attackPower += atkPerPlasmaCycle;
            plasmasBuilt = 0;
            //Debug.Log($"<color=cyan>[DirectorAI] Plazma döngüsü tamamlandı! " +
              //        $"ATK +{atkPerPlasmaCycle}</color>");

            TryCompleteRobot();
        }
    }

    private void ProduceChip()
    {
        chipsBuilt++;

        if (chipsBuilt >= chipsPerRobot)
        {
            currentRobotStats.moveSpeed += spdPerChipCycle;
            chipsBuilt = 0;

            // Çip döngüsü robotu tamamlamaz — SPD bonus stat'tır,
            // HP+ATK hazır olduğunda robotla birlikte gider.
        }
    }

    /// <summary>
    /// HP ve ATK döngüleri en az bir kez tamamlandıysa robotu kaydet.
    /// Döngüler bağımsız çalışır — hangisi önce biterse bekler.
    /// Çip (SPD) döngüsü opsiyoneldir, hazır olan bonus robota eklenir.
    /// </summary>
    private void TryCompleteRobot()
{
    if (currentRobotStats.maxHP == 0 || currentRobotStats.attackPower == 0)
        return;

    robotsBuilt++;

    // RobotStats → RobotStatSheet'e dönüştür (zorluk stat çarpanıyla)
    RobotStatSheet sheet = new RobotStatSheet
    {
        HP  = Mathf.RoundToInt(currentRobotStats.maxHP       * tuning.statMult),
        ATK = Mathf.RoundToInt(currentRobotStats.attackPower * tuning.statMult),
        SPD = Mathf.RoundToInt(currentRobotStats.moveSpeed   * tuning.statMult)
    };

    // Silahsız robot arenada saldıramaz — oyuncuyla aynı kurallarla donat
    EquipWeapons(sheet);

    // Zorluğa bağlı modül: Easy hiç, Normal sadece son robot, Hard hepsi
    MaybeEquipModule(sheet);

    // Director AI rastgele zırh seçer
    ArmorType randomArmor = (ArmorType)Random.Range(1, 5);

    MatchData.Instance?.AddOpponentRobot(sheet, randomArmor);

    //Debug.Log($"<color=green>[DirectorAI] 🤖 Rakip Robot {robotsBuilt} " +
             // $"tamamlandı! Zırh: {randomArmor}</color>");

    currentRobotStats.Reset();

    if (robotsBuilt >= maxRobots)
    {
        isActive = false;
        //Debug.Log("[DirectorAI] Maksimum robot sayısına ulaşıldı.");
    }
}

    // ── Silah Donanımı ───────────────────────────────────────────────────

    private static readonly ItemType[] offensiveWeapons =
        { ItemType.Sword, ItemType.Laser, ItemType.Rocket };

    private static readonly ItemType[] utilityWeapons =
        { ItemType.Shield, ItemType.EMP, ItemType.Laser, ItemType.Rocket, ItemType.Sword };

    /// <summary>
    /// Rakip robota zorluğa göre 2-3 silah takar.
    /// İlk silah her zaman saldırı silahıdır (Shield+EMP kombosu savaşamaz).
    /// Oyuncudaki gibi silah montajı stat bonusu da verir (StatRoller).
    /// </summary>
    private void EquipWeapons(RobotStatSheet sheet)
    {
        int weaponTarget = tuning.weaponCount;

        // 1. silah: garantili saldırı silahı
        InstallWeapon(sheet, offensiveWeapons[Random.Range(0, offensiveWeapons.Length)]);

        // Kalanlar: takılı olmayan tiplerden rastgele
        int attempts = 0;
        while (sheet.weaponCount < weaponTarget && attempts++ < 20)
        {
            ItemType candidate = utilityWeapons[Random.Range(0, utilityWeapons.Length)];
            if (HasWeapon(sheet, candidate)) continue;
            InstallWeapon(sheet, candidate);
        }
    }

    private void InstallWeapon(RobotStatSheet sheet, ItemType type)
    {
        WeaponData weapon = WeaponData.Create(type);
        if (weapon == null || sheet.weaponCount >= sheet.equippedWeapons.Length) return;

        sheet.equippedWeapons[sheet.weaponCount] = weapon;
        sheet.weaponCount++;

        StatRoller.ApplyStat(type, sheet);   // Oyuncuyla aynı: Kılıç ATK+80, Kalkan DEF+70
    }

    private bool HasWeapon(RobotStatSheet sheet, ItemType type)
    {
        for (int i = 0; i < sheet.weaponCount; i++)
            if (sheet.equippedWeapons[i] != null &&
                sheet.equippedWeapons[i].sourceItem == type)
                return true;
        return false;
    }

    /// <summary>Zorluk tablosuna göre modül takar.</summary>
    private void MaybeEquipModule(RobotStatSheet sheet)
    {
        bool give = tuning.moduleOnAll ||
                    (tuning.moduleOnLastRobot && robotsBuilt >= maxRobots);
        if (!give) return;

        sheet.equippedModule = (ModuleType)Random.Range(1, 4); // Repair/Overdrive/Targeting

        //Debug.Log($"[DirectorAI] Rakip robota modül: {sheet.equippedModule}");
    }
}