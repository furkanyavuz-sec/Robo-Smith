// ItemShapeBuilder.cs — Item'lara paket propu şekli giydirir
// Görev: PickupItem'ın tipine göre MapTheme.itemShapes eşlemesinden mesh
//   bulur: kök küp gizlenir, prop child olarak ~0.5m kutuya sığdırılır.
//   Renk VisualThemeManager'dan tipe göre otomatik biner (tint tüm
//   child'lara işler) — şekil malzemeyi, renk kimliği anlatır.
//   Eşlemede olmayan tip eski küp görünümünde kalır.
// Çağrılar: ItemVisual.Start (spawn) + PickupItem.SetType (tip değişimi).

using UnityEngine;

public static class ItemShapeBuilder
{
    public static void Apply(PickupItem item)
    {
        if (item == null) return;

        MapTheme th = ThemeRef.Current;
        if (th == null || th.itemShapes == null) return;

        GameObject prefab = null;
        foreach (MapTheme.ItemShape e in th.itemShapes)
            if (e.type == item.Type) { prefab = e.prefab; break; }

        Transform old = item.transform.Find("Shape");
        Renderer rootRend = item.GetComponent<Renderer>();

        if (prefab == null)
        {
            // Eşleme yok: küpe geri dön
            if (old != null) Object.Destroy(old.gameObject);
            if (rootRend != null) rootRend.enabled = true;
            return;
        }

        // Aynı tip için kurulmuşsa yeniden kurma (isimde tip taşınır)
        string shapeName = "Shape";
        if (old != null)
        {
            if (old.GetComponent<ShapeTag>() is ShapeTag tag &&
                tag.type == item.Type) return;
            Object.Destroy(old.gameObject);
        }

        if (rootRend != null) rootRend.enabled = false;

        GameObject shape = Object.Instantiate(prefab, item.transform);
        shape.name = shapeName;
        shape.AddComponent<ShapeTag>().type = item.Type;

        foreach (Collider c in shape.GetComponentsInChildren<Collider>())
            Object.Destroy(c);

        // ~0.5m kutuya uniform sığdır, merkezi pivota getir
        Renderer[] rends = shape.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f);
            shape.transform.localScale *= 0.5f / maxDim;

            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            shape.transform.position += item.transform.position - b.center;
        }

        // Tip rengini yeni şekle de bindir
        VisualThemeManager theme = Object.FindAnyObjectByType<VisualThemeManager>();
        if (theme != null) theme.ApplyItemColor(item);
    }

    /// <summary>Şeklin hangi tip için kurulduğunu işaretler.</summary>
    public class ShapeTag : MonoBehaviour
    {
        public ItemType type;
    }
}
