// RobotStats.cs
// Görev: Robotun savaş istatistiklerini tutar.
// Neden ayrı class? Hafta 5'te BattleRobot.cs bu veriyi
// doğrudan okuyacak. RobotChassis sadece yazar, BattleRobot okur.
// NGO'da NetworkVariable<int> olacak alanlar yorum satırında işaretlendi.

using UnityEngine;

[System.Serializable]
public class RobotStats
{
    [Header("Temel İstatistikler")]
    public int maxHP        = 0;   // NetworkVariable<int> — NGO'da
    public int attackPower  = 0;   // NetworkVariable<int> — NGO'da
    public int moveSpeed    = 0;   // NetworkVariable<int> — NGO'da

    public void Reset()
    {
        maxHP       = 0;
        attackPower = 0;
        moveSpeed   = 0;
    }

    public override string ToString() =>
        $"HP: {maxHP} | ATK: {attackPower} | SPD: {moveSpeed}";
}