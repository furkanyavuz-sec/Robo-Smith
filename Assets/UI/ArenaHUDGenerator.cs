// ArenaHUDGenerator.cs
// Görev: Arena HUD'ını tek tıkla, tutarlı görsel dille kurar.
// Kullanım: Sahnedeki HUD_Generator objesini seç → Inspector'da bu
//           bileşenin ⋮ menüsü → "Generate Arena HUD".
// Kurduğu paneller:
//   • Üst-orta:   Arena geri sayımı (ArenaTimer)
//   • Sol-üst:    Oyuncu takımı HP listesi (TeamStatusPanel, mavi)
//   • Sağ-üst:    Rakip takım HP listesi  (TeamStatusPanel, kırmızı)
//   • Overtime:   Uyarı bandı + kenar pulse (OvertimeUI)
//   • Maç sonu:   Sonuç ekranı + butonlar  (MatchResultUI)
// Tüm referanslar otomatik bağlanır, ArenaHUDManager kurulur.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArenaHUDGenerator : MonoBehaviour
{
    [ContextMenu("Generate Arena HUD")]
    public void Generate()
    {
        Clear();

        Canvas canvas = UIFactory.CreateCanvas("ArenaHUD_Canvas", transform, 5);

        ArenaTimer      timer      = BuildTimerPanel(canvas.transform);
        TeamStatusPanel playerTeam = BuildTeamPanel(canvas.transform, 0,
            "TAKIMIN", UIFactory.TeamBlue, UIFactory.HeaderBlue, false);
        TeamStatusPanel enemyTeam  = BuildTeamPanel(canvas.transform, 1,
            "RAKİP", UIFactory.TeamRed, UIFactory.HeaderRed, true);
        OvertimeUI      overtime   = BuildOvertimeUI(canvas);
        MatchResultUI   result     = BuildResultUI(canvas);

        ArenaHUDManager hud = canvas.gameObject.AddComponent<ArenaHUDManager>();
        UIFactory.SetField(hud, "arenaTimer",        timer);
        UIFactory.SetField(hud, "playerTeamPanel",   playerTeam);
        UIFactory.SetField(hud, "opponentTeamPanel", enemyTeam);
        UIFactory.SetField(hud, "overtimeUI",        overtime);
        UIFactory.SetField(hud, "matchResultUI",     result);

        WarnAboutDuplicates();
        Debug.Log("[ArenaHUDGenerator] ✅ Arena HUD'ı kuruldu. " +
                  "Eski HUD objelerini sildiğinden emin ol, sonra sahneyi kaydet.");
    }

    // ── Üst-orta: Arena Zamanlayıcısı ────────────────────────────────────
    private ArenaTimer BuildTimerPanel(Transform canvas)
    {
        GameObject panel = UIFactory.CreatePanel("ArenaTimerPanel", canvas,
            new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(300, 112),
            UIFactory.PanelBG);

        TextMeshProUGUI phase = UIFactory.CreateText("PhaseText", panel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(280, 30),
            "⚔️ ARENA", 20, FontStyles.Bold, new Color(1f, 0.6f, 0.3f));

        TextMeshProUGUI timerText = UIFactory.CreateText("TimerText", panel.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 4), new Vector2(280, 68),
            "02:00", 52, FontStyles.Bold, UIFactory.TextMain);

        ArenaTimer ui = panel.AddComponent<ArenaTimer>();
        UIFactory.SetField(ui, "timerText", timerText);
        UIFactory.SetField(ui, "phaseText", phase);
        return ui;
    }

    // ── Kenarlar: Takım Panelleri ────────────────────────────────────────
    private TeamStatusPanel BuildTeamPanel(Transform canvas, int teamID,
        string teamName, Color teamColor, Color headerColor, bool rightSide)
    {
        Vector2 anchor = rightSide ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        Vector2 pos    = rightSide ? new Vector2(-12, -12) : new Vector2(12, -12);

        GameObject panel = UIFactory.CreatePanel($"TeamPanel_{teamName}", canvas,
            anchor, pos, new Vector2(340, 236), UIFactory.PanelBG);

        // Başlık şeridi: takım adı solda, canlı robot sayısı sağda
        GameObject header = UIFactory.CreatePanel("Header", panel.transform,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(340, 40), headerColor);

        TextMeshProUGUI nameText = UIFactory.CreateText("TeamNameText",
            header.transform,
            new Vector2(0f, 0.5f), new Vector2(12, 0), new Vector2(220, 30),
            teamName, 19, FontStyles.Bold, UIFactory.TextMain,
            TextAlignmentOptions.MidlineLeft);

        TextMeshProUGUI countText = UIFactory.CreateText("RobotCountText",
            header.transform,
            new Vector2(1f, 0.5f), new Vector2(-12, 0), new Vector2(90, 30),
            "🤖 0/0", 17, FontStyles.Bold, UIFactory.TextMain,
            TextAlignmentOptions.MidlineRight);

        // Robot listesi (VerticalLayoutGroup)
        GameObject list = new GameObject("RobotList");
        list.transform.SetParent(panel.transform, false);
        RectTransform listRect    = list.AddComponent<RectTransform>();
        listRect.anchorMin        = new Vector2(0.5f, 1f);
        listRect.anchorMax        = new Vector2(0.5f, 1f);
        listRect.pivot            = new Vector2(0.5f, 1f);
        listRect.anchoredPosition = new Vector2(0, -46);
        listRect.sizeDelta        = new Vector2(324, 184);

        VerticalLayoutGroup layout   = list.AddComponent<VerticalLayoutGroup>();
        layout.spacing               = 6;
        layout.padding               = new RectOffset(4, 4, 4, 4);
        layout.childAlignment        = TextAnchor.UpperCenter;
        layout.childControlWidth     = true;
        layout.childControlHeight    = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Satır şablonu (pasif — TeamStatusPanel bundan klonlar)
        GameObject template = BuildHPEntryTemplate(panel.transform);

        TeamStatusPanel ui = panel.AddComponent<TeamStatusPanel>();
        UIFactory.SetField(ui, "teamID",           teamID);
        UIFactory.SetField(ui, "teamName",         teamName);
        UIFactory.SetField(ui, "teamColor",        teamColor);
        UIFactory.SetField(ui, "teamNameText",     nameText);
        UIFactory.SetField(ui, "robotCountText",   countText);
        UIFactory.SetField(ui, "robotListParent",  list.transform);
        UIFactory.SetField(ui, "robotEntryPrefab", template);
        return ui;
    }

    private GameObject BuildHPEntryTemplate(Transform parent)
    {
        GameObject entry = UIFactory.CreatePanel("HPEntry_Template", parent,
            new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(316, 50),
            new Color(1f, 1f, 1f, 0.06f));

        TextMeshProUGUI nameText = UIFactory.CreateText("NameText", entry.transform,
            new Vector2(0f, 1f), new Vector2(8, -3), new Vector2(180, 22),
            "Robot", 14, FontStyles.Bold, UIFactory.TextMain,
            TextAlignmentOptions.MidlineLeft);

        TextMeshProUGUI hpText = UIFactory.CreateText("HPText", entry.transform,
            new Vector2(1f, 1f), new Vector2(-8, -3), new Vector2(120, 22),
            "0/0", 13, FontStyles.Normal, UIFactory.TextDim,
            TextAlignmentOptions.MidlineRight);

        Image fill = UIFactory.CreateFillBar("HPBar", entry.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(300, 12),
            UIFactory.TeamBlue);

        RobotHPEntry entryScript = entry.AddComponent<RobotHPEntry>();
        UIFactory.SetField(entryScript, "robotNameText", nameText);
        UIFactory.SetField(entryScript, "hpText",        hpText);
        UIFactory.SetField(entryScript, "hpFill",        fill);

        entry.SetActive(false);   // Şablon görünmez — sadece klonlanır
        return entry;
    }

    // ── Overtime Uyarısı ─────────────────────────────────────────────────
    private OvertimeUI BuildOvertimeUI(Canvas canvas)
    {
        // Kenar pulse: tam ekran, başta şeffaf, script alfa'yı yönetir
        GameObject border = UIFactory.CreateStretchPanel("OvertimeBorder",
            canvas.transform, new Color(1f, 0f, 0f, 0f));

        // Uyarı bandı: zamanlayıcının hemen altında
        GameObject bandPanel = UIFactory.CreatePanel("OvertimePanel", canvas.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -132), new Vector2(430, 96),
            new Color(0.25f, 0.02f, 0.02f, 0.85f));

        TextMeshProUGUI overtimeText = UIFactory.CreateText("OvertimeText",
            bandPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(420, 52),
            "🔥 OVERTIME!", 38, FontStyles.Bold, new Color(1f, 0.25f, 0.2f));

        TextMeshProUGUI damageMult = UIFactory.CreateText("DamageMultText",
            bandPanel.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(420, 30),
            "💥 Hasar Çarpanı: ×1.00", 19, FontStyles.Normal, UIFactory.TextMain);

        bandPanel.SetActive(false);   // Overtime başlayınca script açar

        // Bileşen aktif bir objede olmalı ki Update çalışsın → canvas köküne
        OvertimeUI ui = canvas.gameObject.AddComponent<OvertimeUI>();
        UIFactory.SetField(ui, "overtimePanel",  bandPanel);
        UIFactory.SetField(ui, "overtimeText",   overtimeText);
        UIFactory.SetField(ui, "borderImage",    border.GetComponent<Image>());
        UIFactory.SetField(ui, "damageMultText", damageMult);
        return ui;
    }

    // ── Maç Sonu Ekranı ──────────────────────────────────────────────────
    private MatchResultUI BuildResultUI(Canvas canvas)
    {
        // Tam ekran kaplama — açıkken oyuna tıklamayı engeller
        GameObject panel = UIFactory.CreateStretchPanel("ResultPanel",
            canvas.transform, new Color(0f, 0f, 0f, 0.85f), blockClicks: true);

        TextMeshProUGUI title = UIFactory.CreateText("ResultTitle", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(900, 110),
            "KAZANDIN!", 68, FontStyles.Bold, UIFactory.TextMain);

        TextMeshProUGUI sub = UIFactory.CreateText("ResultSub", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(800, 40),
            "", 24, FontStyles.Normal, UIFactory.TextDim);

        TextMeshProUGUI stats = UIFactory.CreateText("ResultStats", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(700, 190),
            "", 20, FontStyles.Normal, UIFactory.TextMain);
        stats.textWrappingMode = TextWrappingModes.Normal;

        Button rematch = UIFactory.CreateButton("RematchButton", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(-130, -280), new Vector2(230, 58),
            "TEKRAR OYNA", new Color(0.2f, 0.65f, 0.3f));

        Button menu = UIFactory.CreateButton("MainMenuButton", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(130, -280), new Vector2(230, 58),
            "ANA MENÜ", new Color(0.35f, 0.38f, 0.45f));

        panel.SetActive(false);   // Maç bitince ShowResult açar

        // Bileşen aktif objede olmalı → canvas köküne
        MatchResultUI ui = canvas.gameObject.AddComponent<MatchResultUI>();
        UIFactory.SetField(ui, "resultPanel",      panel);
        UIFactory.SetField(ui, "resultTitleText",  title);
        UIFactory.SetField(ui, "resultSubText",    sub);
        UIFactory.SetField(ui, "statsText",        stats);
        UIFactory.SetField(ui, "resultBackground", panel.GetComponent<Image>());
        UIFactory.SetField(ui, "rematchButton",    rematch);
        UIFactory.SetField(ui, "mainMenuButton",   menu);
        return ui;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────

    private void WarnAboutDuplicates()
    {
        WarnIfOutside<ArenaTimer>();
        WarnIfOutside<TeamStatusPanel>();
        WarnIfOutside<OvertimeUI>();
        WarnIfOutside<MatchResultUI>();
        WarnIfOutside<ArenaHUDManager>();
    }

    private void WarnIfOutside<T>() where T : MonoBehaviour
    {
        foreach (T comp in FindObjectsByType<T>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!comp.transform.IsChildOf(transform))
                Debug.LogWarning($"[ArenaHUDGenerator] ⚠️ Eski {typeof(T).Name} bulundu: " +
                                 $"'{comp.gameObject.name}' — çakışmaması için sil!");
        }
    }

    [ContextMenu("Clear Arena HUD")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}
