// ArenaHUDManager.cs
// Görev: Arena sahnesi UI bileşenlerini yönetir.
// ArenaManager ile konuşarak maç sonucunu MatchResultUI'ya iletir.

using UnityEngine;

public class ArenaHUDManager : MonoBehaviour
{
    public static ArenaHUDManager Instance { get; private set; }

    [Header("UI Bileşenleri")]
    [SerializeField] private ArenaTimer      arenaTimer;
    [SerializeField] private TeamStatusPanel playerTeamPanel;
    [SerializeField] private TeamStatusPanel opponentTeamPanel;
    [SerializeField] private OvertimeUI      overtimeUI;
    [SerializeField] private MatchResultUI   matchResultUI;

    private float matchStartTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        matchStartTime = Time.time;
    }

    /// <summary>ArenaManager maç bitince çağırır.</summary>
    public void OnMatchOver(bool playerWon, int playerAlive, int opponentAlive)
    {
        float duration = Time.time - matchStartTime;
        matchResultUI?.ShowResult(playerWon, playerAlive, opponentAlive, duration);
    }
}