// GameManager.cs
// Görev: Oyunun ana orkestratörü.
//   - Preparation (10 dk) ve Arena (2 dk) timerlarını yönetir
//   - RobotChassis'ten tamamlanan robotları MatchData'ya kaydeder
//   - Süre dolunca Arena sahnesine geçer
//   - Overtime: robotlar hâlâ hayattaysa hasar çarpanını artırır
// NGO notu: Timer server'da koşacak, ClientRpc ile sync edilecek.

using Unity.Netcode;
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

    [Header("Sahne İsimleri (Devam)")]
    [SerializeField] private string garageSceneName = "SampleScene";

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

    /// <summary>
    /// MP client: timer'ı yerel koşturmaz, NetworkGameState'ten okur.
    /// Host hem server hem oyuncu olduğundan normal yoldan yönetir.
    /// </summary>
    private bool IsNetworkClient =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening &&
        !NetworkManager.Singleton.IsServer;

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // FPS sınırını kaldır: VSync monitör tazelemesine (60Hz)
        // kilitliyordu — kare hızı serbest, takılma hissi azalır
        QualitySettings.vSyncCount   = 0;
        Application.targetFrameRate  = -1;

        // Arena sahnesine geçince de yaşamalı — timer'ı o sürdürecek
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == arenaSceneName)
        {
            BeginArenaPhase();
        }
        else if (scene.name == garageSceneName)
        {
            // Garaj sahnesi HER yüklendiğinde maç temiz başlar (rematch,
            // menüden yeni oyun, MP lobby'den geçiş). DontDestroyOnLoad'lu
            // eski kopyanın bayat timer'ı yeni maça sızmasın.
            playerChassis = FindObjectsByType<RobotChassis>();

            if (GameSettings.DifficultyChosen)
                difficulty = GameSettings.SelectedDifficulty;

            if (MatchData.Instance != null)
            {
                MatchData.Instance.Reset();
                MatchData.Instance.SelectedDifficulty = difficulty;
            }

            StartPreparation();
        }
        else
        {
            // Menü/lobby gibi oyun dışı sahne: maçı durdur — arka planda
            // sayıp kullanıcıyı menüden arenaya çekmesin
            matchStarted = false;
            currentPhase = GamePhase.Lobby;
        }
    }

    private void Start()
    {
        // Ana menüden zorluk seçildiyse Inspector değerini ezer
        if (GameSettings.DifficultyChosen)
            difficulty = GameSettings.SelectedDifficulty;

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

        // Client: faz geçişlerine karar VERMEZ — server verir, sahneyi NGO
        // taşır, düzeltilmiş timer ApplyNetworkState ile gelir. Yereldeki
        // azaltma yalnız iki senkron arasını yumuşatır.
        if (IsNetworkClient) return;

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
        // MP'de sahneyi NGO taşır — tüm client'lar birlikte geçer
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                arenaSceneName, LoadSceneMode.Single);
            return;
        }

        SceneManager.LoadScene(arenaSceneName);
    }

    /// <summary>
    /// MP client: NetworkGameState server'dan gelen faz + timer'ı uygular.
    /// Küçük sapmaları yut (4 Hz yazım + yerel azaltma arası titreme olmasın).
    /// </summary>
    public void ApplyNetworkState(GamePhase phase, float timer)
    {
        if (!IsNetworkClient) return;

        currentPhase = phase;
        matchStarted = phase == GamePhase.Preparation ||
                       phase == GamePhase.Arena ||
                       phase == GamePhase.Overtime;

        if (Mathf.Abs(phaseTimer - timer) > 0.35f)
            phaseTimer = timer;
    }

    /// <summary>
    /// Arena sahnesi yüklenince çağrılır: 2 dakikalık geri sayımı başlatır.
    /// Süre biterse Update() içinde Overtime'a geçilir.
    /// </summary>
    private void BeginArenaPhase()
    {
        currentPhase = GamePhase.Arena;
        phaseTimer   = arenaDuration;
        matchStarted = true;

        Debug.Log($"[GameManager] ⚔️ Arena fazı başladı! Süre: {arenaDuration}s");
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

        if (chassis.StatSheet.weaponCount == 0)
            Debug.LogWarning($"[GameManager] ⚠️ '{chassis.name}' silahsız arenaya " +
                             $"gidiyor — saldıramayacak! Silah takmayı unutma.");

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