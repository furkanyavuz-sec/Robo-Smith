// TeamStatusPanel.cs
// Görev: Ekranın kenarında takım robotlarının HP durumunu gösterir.
// ArenaManager'dan robot listesini okur.

using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TeamStatusPanel : MonoBehaviour
{
    [Header("Takım Ayarları")]
    [SerializeField] private int    teamID = 0;        // 0 = oyuncu, 1 = rakip
    [SerializeField] private string teamName = "TAKIM A";
    [SerializeField] private Color  teamColor = new Color(0.2f, 0.4f, 1f);

    [Header("UI Referansları")]
    [SerializeField] private TextMeshProUGUI teamNameText;
    [SerializeField] private TextMeshProUGUI robotCountText;
    [SerializeField] private Transform       robotListParent;  // Vertical Layout Group
    [SerializeField] private GameObject      robotEntryPrefab; // HP bar prefabı

    [Header("Güncelleme")]
    [SerializeField] private float updateInterval = 0.1f;

    private float                    updateTimer = 0f;
    private List<RobotHPEntry>       entries     = new();

    private void Start()
    {
        if (teamNameText != null)
        {
            teamNameText.text  = teamName;
            teamNameText.color = teamColor;
        }
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer > 0f) return;
        updateTimer = updateInterval;

        RefreshRobotList();
    }

    private void RefreshRobotList()
    {
        if (ArenaManager.Instance == null) return;

        List<BattleRobot> robots = teamID == 0
            ? ArenaManager.Instance.GetPlayerRobots()
            : ArenaManager.Instance.GetOpponentRobots();

        // Entry sayısını robota göre ayarla
        while (entries.Count < robots.Count)
            AddEntry();

        // Her entry'i güncelle
        for (int i = 0; i < entries.Count; i++)
        {
            if (i < robots.Count && robots[i] != null && !robots[i].IsDead)
            {
                entries[i].gameObject.SetActive(true);
                entries[i].UpdateHP(
                    robots[i].CurrentHP,
                    robots[i].MaxHP,
                    $"Robot {i + 1}",
                    teamColor
                );
            }
            else
            {
                entries[i].gameObject.SetActive(false);
            }
        }

        // Robot sayısı
        int alive = robots.FindAll(r => r != null && !r.IsDead).Count;
        if (robotCountText != null)
            robotCountText.text = $"🤖 {alive}/{robots.Count}";
    }

    private void AddEntry()
    {
        if (robotEntryPrefab == null || robotListParent == null) return;

        GameObject obj   = Instantiate(robotEntryPrefab, robotListParent);
        RobotHPEntry entry = obj.GetComponent<RobotHPEntry>();
        if (entry != null) entries.Add(entry);
    }
}

// ── RobotHPEntry: Her robot için tek satır HP bar ──────────────────────