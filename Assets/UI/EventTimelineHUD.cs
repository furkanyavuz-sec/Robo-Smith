// EventTimelineHUD.cs — Hazırlık fazı olay zaman çizelgesi
// Görev: Ekranın en üstünde ince bir şeritte sıradaki pencereleri gösterir:
//   "HURDALIK 00:42  •  DRONE 01:12  •  HURDALIK 03:12"
//   Açık pencere varsa kalan süresiyle vurgulanır. Oyuncu 5 zamanlı olayı
//   planlayabilir hale gelir ("45 sn sonra hurdalık — plazmayı işleyip koş").
// Prefab gerektirmez — RaidAnnouncer deseniyle kendi Canvas'ını kurar.
// Kurulum: zone'lar Awake'te Ensure() çağırır.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventTimelineHUD : MonoBehaviour
{
    private static EventTimelineHUD instance;

    private TextMeshProUGUI label;
    private float refreshTimer;

    private const string DRONE_HEX = "33D9E6";   // Çekirdek bölge camgöbeği
    private const string SCRAP_HEX = "F2D919";   // Hurdalık sarısı

    public static void Ensure()
    {
        if (instance != null) return;

        GameObject root = new GameObject("EventTimelineHUD");
        DontDestroyOnLoad(root);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;   // Anonsun (500) hemen altında

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObj = new GameObject("Cizelge");
        textObj.transform.SetParent(root.transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset sciFi  = DisplayFontApplier.GetFont();
        if (sciFi != null) text.font = sciFi;
        text.fontSize  = 19f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 2f;
        text.text = "";

        RectTransform rect = text.rectTransform;
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        // TimerPanel üst-ortada y -12..-124 bandını kaplar — şerit hemen altına
        rect.anchoredPosition = new Vector2(0f, -130f);
        rect.sizeDelta        = new Vector2(1600f, 34f);

        instance = root.AddComponent<EventTimelineHUD>();
        instance.label = text;
    }

    private void Update()
    {
        // Saniyede ~4 güncelleme yeter — string üretimini boğma
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.25f;

        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentPhase != GamePhase.Preparation)
        {
            label.text = "";
            return;
        }

        // MP'de etkinlikler Faz 3'e kadar kapalı — donuk çizelge gösterme
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            label.text = "";
            return;
        }

        label.text = BuildTimeline();
    }

    private static string BuildTimeline()
    {
        // (etiket, renk, kalan sn, açık mı) — iki zone'dan toplanır
        List<(string name, string hex, float t, bool open)> entries = new();

        DroneRaidZone drone = DroneRaidZone.Instance;
        if (drone != null)
        {
            if (drone.OpenTimeRemaining >= 0f)
                entries.Add(("DRONE", DRONE_HEX, drone.OpenTimeRemaining, true));
            foreach (float t in drone.UpcomingWindows())
                entries.Add(("DRONE", DRONE_HEX, t, false));
        }

        ScrapWindowZone scrap = ScrapWindowZone.Instance;
        if (scrap != null)
        {
            if (scrap.OpenTimeRemaining >= 0f)
                entries.Add(("HURDALIK", SCRAP_HEX, scrap.OpenTimeRemaining, true));
            foreach (float t in scrap.UpcomingWindows())
                entries.Add(("HURDALIK", SCRAP_HEX, t, false));
        }

        if (entries.Count == 0) return "";

        // Açık olan en öne, kalanlar zamana göre; en fazla 3 çip göster
        entries.Sort((a, b) => a.open != b.open
            ? (a.open ? -1 : 1)
            : a.t.CompareTo(b.t));

        var parts = new List<string>();
        for (int i = 0; i < entries.Count && i < 3; i++)
        {
            var e = entries[i];
            string time = $"{(int)(e.t / 60f):0}:{(int)(e.t % 60f):00}";

            parts.Add(e.open
                ? $"<color=#{e.hex}>▶ {e.name} AÇIK {time}</color>"
                : i == 0
                    ? $"<color=#{e.hex}>{e.name} {time}</color>"
                    : $"<color=#{e.hex}><alpha=#77>{e.name} {time}</color>");
        }

        return string.Join("   <color=#4A5568>•</color>   ", parts);
    }
}
