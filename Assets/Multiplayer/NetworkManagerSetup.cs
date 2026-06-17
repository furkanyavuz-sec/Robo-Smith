// NetworkManagerSetup.cs
// Görev: Host/Client/Server başlatma ve bağlantı yönetimi.
// Direkt IP bağlantısı kullanır (lokal test için).
// Relay entegrasyonu sonradan eklenecek.

using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManagerSetup : MonoBehaviour
{
    public static NetworkManagerSetup Instance { get; private set; }

    [Header("Bağlantı Ayarları")]
    [SerializeField] private string defaultIP   = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 7777;

    [Header("Sahne Ayarları")]
    [SerializeField] private string preparationScene = "SampleScene";
    [SerializeField] private string arenaScene       = "ArenaScene";

    [Header("Oyuncu Ayarları")]
    [SerializeField] private int maxPlayers = 6;   // 3v3 için

    private UnityTransport transport;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    }

    // ── Bağlantı Metodları ───────────────────────────────────────────────

    public void StartHost(string ip = null, ushort port = 0)
    {
        SetConnectionData(ip, port);
        NetworkManager.Singleton.StartHost();

        Debug.Log($"[Network] Host başlatıldı. IP: {transport.ConnectionData.Address} " +
                  $"Port: {transport.ConnectionData.Port}");
    }

    public void StartClient(string ip = null, ushort port = 0)
    {
        SetConnectionData(ip, port);
        NetworkManager.Singleton.StartClient();

        Debug.Log($"[Network] Client bağlanıyor. IP: {transport.ConnectionData.Address} " +
                  $"Port: {transport.ConnectionData.Port}");
    }

    public void StartServer(string ip = null, ushort port = 0)
    {
        SetConnectionData(ip, port);
        NetworkManager.Singleton.StartServer();

        Debug.Log($"[Network] Server başlatıldı.");
    }

    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();
        Debug.Log("[Network] Bağlantı kesildi.");
    }

    private void SetConnectionData(string ip, ushort port)
    {
        transport.SetConnectionData(
            string.IsNullOrEmpty(ip) ? defaultIP : ip,
            port == 0 ? defaultPort : port
        );
    }

    // ── Sahne Yönetimi ───────────────────────────────────────────────────

    /// <summary>
    /// Server/Host tüm oyuncuları aynı sahneye gönderir.
    /// NGO'nun NetworkSceneManager'ı kullanır — tüm clientlar otomatik geçer.
    /// </summary>
    public void LoadSceneForAll(string sceneName)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        NetworkManager.Singleton.SceneManager.LoadScene(
            sceneName, LoadSceneMode.Single);
    }

    public void LoadPreparationScene() => LoadSceneForAll(preparationScene);
    public void LoadArenaScene()       => LoadSceneForAll(arenaScene);

    // ── Callback'ler ─────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback    += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback   += OnClientDisconnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback    -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback   -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[Network] Client bağlandı: {clientId} | " +
                  $"Toplam: {NetworkManager.Singleton.ConnectedClients.Count}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[Network] Client ayrıldı: {clientId}");
    }
}