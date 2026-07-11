// UIFactory.cs
// Görev: HUD generator'larının ortak yapı taşları — tek görsel dil.
// Renk paleti + panel/metin/buton oluşturucular.
// LobbyUIGenerator'daki desenin paylaşılan versiyonu.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UIFactory
{
    // ── Palet: tüm HUD'larda aynı renk dili ─────────────────────────────
    public static readonly Color PanelBG      = new Color(0.06f, 0.07f, 0.10f, 0.85f);
    public static readonly Color HeaderBlue   = new Color(0.16f, 0.35f, 0.70f, 0.95f);
    public static readonly Color HeaderRed    = new Color(0.70f, 0.18f, 0.18f, 0.95f);
    public static readonly Color TextMain     = Color.white;
    public static readonly Color TextDim      = new Color(0.65f, 0.68f, 0.75f);
    public static readonly Color Divider      = new Color(1f, 1f, 1f, 0.10f);

    public static readonly Color StatHP  = new Color(0.45f, 1f,    0.55f);
    public static readonly Color StatATK = new Color(1f,    0.55f, 0.40f);
    public static readonly Color StatSPD = new Color(0.45f, 0.85f, 1f);
    public static readonly Color StatDEF = new Color(0.60f, 0.70f, 1f);

    public static readonly Color TeamBlue = new Color(0.30f, 0.55f, 1f);
    public static readonly Color TeamRed  = new Color(1f,    0.35f, 0.30f);

    // ── Canvas ───────────────────────────────────────────────────────────
    public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Canvas canvas       = obj.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler        = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        obj.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ── Panel / Görsel ───────────────────────────────────────────────────

    /// <summary>Köşe/kenar hizalı panel. anchor hem anchor hem pivot olur.</summary>
    public static GameObject CreatePanel(string name, Transform parent,
        Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect    = obj.AddComponent<RectTransform>();
        rect.anchorMin        = anchor;
        rect.anchorMax        = anchor;
        rect.pivot            = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta        = size;

        Image img         = obj.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;   // HUD tıklamayı engellemesin
        return obj;
    }

    /// <summary>Ebeveyni tamamen kaplayan panel (tam ekran arka planlar için).</summary>
    public static GameObject CreateStretchPanel(string name, Transform parent,
        Color color, bool blockClicks = false)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img         = obj.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = blockClicks;
        return obj;
    }

    // ── Metin ────────────────────────────────────────────────────────────
    public static TextMeshProUGUI CreateText(string name, Transform parent,
        Vector2 anchor, Vector2 pos, Vector2 size,
        string text, float fontSize, FontStyles style, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect    = obj.AddComponent<RectTransform>();
        rect.anchorMin        = anchor;
        rect.anchorMax        = anchor;
        rect.pivot            = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta        = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text            = text;
        tmp.fontSize        = fontSize;
        tmp.fontStyle       = style;
        tmp.color           = color;
        tmp.alignment       = alignment;
        tmp.raycastTarget   = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode    = TextOverflowModes.Overflow;
        return tmp;
    }

    // ── Buton ────────────────────────────────────────────────────────────
    public static Button CreateButton(string name, Transform parent,
        Vector2 anchor, Vector2 pos, Vector2 size, string label, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect    = obj.AddComponent<RectTransform>();
        rect.anchorMin        = anchor;
        rect.anchorMax        = anchor;
        rect.pivot            = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta        = size;

        Image img = obj.AddComponent<Image>();
        img.color = color;

        Button btn = obj.AddComponent<Button>();
        ColorBlock colors       = btn.colors;
        colors.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f);
        colors.pressedColor     = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f);
        btn.colors              = colors;

        TextMeshProUGUI tmp = CreateText("Text", obj.transform,
            new Vector2(0.5f, 0.5f), Vector2.zero, size,
            label, 22, FontStyles.Bold, Color.white);
        tmp.raycastTarget = false;

        return btn;
    }

    /// <summary>Yatay doluluk barı (Filled Image). Fill bileşenini döndürür.</summary>
    public static Image CreateFillBar(string name, Transform parent,
        Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor)
    {
        // Arka plan
        GameObject bg = CreatePanel(name, parent, anchor, pos, size,
            new Color(0f, 0f, 0f, 0.45f));

        // Doluluk
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(bg.transform, false);
        RectTransform rect = fillObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(1, 1);
        rect.offsetMax = new Vector2(-1, -1);

        Image fill         = fillObj.AddComponent<Image>();
        fill.color         = fillColor;
        fill.raycastTarget = false;
        fill.type          = Image.Type.Filled;
        fill.fillMethod    = Image.FillMethod.Horizontal;
        fill.fillOrigin    = 0;
        fill.fillAmount    = 1f;

#if UNITY_EDITOR
        // Filled tipin düzgün çizilmesi için built-in sprite ata
        fill.sprite = UnityEditor.AssetDatabase
            .GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
#endif
        return fill;
    }

    // ── Reflection: private [SerializeField] alan bağlama ───────────────
    public static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field == null)
        {
            Debug.LogError($"[UIFactory] '{target.GetType().Name}.{fieldName}' " +
                           $"alanı bulunamadı — script değişmiş olabilir!");
            return;
        }
        field.SetValue(target, value);
    }
}
