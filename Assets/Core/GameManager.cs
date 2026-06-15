// GameManager.cs
// Görev: Oyunun ana orkestratörü.
//   - Preparation (10 dk) ve Arena (2 dk) timerlarını yönetir
//   - RobotChassis'ten tamamlanan robotları MatchData'ya kaydeder
//   - Süre dolunca Arena sahnesine geçer
//   - Overtime: robotlar hâlâ hayattaysa hasar çarpanını artırır
// NGO notu: Timer server'da koşacak, ClientRpc ile sync edilecek.

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ── Inspector Ayarları ───────────────────────────────────────────────
    [Header("Sahne İsimleri")]
    [SerializeField] private string arenaSceneName = "ArenaScene";

    [Header("Süre Ayarları (Saniye)")]
    [SerializeField] private float preparationDuration = 600f;  // 10 dakika
    [SerializeField] private float arenaDuration       = 120f;  // 2 dakika

    [Header("Overtime Ayarları")]
    [SerializeField] private float overtimeDamageIncrement  = 0.25f;  // Her aralıkta +%25
    [SerializeField] private float overtimeEscalationInterval = 30f;  // Her 30 sn artır

    [Header("Robot Referansları")]
    [SerializeField] private RobotChassis[] playerChassis;   // Inspector'dan bağla (max 3)

    [Header("Zorluk")]
    [SerializeField] private Difficulty difficulty = Difficulty.Normal;

    // ── Dahili Durum ─────────────────────────────────────────────────────
    private GamePhase currentPhase    = GamePhase.Lobby;
    private float     phaseTimer      = 0f;
    private float     overtimeTimer   = 0f;
    private bool      matchStarted    = false;

    // ── Public API ───────────────────────────────────────────────────────
    public GamePhase  CurrentPhase => currentPhase;
    public float      PhaseTimer   => phaseTimer;           // UI için
    public Difficulty Difficulty   => difficulty;

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // MatchData'yı hazırla
        if (MatchData.Instance != null)
        {
            MatchData.Instance.Reset();
            MatchData.Instance.SelectedDifficulty = difficulty;
        }

        StartPreparation();
    }

    private void Update()
    {
        if (!matchStarted) return;

        phaseTimer -= Time.deltaTime;

        switch (currentPhase)
        {
            case GamePhase.Preparation:
                if (phaseTimer <= 0f)
                    StartArenaTransition();
                break;

            case GamePhase.Arena:
                if (phaseTimer <= 0f)
                    StartOvertime();
                break;

            case GamePhase.Overtime:
                HandleOvertimeEscalation();
                break;
        }
    }

    // ── Faz Geçişleri ────────────────────────────────────────────────────

    private void StartPreparation()
    {
        currentPhase = GamePhase.Preparation;
        phaseTimer   = preparationDuration;
        matchStarted = true;

        Debug.Log("[GameManager] ⚙️ Hazırlık fazı başladı! Süre: 10 dakika");
    }

    private void StartArenaTransition()
    {
        currentPhase = GamePhase.Arena;
        matchStarted = false;   // Timer dursun, sahne geçişi başlasın

        // Tamamlanmış robotları MatchData'ya kaydet
        CollectPlayerRobots();

        Debug.Log("[GameManager] ⚔️ Süre doldu! Arena sahnesine geçiliyor...");

        // Küçük bir gecikmeyle sahneyi yükle (log'ların görünmesi için)
        Invoke(nameof(LoadArenaScene), 1.5f);
    }

    private void LoadArenaScene()
    {
        SceneManager.LoadScene(arenaSceneName);
    }

    private void StartOvertime()
    {
        currentPhase  = GamePhase.Overtime;
        overtimeTimer = 0f;

        if (MatchData.Instance != null)
            MatchData.Instance.OvertimeDamageMultiplier = 1f;

        Debug.Log("[GameManager] 🔥 OVERTIME! Hasar artmaya başlıyor!");
    }

    private void HandleOvertimeEscalation()
    {
        overtimeTimer += Time.deltaTime;

        if (overtimeTimer >= overtimeEscalationInterval)
        {
            overtimeTimer = 0f;

            if (MatchData.Instance != null)
            {
                MatchData.Instance.OvertimeDamageMultiplier += overtimeDamageIncrement;

                Debug.Log($"[GameManager] 📈 Overtime hasar çarpanı: " +
                          $"×{MatchData.Instance.OvertimeDamageMultiplier:F2}");
            }
        }
    }

    // ── Robot Toplama ────────────────────────────────────────────────────

    /// <summary>
    /// Preparation sonu: tamamlanmış şasilerdeki istatistikleri
    /// MatchData'ya kopyalar. Arena sahnesi buradan okur.
    /// </summary>
    private void CollectPlayerRobots()
{
    if (MatchData.Instance == null)
    {
        Debug.LogError("[GameManager] MatchData bulunamadı!");
        return;
    }

    foreach (RobotChassis chassis in playerChassis)
    {
        if (chassis == null) continue;

        if (chassis.StatSheet.HP == 0 && chassis.StatSheet.ATK == 0)
        {
            Debug.LogWarning($"[GameManager] '{chassis.name}' boş şasi, atlandı.");
            continue;
        }

        MatchData.Instance.AddPlayerRobot(
            chassis.StatSheet,
            chassis.EquippedArmor
        );
    }

    Debug.Log($"[GameManager] {MatchData.Instance.PlayerTeamSheets.Count} " +
              $"oyuncu robotu arenaya gönderildi.");
}

    // ── Dışarıdan Çağrılar ───────────────────────────────────────────────

    /// <summary>
    /// BattleRobot.cs (Hafta 5) maç bitişini buraya bildirir.
    /// </summary>
    public void OnMatchOver(bool playerWon)
    {
        currentPhase = GamePhase.GameOver;
        matchStarted = false;

        Debug.Log(playerWon
            ? "[GameManager] 🏆 Oyuncu kazandı!"
            : "[GameManager] 💀 Rakip kazandı!");

        // Hafta 6: UI sonuç ekranı buraya
    }

    /// <summary>Hazırlık süresinde kalan saniyeyi döndürür (UI için).</summary>
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(phaseTimer / 60f);
        int seconds = Mathf.FloorToInt(phaseTimer % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
}