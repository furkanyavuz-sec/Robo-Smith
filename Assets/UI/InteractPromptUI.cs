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

    private void Update()
{
    if (player == null) return;

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
        return;
    }

    RefreshPrompt(closest);
}

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
                ePrompt     = player.HeldObject != null ? "E: İşle" : "E: Al";
                break;

            case RobotChassis c:
                stationName = "Robot Şasisi";
                bgColor     = chassisColor;

                if (player.HeldObject != null &&
                    player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
                {
                    ePrompt = item.Type.IsWeapon()    ? "E: Silah Tak"  :
                              item.Type.IsProcessed() ? "E: Zırha Ekle" : "";
                    qPrompt = c.CanInteractUpgrade(player) ? "Q: Silahı Geliştir" : "";
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
                ePrompt     = player.HeldObject != null ? "E: Üret" : "E: Al";
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
}