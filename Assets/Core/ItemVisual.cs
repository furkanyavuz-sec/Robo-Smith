// ItemVisual.cs
// Görev: PickupItem prefabına eklenir.
// Runtime'da spawn edildiği anda VisualThemeManager'ı bulup
// kendi rengini kendisi uygular.
// Böylece VisualThemeManager sadece Start'ta değil,
// oyun boyunca spawn olan her item da doğru renkle doğar.

using UnityEngine;

[RequireComponent(typeof(PickupItem))]
public class ItemVisual : MonoBehaviour
{
    private void Start()
    {
        PickupItem item = GetComponent<PickupItem>();

        VisualThemeManager theme =
            FindAnyObjectByType<VisualThemeManager>();

        if (theme == null)
        {
            Debug.LogWarning("[ItemVisual] Sahnede VisualThemeManager yok.");
            return;
        }

        theme.ApplyItemColor(item);
    }
}