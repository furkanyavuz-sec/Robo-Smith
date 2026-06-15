// PlasmaSource.cs
// Görev: Haritanın tarafsız bölgesinde (Scrapyard) durur.
// Oyuncu boş elle gelirse RawPlasma üretip eline verir.
// SupplyBin'den farkı: verdiği item türü ve görsel kimliği.
// NGO notu: Spawn işlemi ileride ServerRpc'ye taşınacak.

using UnityEngine;

public class PlasmaSource : BaseStation
{
    [Header("Plazma Kaynağı Ayarları")]
    [SerializeField] private GameObject rawPlasmaPrefab;
    [SerializeField] private float      respawnCooldown = 2f; // SupplyBin'den biraz yavaş

    private float lastSupplyTime = -999f;

    // ── BaseStation Sözleşmesi ───────────────────────────────────────────

    public override bool CanInteract(PlayerInteraction player)
    {
        // Eli dolu gelirse ret — yeni kaynak alamaz
        if (player.HeldObject != null) return false;

        // Cooldown kontrolü
        if (Time.time - lastSupplyTime < respawnCooldown) return false;

        return true;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        if (rawPlasmaPrefab == null)
        {
            Debug.LogError("[PlasmaSource] rawPlasmaPrefab atanmamış!");
            return;
        }

        // RawPlasma oluştur
        GameObject spawnedPlasma = Instantiate(
            rawPlasmaPrefab,
            transform.position + Vector3.up * 1.2f,
            Quaternion.identity
        );

        // ItemType'ı ata
        if (spawnedPlasma.TryGetComponent<PickupItem>(out PickupItem item))
            item.SetType(ItemType.RawPlasma);

        // Doğrudan oyuncunun eline ver — HoldPoint'e kilitlenir
        player.PickupFromStation(spawnedPlasma);

        lastSupplyTime = Time.time;

        Debug.Log("[PlasmaSource] RawPlasma verildi.");
    }
}