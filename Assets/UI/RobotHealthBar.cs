// RobotHealthBar.cs
// Görev: Robotun üstünde dünya uzayında (World Space) HP barı gösterir.
//        HP azaldıkça renk yeşilden kırmızıya kayar.
//        Canvas her zaman kameraya bakar (Billboard efekti).

using UnityEngine;
using UnityEngine.UI;

public class RobotHealthBar : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private Canvas    worldCanvas;
    [SerializeField] private Image     fillImage;
    [SerializeField] private Transform barPivot;     // Kameraya bakan pivot

    [Header("Renkler")]
    [SerializeField] private Color fullColor  = Color.green;
    [SerializeField] private Color emptyColor = Color.red;

    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;

        if (worldCanvas != null)
            worldCanvas.renderMode = RenderMode.WorldSpace;
    }

    private void LateUpdate()
    {
        // Billboard: bar her zaman kameraya baksın
        if (barPivot != null && mainCam != null)
            barPivot.forward = mainCam.transform.forward;
    }

    /// <summary>BattleRobot her hasar alındığında çağırır.</summary>
    public void UpdateBar(int currentHP, int maxHP)
    {
        if (fillImage == null) return;

        float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
        fillImage.fillAmount = ratio;
        fillImage.color      = Color.Lerp(emptyColor, fullColor, ratio);
    }
}