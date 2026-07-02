// PrepHUDGenerator.cs
// Görev: Hazırlık fazı HUD'ını tek tıkla, tutarlı görsel dille kurar.
// Kullanım: Sahnedeki HUD_Generator objesini seç → Inspector'da bu
//           bileşenin ⋮ menüsü → "Generate Prep HUD".
// Kurduğu paneller:
//   • Üst-orta:  Faz + geri sayım (TimerUI)
//   • Sol-üst:   Robot durumu — stat/silah/zırh/sinerji (RobotStatusUI)
//   • Alt-orta:  İstasyon etkileşim ipucu (InteractPromptUI)
// Tüm script referansları otomatik bağlanır (LobbyUIGenerator deseni).

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrepHUDGenerator : MonoBehaviour
{
    [ContextMenu("Generate Prep HUD")]
    public void Generate()
    {
        Clear();

        Canvas canvas = UIFactory.CreateCanvas("PrepHUD_Canvas", transform, 5);

        GameObject timerPanel    = BuildTimerPanel(canvas.transform);
        GameObject statusPanel   = BuildRobotStatusPanel(canvas.transform);
        GameObject promptPanel   = BuildInteractPromptPanel(canvas.transform);

        // HUDManager: faza göre panelleri açar/kapatır
        HUDManager hud = canvas.gameObject.AddComponent<HUDManager>();
        UIFactory.SetField(hud, "timerPanel",          timerPanel);
        UIFactory.SetField(hud, "robotStatusPanel",    statusPanel);
        UIFactory.SetField(hud, "interactPromptPanel", promptPanel);

        WarnAboutDuplicates();
        Debug.Log("[PrepHUDGenerator] ✅ Hazırlık HUD'ı kuruldu. " +
                  "Eski HUD objelerini sildiğinden emin ol, sonra sahneyi kaydet.");
    }

    // ── Üst-orta: Zamanlayıcı ────────────────────────────────────────────
    private GameObject BuildTimerPanel(Transform canvas)
    {
        GameObject panel = UIFactory.CreatePanel("TimerPanel", canvas,
            new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(300, 112),
            UIFactory.PanelBG);

        TextMeshProUGUI phase = UIFactory.CreateText("PhaseText", panel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(280, 30),
            "HAZIRLIK", 20, FontStyles.Bold, new Color(0.55f, 0.75f, 1f));

        TextMeshProUGUI timer = UIFactory.CreateText("TimerText", panel.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 4), new Vector2(280, 68),
            "10:00", 52, FontStyles.Bold, UIFactory.TextMain);

        TimerUI ui = panel.AddComponent<TimerUI>();
        UIFactory.SetField(ui, "timerText", timer);
        UIFactory.SetField(ui, "phaseText", phase);
        return panel;
    }

    // ── Sol-üst: Robot Durumu ────────────────────────────────────────────
    private GameObject BuildRobotStatusPanel(Transform canvas)
    {
        GameObject panel = UIFactory.CreatePanel("RobotStatusPanel", canvas,
            new Vector2(0f, 1f), new Vector2(12, -12), new Vector2(390, 340),
            UIFactory.PanelBG);

        // Başlık şeridi
        GameObject header = UIFactory.CreatePanel("Header", panel.transform,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(390, 38),
            UIFactory.HeaderBlue);
        UIFactory.CreateText("HeaderText", header.transform,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 38),
            "ROBOT DURUMU", 19, FontStyles.Bold, UIFactory.TextMain);

        // Stat 2x2 ızgara — her stat kendi renginde
        TextMeshProUGUI hp  = StatText(panel, "HPText",  new Vector2(20,  -52), UIFactory.StatHP,  "HP:  0");
        TextMeshProUGUI atk = StatText(panel, "ATKText", new Vector2(205, -52), UIFactory.StatATK, "ATK: 0");
        TextMeshProUGUI spd = StatText(panel, "SPDText", new Vector2(20,  -86), UIFactory.StatSPD, "SPD: 0");
        TextMeshProUGUI def = StatText(panel, "DEFText", new Vector2(205, -86), UIFactory.StatDEF, "DEF: 0");

        UIFactory.CreatePanel("Divider1", panel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(358, 2),
            UIFactory.Divider);

        UIFactory.CreateText("WeaponsLabel", panel.transform,
            new Vector2(0f, 1f), new Vector2(20, -130), new Vector2(350, 22),
            "SİLAHLAR", 13, FontStyles.Bold, UIFactory.TextDim,
            TextAlignmentOptions.MidlineLeft);

        // 3 silah yuvası
        var slots = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            slots[i] = UIFactory.CreateText($"WeaponSlot{i + 1}", panel.transform,
                new Vector2(0f, 1f), new Vector2(20, -156 - i * 28), new Vector2(350, 26),
                $"Bos Yuva {i + 1}", 16, FontStyles.Normal, UIFactory.TextDim,
                TextAlignmentOptions.MidlineLeft);
        }

        UIFactory.CreatePanel("Divider2", panel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -246), new Vector2(358, 2),
            UIFactory.Divider);

        TextMeshProUGUI armor = UIFactory.CreateText("ArmorText", panel.transform,
            new Vector2(0f, 1f), new Vector2(20, -254), new Vector2(350, 26),
            "Zırh: Yok", 14, FontStyles.Normal, UIFactory.TextMain,
            TextAlignmentOptions.MidlineLeft);

        TextMeshProUGUI synergy = UIFactory.CreateText("SynergyText", panel.transform,
            new Vector2(0f, 1f), new Vector2(20, -284), new Vector2(350, 26),
            "Sinerji: Yok", 15, FontStyles.Bold, UIFactory.TextDim,
            TextAlignmentOptions.MidlineLeft);

        // Script bağlantıları
        RobotStatusUI ui = panel.AddComponent<RobotStatusUI>();
        UIFactory.SetField(ui, "hpText",          hp);
        UIFactory.SetField(ui, "atkText",         atk);
        UIFactory.SetField(ui, "spdText",         spd);
        UIFactory.SetField(ui, "defText",         def);
        UIFactory.SetField(ui, "weaponSlotTexts", slots);
        UIFactory.SetField(ui, "synergyText",     synergy);
        UIFactory.SetField(ui, "armorText",       armor);

        // Sahnedeki şasileri otomatik bul ve bağla
        RobotChassis[] chassis =
            FindObjectsByType<RobotChassis>(FindObjectsSortMode.None);
        UIFactory.SetField(ui, "chassisList", chassis);
        if (chassis.Length == 0)
            Debug.LogWarning("[PrepHUDGenerator] Sahnede RobotChassis bulunamadı — " +
                             "panel boş kalır. Şasi ekledikten sonra tekrar Generate et.");

        return panel;
    }

    private TextMeshProUGUI StatText(GameObject panel, string name,
        Vector2 pos, Color color, string text)
    {
        return UIFactory.CreateText(name, panel.transform,
            new Vector2(0f, 1f), pos, new Vector2(180, 30),
            text, 20, FontStyles.Bold, color, TextAlignmentOptions.MidlineLeft);
    }

    // ── Alt-orta: Etkileşim İpucu ────────────────────────────────────────
    private GameObject BuildInteractPromptPanel(Transform canvas)
    {
        // Diğer panellerle aynı dil: koyu gövde + renkli başlık şeridi.
        // Script, istasyon rengini yalnızca şerite boyar (panelBackground → şerit).
        GameObject panel = UIFactory.CreatePanel("InteractPromptPanel", canvas,
            new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(470, 118),
            UIFactory.PanelBG);

        GameObject header = UIFactory.CreatePanel("Header", panel.transform,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(470, 38),
            Color.clear);   // Rengi script istasyona göre boyar

        TextMeshProUGUI stationName = UIFactory.CreateText("StationNameText",
            header.transform,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 34),
            "", 21, FontStyles.Bold, UIFactory.TextMain);

        TextMeshProUGUI ePrompt = UIFactory.CreateText("EPromptText", panel.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 44), new Vector2(440, 28),
            "", 21, FontStyles.Bold, UIFactory.TextMain);

        TextMeshProUGUI qPrompt = UIFactory.CreateText("QPromptText", panel.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(440, 26),
            "", 18, FontStyles.Bold, new Color(1f, 0.85f, 0.4f));

        CanvasGroup group = panel.AddComponent<CanvasGroup>();

        InteractPromptUI ui = panel.AddComponent<InteractPromptUI>();
        UIFactory.SetField(ui, "panelCanvasGroup", group);
        UIFactory.SetField(ui, "stationNameText",  stationName);
        UIFactory.SetField(ui, "ePromptText",      ePrompt);
        UIFactory.SetField(ui, "qPromptText",      qPrompt);
        UIFactory.SetField(ui, "panelBackground",  header.GetComponent<Image>());

        // Station layer'ı sahnedeki bir istasyondan kopyala (elle ayar gerekmesin)
        BaseStation anyStation = FindFirstObjectByType<BaseStation>();
        int layerBits = anyStation != null ? 1 << anyStation.gameObject.layer : 0;
        UIFactory.SetField(ui, "stationLayer", (LayerMask)layerBits);
        if (anyStation == null)
            Debug.LogWarning("[PrepHUDGenerator] Sahnede istasyon yok — " +
                             "InteractPromptUI'nin Station Layer alanını elle ayarla.");

        return panel;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────

    private void WarnAboutDuplicates()
    {
        WarnIfOutside<TimerUI>();
        WarnIfOutside<RobotStatusUI>();
        WarnIfOutside<InteractPromptUI>();
        WarnIfOutside<HUDManager>();
    }

    private void WarnIfOutside<T>() where T : MonoBehaviour
    {
        foreach (T comp in FindObjectsByType<T>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!comp.transform.IsChildOf(transform))
                Debug.LogWarning($"[PrepHUDGenerator] ⚠️ Eski {typeof(T).Name} bulundu: " +
                                 $"'{comp.gameObject.name}' — çakışmaması için sil!");
        }
    }

    [ContextMenu("Clear Prep HUD")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}
