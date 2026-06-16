// ArenaManager.cs — Tam güncel versiyon

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ArenaManager : MonoBehaviour
{
    public static ArenaManager Instance { get; private set; }

    [Header("Spawn Noktaları")]
    [SerializeField] private Transform[] playerSpawnPoints;
    [SerializeField] private Transform[] opponentSpawnPoints;

    [Header("Robot Prefabı")]
    [SerializeField] private GameObject robotPrefab;

    private List<BattleRobot> playerRobots   = new();
    private List<BattleRobot> opponentRobots = new();
    public List<BattleRobot> GetPlayerRobots()   => playerRobots;
    public List<BattleRobot> GetOpponentRobots() => opponentRobots;
    private bool matchOver = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => SpawnRobots();

    private void SpawnRobots()
{
    if (MatchData.Instance == null)
    {
        Debug.LogWarning("[ArenaManager] MatchData yok, varsayilan robotlar spawn ediliyor.");
        SpawnDefaultRobots();
        return;
    }

    // MatchData boşsa varsayılan robot spawn et
    if (MatchData.Instance.PlayerTeamSheets.Count == 0)
    {
        Debug.LogWarning("[ArenaManager] PlayerTeam bos, varsayilan robot spawn ediliyor.");
        SpawnDefaultRobots();
        return;
    }

    SpawnTeam(
        MatchData.Instance.PlayerTeamSheets,
        MatchData.Instance.PlayerTeamArmors,
        playerSpawnPoints, 0, playerRobots
    );

    SpawnTeam(
        MatchData.Instance.OpponentTeamSheets,
        MatchData.Instance.OpponentTeamArmors,
        opponentSpawnPoints, 1, opponentRobots
    );

    Debug.Log($"[ArenaManager] Oyuncu: {playerRobots.Count} | " +
              $"Rakip: {opponentRobots.Count} — SAVAS BASLIYOR!");
}

    private void SpawnDefaultRobots()
{
    // Varsayilan stat sheet
    RobotStatSheet defaultPlayerSheet = new RobotStatSheet
    {
        HP  = 150,
        ATK = 80,
        SPD = 60,
        DEF = 40
    };

    RobotStatSheet defaultOpponentSheet = new RobotStatSheet
    {
        HP  = 120,
        ATK = 70,
        SPD = 50,
        DEF = 30
    };

    // Varsayilan silah ekle
    defaultPlayerSheet.equippedWeapons[0]   = WeaponData.Create(ItemType.Sword);
    defaultPlayerSheet.weaponCount          = 1;
    defaultOpponentSheet.equippedWeapons[0] = WeaponData.Create(ItemType.Laser);
    defaultOpponentSheet.weaponCount        = 1;

    // Oyuncu robotu spawn et
    if (playerSpawnPoints.Length > 0)
    {
        GameObject obj = Instantiate(
            robotPrefab,
            playerSpawnPoints[0].position,
            playerSpawnPoints[0].rotation
        );
        BattleRobot robot = obj.GetComponent<BattleRobot>();
        robot.Initialize(defaultPlayerSheet, ArmorType.HeavyPlate, 0);
        playerRobots.Add(robot);
    }

    // Rakip robotu spawn et
    if (opponentSpawnPoints.Length > 0)
    {
        GameObject obj = Instantiate(
            robotPrefab,
            opponentSpawnPoints[0].position,
            opponentSpawnPoints[0].rotation
        );
        BattleRobot robot = obj.GetComponent<BattleRobot>();
        robot.Initialize(defaultOpponentSheet, ArmorType.EnergyShield, 1);
        opponentRobots.Add(robot);
    }

    Debug.Log($"[ArenaManager] Varsayilan robotlar spawn edildi. " +
              $"Oyuncu: {playerRobots.Count} | Rakip: {opponentRobots.Count}");
}
    private void SpawnTeam(
        List<RobotStatSheet> sheets,
        List<ArmorType>      armors,
        Transform[]          spawnPoints,
        int                  teamId,
        List<BattleRobot>    teamList)
    {
        for (int i = 0; i < sheets.Count && i < spawnPoints.Length; i++)
        {
            GameObject obj = Instantiate(
                robotPrefab,
                spawnPoints[i].position,
                spawnPoints[i].rotation
            );

            BattleRobot robot = obj.GetComponent<BattleRobot>();
            robot.Initialize(sheets[i], armors[i], teamId);
            teamList.Add(robot);
        }
    }

    public BattleRobot GetClosestEnemy(BattleRobot requester)
    {
        List<BattleRobot> enemies = requester.TeamID == 0
                                  ? opponentRobots
                                  : playerRobots;

        return enemies
            .Where(r => r != null && !r.IsDead)
            .OrderBy(r => Vector3.Distance(
                requester.transform.position, r.transform.position))
            .FirstOrDefault();
    }

    public void OnRobotDestroyed(BattleRobot robot)
    {
        if (matchOver) return;

        playerRobots.RemoveAll(r   => r == null || r.IsDead);
        opponentRobots.RemoveAll(r => r == null || r.IsDead);

        Debug.Log($"[ArenaManager] Kalan → Oyuncu: {playerRobots.Count} | " +
                  $"Rakip: {opponentRobots.Count}");

        if (playerRobots.Count == 0 || opponentRobots.Count == 0)
            DetermineWinner();
    }

    private void DetermineWinner()
{
    matchOver = true;
    bool playerWon = opponentRobots.Count == 0 && playerRobots.Count > 0;

    Debug.Log(playerWon
        ? "<color=green>[ArenaManager] 🏆 OYUNCU KAZANDI!</color>"
        : "<color=red>[ArenaManager] 💀 RAKİP KAZANDI!</color>");

    // HUD'a bildir
    ArenaHUDManager.Instance?.OnMatchOver(
        playerWon,
        playerRobots.Count,
        opponentRobots.Count
    );

    GameManager.Instance?.OnMatchOver(playerWon);
}
}