// MatchData.cs — Tam güncel versiyon
// RobotStatSheet + ArmorType listeleri eklendi
// DontDestroyOnLoad ile sahneler arası veri taşır

using System.Collections.Generic;
using UnityEngine;

public class MatchData : MonoBehaviour
{
    public static MatchData Instance { get; private set; }

    [Header("Oyuncu Takımı")]
    public List<RobotStatSheet> PlayerTeamSheets  = new();
    public List<ArmorType>      PlayerTeamArmors  = new();

    [Header("Rakip Takımı")]
    public List<RobotStatSheet> OpponentTeamSheets = new();
    public List<ArmorType>      OpponentTeamArmors = new();

    [Header("Maç Ayarları")]
    public Difficulty SelectedDifficulty         = Difficulty.Normal;
    public float      OvertimeDamageMultiplier   = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Oyuncu Robotu Ekle ───────────────────────────────────────────────

    public void AddPlayerRobot(RobotStatSheet sheet, ArmorType armor)
    {
        if (PlayerTeamSheets.Count >= 3)
        {
            Debug.LogWarning("[MatchData] Oyuncu takımı maksimum 3 robot!");
            return;
        }

        PlayerTeamSheets.Add(sheet);
        PlayerTeamArmors.Add(armor);

        Debug.Log($"[MatchData] Oyuncu robotu eklendi " +
                  $"({PlayerTeamSheets.Count}/3) | {sheet}");
    }

    // ── Rakip Robotu Ekle ────────────────────────────────────────────────

    public void AddOpponentRobot(RobotStatSheet sheet, ArmorType armor)
    {
        if (OpponentTeamSheets.Count >= 3)
        {
            Debug.LogWarning("[MatchData] Rakip takımı maksimum 3 robot!");
            return;
        }

        OpponentTeamSheets.Add(sheet);
        OpponentTeamArmors.Add(armor);

        Debug.Log($"[MatchData] Rakip robotu eklendi " +
                  $"({OpponentTeamSheets.Count}/3) | {sheet}");
    }

    // ── Sıfırla ──────────────────────────────────────────────────────────

    public void Reset()
    {
        PlayerTeamSheets.Clear();
        PlayerTeamArmors.Clear();
        OpponentTeamSheets.Clear();
        OpponentTeamArmors.Clear();
        OvertimeDamageMultiplier = 1f;
    }
}