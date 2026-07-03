// DeathExplosion.cs
// Görev: Robot yok edildiğinde parça savrulması + kısa flaş efekti.
// Prefab gerektirmez — parçalar koddan üretilir, fizikle savrulur,
// yere düşüp küçülerek kaybolur.
// Kullanım: DeathExplosion.Spawn(pozisyon, takımRengi);

using UnityEngine;

public static class DeathExplosion
{
    private const int   PieceCount    = 10;
    private const float PieceLifetime = 1.2f;

    public static void Spawn(Vector3 position, Color teamColor) =>
        SpawnInternal(position, teamColor, PieceCount, 0.12f, 0.28f, 2.4f);

    /// <summary>Roket çarpması gibi küçük patlamalar için hafif versiyon.</summary>
    public static void SmallBlast(Vector3 position, Color color) =>
        SpawnInternal(position, color, 5, 0.08f, 0.16f, 1.4f);

    private static void SpawnInternal(Vector3 position, Color teamColor,
        int pieceCount, float minSize, float maxSize, float flashScale)
    {
        Vector3 center = position + Vector3.up * 0.5f;

        // Savrulan parçalar — takım rengi ve koyu metal karışımı
        for (int i = 0; i < pieceCount; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Enkaz";
            piece.transform.position = center + Random.insideUnitSphere * 0.35f;
            piece.transform.rotation = Random.rotation;
            piece.transform.localScale = Vector3.one * Random.Range(minSize, maxSize);

            Color c = i % 3 == 0
                ? new Color(0.18f, 0.18f, 0.20f)              // Koyu metal
                : Color.Lerp(teamColor, Color.black, Random.Range(0f, 0.35f));
            piece.GetComponent<Renderer>().sharedMaterial =
                StationVisuals.GetMaterial(c);

            Rigidbody rb = piece.AddComponent<Rigidbody>();
            rb.mass = 0.3f;

            Vector3 dir = (Random.insideUnitSphere + Vector3.up * 1.4f).normalized;
            rb.AddForce(dir * Random.Range(3f, 6f), ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * Random.Range(4f, 10f),
                ForceMode.VelocityChange);

            piece.AddComponent<DebrisPiece>();
        }

        // Kısa patlama flaşı
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "PatlamaFlasi";
        flash.transform.position = center;
        Object.Destroy(flash.GetComponent<Collider>());
        flash.GetComponent<Renderer>().sharedMaterial =
            StationVisuals.GetMaterial(new Color(1f, 0.95f, 0.8f));
        flash.AddComponent<ExplosionFlash>().maxScale = flashScale;
    }

    // ── Parça davranışı: bekle → küçül → yok ol ─────────────────────────
    private class DebrisPiece : MonoBehaviour
    {
        private float age;
        private Vector3 initialScale;

        private void Start() => initialScale = transform.localScale;

        private void Update()
        {
            age += Time.deltaTime;

            // Ömrün son %30'unda küçülerek kaybol
            float shrinkStart = PieceLifetime * 0.7f;
            if (age > shrinkStart)
            {
                float t = (age - shrinkStart) / (PieceLifetime - shrinkStart);
                transform.localScale = initialScale * Mathf.Max(0f, 1f - t);
            }

            if (age >= PieceLifetime) Destroy(gameObject);
        }
    }

    // ── Flaş: hızla büyü ve sön ─────────────────────────────────────────
    private class ExplosionFlash : MonoBehaviour
    {
        public float maxScale = 2.4f;

        private const float Duration = 0.18f;
        private float age;

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / Duration;
            transform.localScale = Vector3.one * Mathf.Lerp(0.4f, maxScale, t);

            if (age >= Duration) Destroy(gameObject);
        }
    }
}
