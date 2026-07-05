// NetworkManagerGenerator.cs
// Görev: MainMenu sahnesinde NetworkManager'ı otomatik oluşturur.
// Inspector'dan "Generate Network Manager" ile çalıştır.

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkManagerGenerator : MonoBehaviour
{
    [Header("Ayarlar")]
    [SerializeField] private string defaultIP   = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 7777;
    [SerializeField] private GameObject playerPrefab; // Inspector'dan bağla

    [ContextMenu("Generate Network Manager")]
public void GenerateNetworkManager()
{
    ClearNetworkManager();

    // NetworkManager objesi
    GameObject nmObj = new GameObject("NetworkManager");
    nmObj.transform.SetParent(transform);

    // NGO bileşenleri
    NetworkManager    nm        = nmObj.AddComponent<NetworkManager>();
    UnityTransport    transport = nmObj.AddComponent<UnityTransport>();
    transport.SetConnectionData(defaultIP, defaultPort);

    // Transport'u NetworkManager'a bağla
    nm.NetworkConfig.NetworkTransport = transport;

    // Player Prefab — Inspector boşsa asset'lerden kendimiz buluruz
    // (PlayerPrefab'sız NetworkManager oyuncu spawn edemez = gri ekran)
    if (playerPrefab == null)
        playerPrefab = FindPlayerPrefab();

    if (playerPrefab == null)
    {
        Debug.LogError("[NetworkManagerGenerator] ❌ Player prefabı bulunamadı! " +
                       "Inspector'dan 'Player Prefab' alanına Player Variant'ı bağla " +
                       "ve tekrar üret — bu olmadan multiplayer oyuncu spawn EDEMEZ.");
    }
    else if (playerPrefab.GetComponent<NetworkObject>() == null)
    {
        Debug.LogError($"[NetworkManagerGenerator] ❌ '{playerPrefab.name}' " +
                       "prefabında NetworkObject yok — ağ oyuncusu olamaz!");
    }
    else
    {
        nm.NetworkConfig.PlayerPrefab = playerPrefab;
        Debug.Log($"[NetworkManagerGenerator] Player prefabı bağlandı: " +
                  $"{playerPrefab.name}");
    }

    nmObj.AddComponent<DontDestroyHelper>();

    Debug.Log("[NetworkManagerGenerator] ✅ NetworkManager oluşturuldu. " +
              "Sahneyi kaydet (Ctrl+S).");
}

    /// <summary>
    /// Ağ oyuncusu prefabını asset'lerde arar: NetworkObject'li olan
    /// "Player Variant" öncelikli, yoksa "Player".
    /// </summary>
    private GameObject FindPlayerPrefab()
    {
#if UNITY_EDITOR
        foreach (string searchName in new[] { "Player Variant", "Player" })
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                $"{searchName} t:Prefab");

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab.GetComponent<NetworkObject>() != null)
                {
                    Debug.Log($"[NetworkManagerGenerator] Player prefabı " +
                              $"otomatik bulundu: {path}");
                    return prefab;
                }
            }
        }
#endif
        return null;
    }

    [ContextMenu("Clear Network Manager")]
    public void ClearNetworkManager()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}