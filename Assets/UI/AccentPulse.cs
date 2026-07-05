// AccentPulse.cs
// Vurgu çizgisi/ışığı için yavaş alpha nabzı (ping-pong).

using UnityEngine;
using UnityEngine.UI;

public class AccentPulse : MonoBehaviour
{
    [SerializeField] private float minAlpha = 0.35f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private float speed    = 1.2f;

    private Graphic graphic;

    private void Awake() => graphic = GetComponent<Graphic>();

    private void Update()
    {
        if (graphic == null) return;

        float t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
        Color c = graphic.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        graphic.color = c;
    }
}
