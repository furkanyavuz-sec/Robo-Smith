// MatchResultUI.cs
// Görev: Maç bitince animasyonlu sonuç ekranı gösterir.
// Kazanıldı/Kaybedildi + hayatta kalan robotlar + toplam hasar.

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MatchResultUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject      resultPanel;
    [SerializeField] private TextMeshProUGUI resultTitleText;   // "KAZANDIN!" / "KAYBETTİN!"
    [SerializeField] private TextMeshProUGUI resultSubText;     // Detaylar
    [SerializeField] private TextMeshProUGUI statsText;         // İstatistikler
    [SerializeField] private Image           resultBackground;

    [Header("Butonlar")]
    [SerializeField] private Button rematchButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Animasyon")]
    [SerializeField] private float titleAnimDuration = 1f;
    [SerializeField] private float statsDelay        = 0.5f;

    [Header("Renkler")]
    [SerializeField] private Color winColor  = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color loseColor = new Color(0.8f, 0.2f, 0.2f);
    [SerializeField] private Color drawColor = new Color(0.8f, 0.8f, 0.2f);

    private void Start()
    {
        resultPanel?.SetActive(false);

        if (rematchButton != null)
            rematchButton.onClick.AddListener(OnRematch);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    /// <summary>ArenaManager maç bitince çağırır.</summary>
    public void ShowResult(bool playerWon, int playerRobotsAlive,
                           int opponentRobotsAlive, float matchDuration)
    {
        resultPanel?.SetActive(true);
        StartCoroutine(AnimateResult(playerWon, playerRobotsAlive,
                                     opponentRobotsAlive, matchDuration));
    }

    private IEnumerator AnimateResult(bool playerWon, int playerAlive,
                                       int opponentAlive, float duration)
    {
        // Arka plan rengi
        if (resultBackground != null)
        {
            Color bgColor = playerWon
                ? new Color(winColor.r,  winColor.g,  winColor.b,  0.85f)
                : new Color(loseColor.r, loseColor.g, loseColor.b, 0.85f);
            resultBackground.color = bgColor;
        }

        // Başlık animasyonu
        if (resultTitleText != null)
        {
            resultTitleText.text  = playerWon ? "🏆 KAZANDIN!" : "💀 KAYBETTİN!";
            resultTitleText.color = playerWon ? winColor : loseColor;
            resultTitleText.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < titleAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / titleAnimDuration;

                // Elastic scale animasyonu
                float scale = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.2f;
                if (t > 0.7f) scale = Mathf.Lerp(1.2f, 1f, (t - 0.7f) / 0.3f);
                resultTitleText.transform.localScale = Vector3.one * scale;

                yield return null;
            }

            resultTitleText.transform.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(statsDelay);

        // Alt metin
        if (resultSubText != null)
        {
            resultSubText.text = playerWon
                ? $"Rakibin tüm robotlarını yok ettin!"
                : $"Tüm robotların yok edildi!";
        }

        // İstatistikler
        if (statsText != null)
        {
            float minutes = Mathf.FloorToInt(duration / 60f);
            float seconds = Mathf.FloorToInt(duration % 60f);

            statsText.text =
                $"─────────────────\n" +
                $"🤖 Hayatta Kalan Robotlar\n" +
                $"   Sen: {playerAlive}  |  Rakip: {opponentAlive}\n\n" +
                $"⏱️ Maç Süresi: {minutes:00}:{seconds:00}\n" +
                $"─────────────────";

            // Stats metni kayarak gelsin
            statsText.transform.localPosition += Vector3.down * 50f;
            float elapsed = 0f;
            Vector3 targetPos = statsText.transform.localPosition + Vector3.up * 50f;

            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                statsText.transform.localPosition = Vector3.Lerp(
                    statsText.transform.localPosition, targetPos,
                    elapsed / 0.5f
                );
                yield return null;
            }
        }
    }

    private void OnRematch()
    {
        if (MatchData.Instance != null) MatchData.Instance.Reset();
        SceneManager.LoadScene("SampleScene");
    }

    private void OnMainMenu()
    {
        if (MatchData.Instance != null) MatchData.Instance.Reset();
        SceneManager.LoadScene("MainMenu");
    }
}