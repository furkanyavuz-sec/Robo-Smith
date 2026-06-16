// ArenaTimer.cs
// Görev: Arena sahnesi için geri sayım.
// GameManager'dan Arena/Overtime fazını okur.

using UnityEngine;
using TMPro;

public class ArenaTimer : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI phaseText;

    [Header("Renkler")]
    [SerializeField] private Color normalColor   = Color.white;
    [SerializeField] private Color warningColor  = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private Color overtimeColor = new Color(1f, 0.2f, 0.2f);

    private RectTransform timerRect;
    private Vector3       originalScale;

    private void Awake()
    {
        timerRect     = timerText?.GetComponent<RectTransform>();
        originalScale = timerRect != null ? timerRect.localScale : Vector3.one;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        float     timeLeft = GameManager.Instance.PhaseTimer;
        GamePhase phase    = GameManager.Instance.CurrentPhase;

        UpdateTimer(timeLeft, phase);
        UpdatePhase(phase);
        UpdateVisual(timeLeft, phase);
    }

    private void UpdateTimer(float timeLeft, GamePhase phase)
    {
        if (timerText == null) return;

        if (phase == GamePhase.Overtime)
        {
            timerText.text = "OVERTIME";
            return;
        }

        int minutes = Mathf.FloorToInt(timeLeft / 60f);
        int seconds = Mathf.FloorToInt(timeLeft % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdatePhase(GamePhase phase)
    {
        if (phaseText == null) return;
        phaseText.text = phase switch
        {
            GamePhase.Arena    => "⚔️ ARENA",
            GamePhase.Overtime => "🔥 OVERTIME",
            _                  => ""
        };
    }

    private void UpdateVisual(float timeLeft, GamePhase phase)
    {
        if (timerText == null) return;

        if (phase == GamePhase.Overtime)
        {
            // Overtime: sürekli kırmızı pulse
            float pulse = Mathf.PingPong(Time.time * 2f, 1f);
            timerText.color = Color.Lerp(overtimeColor, Color.white, pulse * 0.3f);
            if (timerRect != null)
                timerRect.localScale = originalScale * (1f + Mathf.PingPong(Time.time, 0.05f));
        }
        else if (timeLeft <= 10f)
        {
            timerText.color = criticalColor;
            float pulse = Mathf.PingPong(Time.time * 4f, 0.05f);
            if (timerRect != null)
                timerRect.localScale = originalScale * (1f + pulse);
        }
        else if (timeLeft <= 30f)
        {
            timerText.color = warningColor;
            if (timerRect != null)
                timerRect.localScale = originalScale;
        }
        else
        {
            timerText.color = normalColor;
            if (timerRect != null)
                timerRect.localScale = originalScale;
        }
    }
}