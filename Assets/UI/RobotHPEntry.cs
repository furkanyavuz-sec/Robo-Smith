// RobotHPEntry.cs
// Görev: Kenar paneldeki her robot için tek satır HP gösterimi.

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RobotHPEntry : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI robotNameText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image           hpFill;

    public void UpdateHP(int current, int max, string name, Color teamColor)
    {
        if (robotNameText != null) robotNameText.text = name;

        float ratio = max > 0 ? (float)current / max : 0f;

        if (hpText != null)
            hpText.text = $"{current}/{max}";

        if (hpFill != null)
        {
            hpFill.fillAmount = ratio;
            hpFill.color      = Color.Lerp(Color.red, teamColor, ratio);
        }
    }
}