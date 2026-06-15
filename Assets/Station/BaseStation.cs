// BaseStation.cs
// Görev: Tüm istasyonların uymak zorunda olduğu sözleşme.
// "abstract" seçildi (interface değil) çünkü:
//   - MonoBehaviour'dan miras almamız gerekiyor (Unity lifecycle)
//   - Ortak alanlar (stationName, promptText) burada saklanacak
//   - Interface + MonoBehaviour birlikte kullanmak NGO'da karmaşıklaşır

using UnityEngine;

public abstract class BaseStation : MonoBehaviour
{
    [Header("İstasyon Kimliği")]
    [SerializeField] protected string stationName = "İstasyon";
    [SerializeField] protected string interactPrompt = "E: Etkileşim";

    // ── Türetilmiş sınıfların doldurmak ZORUNDA olduğu metodlar ──

    /// <summary>
    /// Oyuncu E'ye bastığında çağrılır.
    /// Her istasyon kendi mantığını burada uygular.
    /// </summary>
    public abstract void Interact(PlayerInteraction player);

    /// <summary>
    /// Bu istasyonla şu an etkileşim kurulabilir mi?
    /// UI ipucu göstermek ve Interact() çağırmadan önce kontrol için kullanılır.
    /// </summary>
    public abstract bool CanInteract(PlayerInteraction player);

    // ── Ortak yardımcı metodlar (tüm istasyonlar kullanabilir) ──

    /// <summary>
    /// Inspector'da istasyonun etkileşim alanını gösterir.
    /// Override edilerek özelleştirilebilir.
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.1f);

        // İstasyon adını scene view'da göster
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1f,
            stationName
        );
#endif
    }
}