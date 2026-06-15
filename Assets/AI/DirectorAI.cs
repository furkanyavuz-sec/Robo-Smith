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

    [Header("Şasi Hedefleri (Rakip robot başına)")]
    [SerializeField] private int platesPerRobot  = 3;
    [SerializeField] private int plasmasPerRobot = 1;
    [SerializeField] private int maxRobots       = 3;

    [Header("İstatistik Başına Bonus (Oyuncuyla aynı)")]
    [SerializeField] private int hpPerPlateCycle    = 150;   // 3 plaka = +150 HP
    [SerializeField] private int atkPerPlasmaCycle  = 300;   // 4 plazma = +300 ATK

    // ── Dahili Durum ─────────────────────────────────────────────────────
    private float     plateTimer     = 0f;
    private float     plasmaTimer    = 0f;
    private float     difficultyMult = 1f;

    private int       platesBuilt    = 0;
    private int       plasmasBuilt   = 0;
    private int       robotsBuilt    = 0;

    private RobotStats currentRobotStats = new RobotStats();

    private bool isActive = false;

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    private void Start()
    {
        // Zorluk çarpanını belirle
        Difficulty diff = MatchData.Instance != null
                        ? MatchData.Instance.SelectedDifficulty
                        : Difficulty.Normal;

        difficultyMult = diff switch
        {
            Difficulty.Easy   => 2.0f,    // Yarı hız → interval 2× uzar
            Difficulty.Normal => 1.0f,
            Difficulty.Hard   => 0.67f,   // %150 hız → interval 0.67× kısalır
            _                 => 1.0f
        };

        // Timer'ları zorluğa göre ayarla
        plateTimer  = basePlateInterval  * difficultyMult;
        plasmaTimer = basePlasmaInterval * difficultyMult;

        isActive = true;

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

    /// <summary>
    /// Her iki döngü de en az bir kez tamamlandıysa robotu kaydet.
    /// Döngüler bağımsız çalışır — hangisi önce biterse bekler.
    /// </summary>
    private void TryCompleteRobot()
{
    if (currentRobotStats.maxHP == 0 || currentRobotStats.attackPower == 0)
        return;

    robotsBuilt++;

    // RobotStats → RobotStatSheet'e dönüştür
    RobotStatSheet sheet = new RobotStatSheet
    {
        HP  = currentRobotStats.maxHP,
        ATK = currentRobotStats.attackPower,
        SPD = currentRobotStats.moveSpeed
    };

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
}