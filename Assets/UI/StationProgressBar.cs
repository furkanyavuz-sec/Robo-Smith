// StationProgressBar.cs
// Görev: İşleme/üretim/montaj sırasında istasyonun üstünde dolan bar +
//        kalan süre yazısı gösterir. Kameraya dönüktür (billboard).
// Kullanım (istasyon Update'inden):
//   StationProgressBar.Show(gameObject, ilerleme01, kalanSaniye);
//   ...iş bitince: StationProgressBar.Hide(gameObject);
// Bar ilk Show çağrısında kendini oluşturur — prefab/sahne kablosu gerekmez.

using TMPro;
using UnityEngine;

public class StationProgressBar : MonoBehaviour
{
    private const float WIDTH  = 1.5f;
    private const float HEIGHT = 0.16f;

    private Transform       fill;
    private Renderer        fillRenderer;
    private TextMeshPro     timeLabel;
    private MaterialPropertyBlock mpb;

    private static readonly int PropColor     = Shader.PropertyToID("_Color");
    private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    private static Camera cam;

    // ── Statik API ───────────────────────────────────────────────────────

    public static void Show(GameObject station, float progress01, float secondsLeft)
    {
        StationProgressBar bar =
            station.GetComponentInChildren<StationProgressBar>(true);

        if (bar == null) bar = Create(station);

        if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);
        bar.Set(progress01, secondsLeft);
    }

    public static void Hide(GameObject station)
    {
        StationProgressBar bar =
            station.GetComponentInChildren<StationProgressBar>(true);

        if (bar != null && bar.gameObject.activeSelf)
            bar.gameObject.SetActive(false);
    }

    // ── Kurulum ──────────────────────────────────────────────────────────

    private static StationProgressBar Create(GameObject station)
    {
        GameObject root = new GameObject("ProgressBar");
        root.transform.SetParent(station.transform, false);
        root.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        root.AddComponent<StationDecorTag>();   // Tema yöneticisi boyamasın

        StationProgressBar bar = root.AddComponent<StationProgressBar>();
        bar.mpb = new MaterialPropertyBlock();

        // Arka plan
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.name = "BG";
        PrepPiece(bg, root.transform, Vector3.zero,
            new Vector3(WIDTH + 0.06f, HEIGHT + 0.06f, 0.03f));
        bg.GetComponent<Renderer>().sharedMaterial =
            StationVisuals.GetMaterial(new Color(0.05f, 0.05f, 0.08f));

        // Doluluk — sol hizalı, Set() genişliğini ayarlar
        GameObject fillObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillObj.name = "Fill";
        PrepPiece(fillObj, root.transform, new Vector3(0f, 0f, -0.025f),
            new Vector3(WIDTH, HEIGHT, 0.03f));
        bar.fill         = fillObj.transform;
        bar.fillRenderer = fillObj.GetComponent<Renderer>();
        bar.fillRenderer.sharedMaterial = StationVisuals.GetMaterial(Color.white);

        // Kalan süre yazısı
        GameObject labelObj = new GameObject("TimeLabel");
        labelObj.transform.SetParent(root.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 0.24f, 0f);
        labelObj.AddComponent<StationDecorTag>();

        bar.timeLabel           = labelObj.AddComponent<TextMeshPro>();
        bar.timeLabel.fontSize  = 2f;
        bar.timeLabel.fontStyle = FontStyles.Bold;
        bar.timeLabel.alignment = TextAlignmentOptions.Center;
        bar.timeLabel.color     = Color.white;
        bar.timeLabel.rectTransform.sizeDelta = new Vector2(3f, 0.5f);

        return bar;
    }

    private static void PrepPiece(GameObject obj, Transform parent,
        Vector3 localPos, Vector3 scale)
    {
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale    = scale;
        obj.AddComponent<StationDecorTag>();

        if (obj.TryGetComponent<Collider>(out Collider col))
        {
            if (Application.isPlaying) Destroy(col);
            else                       DestroyImmediate(col);
        }
    }

    // ── Güncelleme ───────────────────────────────────────────────────────

    private void Set(float progress01, float secondsLeft)
    {
        progress01 = Mathf.Clamp01(progress01);

        // Sol hizalı doluluk: genişlik + merkez kaydırma birlikte
        float w = Mathf.Max(0.001f, WIDTH * progress01);
        fill.localScale    = new Vector3(w, HEIGHT, 0.03f);
        fill.localPosition = new Vector3(-WIDTH / 2f + w / 2f, 0f, -0.025f);

        // Renk: sarıdan yeşile (mevcut istasyon geleneği)
        Color c = Color.Lerp(new Color(0.95f, 0.8f, 0.15f),
                             new Color(0.35f, 0.9f, 0.35f), progress01);
        fillRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(PropColor,     c);
        mpb.SetColor(PropBaseColor, c);
        fillRenderer.SetPropertyBlock(mpb);

        if (timeLabel != null)
            timeLabel.text = $"{Mathf.Max(0f, secondsLeft):F1}s";
    }

    private void LateUpdate()
    {
        // Kameraya dön
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position);
    }
}
