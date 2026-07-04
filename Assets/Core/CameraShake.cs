// CameraShake.cs — Travma tabanlı kamera sarsıntısı
// Görev: Vuruş/patlama anlarında kameraya kısa sarsıntı ekler.
// CameraController ve FirstPersonView pozisyonu LateUpdate'te KURAR;
// bu bileşen [DefaultExecutionOrder] ile onlardan SONRA koşup üstüne
// ofset ekler — iki kamera modunda da çalışır, kalıcı kayma yapmaz.
// Kullanım: CameraShake.Add(0.4f);

using UnityEngine;

[DefaultExecutionOrder(500)]
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;
    private static float trauma;          // 0..1 — karesi alınarak uygulanır

    private const float DECAY     = 1.6f; // Saniyede sönme
    private const float MAX_SHIFT = 0.35f;

    /// <summary>Sarsıntı ekle (0.1 hafif, 0.4 orta, 0.7 patlama).</summary>
    public static void Add(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
        EnsureOnCamera();
    }

    private static void EnsureOnCamera()
    {
        if (instance != null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        instance = cam.GetComponent<CameraShake>();
        if (instance == null)
            instance = cam.gameObject.AddComponent<CameraShake>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        if (trauma <= 0f) return;

        trauma = Mathf.Max(0f, trauma - DECAY * Time.deltaTime);

        // Karesel eğri: küçük travmalar nazik, büyükler sert hissettirir
        float strength = trauma * trauma * MAX_SHIFT;
        transform.position += new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)) * strength;
    }
}
