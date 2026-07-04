// LobbyUIGenerator.cs — v2: Fütüristik lobby (ana menüyle aynı görsel dil)
// Koyu zemin + neon vurgular + Audiowide (DisplayFontTag) + kart düzeni.
// TMP_InputField'lar artık doğru viewport yapısıyla kurulur.
// Kullanım: obje → sağ tık → "Generate Lobby UI" → sahneyi kaydet.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUIGenerator : MonoBehaviour
{
    [Header("Renkler")]
    [SerializeField] private Color deepBG     = new Color(0.030f, 0.045f, 0.075f, 1f);
    [SerializeField] private Color cardBG     = new Color(1f, 1f, 1f, 0.045f);
    [SerializeField] private Color inputBG    = new Color(0.07f, 0.10f, 0.15f, 1f);
    [SerializeField] private Color hostColor  = new Color(0.20f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color joinColor  = new Color(0.25f, 0.55f, 0.95f, 1f);
    [SerializeField] private Color leaveColor = new Color(0.95f, 0.32f, 0.26f, 1f);
    [SerializeField] private Color startColor = new Color(0.95f, 0.75f, 0.15f, 1f);

    [ContextMenu("Generate Lobby UI")]
    public void GenerateLobbyUI()
    {
        ClearLobbyUI();

        Canvas canvas = UIFactory.CreateCanvas("LobbyCanvas", transform, 10);

        GameObject bg = UIFactory.CreateStretchPanel("Background",
            canvas.transform, deepBG, blockClicks: true);

        // ── Başlık ──────────────────────────────────────────────────────
        TextMeshProUGUI title = UIFactory.CreateText("Title", bg.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(900, 64),
            "MULTIPLAYER LOBİ", 38, FontStyles.Bold, UIFactory.TextMain);
        title.characterSpacing = 8f;
        title.gameObject.AddComponent<DisplayFontTag>();

        UIFactory.CreateText("Subtitle", bg.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -118), new Vector2(1100, 32),
            "Host Mavi takımdır, misafir Kırmızı — aynı ağdaki rakibinin IP'siyle bağlan",
            18, FontStyles.Italic, UIFactory.TextDim);

        // ── Orta kart ───────────────────────────────────────────────────
        GameObject card = UIFactory.CreatePanel("Card", bg.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(600, 620),
            cardBG);

        // Kartın sol neon şeridi
        UIFactory.CreatePanel("CardAccent", card.transform,
            new Vector2(0f, 0.5f), new Vector2(3, 0), new Vector2(5, 600),
            joinColor);

        // ── Bağlantı girişleri ──────────────────────────────────────────
        UIFactory.CreateText("IPLabel", card.transform,
            new Vector2(0.5f, 1f), new Vector2(-160, -60), new Vector2(180, 36),
            "SUNUCU IP", 17, FontStyles.Bold, UIFactory.TextDim);

        TMP_InputField ipInput = CreateInputField("IPInputField",
            card.transform, new Vector2(80, -60), new Vector2(260, 46),
            "127.0.0.1");

        UIFactory.CreateText("PortLabel", card.transform,
            new Vector2(0.5f, 1f), new Vector2(-160, -118), new Vector2(180, 36),
            "PORT", 17, FontStyles.Bold, UIFactory.TextDim);

        TMP_InputField portInput = CreateInputField("PortInputField",
            card.transform, new Vector2(80, -118), new Vector2(260, 46),
            "7777");

        UIFactory.CreatePanel("Divider1", card.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(540, 2),
            UIFactory.Divider);

        // ── Host / Bağlan ───────────────────────────────────────────────
        Button hostBtn = NeonButton(card.transform, "HostButton",
            new Vector2(-140, -215), new Vector2(250, 58), "HOST OL", hostColor);

        Button clientBtn = NeonButton(card.transform, "ClientButton",
            new Vector2(140, -215), new Vector2(250, 58), "BAĞLAN", joinColor);

        // ── Durum ───────────────────────────────────────────────────────
        TextMeshProUGUI statusText = UIFactory.CreateText("StatusText",
            card.transform, new Vector2(0.5f, 1f), new Vector2(0, -286),
            new Vector2(540, 56), "Bağlantı bekleniyor...", 17,
            FontStyles.Normal, new Color(0.85f, 0.85f, 0.55f));

        TextMeshProUGUI playerCountText = UIFactory.CreateText("PlayerCountText",
            card.transform, new Vector2(0.5f, 1f), new Vector2(0, -344),
            new Vector2(400, 36), "Oyuncu: 0/2", 20, FontStyles.Bold,
            UIFactory.TextMain);
        playerCountText.gameObject.AddComponent<DisplayFontTag>();

        UIFactory.CreatePanel("Divider2", card.transform,
            new Vector2(0.5f, 1f), new Vector2(0, -392), new Vector2(540, 2),
            UIFactory.Divider);

        // ── Oyunu Başlat (host'a özel, 2 oyuncu olunca belirir) ─────────
        Button startBtn = NeonButton(card.transform, "StartMatchButton",
            new Vector2(0, -462), new Vector2(420, 64), "OYUNU BAŞLAT", startColor);
        startBtn.gameObject.SetActive(false);

        // ── Ayrıl ───────────────────────────────────────────────────────
        Button disconnectBtn = NeonButton(card.transform, "DisconnectButton",
            new Vector2(0, -548), new Vector2(250, 48), "AYRIL", leaveColor);
        disconnectBtn.gameObject.SetActive(false);

        // ── LobbyUI bağla ───────────────────────────────────────────────
        LobbyUI lobbyUI = canvas.gameObject.AddComponent<LobbyUI>();
        UIFactory.SetField(lobbyUI, "lobbyPanel",       bg);
        UIFactory.SetField(lobbyUI, "ipInputField",     ipInput);
        UIFactory.SetField(lobbyUI, "portInputField",   portInput);
        UIFactory.SetField(lobbyUI, "hostButton",       hostBtn);
        UIFactory.SetField(lobbyUI, "clientButton",     clientBtn);
        UIFactory.SetField(lobbyUI, "disconnectButton", disconnectBtn);
        UIFactory.SetField(lobbyUI, "startMatchButton", startBtn);
        UIFactory.SetField(lobbyUI, "statusText",       statusText);
        UIFactory.SetField(lobbyUI, "playerCountText",  playerCountText);

        Debug.Log("[LobbyUIGenerator] ✅ Fütüristik lobby kuruldu. Sahneyi kaydet (Ctrl+S).");
    }

    // ── Yapı Taşları ─────────────────────────────────────────────────────

    /// <summary>Ana menüyle aynı dil: koyu gövde + sol neon şerit + display font.</summary>
    private Button NeonButton(Transform parent, string name, Vector2 pos,
        Vector2 size, string label, Color accent)
    {
        GameObject body = UIFactory.CreatePanel(name, parent,
            new Vector2(0.5f, 1f), pos, size,
            new Color(0.06f, 0.09f, 0.14f, 0.97f));

        Image img = body.GetComponent<Image>();
        img.raycastTarget = true;

        Button btn = body.AddComponent<Button>();
        ColorBlock colors       = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor    = colors.highlightedColor;
        btn.colors              = colors;

        UIFactory.CreatePanel("Accent", body.transform,
            new Vector2(0f, 0.5f), new Vector2(3, 0),
            new Vector2(5, size.y - 10), accent);

        TextMeshProUGUI text = UIFactory.CreateText("Label", body.transform,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 24, 34),
            label, 20, FontStyles.Bold, Color.Lerp(accent, Color.white, 0.55f));
        text.characterSpacing = 4f;
        text.gameObject.AddComponent<DisplayFontTag>();

        return btn;
    }

    /// <summary>TMP_InputField — doğru viewport (Text Area + RectMask2D) ile.</summary>
    private TMP_InputField CreateInputField(string name, Transform parent,
        Vector2 pos, Vector2 size, string defaultText)
    {
        GameObject root = UIFactory.CreatePanel(name, parent,
            new Vector2(0.5f, 1f), pos, size, inputBG);
        root.GetComponent<Image>().raycastTarget = true;

        TMP_InputField input = root.AddComponent<TMP_InputField>();

        // Text Area (viewport) — TMP input'un metni taşırmaması için şart
        GameObject area = new GameObject("Text Area");
        area.transform.SetParent(root.transform, false);
        RectTransform areaRect = area.AddComponent<RectTransform>();
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(12, 6);
        areaRect.offsetMax = new Vector2(-12, -6);
        area.AddComponent<RectMask2D>();

        // Placeholder
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(area.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        TextMeshProUGUI ph = phObj.AddComponent<TextMeshProUGUI>();
        ph.text      = defaultText;
        ph.fontSize  = 18;
        ph.fontStyle = FontStyles.Italic;
        ph.color     = new Color(0.45f, 0.50f, 0.58f);
        ph.alignment = TextAlignmentOptions.MidlineLeft;

        // Asıl metin
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(area.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize  = 18;
        text.color     = UIFactory.TextMain;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        input.textViewport  = areaRect;
        input.textComponent = text;
        input.placeholder   = ph;
        input.text          = defaultText;
        return input;
    }

    [ContextMenu("Clear Lobby UI")]
    public void ClearLobbyUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}
