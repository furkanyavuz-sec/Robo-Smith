// TrashBin.cs
// Görev: Oyuncunun elindeki nesneyi yok eder.
// Kullanım: Yanlış işlenmiş item'ları temizlemek için.
// Kural: Eli boş gelirse etkileşim reddedilir.

using UnityEngine;

public class TrashBin : BaseStation
{
    [Header("Çöp Kutusu Ayarları")]
    [SerializeField] private float destroyDelay = 0.15f; // Kısa gecikme — görsel his

    public override bool CanInteract(PlayerInteraction player)
    {
        // Sadece eli dolu oyuncularla etkileşim
        return player.HeldObject != null;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        GameObject trash = player.HeldObject;

        // Önce oyuncunun elinden düşür
        player.ForceDropFromStation();

        // Sonra yok et
        Destroy(trash, destroyDelay);

        Debug.Log($"[TrashBin] '{trash.name}' imha edildi.");
    }
}