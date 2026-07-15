// Fx.cs — Prosedürel parçacık efektleri (asset gerektirmez)
// Görev: Sfx'in görsel kardeşi. Tek atımlık patlamalar (item alma, yumruk
//   isabeti) ve döngülü kıvılcımlar (çalışan istasyon) üretir.
//   Materyaller renk başına önbelleklenir; URP particle shader'ı yoksa
//   Sprites/Default'a düşer.

using System.Collections.Generic;
using UnityEngine;

public static class Fx
{
    private static readonly Dictionary<Color, Material> mats = new();

    private static Material Mat(Color c)
    {
        if (mats.TryGetValue(c, out Material m) && m != null) return m;

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        m = new Material(sh) { color = c };
        m.SetColor("_BaseColor", c);
        mats[c] = m;
        return m;
    }

    private static ParticleSystem Create(string fxName, Transform parent,
        Color color, float size)
    {
        GameObject go = new GameObject(fxName);
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.startColor       = color;
        main.startSize        = size;
        main.gravityModifier  = 0.65f;
        main.playOnAwake      = false;

        ParticleSystem.EmissionModule em = ps.emission;
        em.enabled = false;   // Varsayılan: elle Emit / SparkLoop açar

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.08f;

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat(color);
        return ps;
    }

    /// <summary>Tek atımlık patlama — item alma, darbe isabeti vb.</summary>
    public static void Burst(Vector3 pos, Color color, int count = 16,
        float speed = 3f, float size = 0.12f, float life = 0.45f)
    {
        ParticleSystem ps = Create("Fx_Burst", null, color, size);
        ps.transform.position = pos;

        ParticleSystem.MainModule main = ps.main;
        main.startSpeed    = speed;
        main.startLifetime = life;

        ps.Emit(count);
        Object.Destroy(ps.gameObject, life + 0.3f);
    }

    /// <summary>
    /// Döngülü kıvılcım pınarı (çalışan istasyon). Durdurulmuş döner —
    /// çağıran Play/Stop ile yönetir.
    /// </summary>
    public static ParticleSystem SparkLoop(Transform parent, Vector3 localPos,
        Color color)
    {
        ParticleSystem ps = Create("Fx_Sparks", parent, color, 0.09f);
        ps.transform.localPosition = localPos;

        ParticleSystem.MainModule main = ps.main;
        main.startSpeed    = 2.1f;
        main.startLifetime = 0.4f;
        main.gravityModifier = 1.1f;
        main.loop = true;

        ParticleSystem.EmissionModule em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 13f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 24f;
        shape.radius    = 0.06f;
        shape.rotation  = new Vector3(-90f, 0f, 0f);   // Yukarı fışkırır

        return ps;
    }
}
