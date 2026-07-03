// DamagePopup.cs
// Görev: Arena'da robotların üstünde yüzen hasar/durum yazıları.
// Prefab gerektirmez — tamamen koddan oluşturulur (TMP default fontu kullanır).
// Renk kodu: beyaz = normal hasar, yeşil = zırh direnci, kırmızı = zırh zayıflığı,
//            camgöbeği = kalkan bloğu, mor = EMP.

using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro text;
    private Color       baseColor;
    private float       age;

    private const float LIFETIME   = 0.9f;
    private const float RISE_SPEED = 2.2f;

    private static Camera cam;

    /// <summary>
    /// Dünya pozisyonunda yüzen yazı oluşturur.
    /// scale: vurgu için büyütme (örn. kritik/blok mesajları 1.3f).
    /// </summary>
    public static void Spawn(Vector3 worldPos, string message, Color color, float scale = 1f)
    {
        GameObject obj = new GameObject("DamagePopup");

        // Robotun üstünde, hafif rastgele yatay sapmayla (üst üste binmesin)
        Vector2 jitter = Random.insideUnitCircle * 0.4f;
        obj.transform.position = worldPos + new Vector3(jitter.x, 2.2f, jitter.y);

        DamagePopup popup = obj.AddComponent<DamagePopup>();
        popup.text            = obj.AddComponent<TextMeshPro>();
        TMP_FontAsset sciFi   = DisplayFontApplier.GetFont();
        if (sciFi != null) popup.text.font = sciFi;
        popup.text.text       = message;
        popup.text.fontSize   = 5f * scale;
        popup.text.alignment  = TextAlignmentOptions.Center;
        popup.text.fontStyle  = FontStyles.Bold;
        popup.text.color      = color;
        popup.baseColor       = color;
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * (RISE_SPEED * Time.deltaTime);

        // Her zaman kameraya dön (billboard)
        if (cam == null) cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position);

        // Son %40'ta solarak kaybol
        float t     = age / LIFETIME;
        float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        text.color  = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        if (age >= LIFETIME) Destroy(gameObject);
    }
}
