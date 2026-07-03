// FuturisticFont.cs
// Görev: Fütüristik display fontu (Audiowide — Google Fonts, OFL lisanslı).
// TMP font asset'i RUNTIME'da TTF'den üretilir; sahneye asset referansı
// kaydedilmez (generate anında üretilse kaydedilemez ve kaybolurdu).
//   DisplayFontTag     → generator bu işareti başlık/butonlara koyar
//   DisplayFontApplier → canvas'ta Awake'te tüm işaretli metinlere uygular
//   AccentPulse        → vurgu çizgilerinde yavaş parlama (alpha ping-pong)

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bu metne display (sci-fi) fontu uygulanacak.</summary>
public class DisplayFontTag : MonoBehaviour { }

/// <summary>Canvas köküne eklenir; işaretli metinlere fontu uygular.</summary>
public class DisplayFontApplier : MonoBehaviour
{
    private static TMP_FontAsset cached;

    public static TMP_FontAsset GetFont()
    {
        if (cached != null) return cached;

        Font ttf = Resources.Load<Font>("Fonts/Audiowide-Regular");
        if (ttf == null)
        {
            Debug.LogWarning("[FuturisticFont] Audiowide-Regular.ttf bulunamadı — " +
                             "varsayılan font kullanılacak.");
            return null;
        }

        cached = TMP_FontAsset.CreateFontAsset(ttf);
        return cached;
    }

    private void Awake()
    {
        TMP_FontAsset font = GetFont();
        if (font == null) return;

        foreach (DisplayFontTag tag in
                 GetComponentsInChildren<DisplayFontTag>(true))
        {
            if (tag.TryGetComponent<TextMeshProUGUI>(out TextMeshProUGUI tmp))
                tmp.font = font;
        }
    }
}

/// <summary>Vurgu çizgisi/ışığı için yavaş alpha nabzı.</summary>
public class AccentPulse : MonoBehaviour
{
    [SerializeField] private float minAlpha = 0.35f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private float speed    = 1.2f;

    private Graphic graphic;

    private void Awake() => graphic = GetComponent<Graphic>();

    private void Update()
    {
        if (graphic == null) return;

        float t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
        Color c = graphic.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        graphic.color = c;
    }
}
