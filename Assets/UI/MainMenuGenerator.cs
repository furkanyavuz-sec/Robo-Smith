// MainMenuGenerator.cs
// Görev: Ana menü UI'ını tek tıkla kurar (LobbyUIGenerator deseni).
// Kullanım: MainMenu sahnesindeki MainMenu_Generator objesini seç →
//           bileşen menüsü → "Generate Main Menu".
// Ekranlar: Ana menü / Zorluk seçimi / Nasıl Oynanır (+ lobby dönüş butonu)
// Tüm MainMenuUI referansları otomatik bağlanır.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuGenerator : MonoBehaviour
{
    // ── Fütüristik Palet ─────────────────────────────────────────────────
    private static readonly Color DeepBG    = new Color(0.030f, 0.045f, 0.075f, 1f);
    private static readonly Color NeonCyan  = new Color(0.25f, 0.85f, 1f);
    private static readonly Color NeonTeal  = new Color(0.20f, 0.90f, 0.65f);
    private static readonly Color NeonSlate = new Color(0.60f, 0.70f, 0.90f);
    private static readonly Color NeonRed   = new Color(1f, 0.35f, 0.35f);
    private static readonly Color NeonAmber = new Color(1f, 0.75f, 0.30f);
    private static readonly Color ButtonBG  = new Color(0.06f, 0.09f, 0.14f, 0.95f);

    [ContextMenu("Generate Main Menu")]
    public void Generate()
    {
        Clear();

        Canvas canvas = UIFactory.CreateCanvas("MainMenu_Canvas", transform, 20);

        // Sci-fi font runtime'da işaretli metinlere uygulanır
        canvas.gameObject.AddComponent<DisplayFontApplier>();

        // ── Ana Panel ────────────────────────────────────────────────────
        GameObject main = UIFactory.CreateStretchPanel("MainPanel",
            canvas.transform, DeepBG, blockClicks: true);

        BuildBackdrop(main.transform);

        // Başlık bloğu — sci-fi font + harf aralığı + nabızlı neon çizgi
        TextMeshProUGUI title = UIFactory.CreateText("Title", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 235), new Vector2(1100, 100),
            "ROBOSMITH", 74, FontStyles.Bold, UIFactory.TextMain);
        title.characterSpacing = 12f;
        title.gameObject.AddComponent<DisplayFontTag>();

        GameObject underline = UIFactory.CreatePanel("TitleLine", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 178), new Vector2(660, 3),
            NeonCyan);
        underline.AddComponent<AccentPulse>();

        TextMeshProUGUI subtitle = UIFactory.CreateText("Subtitle", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 148), new Vector2(900, 34),
            "ROBOT YAP  ·  SAVAŞTIR  ·  KAZAN", 17, FontStyles.Normal,
            new Color(0.50f, 0.72f, 0.90f));
        subtitle.characterSpacing = 6f;

        Button single = MenuButton(main.transform, "SinglePlayerButton",
            new Vector2(0, 52),   "TEK KİŞİLİK",   NeonCyan);
        Button multi  = MenuButton(main.transform, "MultiplayerButton",
            new Vector2(0, -22),  "MULTIPLAYER",   NeonTeal);
        Button howTo  = MenuButton(main.transform, "HowToButton",
            new Vector2(0, -96),  "NASIL OYNANIR", NeonSlate);
        Button quit   = MenuButton(main.transform, "QuitButton",
            new Vector2(0, -170), "ÇIKIŞ",         NeonRed);

        UIFactory.CreateText("Version", main.transform,
            new Vector2(1f, 0f), new Vector2(-24, 16), new Vector2(300, 26),
            "PROTOTİP  //  RoboSmith", 13, FontStyles.Normal,
            new Color(0.35f, 0.45f, 0.58f), TextAlignmentOptions.MidlineRight);

        // ── Zorluk Paneli ────────────────────────────────────────────────
        GameObject diff = UIFactory.CreateStretchPanel("DifficultyPanel",
            canvas.transform, DeepBG, blockClicks: true);

        BuildBackdrop(diff.transform);
        BuildPanelHeader(diff.transform, "ZORLUK SEÇ",
            "Rakip yapay zekânın hızını, robot sayısını ve gücünü belirler");

        Button easy = MenuButton(diff.transform, "EasyButton",
            new Vector2(0, 55), "KOLAY", NeonTeal,
            "2 zayıf rakip robot · çok yavaş üretim");

        Button normal = MenuButton(diff.transform, "NormalButton",
            new Vector2(0, -25), "NORMAL", NeonAmber,
            "3 rakip robot · dengeli tempo");

        Button hard = MenuButton(diff.transform, "HardButton",
            new Vector2(0, -105), "ZOR", NeonRed,
            "3 tam güçlü robot · 3 silah · modüllü");

        Button diffBack = MenuButton(diff.transform, "DiffBackButton",
            new Vector2(0, -200), "← GERİ", NeonSlate);

        // ── Nasıl Oynanır Paneli ─────────────────────────────────────────
        GameObject howToPanel = UIFactory.CreateStretchPanel("HowToPanel",
            canvas.transform, DeepBG, blockClicks: true);

        BuildBackdrop(howToPanel.transform);

        TextMeshProUGUI howToTitle = UIFactory.CreateText("HowToTitle",
            howToPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(800, 56),
            "NASIL OYNANIR", 38, FontStyles.Bold, UIFactory.TextMain);
        howToTitle.characterSpacing = 8f;
        howToTitle.gameObject.AddComponent<DisplayFontTag>();

        UIFactory.CreateText("GoalText", howToPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -105), new Vector2(1200, 34),
            "10 dakikada en güçlü robot takımını kur — süre bitince robotlar arenada otomatik savaşır",
            20, FontStyles.Italic, UIFactory.TextDim);

        UIFactory.CreateText("ControlsText", howToPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(1200, 32),
            "WASD  Hareket      E  Al / Kullan      Q  Silah Geliştir      Tab / F  Zırh Seç",
            19, FontStyles.Bold, UIFactory.TextMain);

        BuildRecipeChart(howToPanel.transform);

        Button howToBack = MenuButton(howToPanel.transform, "HowToBackButton",
            new Vector2(0, 42), "← GERİ", NeonSlate, null,
            new Vector2(0.5f, 0f), new Vector2(220, 52));

        // ── Lobby'den Dönüş Butonu (lobby açıkken görünür) ───────────────
        Button lobbyBack = MenuButton(canvas.transform, "LobbyBackButton",
            new Vector2(24, -24), "← MENÜ", NeonSlate, null,
            new Vector2(0f, 1f), new Vector2(180, 50));

        // ── MainMenuUI bağla ─────────────────────────────────────────────
        MainMenuUI ui = canvas.gameObject.AddComponent<MainMenuUI>();
        UIFactory.SetField(ui, "mainPanel",            main);
        UIFactory.SetField(ui, "difficultyPanel",      diff);
        UIFactory.SetField(ui, "howToPanel",           howToPanel);
        UIFactory.SetField(ui, "singlePlayerButton",   single);
        UIFactory.SetField(ui, "multiplayerButton",    multi);
        UIFactory.SetField(ui, "howToButton",          howTo);
        UIFactory.SetField(ui, "quitButton",           quit);
        UIFactory.SetField(ui, "easyButton",           easy);
        UIFactory.SetField(ui, "normalButton",         normal);
        UIFactory.SetField(ui, "hardButton",           hard);
        UIFactory.SetField(ui, "difficultyBackButton", diffBack);
        UIFactory.SetField(ui, "howToBackButton",      howToBack);
        UIFactory.SetField(ui, "lobbyBackButton",      lobbyBack);

        // Alt paneller başlangıçta kapalı (MainMenuUI.Start da garanti eder)
        diff.SetActive(false);
        howToPanel.SetActive(false);
        lobbyBack.gameObject.SetActive(false);

        Debug.Log("[MainMenuGenerator] ✅ Ana menü kuruldu. Sahneyi kaydet (Ctrl+S).");
    }

    // ── Fütüristik Yapı Taşları ──────────────────────────────────────────

    /// <summary>
    /// Sci-fi menü butonu: koyu gövde + sol neon şerit + display font etiket.
    /// subtitle verilirse etiketin altında küçük açıklama satırı olur.
    /// </summary>
    private Button MenuButton(Transform parent, string name, Vector2 pos,
        string label, Color accent, string subtitle = null,
        Vector2? anchorOverride = null, Vector2? sizeOverride = null)
    {
        Vector2 anchor = anchorOverride ?? new Vector2(0.5f, 0.5f);
        Vector2 size   = sizeOverride   ?? new Vector2(430, 64);

        GameObject bg = UIFactory.CreatePanel(name, parent, anchor, pos, size, Color.white);
        Image img = bg.GetComponent<Image>();
        img.raycastTarget = true;   // Tıklanabilir olmalı

        Button btn = bg.AddComponent<Button>();
        ColorBlock colors        = btn.colors;
        colors.normalColor       = ButtonBG;
        colors.highlightedColor  = new Color(0.10f, 0.16f, 0.24f, 0.98f);
        colors.pressedColor      = new Color(0.03f, 0.05f, 0.08f, 1f);
        colors.selectedColor     = colors.highlightedColor;
        btn.colors               = colors;

        // Sol neon şerit
        UIFactory.CreatePanel("Accent", bg.transform,
            new Vector2(0f, 0.5f), new Vector2(3, 0),
            new Vector2(5, size.y - 10), accent);

        // Etiket (sci-fi font)
        float labelY = subtitle == null ? 0f : 11f;
        TextMeshProUGUI text = UIFactory.CreateText("Label", bg.transform,
            new Vector2(0f, 0.5f), new Vector2(30, labelY),
            new Vector2(size.x - 44, 30),
            label, 20, FontStyles.Bold, Color.Lerp(accent, Color.white, 0.55f),
            TextAlignmentOptions.MidlineLeft);
        text.characterSpacing = 4f;
        text.gameObject.AddComponent<DisplayFontTag>();

        if (subtitle != null)
            UIFactory.CreateText("Sub", bg.transform,
                new Vector2(0f, 0.5f), new Vector2(30, -14),
                new Vector2(size.x - 44, 22),
                subtitle, 13, FontStyles.Normal, new Color(0.55f, 0.65f, 0.78f),
                TextAlignmentOptions.MidlineLeft);

        return btn;
    }

    /// <summary>İnce neon ızgara + köşe braketleri — sci-fi zemin dokusu.</summary>
    private void BuildBackdrop(Transform parent)
    {
        Color grid = new Color(0.30f, 0.80f, 1f, 0.045f);
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int i = -4; i <= 4; i++)
            UIFactory.CreatePanel($"GridV{i + 4}", parent, center,
                new Vector2(i * 200, 0), new Vector2(1, 1200), grid);

        for (int j = -2; j <= 2; j++)
            UIFactory.CreatePanel($"GridH{j + 2}", parent, center,
                new Vector2(0, j * 230), new Vector2(2000, 1), grid);

        CornerBracket(parent, 0, 0);
        CornerBracket(parent, 1, 0);
        CornerBracket(parent, 0, 1);
        CornerBracket(parent, 1, 1);
    }

    private void CornerBracket(Transform parent, int ax, int ay)
    {
        Color c = new Color(0.25f, 0.85f, 1f, 0.30f);
        Vector2 anchor = new Vector2(ax, ay);
        float dx = ax == 0 ? 26f : -26f;
        float dy = ay == 0 ? 26f : -26f;

        UIFactory.CreatePanel("BracketH", parent, anchor,
            new Vector2(dx, dy), new Vector2(110, 3), c);
        UIFactory.CreatePanel("BracketV", parent, anchor,
            new Vector2(dx, dy), new Vector2(3, 110), c);
    }

    /// <summary>Panel başlığı: sci-fi font + nabızlı neon çizgi + ipucu.</summary>
    private void BuildPanelHeader(Transform parent, string title, string hint)
    {
        TextMeshProUGUI t = UIFactory.CreateText("PanelTitle", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0, 215), new Vector2(900, 64),
            title, 42, FontStyles.Bold, UIFactory.TextMain);
        t.characterSpacing = 10f;
        t.gameObject.AddComponent<DisplayFontTag>();

        GameObject line = UIFactory.CreatePanel("PanelTitleLine", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0, 172), new Vector2(520, 3),
            NeonCyan);
        line.AddComponent<AccentPulse>();

        UIFactory.CreateText("PanelHint", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0, 138), new Vector2(1000, 32),
            hint, 17, FontStyles.Italic, new Color(0.50f, 0.62f, 0.76f));
    }

    // ── Üretim Rehberi Tablosu ───────────────────────────────────────────
    // Renkler StationVisuals.ItemColor'dan çekilir — oyundaki kutu/item/silah
    // renkleriyle her zaman aynı kalır. ■ karakteri renk çipi görevi görür.

    private void BuildRecipeChart(Transform parent)
    {
        // Sol kolon: Garaj zinciri
        BuildChartSection(parent, new Vector2(-620, 0), "GARAJ ZİNCİRİ",
            new Color(0.55f, 0.75f, 1f),
            $"{Chip(ItemType.Iron)} Demir  →  {Chip(ItemType.SteelPlate)} Çelik Plaka   <color=#73FF8C>HP+</color>\n" +
            $"{Chip(ItemType.RawPlasma)} Ham Plazma  →  {Chip(ItemType.PlasmaCore)} Plazma Çekirdeği   <color=#FF8C66>ATK+</color>\n" +
            $"{Chip(ItemType.Circuit)} Devre  →  {Chip(ItemType.Microchip)} Mikroçip   <color=#73D9FF>SPD+</color>",
            "İşleme Masası'nda dönüştür, şasiye tak (E)");

        // Orta kolon: Silahlar
        BuildChartSection(parent, new Vector2(0, 0), "SİLAHLAR",
            new Color(0.90f, 0.71f, 0.40f),
            $"{Chip(ItemType.ScrapMetal)} Hurda Metal  →  {Colored("Kılıç", ItemType.Sword)}\n" +
            $"{Chip(ItemType.CrystalShard)} Kristal Kıymık  →  {Colored("Lazer", ItemType.Laser)}\n" +
            $"{Chip(ItemType.RocketFuel)} Roket Yakıtı  →  {Colored("Roket", ItemType.Rocket)}\n" +
            $"{Chip(ItemType.ShieldAlloy)} Kalkan Alaşımı  →  {Colored("Kalkan", ItemType.Shield)}\n" +
            $"{Chip(ItemType.EMPCore)} EMP Çekirdeği  →  {Colored("EMP", ItemType.EMP)}",
            "Hurdalıktan topla, Atölye'de üret.\nİşlenmiş ürünle Q → Lv5'e kadar geliştir");

        // Sağ kolon: Modüller
        BuildChartSection(parent, new Vector2(620, 0), "MODÜLLER",
            new Color(0.69f, 0.52f, 0.96f),
            $"{Chip(ItemType.SteelPlate)} Plaka + {Chip(ItemType.Microchip)} Çip  →  {Colored("Onarım", ItemType.RepairModule)}\n" +
            $"<size=70%><color=#9AA3B3>arenada saniyede +3 HP</color></size>\n" +
            $"{Chip(ItemType.SteelPlate)} Plaka + {Chip(ItemType.PlasmaCore)} Çekirdek  →  {Colored("Aşırı Yükleme", ItemType.OverdriveModule)}\n" +
            $"<size=70%><color=#9AA3B3>HP %40 altında hasar +%40</color></size>\n" +
            $"{Chip(ItemType.PlasmaCore)} Çekirdek + {Chip(ItemType.Microchip)} Çip  →  {Colored("Hedefleme", ItemType.TargetingComputer)}\n" +
            $"<size=70%><color=#9AA3B3>bekleme -%20, menzil +%15</color></size>",
            "Montaj İstasyonu'nda 10 sn — robot başına 1 modül");

        // Alt ipucu — renk dilinin anahtarı
        UIFactory.CreateText("ColorHint", parent,
            new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(1300, 34),
            "Kutunun rengi = içeriğin rengi = silahın rengi — renkleri ezberleyen haritayı okur",
            19, FontStyles.Italic, UIFactory.TextDim);
    }

    private void BuildChartSection(Transform parent, Vector2 center,
        string header, Color headerColor, string rows, string note)
    {
        // Bölüm arka planı — hafif koyu kart
        GameObject card = UIFactory.CreatePanel($"Chart_{header}", parent,
            new Vector2(0.5f, 0.5f), center, new Vector2(580, 400),
            new Color(1f, 1f, 1f, 0.04f));

        TextMeshProUGUI headerTmp = UIFactory.CreateText("Header", card.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(540, 34),
            header, 21, FontStyles.Bold, headerColor);
        headerTmp.characterSpacing = 6f;
        headerTmp.gameObject.AddComponent<DisplayFontTag>();

        UIFactory.CreatePanel("HeaderLine", card.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(520, 2),
            UIFactory.Divider);

        TextMeshProUGUI body = UIFactory.CreateText("Rows", card.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(530, 250),
            rows, 18, FontStyles.Normal, UIFactory.TextMain,
            TextAlignmentOptions.TopLeft);
        body.lineSpacing = 14f;

        TextMeshProUGUI noteText = UIFactory.CreateText("Note", card.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(530, 56),
            note, 14, FontStyles.Italic, UIFactory.TextDim,
            TextAlignmentOptions.BottomLeft);
        noteText.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>Item renginde kare çip (■) — rich text.</summary>
    private static string Chip(ItemType type) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(StationVisuals.ItemColor(type))}>■</color>";

    /// <summary>Metni item'ın renginde boyar.</summary>
    private static string Colored(string text, ItemType type) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(StationVisuals.ItemColor(type))}>{text}</color>";

    [ContextMenu("Clear Main Menu")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}
