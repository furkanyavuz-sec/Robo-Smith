// SupplyBin.cs
// Görev: Oyuncu boş elle gelirse belirtilen ham maddeyi spawn edip verir.
// Kural: Eli dolu gelirse etkileşim reddedilir (CanInteract false döner).
// Konfigürasyon: Inspector'dan hangi item'ı vereceği ayarlanır →
//   Demir garajı için Iron, Scrapyard için Energy/Circuit.

using UnityEngine;

public class SupplyBin : BaseStation
{
    [Header("Tedarik Ayarları")]
    [SerializeField] private ItemType supplyItemType = ItemType.Iron;
    [SerializeField] private GameObject itemPrefab;     // Spawn edilecek prefab
    [SerializeField] private float respawnCooldown = 1f; // Saniye — spam önleme

    /// <summary>VisualThemeManager gövdeyi içerik rengine boyamak için okur.</summary>
    public ItemType SupplyType => supplyItemType;

    private float lastSupplyTime = -999f;

    public override bool CanInteract(PlayerInteraction player)
    {
        // Koşul 1: Oyuncunun eli boş olmalı
        if (player.HeldObject != null) return false;

        // Koşul 2: Cooldown dolmuş olmalı
        if (Time.time - lastSupplyTime < respawnCooldown) return false;

        return true;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        // Prefab'ı spawn et
        GameObject newItem = Instantiate(
            itemPrefab,
            transform.position + Vector3.up * 1.2f,  // Masanın hemen üstünde
            Quaternion.identity
        );

        // ItemType'ı ata
        if (newItem.TryGetComponent<PickupItem>(out PickupItem item))
            item.SetType(supplyItemType);

        // Oyuncunun eline ver
        player.PickupFromStation(newItem);

        lastSupplyTime = Time.time;

        Debug.Log($"[SupplyBin] {supplyItemType} verildi.");
    }
}