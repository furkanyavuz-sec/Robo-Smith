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

    private void Start()
    {
        hostButton?.onClick.AddListener(OnHostClicked);
        clientButton?.onClick.AddListener(OnClientClicked);
        disconnectButton?.onClick.AddListener(OnDisconnectClicked);

        disconnectButton?.gameObject.SetActive(false);

        // Varsayılan değerler
        if (ipInputField   != null) ipInputField.text   = "127.0.0.1";
        if (portInputField != null) portInputField.text = "7777";

        UpdateStatus("Bağlantı bekleniyor...");
    }

    private void Update()
    {
        UpdatePlayerCount();
    }

    // ── Buton Olayları ───────────────────────────────────────────────────

    private void OnHostClicked()
    {
        string ip   = ipInputField?.text   ?? "127.0.0.1";
        ushort port = ushort.TryParse(portInputField?.text, out ushort p) ? p : (ushort)7777;

        NetworkManagerSetup.Instance?.StartHost(ip, port);

        UpdateStatus($"Host olarak bekleniyor...\nIP: {ip}:{port}");
        SetButtonsConnected(true);
    }

    private void OnClientClicked()
    {
        string ip   = ipInputField?.text   ?? "127.0.0.1";
        ushort port = ushort.TryParse(portInputField?.text, out ushort p) ? p : (ushort)7777;

        NetworkManagerSetup.Instance?.StartClient(ip, port);

        UpdateStatus($"Bağlanıyor...\n{ip}:{port}");
        SetButtonsConnected(true);
    }

    private void OnDisconnectClicked()
    {
        NetworkManagerSetup.Instance?.Disconnect();
        UpdateStatus("Bağlantı kesildi.");
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