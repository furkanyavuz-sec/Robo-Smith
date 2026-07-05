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

    // NetworkManager objesi — KÖK seviyede kalmalı: NGO nested
    // NetworkManager'ı kabul etmiyor ("Invalid Nested NetworkManager")
    GameObject nmObj = new GameObject("NetworkManager");

    // NGO bileşenleri
    NetworkManager    nm        = nmObj.AddComponent<NetworkManager>();
    UnityTransport    transport = nmObj.AddComponent<UnityTransport>();
    transport.SetConnectionData(defaultIP, defaultPort);

    // Transport'u NetworkManager'a bağla
    nm.NetworkConfig.NetworkTransport = transport;

    // LAN oyunu: 60 tick — pozisyon senkronu sıklaşır, uzak oyuncunun
    // ara doldurma (interpolasyon) mesafeleri kısalır, duvar köşesinden
    // geçmiş gibi görünme artefaktı azalır (varsayılan 30 idi)
    nm.NetworkConfig.TickRate = 60;

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

    // MP Faz 2: item prefablarını ağa hazırla (NetworkObject + NetworkTransform
    // + NetworkItem ekle, asset'i kaydet) ve runtime kayıt listesini bağla
    GameObject[] itemPrefabs = PrepareItemPrefabs();
    ItemPrefabRegistry registry = nmObj.AddComponent<ItemPrefabRegistry>();
    UIFactory.SetField(registry, "itemPrefabs", itemPrefabs);

    Debug.Log($"[NetworkManagerGenerator] ✅ NetworkManager oluşturuldu, " +
              $"{itemPrefabs.Length} item prefabı ağa hazırlandı. " +
              "Sahneyi kaydet (Ctrl+S).");
}

    /// <summary>
    /// PickupItem taşıyan TÜM prefabları bulur; eksikse NetworkObject +
    /// NetworkTransform + NetworkItem ekleyip asset'i kaydeder.
    /// Dönen liste ItemPrefabRegistry'ye bağlanır (runtime AddNetworkPrefab).
    /// </summary>
    private GameObject[] PrepareItemPrefabs()
    {
#if UNITY_EDITOR
        var prepared = new System.Collections.Generic.List<GameObject>();

        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset =
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (asset == null || asset.GetComponent<PickupItem>() == null)
                continue;

            GameObject root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            if (root.GetComponent<NetworkObject>() == null)
            {
                root.AddComponent<NetworkObject>();
                changed = true;
            }
            if (root.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null)
            {
                root.AddComponent<Unity.Netcode.Components.NetworkTransform>();
                changed = true;
            }
            if (root.GetComponent<NetworkItem>() == null)
            {
                root.AddComponent<NetworkItem>();
                changed = true;
            }

            if (changed)
            {
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[NetworkManagerGenerator] Ağa hazırlandı: {path}");
            }

            UnityEditor.PrefabUtility.UnloadPrefabContents(root);
            prepared.Add(
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        return prepared.ToArray();
#else
        return new GameObject[0];
#endif
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
        // Eski sürümün altımıza koyduğu kopyalar
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // Sahnedeki TÜM NetworkManager'lar (kökte kalanlar dahil) —
        // birden fazla kopya NGO'yu bozar, üretim hep tek kopyayla başlar
        foreach (NetworkManager nm in FindObjectsByType<NetworkManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroyImmediate(nm.gameObject);
    }
}