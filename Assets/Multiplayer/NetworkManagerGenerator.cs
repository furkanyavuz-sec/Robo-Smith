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

    // Player Prefab
    if (playerPrefab != null)
        nm.NetworkConfig.PlayerPrefab = playerPrefab;

    nmObj.AddComponent<DontDestroyHelper>();

    Debug.Log("[NetworkManagerGenerator] NetworkManager olusturuldu! " +
              "NetworkManagerSetup.cs'i manuel ekle.");
}

    [ContextMenu("Clear Network Manager")]
    public void ClearNetworkManager()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}