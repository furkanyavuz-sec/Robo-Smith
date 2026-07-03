// PlayerBodyVisual.cs
// Görev: Oyuncu kapsülüne hafif robot kimliği giydirir —
//        vizör, omuzlar, sırt çantası, anten.
// Player prefabına eklidir; Start'ta kendini kurar, kablo gerektirmez.
// Parçalar StationDecorTag taşır: VisualThemeManager renklerini ezmez.

using UnityEngine;

public class PlayerBodyVisual : MonoBehaviour
{
    [Header("Renkler")]
    [SerializeField] private Color bodyColor   = new Color(0.10f, 0.45f, 0.95f); // Oyuncu mavisi
    [SerializeField] private Color visorColor  = new Color(0.55f, 0.95f, 1f);    // Parlak vizör
    [SerializeField] private Color detailColor = new Color(0.16f, 0.17f, 0.20f); // Koyu metal

    private void Start()
    {
        // Yeniden spawn durumunda çift kurulum olmasın
        if (transform.Find("PlayerBody") != null) return;

        GameObject body = new GameObject("PlayerBody");
        body.transform.SetParent(transform, false);

        // Vizör — bakış yönünde (kapsül önü +z)
        Part(body, "Vizor", PrimitiveType.Cube,
            new Vector3(0f, 0.55f, 0.44f), new Vector3(0.34f, 0.11f, 0.06f),
            visorColor);

        // Omuzlar
        Part(body, "Omuz_Sag", PrimitiveType.Cube,
            new Vector3(0.56f, 0.30f, 0f), new Vector3(0.26f, 0.22f, 0.26f),
            bodyColor);
        Part(body, "Omuz_Sol", PrimitiveType.Cube,
            new Vector3(-0.56f, 0.30f, 0f), new Vector3(0.26f, 0.22f, 0.26f),
            bodyColor);

        // Sırt çantası — mühendisin alet çantası
        Part(body, "SirtCantasi", PrimitiveType.Cube,
            new Vector3(0f, 0.10f, -0.52f), new Vector3(0.42f, 0.5f, 0.18f),
            detailColor);

        // Anten + parlak uç
        Part(body, "Anten", PrimitiveType.Cylinder,
            new Vector3(0.16f, 1.12f, -0.08f), new Vector3(0.03f, 0.13f, 0.03f),
            detailColor);
        Part(body, "AntenUcu", PrimitiveType.Sphere,
            new Vector3(0.16f, 1.27f, -0.08f), Vector3.one * 0.09f,
            visorColor);
    }

    private void Part(GameObject parent, string partName, PrimitiveType type,
        Vector3 localPos, Vector3 scale, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = partName;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale    = scale;

        // Dekor — fizik/etkileşime karışmasın
        if (obj.TryGetComponent<Collider>(out Collider col))
            Destroy(col);

        // Tema yöneticisi bu parçaların rengini ezmesin
        obj.AddComponent<StationDecorTag>();

        obj.GetComponent<Renderer>().sharedMaterial =
            StationVisuals.GetMaterial(color);
    }
}
