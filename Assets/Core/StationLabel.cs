// StationLabel.cs
// Yüzen etiket — her karede kameraya döner. Sci-fi fontu runtime'da yükler.

using TMPro;
using UnityEngine;

public class StationLabel : MonoBehaviour
{
    private static Camera cam;

    private void Start()
    {
        // Fütüristik font (Audiowide) — runtime'da uygulanır, sahneye gömülmez
        TMP_FontAsset font = DisplayFontApplier.GetFont();
        if (font != null && TryGetComponent<TMP_Text>(out TMP_Text tmp))
            tmp.font = font;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position);
    }
}
