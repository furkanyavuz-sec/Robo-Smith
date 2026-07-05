// ItemPrefabRegistry.cs — MP Faz 2: Item prefablarının ağ kaydı
// Görev: NetworkManager objesinde durur; Awake'te tüm item prefablarını
//   NGO'ya kaydeder (AddNetworkPrefab) — kayıtsız prefab spawn EDİLEMEZ.
// Kurulum: NetworkManagerGenerator, PickupItem'lı prefabları bulup
//   listeyi bağlar (elle kablolama yok).

using Unity.Netcode;
using UnityEngine;

public class ItemPrefabRegistry : MonoBehaviour
{
    [SerializeField] private GameObject[] itemPrefabs;

    private void Awake()
    {
        NetworkManager nm = GetComponent<NetworkManager>();
        if (nm == null || itemPrefabs == null) return;

        int count = 0;
        foreach (GameObject prefab in itemPrefabs)
        {
            if (prefab == null) continue;

            try
            {
                nm.AddNetworkPrefab(prefab);
                count++;
            }
            catch (System.Exception e)
            {
                // Çift kayıt vb. — oyunu durdurmasın, sadece bildir
                Debug.LogWarning($"[ItemPrefabRegistry] '{prefab.name}' " +
                                 $"kaydedilemedi: {e.Message}");
            }
        }

        Debug.Log($"[ItemPrefabRegistry] {count} item prefabı ağa kaydedildi.");
    }
}
