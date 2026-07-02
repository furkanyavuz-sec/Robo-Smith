// LobbyUIGenerator.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUIGenerator : MonoBehaviour
{
    [Header("Renkler")]
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
    [SerializeField] private Color hostColor       = new Color(0.2f,  0.7f,  0.3f,  1f);
    [SerializeField] private Color clientColor     = new Color(0.2f,  0.4f,  0.9f,  1f);
    [SerializeField] private Color disconnectColor = new Color(0.8f,  0.2f,  0.2f,  1f);
    [SerializeField] private Color startMatchColor = new Color(0.9f,  0.6f,  0.1f,  1f); // Başlat butonu için turuncu/altın renk
    [SerializeField] private Color inputBGColor    = new Color(0.15f, 0.15f, 0.2f,  1f);
    [SerializeField] private Color textColor       = Color.white;

    [ContextMenu("Generate Lobby UI")]
    public void GenerateLobbyUI()
    {
        ClearLobbyUI();

        // Ana Canvas
        Canvas canvas = CreateCanvas();

        // Arka plan
        GameObject bg = CreatePanel("Background", canvas.transform,
            Vector2.zero, Vector2.one, backgroundColor);

        // Başlık
        CreateText("TitleText", bg.transform,
            new Vector2(0, 150), new Vector2(600, 80),
            "ROBOSMITH", 48, FontStyles.Bold, textColor);

        CreateText("SubtitleText", bg.transform,
            new Vector2(0, 90), new Vector2(600, 40),
            "Multiplayer Lobi", 20, FontStyles.Normal,
            new Color(0.7f, 0.7f, 0.7f));

        // Ayraç çizgi
        CreatePanel("Divider", bg.transform,
            new Vector2(0, 60), new Vector2(400, 2),
            new Color(0.3f, 0.3f, 0.4f));

        // IP Input
        CreateText("IPLabel", bg.transform,
            new Vector2(-100, 20), new Vector2(180, 35),
            "Sunucu IP:", 16, FontStyles.Normal, textColor);

        TMP_InputField ipInput = CreateInputField("IPInputField", bg.transform,
            new Vector2(80, 20), new Vector2(200, 40), "127.0.0.1");

        // Port Input
        CreateText("PortLabel", bg.transform,
            new Vector2(-100, -30), new Vector2(180, 35),
            "Port:", 16, FontStyles.Normal, textColor);

        TMP_InputField portInput = CreateInputField("PortInputField", bg.transform,
            new Vector2(80, -30), new Vector2(200, 40), "7777");

        // Durum metni
        TextMeshProUGUI statusText = CreateText("StatusText", bg.transform,
            new Vector2(0, -80), new Vector2(500, 50),
            "Baglanti bekleniyor...", 16, FontStyles.Normal,
            new Color(0.8f, 0.8f, 0.5f));

        // Oyuncu sayısı
        TextMeshProUGUI playerCountText = CreateText("PlayerCountText", bg.transform,
            new Vector2(0, -120), new Vector2(300, 35),
            "Oyuncu: 0/6", 18, FontStyles.Bold, textColor);

        // Host Butonu
        Button hostBtn = CreateButton("HostButton", bg.transform,
            new Vector2(-110, -175), new Vector2(180, 50),
            "HOST OL", hostColor);

        // Client Butonu
        Button clientBtn = CreateButton("ClientButton", bg.transform,
            new Vector2(110, -175), new Vector2(180, 50),
            "BAGLAN", clientColor);

        // Oyunu Başlat Butonu (Sadece Host'a özel, başta kapalı doğacak)
        Button startMatchBtn = CreateButton("StartMatchButton", bg.transform,
            new Vector2(0, -240), new Vector2(200, 45),
            "OYUNU BAŞLAT", startMatchColor);
        startMatchBtn.gameObject.SetActive(false);

        // Disconnect Butonu (başta kapalı, koordinatını bir tık aşağı kaydırdım çakışmasınlar diye)
        Button disconnectBtn = CreateButton("DisconnectButton", bg.transform,
            new Vector2(0, -300), new Vector2(200, 45),
            "AYRIL", disconnectColor);
        disconnectBtn.gameObject.SetActive(false);

        // LobbyUI scriptini canvas'a ekle ve referansları bağla
        LobbyUI lobbyUI = canvas.gameObject.AddComponent<LobbyUI>();
        SetLobbyUIReferences(lobbyUI, bg, ipInput, portInput,
                             hostBtn, clientBtn, disconnectBtn, startMatchBtn,
                             statusText, playerCountText);

        Debug.Log("[LobbyUIGenerator] Lobby UI (Oyunu Başlat Dahil) Başarıyla Oluşturuldu!");
    }

    // ── Yardımcı Oluşturucular ───────────────────────────────────────────

    private Canvas CreateCanvas()
    {
        GameObject canvasObj = new GameObject("LobbyCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        return canvas;
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();

        if (sizeDelta == Vector2.zero)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta        = sizeDelta;
        }

        Image img  = obj.AddComponent<Image>();
        img.color  = color;
        return obj;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, string text, int fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect   = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text            = text;
        tmp.fontSize        = fontSize;
        tmp.fontStyle       = style;
        tmp.color           = color;
        tmp.alignment       = TextAlignmentOptions.Center;
        return tmp;
    }

    private TMP_InputField CreateInputField(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, string placeholder)
    {
        GameObject bg = new GameObject(name);
        bg.transform.SetParent(parent, false);
        RectTransform bgRect   = bg.AddComponent<RectTransform>();
        bgRect.anchoredPosition = anchoredPos;
        bgRect.sizeDelta        = sizeDelta;

        Image bgImg  = bg.AddComponent<Image>();
        bgImg.color  = inputBGColor;

        TMP_InputField input = bg.AddComponent<TMP_InputField>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(bg.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin     = Vector2.zero;
        textRect.anchorMax     = Vector2.one;
        textRect.offsetMin     = new Vector2(10, 5);
        textRect.offsetMax     = new Vector2(-10, -5);

        TextMeshProUGUI textTMP = textObj.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize        = 16;
        textTMP.color           = textColor;
        textTMP.alignment       = TextAlignmentOptions.MidlineLeft;

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(bg.transform, false);
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin     = Vector2.zero;
        phRect.anchorMax     = Vector2.one;
        phRect.offsetMin     = new Vector2(10, 5);
        phRect.offsetMax     = new Vector2(-10, -5);

        TextMeshProUGUI phTMP = phObj.AddComponent<TextMeshProUGUI>();
        phTMP.text            = placeholder;
        phTMP.fontSize        = 16;
        phTMP.color           = new Color(0.5f, 0.5f, 0.5f);
        phTMP.alignment       = TextAlignmentOptions.MidlineLeft;
        phTMP.fontStyle       = FontStyles.Italic;

        input.textComponent   = textTMP;
        input.placeholder     = phTMP;
        input.text            = placeholder;
        return input;
    }

    private Button CreateButton(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, string label, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect   = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;

        Image img  = obj.AddComponent<Image>();
        img.color  = color;

        Button btn = obj.AddComponent<Button>();
        ColorBlock colors      = btn.colors;
        colors.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f);
        colors.pressedColor    = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f);
        btn.colors             = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin     = Vector2.zero;
        textRect.anchorMax     = Vector2.one;
        textRect.offsetMin     = Vector2.zero;
        textRect.offsetMax     = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text            = label;
        tmp.fontSize        = 18;
        tmp.fontStyle       = FontStyles.Bold;
        tmp.color           = Color.white;
        tmp.alignment       = TextAlignmentOptions.Center;
        return btn;
    }

    private void SetLobbyUIReferences(LobbyUI lobbyUI, GameObject panel, TMP_InputField ipInput, TMP_InputField portInput,
                                      Button hostBtn, Button clientBtn, Button disconnectBtn, Button startMatchBtn,
                                      TextMeshProUGUI statusText, TextMeshProUGUI playerCountText)
    {
        var type = typeof(LobbyUI);

        SetPrivateField(type, lobbyUI, "lobbyPanel",       panel);
        SetPrivateField(type, lobbyUI, "ipInputField",     ipInput);
        SetPrivateField(type, lobbyUI, "portInputField",   portInput);
        SetPrivateField(type, lobbyUI, "hostButton",       hostBtn);
        SetPrivateField(type, lobbyUI, "clientButton",     clientBtn);
        SetPrivateField(type, lobbyUI, "disconnectButton", disconnectBtn);
        SetPrivateField(type, lobbyUI, "startMatchButton",  startMatchBtn); // Sinsi Reflection bağlantısı!
        SetPrivateField(type, lobbyUI, "statusText",       statusText);
        SetPrivateField(type, lobbyUI, "playerCountText",  playerCountText);
    }

    private void SetPrivateField(System.Type type, object obj, string fieldName, object value)
    {
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(obj, value);
    }

    [ContextMenu("Clear Lobby UI")]
    public void ClearLobbyUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}