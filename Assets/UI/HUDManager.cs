// HUDManager.cs
// Görev: Tüm UI bileşenlerini yönetir.
// GamePhase değişince doğru panelleri açar/kapatır.

using UnityEngine;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Preparation HUD")]
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private GameObject robotStatusPanel;
    [SerializeField] private GameObject interactPromptPanel;

    [Header("Animasyon")]
    [SerializeField] private GameObject countdownPanel;  // 3-2-1 başlangıç

    private GamePhase lastPhase = GamePhase.Lobby;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        GamePhase current = GameManager.Instance.CurrentPhase;
        if (current == lastPhase) return;

        lastPhase = current;
        OnPhaseChanged(current);
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        // Tüm panelleri kapat
        timerPanel?.SetActive(false);
        robotStatusPanel?.SetActive(false);
        interactPromptPanel?.SetActive(false);

        switch (phase)
        {
            case GamePhase.Preparation:
                timerPanel?.SetActive(true);
                robotStatusPanel?.SetActive(true);
                interactPromptPanel?.SetActive(true);
                break;

            case GamePhase.Arena:
            case GamePhase.Overtime:
                // Arena HUD ayrı sahnede — burada kapalı
                break;
        }
    }

    /// <summary>GameManager faz değişimini buraya bildirir.</summary>
    public void NotifyPhaseChange(GamePhase phase) => OnPhaseChanged(phase);
}