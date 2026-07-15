// TimerUI.cs
// Görev: Ekranın üst ortasında büyük geri sayım gösterir.
// GameManager'dan süreyi okur.
// Son 30 saniyede kırmızıya döner ve titrer.

using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI phaseText;   // "HAZIRLIK" / "ARENA" / "OVERTIME"

    [Header("Renk Ayarları")]
    [SerializeField] private Color normalColor   = Color.white;
    [SerializeField] private Color warningColor  = new Color(1f, 0.6f, 0f);  // Turuncu
    [SerializeField] private Color criticalColor = Color.red;

    [Header("Uyarı Eşikleri")]
    [SerializeField] private float warningThreshold  = 60f;  // 1 dakika kala turuncu
    [SerializeField] private float criticalThreshold = 30f;  // 30 saniye kala kırmızı

    private RectTransform rectTransform;
    private Vector3       originalScale;
    private int           lastWholeSecond = int.MaxValue;

    private void Awake()
    {
        rectTransform = timerText?.GetComponent<RectTransform>();
        originalScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        float timeLeft = GameManager.Instance.PhaseTimer;
        UpdateTimerDisplay(timeLeft);
        UpdatePhaseDisplay();
        UpdateVisualWarning(timeLeft);
        UpdateCountdownAlerts(timeLeft);
    }

    /// <summary>
    /// Hazırlık biterken anons + son 10 sn'de saniye bip'i. Timer MP'de
    /// server'dan senkron — iki makinede de aynı anda çalar.
    /// </summary>
    private void UpdateCountdownAlerts(float timeLeft)
    {
        if (GameManager.Instance.CurrentPhase != GamePhase.Preparation)
        {
            lastWholeSecond = int.MaxValue;
            return;
        }

        int sec = Mathf.CeilToInt(timeLeft);
        if (sec == lastWholeSecond) return;
        lastWholeSecond = sec;

        if (sec == 60)
            RaidAnnouncer.Show("SON 1 DAKİKA — ROBOTLARINI TAMAMLA!",
                new Color(1f, 0.6f, 0f), 3f);
        else if (sec == 30)
            RaidAnnouncer.Show("SON 30 SANİYE!", Color.red, 2.5f);
        else if (sec <= 10 && sec > 0)
            Sfx.Play(Sfx.Id.Announce, 0.3f);
    }

    private void UpdateTimerDisplay(float timeLeft)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timeLeft / 60f);
        int seconds = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdatePhaseDisplay()
    {
        if (phaseText == null) return;

        phaseText.text = GameManager.Instance.CurrentPhase switch
        {
            GamePhase.Preparation => "HAZIRLIK",
            GamePhase.Arena       => "ARENA",
            GamePhase.Overtime    => "OVERTIME",
            _                     => ""
        };
    }

    private void UpdateVisualWarning(float timeLeft)
    {
        if (timerText == null) return;

        if (timeLeft <= criticalThreshold)
        {
            timerText.color = criticalColor;

            // Titreme efekti
            float pulse = Mathf.PingPong(Time.time * 4f, 0.05f);
            if (rectTransform != null)
                rectTransform.localScale = originalScale * (1f + pulse);
        }
        else if (timeLeft <= warningThreshold)
        {
            timerText.color = warningColor;
            if (rectTransform != null)
                rectTransform.localScale = originalScale;
        }
        else
        {
            timerText.color = normalColor;
            if (rectTransform != null)
                rectTransform.localScale = originalScale;
        }
    }
}