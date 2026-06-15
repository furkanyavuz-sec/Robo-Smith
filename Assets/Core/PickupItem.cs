// PickupItem.cs — ItemType desteği eklendi

using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [field: SerializeField] public ItemType Type { get; private set; }

    // SupplyBin runtime'da tip atamak için çağırır
    public void SetType(ItemType type)
    {
        Type = type;
    }
}