// RaidAnnouncer.cs — Ekran üstü anons yazısı (çekirdek bölge olayları)
// Prefab gerektirmez — DamagePopup deseni: ilk çağrıda kendi Canvas'ını
// koddan kurar, mesajı üstte ortada gösterip solarak kaybeder.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaidAnnouncer : MonoBehaviour
{
    private static RaidAnnouncer instance;

    private TextMeshProUGUI label;
    private float           timer;
    private float           duration;
    private Color           baseColor;

    /// <summary>Ekranın üst ortasında anons gösterir (öncekinin üstüne yazar).</summary>
    public static void Show(string message, Color color, float seconds)
    {
        if (instance == null) instance = Create();

        Sfx.Play(Sfx.Id.Announce, 0.4f);   // Her anons dikkat bip'iyle gelir

        instance.label.text  = message;
        instance.baseColor   = color;
        instance.duration    = seconds;
        instance.timer       = seconds;
        instance.label.color = color;
    }

    private static RaidAnnouncer Create()
    {
        GameObject root = new GameObject("RaidAnnouncer");
        DontDestroyOnLoad(root);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObj = new GameObject("AnonsYazisi");
        textObj.transform.SetParent(root.transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset sciFi  = DisplayFontApplier.GetFont();
        if (sciFi != null) text.font = sciFi;
        text.fontSize  = 42f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.text      = "";
        text.color     = Color.clear;

        RectTransform rect = text.rectTransform;
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -90f);
        rect.sizeDelta        = new Vector2(1400f, 160f);

        RaidAnnouncer announcer = root.AddComponent<RaidAnnouncer>();
        announcer.label = text;
        return announcer;
    }

    private void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;

        // Son %35'te solarak kaybol
        float t     = 1f - Mathf.Clamp01(timer / duration);
        float alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
        label.color = new Color(baseColor.r, baseColor.g, baseColor.b,
                                Mathf.Clamp01(alpha));

        if (timer <= 0f)
            label.color = Color.clear;
    }
}
