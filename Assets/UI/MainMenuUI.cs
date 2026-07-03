// MainMenuUI.cs
// Görev: Ana menü akışı.
//   TEK KİŞİLİK  → zorluk seç → SampleScene (offline, NGO'suz)
//   MULTIPLAYER  → mevcut LobbyUI panelini açar (Host/Bağlan orada)
//   NASIL OYNANIR → kontroller ve üretim rehberi
//   ÇIKIŞ        → oyunu kapatır
// Referanslar MainMenuGenerator tarafından otomatik bağlanır.

using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject difficultyPanel;
    [SerializeField] private GameObject howToPanel;

    [Header("Ana Butonlar")]
    [SerializeField] private Button singlePlayerButton;
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button howToButton;
    [SerializeField] private Button quitButton;

    [Header("Zorluk Butonları")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button difficultyBackButton;

    [Header("Diğer")]
    [SerializeField] private Button howToBackButton;
    [SerializeField] private Button lobbyBackButton;   // Lobby açıkken "← MENÜ"

    [Header("Sahne")]
    [SerializeField] private string gameSceneName = "SampleScene";

    private GameObject lobbyCanvas;   // LobbyUI'nin kök objesi — runtime bulunur

    private void Start()
    {
        // Lobby canvas'ını bul ve başlangıçta gizle — menü önde
        LobbyUI lobby = FindFirstObjectByType<LobbyUI>(FindObjectsInactive.Include);
        if (lobby != null)
        {
            lobbyCanvas = lobby.gameObject;
            lobbyCanvas.SetActive(false);
        }

        // Buton bağlantıları
        singlePlayerButton?.onClick.AddListener(ShowDifficulty);
        multiplayerButton?.onClick.AddListener(ShowMultiplayer);
        howToButton?.onClick.AddListener(ShowHowTo);
        quitButton?.onClick.AddListener(QuitGame);

        easyButton?.onClick.AddListener(()   => StartSinglePlayer(Difficulty.Easy));
        normalButton?.onClick.AddListener(() => StartSinglePlayer(Difficulty.Normal));
        hardButton?.onClick.AddListener(()   => StartSinglePlayer(Difficulty.Hard));

        difficultyBackButton?.onClick.AddListener(BackToMain);
        howToBackButton?.onClick.AddListener(BackToMain);
        lobbyBackButton?.onClick.AddListener(BackFromLobby);

        BackToMain();
        lobbyBackButton?.gameObject.SetActive(false);
    }

    // ── Akış ─────────────────────────────────────────────────────────────

    private void ShowDifficulty()
    {
        mainPanel?.SetActive(false);
        difficultyPanel?.SetActive(true);
        howToPanel?.SetActive(false);
    }

    private void ShowHowTo()
    {
        mainPanel?.SetActive(false);
        difficultyPanel?.SetActive(false);
        howToPanel?.SetActive(true);
    }

    private void BackToMain()
    {
        mainPanel?.SetActive(true);
        difficultyPanel?.SetActive(false);
        howToPanel?.SetActive(false);
    }

    private void StartSinglePlayer(Difficulty difficulty)
    {
        GameSettings.SetDifficulty(difficulty);
        Debug.Log($"[MainMenu] Tek kişilik oyun başlıyor — Zorluk: {difficulty}");
        SceneManager.LoadScene(gameSceneName);
    }

    private void ShowMultiplayer()
    {
        mainPanel?.SetActive(false);
        difficultyPanel?.SetActive(false);
        howToPanel?.SetActive(false);

        lobbyCanvas?.SetActive(true);
        lobbyBackButton?.gameObject.SetActive(true);
    }

    private void BackFromLobby()
    {
        // Bağlantı açıksa kapat — menüye temiz dön
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        lobbyCanvas?.SetActive(false);
        lobbyBackButton?.gameObject.SetActive(false);
        BackToMain();
    }

    private void QuitGame()
    {
        Debug.Log("[MainMenu] Çıkış.");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
