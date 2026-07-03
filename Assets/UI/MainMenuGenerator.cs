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
    [ContextMenu("Generate Main Menu")]
    public void Generate()
    {
        Clear();

        Canvas canvas = UIFactory.CreateCanvas("MainMenu_Canvas", transform, 20);

        // ── Ana Panel ────────────────────────────────────────────────────
        GameObject main = UIFactory.CreateStretchPanel("MainPanel",
            canvas.transform, new Color(0.06f, 0.07f, 0.10f, 1f), blockClicks: true);

        UIFactory.CreateText("Title", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(900, 100),
            "ROBOSMITH", 72, FontStyles.Bold, UIFactory.TextMain);

        UIFactory.CreateText("Subtitle", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(700, 40),
            "Robot Yap. Savaştır. Kazan.", 22, FontStyles.Italic, UIFactory.TextDim);

        Button single = UIFactory.CreateButton("SinglePlayerButton", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(320, 60),
            "TEK KİŞİLİK", new Color(0.20f, 0.45f, 0.90f));

        Button multi = UIFactory.CreateButton("MultiplayerButton", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -15), new Vector2(320, 60),
            "MULTIPLAYER", new Color(0.20f, 0.65f, 0.35f));

        Button howTo = UIFactory.CreateButton("HowToButton", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(320, 60),
            "NASIL OYNANIR", new Color(0.35f, 0.38f, 0.48f));

        Button quit = UIFactory.CreateButton("QuitButton", main.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -165), new Vector2(320, 60),
            "ÇIKIŞ", new Color(0.55f, 0.20f, 0.20f));

        // ── Zorluk Paneli ────────────────────────────────────────────────
        GameObject diff = UIFactory.CreateStretchPanel("DifficultyPanel",
            canvas.transform, new Color(0.06f, 0.07f, 0.10f, 1f), blockClicks: true);

        UIFactory.CreateText("DiffTitle", diff.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(700, 70),
            "Zorluk Seç", 44, FontStyles.Bold, UIFactory.TextMain);

        UIFactory.CreateText("DiffHint", diff.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(800, 36),
            "Rakip AI'ın üretim hızını belirler", 18, FontStyles.Normal,
            UIFactory.TextDim);

        Button easy = UIFactory.CreateButton("EasyButton", diff.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(320, 58),
            "KOLAY — Rakip %50 hızda", new Color(0.20f, 0.60f, 0.30f));

        Button normal = UIFactory.CreateButton("NormalButton", diff.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(320, 58),
            "NORMAL — Eşit şartlar", new Color(0.85f, 0.55f, 0.10f));

        Button hard = UIFactory.CreateButton("HardButton", diff.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(320, 58),
            "ZOR — Rakip %150 hızda", new Color(0.75f, 0.20f, 0.20f));

        Button diffBack = UIFactory.CreateButton("DiffBackButton", diff.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(200, 48),
            "← GERİ", new Color(0.30f, 0.32f, 0.40f));

        // ── Nasıl Oynanır Paneli ─────────────────────────────────────────
        GameObject howToPanel = UIFactory.CreateStretchPanel("HowToPanel",
            canvas.transform, new Color(0.06f, 0.07f, 0.10f, 1f), blockClicks: true);

        UIFactory.CreateText("HowToTitle", howToPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(700, 60),
            "Nasıl Oynanır", 40, FontStyles.Bold, UIFactory.TextMain);

        TextMeshProUGUI guide = UIFactory.CreateText("GuideText", howToPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(900, 560),
            BuildGuideText(), 19, FontStyles.Normal, UIFactory.TextMain,
            TextAlignmentOptions.TopLeft);
        guide.textWrappingMode = TextWrappingModes.Normal;

        Button howToBack = UIFactory.CreateButton("HowToBackButton", howToPanel.transform,
            new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(200, 48),
            "← GERİ", new Color(0.30f, 0.32f, 0.40f));

        // ── Lobby'den Dönüş Butonu (lobby açıkken görünür) ───────────────
        Button lobbyBack = UIFactory.CreateButton("LobbyBackButton", canvas.transform,
            new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(140, 44),
            "← MENÜ", new Color(0.30f, 0.32f, 0.40f));

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

    private static string BuildGuideText() =>
        "<b>AMAÇ</b>\n" +
        "10 dakikalık hazırlıkta en güçlü robot takımını kur — " +
        "süre bitince robotlar arenada otomatik savaşır!\n\n" +
        "<b>KONTROLLER</b>\n" +
        "WASD — Hareket      E — Al / Kullan      Q — Silah Geliştir\n" +
        "Tab / F — Zırh Seçimi\n\n" +
        "<b>ÜRETİM ZİNCİRİ</b>\n" +
        "1)  Tedarik kutusundan ham madde al (Demir, Devre, Ham Plazma)\n" +
        "2)  İşleme Masası'nda işle → Çelik Plaka, Mikroçip, Plazma Çekirdeği\n" +
        "3)  Şasiye tak → HP / SPD / ATK kazanır\n" +
        "4)  Ortadaki hurdalıktan madde topla → Silah Atölyesi'nde silaha çevir\n" +
        "5)  İki FARKLI işlenmiş ürünü Montaj İstasyonu'nda birleştir → Modül\n" +
        "6)  İşlenmiş ürünleri takılı silaha taşı (Q) → seviye atlat (Lv5'e kadar)\n\n" +
        "<b>İPUÇLARI</b>\n" +
        "•  Kutunun rengi = verdiği maddenin rengi. Renkleri ezberle!\n" +
        "•  Zırh seçimi taş-kağıt-makas: Ağır Plaka melee'yi keser ama rokete zayıf.\n" +
        "•  Orta bölge rekabetçi — rakip AI da oradan besleniyor sayılır, hızlı ol.\n" +
        "•  3 şasi doldurabilirsin: 3'e 3 takım savaşı!";

    [ContextMenu("Clear Main Menu")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}
