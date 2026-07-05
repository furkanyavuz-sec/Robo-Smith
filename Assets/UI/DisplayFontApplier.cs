// DisplayFontApplier.cs
// Fütüristik display fontu (Audiowide — Google Fonts, OFL lisanslı).
// TMP font asset'i RUNTIME'da TTF'den üretilir; sahneye asset referansı
// kaydedilmez (generate anında üretilse kaydedilemez ve kaybolurdu).
// Canvas köküne eklenir; Awake'te tüm DisplayFontTag'li metinlere uygular.

using TMPro;
using UnityEngine;

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
