using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InteractPromptUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private PlayerInteraction player;
    [SerializeField] private float             detectionRadius = 2.5f;
    [SerializeField] private LayerMask         stationLayer;

    [Header("UI Elemanları")]
    [SerializeField] private CanvasGroup       panelCanvasGroup;  // ← SetActive yerine bunu kullan
    [SerializeField] private TextMeshProUGUI   stationNameText;
    [SerializeField] private TextMeshProUGUI   ePromptText;
    [SerializeField] private TextMeshProUGUI   qPromptText;
    [SerializeField] private Image             panelBackground;

    [Header("İstasyon Renkleri")]
    [SerializeField] private Color supplyBinColor   = new Color(0.18f, 0.72f, 0.32f, 0.8f);
    [SerializeField] private Color trashBinColor    = new Color(0.3f,  0.3f,  0.3f,  0.8f);
    [SerializeField] private Color processorColor   = new Color(0.9f,  0.4f,  0.05f, 0.8f);
    [SerializeField] private Color chassisColor     = new Color(0.2f,  0.6f,  1f,    0.8f);
    [SerializeField] private Color scrapyardColor   = new Color(0.8f,  0.7f,  0.1f,  0.8f);
    [SerializeField] private Color weaponCraftColor = new Color(0.8f,  0.2f,  0.8f,  0.8f);
    [SerializeField] private Color assemblyColor    = new Color(0.55f, 0.25f, 0.95f, 0.8f);
    [SerializeField] private Color droneConsoleColor = new Color(0.10f, 0.65f, 0.75f, 0.8f);

    private void Start()
    {
        // Başlangıçta gizle — SetActive değil alpha
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha          = 0f;
            panelCanvasGroup.interactable   = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
    }

    private float playerSearchTimer;

    private void Update()
{
    // Oyuncu runtime'da spawn olur (OfflinePlayerSpawner / NGO) —
    // referans boşsa yarım saniyede bir aramayı dene
    if (player == null)
    {
        playerSearchTimer -= Time.deltaTime;
        if (playerSearchTimer <= 0f)
        {
            playerSearchTimer = 0.5f;

            // MP'de iki oyuncu kopyası var — ipuçları yerel oyuncuyu izlesin
            foreach (PlayerInteraction pi in
                     FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None))
                if (pi.IsLocalPlayer) { player = pi; break; }
        }
        if (player == null) return;
    }

    Collider[] hits = Physics.OverlapSphere(
        player.transform.position,
        detectionRadius,
        stationLayer
    );

    BaseStation closest     = null;
    float       closestDist = float.MaxValue;

    foreach (Collider col in hits)
    {
        if (!col.TryGetComponent<BaseStation>(out BaseStation s)) continue;
        float dist = Vector3.Distance(player.transform.position, col.transform.position);
        if (dist < closestDist) { closestDist = dist; closest = s; }
    }

    if (closest == null)
    {
        // Tüm metinleri temizle
        if (stationNameText != null) stationNameText.text = "";
        if (ePromptText     != null) ePromptText.text     = "";
        if (qPromptText     != null) qPromptText.text     = "";
        if (panelBackground != null) panelBackground.color = Color.clear;
        targetAlpha = 0f;
    }
    else
    {
        RefreshPrompt(closest);
        targetAlpha = 1f;
    }

    // Paneli yumuşakça göster/gizle — Start'taki alpha=0 burada geri açılır
    if (panelCanvasGroup != null)
        panelCanvasGroup.alpha = Mathf.MoveTowards(
            panelCanvasGroup.alpha, targetAlpha, Time.deltaTime * 8f);
}

    private float targetAlpha = 0f;

    private void RefreshPrompt(BaseStation station)
    {
        string stationName;
        Color  bgColor;
        string ePrompt = "";
        string qPrompt = "";

        switch (station)
        {
            case SupplyBin:
                stationName = "Tedarik Kutusu";
                bgColor     = supplyBinColor;
                ePrompt     = player.HeldObject == null ? "E: Al" : "";
                break;

            case TrashBin:
                stationName = "Çöp Kutusu";
                bgColor     = trashBinColor;
                ePrompt     = player.HeldObject != null ? "E: At" : "";
                break;

            case Processor:
                stationName = "İşleme Masası";
                bgColor     = processorColor;
                ePrompt     = TryGetNetStage(station, out int procStage)
                    ? StagePrompt(procStage, "E: İşle", "İşleniyor...")
                    : (player.HeldObject != null ? "E: İşle" : "E: Al");
                break;

            case RobotChassis c:
                stationName = "Robot Şasisi";
                bgColor     = chassisColor;

                if (player.HeldObject != null &&
                    player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
                {
                    ePrompt = item.Type.IsWeapon()    ? "E: Silah Tak"  :
                              item.Type.IsModule()    ? "E: Modül Tak"  :
                              item.Type.IsProcessed() ? "E: Zırha Ekle" : "";
                    qPrompt = c.CanInteractUpgrade(player)
                            ? ChassisInteractUI.UpgradePromptText(c, player)
                            : "";
                }
                break;

            case ScrapyardStation:
                stationName = "Hurdalık";
                bgColor     = scrapyardColor;
                ePrompt     = player.HeldObject == null ? "E: Topla" : "";
                break;

            case WeaponCraftStation:
                stationName = "Silah Atölyesi";
                bgColor     = weaponCraftColor;
                ePrompt     = TryGetNetStage(station, out int craftStage)
                    ? StagePrompt(craftStage, "E: Üret", "Üretiliyor...")
                    : (player.HeldObject != null ? "E: Üret" : "E: Al");
                break;

            case AssemblyStation assembly:
                stationName = "Montaj İstasyonu";
                bgColor     = assemblyColor;
                ePrompt     = TryGetNetStage(station, out int asmStage) &&
                              asmStage != StationProgressSync.STAGE_IDLE
                    ? StagePrompt(asmStage, "", "Montajlanıyor...")
                    : assembly.GetPromptText(player);
                break;

            case DroneConsole console:
                stationName = "Drone Konsolu";
                bgColor     = droneConsoleColor;
                ePrompt     = DroneConsolePrompt(console);
                break;

            default:
                stationName = station.name;
                bgColor     = Color.grey;
                ePrompt     = "E: Etkileşim";
                break;
        }

        if (stationNameText != null) stationNameText.text = stationName;

        if (ePromptText != null)
        {
            ePromptText.text      = ePrompt;
            ePromptText.gameObject.SetActive(!string.IsNullOrEmpty(ePrompt));
        }

        if (qPromptText != null)
        {
            qPromptText.text      = qPrompt;
            qPromptText.gameObject.SetActive(!string.IsNullOrEmpty(qPrompt));
        }

        if (panelBackground != null) panelBackground.color = bgColor;
    }

    /// <summary>
    /// MP'de süreli istasyonların gerçek aşaması client'ta bayat —
    /// StationProgressSync yayınından okunur. Offline'da false döner.
    /// </summary>
    private static bool TryGetNetStage(BaseStation station, out int stage)
    {
        stage = StationProgressSync.STAGE_IDLE;

        if (Unity.Netcode.NetworkManager.Singleton == null ||
            !Unity.Netcode.NetworkManager.Singleton.IsListening) return false;

        if (!station.TryGetComponent<StationProgressSync>(out StationProgressSync sync))
            return false;

        stage = sync.Stage;
        return true;
    }

    /// <summary>Aşamaya göre ipucu: boşta → verilen, çalışıyor → bekleme, hazır → al.</summary>
    private string StagePrompt(int stage, string idlePrompt, string workingText)
        => stage switch
        {
            StationProgressSync.STAGE_WORKING => workingText,
            StationProgressSync.STAGE_READY   =>
                player.HeldObject == null ? "E: Al" : "",
            _ => player.HeldObject != null ? idlePrompt : ""
        };

    /// <summary>
    /// Drone Konsolu ipucu: pencere kapalıysa geri sayım, açıksa sürüş çağrısı.
    /// </summary>
    private string DroneConsolePrompt(DroneConsole console)
    {
        DroneRaidZone zone = DroneRaidZone.Instance;
        if (zone == null) return "";

        if (zone.DroneUsable)
            return console.CanInteract(player)
                ? "E: Drone'u Sür"
                : "Drone görevde / elin dolu";

        float wait = zone.TimeToNextWindow;
        if (wait < 0f) return "Pencereler bitti";

        int m = Mathf.FloorToInt(wait / 60f);
        int s = Mathf.FloorToInt(wait % 60f);
        return $"Sıradaki pencere: {m:00}:{s:00}";
    }
}