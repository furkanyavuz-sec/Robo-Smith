// ScrapyardStation.cs
// Görev: Haritanın tarafsız orta bölgesinde durur.
// Her oyuncuya belirli aralıklarla Scrapyard ham maddesi verir.
// Hangi maddeyi vereceği Inspector'dan ayarlanır.
// Cooldown: her iki takım da aynı kaynaktan alabilir — rekabetçi!

using UnityEngine;

public class ScrapyardStation : BaseStation
{
    [Header("Kaynak Ayarları")]
    [SerializeField] private ItemType       supplyType  = ItemType.ScrapMetal;
    [SerializeField] private GameObject     itemPrefab;
    [SerializeField] private float          cooldown    = 5f;   // Rekabetçi kaynak — kısa cooldown

    /// <summary>VisualThemeManager gövdeyi içerik rengine boyamak için okur.</summary>
    public ItemType SupplyType => supplyType;

    private float lastPickupTime = -999f;

    public override bool CanInteract(PlayerInteraction player)
    {
        if (player.HeldObject != null)        return false;
        if (Time.time - lastPickupTime < cooldown) return false;
        return true;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        GameObject spawned = Instantiate(
            itemPrefab,
            transform.position + Vector3.up * 1.2f,
            Quaternion.identity
        );

        if (spawned.TryGetComponent<PickupItem>(out PickupItem item))
            item.SetType(supplyType);

        player.PickupFromStation(spawned);
        lastPickupTime = Time.time;

        Debug.Log($"[ScrapyardStation] {supplyType} verildi.");
    }
}