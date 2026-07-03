// ProjectileFactory.cs
// Görev: Prefabsız, koddan üretilen GÖRÜNÜR mermiler.
// WeaponData.Create projectilePrefab atamadığı için mermiler görünmüyordu —
// artık her menzilli saldırı sahnede uçan bir cisim.
//   Lazer  → hızlı camgöbeği enerji oku + iz
//   Roket  → turuncu başlık + geniş iz (çarpınca mini patlama)
//   EMP    → mor enerji küresi
// Trigger çarpışması için kinematik Rigidbody eklenir (robotlarda RB yok).

using UnityEngine;

public static class ProjectileFactory
{
    /// <summary>Lazer benzeri hızlı enerji oku (Projectile bileşenli).</summary>
    public static Projectile CreateBolt(Vector3 origin, Color color)
    {
        GameObject obj = BuildBody(origin, new Vector3(0.14f, 0.14f, 0.55f),
            color, trailWidth: 0.10f, trailTime: 0.15f);

        Projectile p = obj.AddComponent<Projectile>();
        UIFactory.SetField(p, "moveSpeed", 18f);   // Lazer hızlı uçar
        return p;
    }

    /// <summary>Roket başlığı (RocketProjectile bileşenli).</summary>
    public static RocketProjectile CreateRocket(Vector3 origin, Color color)
    {
        GameObject obj = BuildBody(origin, new Vector3(0.24f, 0.24f, 0.5f),
            color, trailWidth: 0.18f, trailTime: 0.35f);

        RocketProjectile r = obj.AddComponent<RocketProjectile>();
        UIFactory.SetField(r, "moveSpeed", 10f);
        return r;
    }

    /// <summary>EMP enerji küresi (Projectile bileşenli).</summary>
    public static Projectile CreateEmpOrb(Vector3 origin, Color color)
    {
        GameObject obj = BuildBody(origin, Vector3.one * 0.28f,
            color, trailWidth: 0.14f, trailTime: 0.25f);

        Projectile p = obj.AddComponent<Projectile>();
        UIFactory.SetField(p, "moveSpeed", 11f);
        return p;
    }

    // ── Ortak Gövde ──────────────────────────────────────────────────────

    private static GameObject BuildBody(Vector3 origin, Vector3 scale,
        Color color, float trailWidth, float trailTime)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "Mermi";
        obj.transform.position   = origin;
        obj.transform.localScale = scale;

        Color bright = Color.Lerp(color, Color.white, 0.35f);
        obj.GetComponent<Renderer>().sharedMaterial =
            StationVisuals.GetMaterial(bright);

        // Trigger çarpışması için kinematik RB şart (robot tarafında RB yok)
        Rigidbody rb   = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        // İz efekti — hareket hissi
        TrailRenderer trail = obj.AddComponent<TrailRenderer>();
        trail.time              = trailTime;
        trail.startWidth        = trailWidth;
        trail.endWidth          = 0.01f;
        trail.minVertexDistance = 0.08f;
        trail.sharedMaterial    = StationVisuals.GetMaterial(color);

        return obj;
    }
}
