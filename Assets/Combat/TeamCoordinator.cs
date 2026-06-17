// TeamCoordinator.cs
// Görev: Aynı takımdaki robotları koordine eder.
// - Ortak hedef belirler
// - Rol dağılımı yapar (saldırgan, savunma, destek)
// - Zayıf takım arkadaşını koruma emri verir

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RobotRole { Aggressor, Defender, Support }

public class TeamCoordinator : MonoBehaviour
{
    public static TeamCoordinator Instance { get; private set; }

    [Header("Koordinasyon Ayarları")]
    [SerializeField] private float updateInterval     = 1f;
    [SerializeField] private float protectHPThreshold = 0.4f; // Bu HP'nin altı korunur

    // Takım bazlı ortak hedef
    private Dictionary<int, BattleRobot> teamFocusTargets = new();

    // Robot rolleri
    private Dictionary<BattleRobot, RobotRole> robotRoles = new();

    private float updateTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer > 0f) return;
        updateTimer = updateInterval;

        UpdateTeamFocusTargets();
        UpdateRobotRoles();
    }

    // ── Ortak Hedef ──────────────────────────────────────────────────────

    private void UpdateTeamFocusTargets()
    {
        if (ArenaManager.Instance == null) return;

        // Her takım için ortak hedef belirle
        UpdateTeamFocus(0, ArenaManager.Instance.GetEnemiesOf(0));
        UpdateTeamFocus(1, ArenaManager.Instance.GetEnemiesOf(1));
    }

    private void UpdateTeamFocus(int teamID, List<BattleRobot> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            teamFocusTargets[teamID] = null;
            return;
        }

        // En zayıf düşmana odaklan — "focus fire" taktiği
        BattleRobot weakest = enemies
            .Where(e => e != null && !e.IsDead)
            .OrderBy(e => (float)e.CurrentHP / e.MaxHP)
            .FirstOrDefault();

        teamFocusTargets[teamID] = weakest;

        if (weakest != null)
            Debug.Log($"[TeamCoordinator] Takim {teamID} odak hedefi: " +
                      $"{weakest.name} (HP: {weakest.CurrentHP}/{weakest.MaxHP})");
    }

    // ── Rol Dağılımı ─────────────────────────────────────────────────────

    private void UpdateRobotRoles()
    {
        if (ArenaManager.Instance == null) return;

        AssignRoles(ArenaManager.Instance.GetPlayerRobots());
        AssignRoles(ArenaManager.Instance.GetOpponentRobots());
    }

    private void AssignRoles(List<BattleRobot> team)
    {
        if (team == null || team.Count == 0) return;

        // HP'ye göre sırala
        var sorted = team
            .Where(r => r != null && !r.IsDead)
            .OrderBy(r => (float)r.CurrentHP / r.MaxHP)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            float hpRatio = (float)sorted[i].CurrentHP / sorted[i].MaxHP;

            RobotRole role;
            if (hpRatio < protectHPThreshold)
                role = RobotRole.Defender;    // Zayıf robot savunmada
            else if (i == sorted.Count - 1)
                role = RobotRole.Aggressor;   // En sağlıklı robot saldırıda
            else
                role = RobotRole.Support;     // Diğerleri destek

            robotRoles[sorted[i]] = role;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────

    public BattleRobot GetTeamFocusTarget(int teamID)
    {
        return teamFocusTargets.TryGetValue(teamID, out BattleRobot target)
            ? target : null;
    }

    public RobotRole GetRole(BattleRobot robot)
    {
        return robotRoles.TryGetValue(robot, out RobotRole role)
            ? role : RobotRole.Aggressor;
    }

    /// <summary>Korunması gereken takım arkadaşı var mı?</summary>
    public BattleRobot GetAllyToProtect(BattleRobot robot)
    {
        List<BattleRobot> allies = robot.TeamID == 0
            ? ArenaManager.Instance?.GetPlayerRobots()
            : ArenaManager.Instance?.GetOpponentRobots();

        if (allies == null) return null;

        return allies
            .Where(a => a != null && a != robot && !a.IsDead &&
                        (float)a.CurrentHP / a.MaxHP < protectHPThreshold)
            .OrderBy(a => (float)a.CurrentHP / a.MaxHP)
            .FirstOrDefault();
    }
}