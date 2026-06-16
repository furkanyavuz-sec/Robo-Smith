// OvertimeUI.cs
// Görev: Overtime başlayınca ekranda uyarı gösterir.
// Üstte "OVERTIME!" yazısı + ekran kenarları kırmızı pulse.

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OvertimeUI : MonoBehaviour
{
    [Header("Overtime Yazısı")]
    [SerializeField] private GameObject      overtimePanel;
    [SerializeField] private TextMeshProUGUI overtimeText;

    [Header("Kenar Pulse Efekti")]
    [SerializeField] private Image borderImage;   // Ekran kenarını kaplayan Image

    [Header("Hasar Çarpanı")]
    [SerializeField] private TextMeshProUGUI damageMultText;

    [Header("Animasyon")]
    [SerializeField] private float flashSpeed   = 2f;
    [SerializeField] private float pulseSpeed   = 1.5f;
    [SerializeField] private float maxBorderAlpha = 0.4f;

    private bool isOvertime = false;

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool overtime = GameManager.Instance.CurrentPhase == GamePhase.Overtime;

        if (overtime && !isOvertime) EnterOvertime();
        if (!overtime && isOvertime) ExitOvertime();

        if (!isOvertime) return;

        AnimateOvertime();
    }

    private void EnterOvertime()
    {
        isOvertime = true;
        overtimePanel?.SetActive(true);
        Debug.Log("[OvertimeUI] OVERTIME başladı!");
    }

    private void ExitOvertime()
    {
        isOvertime = false;
        overtimePanel?.SetActive(false);
        if (borderImage != null)
            borderImage.color = new Color(1f, 0f, 0f, 0f);
    }

    private void AnimateOvertime()
    {
        // "OVERTIME!" yazısı titreşim
        if (overtimeText != null)
        {
            float flash = Mathf.PingPong(Time.time * flashSpeed, 1f);
            overtimeText.color = Color.Lerp(Color.red, Color.white, flash);

            float scale = 1f + Mathf.PingPong(Time.time * pulseSpeed, 0.1f);
            overtimeText.transform.localScale = Vector3.one * scale;
        }

        // Kenar pulse
        if (borderImage != null)
        {
            float alpha = Mathf.PingPong(Time.time * pulseSpeed, maxBorderAlpha);
            borderImage.color = new Color(1f, 0f, 0f, alpha);
        }

        // Hasar çarpanı
        if (damageMultText != null && MatchData.Instance != null)
        {
            float mult = MatchData.Instance.OvertimeDamageMultiplier;
            damageMultText.text = $"💥 Hasar Çarpanı: ×{mult:F2}";
            damageMultText.color = mult > 2f ? Color.red : Color.white;
        }
    }
}