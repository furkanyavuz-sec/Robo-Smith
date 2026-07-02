// LobbyUI.cs
// Görev: Ana menü UI — Host ol veya IP ile bağlan.
// Basit ve işlevsel, tasarım sonra gelir.

using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject lobbyPanel;

    [Header("Bağlantı")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;

    [Header("Butonlar")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;

    [Header("Durum")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI playerCountText;

    [SerializeField] private Button startMatchButton;
    private void Start()
{
    hostButton?.onClick.AddListener(OnHostClicked);
    clientButton?.onClick.AddListener(OnClientClicked);
    disconnectButton?.onClick.AddListener(OnDisconnectClicked);
    
    // 🌟 OYUNU BAŞLAT BUTONUNUN TIKLAMA TETİĞİNİ BURADA BAĞLIYORUZ
    startMatchButton?.onClick.AddListener(OnStartMatchButtonClicked);

    disconnectButton?.gameObject.SetActive(false);
    startMatchButton?.gameObject.SetActive(false); // Başta görünmez doğsun

    // Varsayılan değerler
    if (ipInputField   != null) ipInputField.text   = "127.0.0.1";
    if (portInputField != null) portInputField.text = "7777";

    UpdateStatus("Bağlantı bekleniyor...");
}

    private void Update()
{
    UpdatePlayerCount();

    // 🌟 OYUNU BAŞLAT BUTONUNU CANLANDIRAN SİBER MOTOR
    if (NetworkManager.Singleton != null && startMatchButton != null)
    {
        // Eğer bu ekranı gören kişi HOST ise VE odada en az 2 oyuncu varsa butonu göster
        if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
        {
            if (!startMatchButton.gameObject.activeSelf)
            {
                startMatchButton.gameObject.SetActive(true);
                Debug.Log("[Lobi] Yeterli oyuncu sayısına ulaşıldı, OYUNU BAŞLAT butonu aktif!");
            }
        }
        else
        {
            // Eğer Client ise veya odada tek başınaysa buton gizli kalır
            if (startMatchButton.gameObject.activeSelf)
            {
                startMatchButton.gameObject.SetActive(false);
            }
        }
    }
}

    // ── Buton Olayları ───────────────────────────────────────────────────
    private void OnStartMatchButtonClicked()
{
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
    {
        Debug.Log("[Lobi] Host 'OYUNU BAŞLAT' butonuna bastı! Savaş arenasına geçiliyor...");
        
        // Tüm bağlı oyuncuları aynı anda sırtlayıp SampleScene'e fırlatır
        NetworkManager.Singleton.SceneManager.LoadScene("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}
    private void OnHostClicked()
{
    string ip   = ipInputField?.text   ?? "127.0.0.1";
    ushort port = ushort.TryParse(portInputField?.text, out ushort p) ? p : (ushort)7777;

    // NetworkManagerSetup yerine direkt transport ayarla
    var transport = NetworkManager.Singleton.GetComponent
                    <Unity.Netcode.Transports.UTP.UnityTransport>();
    transport?.SetConnectionData(ip, port);

    NetworkManager.Singleton.StartHost();
    UpdateStatus($"Host olarak bekleniyor...\nIP: {ip}:{port}");
    SetButtonsConnected(true);
}

private void OnClientClicked()
{
    string ip   = ipInputField?.text   ?? "127.0.0.1";
    ushort port = ushort.TryParse(portInputField?.text, out ushort p) ? p : (ushort)7777;

    var transport = NetworkManager.Singleton.GetComponent
                    <Unity.Netcode.Transports.UTP.UnityTransport>();
    transport?.SetConnectionData(ip, port);

    NetworkManager.Singleton.StartClient();
    UpdateStatus($"Baglanıyor...\n{ip}:{port}");
    SetButtonsConnected(true);
}

private void OnDisconnectClicked()
{
    NetworkManager.Singleton.Shutdown();
    UpdateStatus("Baglanti kesildi.");
    SetButtonsConnected(false);
}

    // ── UI Yardımcıları ──────────────────────────────────────────────────

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void UpdatePlayerCount()
    {
        if (playerCountText == null) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            playerCountText.text = "Oyuncu: 0";
            return;
        }

        int count = NetworkManager.Singleton.ConnectedClients.Count;
        playerCountText.text = $"Oyuncu: {count}/6";
    }

    private void SetButtonsConnected(bool connected)
    {
        hostButton?.gameObject.SetActive(!connected);
        clientButton?.gameObject.SetActive(!connected);
        disconnectButton?.gameObject.SetActive(connected);
    }
}