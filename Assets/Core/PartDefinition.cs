// PartDefinition.cs
// Görev: Hangi ItemType hangi stat'ı ne kadar artırır?
// Inspector'dan yeni parça eklemek için sadece bu listeye
// bir satır eklenir — RobotChassis kodu değişmez.

using UnityEngine;

[System.Serializable]
public class PartDefinition
{
    [Tooltip("Hangi item tipi bu yuvaya takılır?")]
    public ItemType acceptedType;

    [Tooltip("Bu parçadan kaç adet gerekli?")]
    public int requiredCount = 1;

    [Tooltip("Her parça eklenince maxHP ne kadar artar?")]
    public int hpBonus      = 0;

    [Tooltip("Her parça eklenince attackPower ne kadar artar?")]
    public int attackBonus  = 0;

    [Tooltip("Her parça eklenince moveSpeed ne kadar artar?")]
    public int speedBonus   = 0;

    // Kaç adet takıldığını runtime'da takip eder
    [HideInInspector] public int installedCount = 0;

    public bool IsComplete => installedCount >= requiredCount;
    public bool Accepts(ItemType type) => type == acceptedType;
}