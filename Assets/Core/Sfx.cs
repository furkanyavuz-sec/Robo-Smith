// Sfx.cs — Prosedürel ses efektleri (asset gerektirmez)
// Görev: Klipleri runtime'da sentezler (sinüs + gürültü), önbellekler ve
//   2D tek-atış çalar. Projenin "prefabsız/assetsiz" felsefesiyle uyumlu —
//   ileride gerçek ses dosyaları gelirse sadece bu sınıf değişir.
// Kullanım: Sfx.Play(Sfx.Id.Punch);

using System.Collections.Generic;
using UnityEngine;

public static class Sfx
{
    public enum Id
    {
        Punch,        // Yumruk savurma (ıskalasa da)
        Hit,          // Darbe isabeti / sersemleme
        Grab,         // Yağma/ödül kapma
        Deposit,      // Depoya bırakma / teslimat
        Announce,     // Anons bip'i (RaidAnnouncer)
        WindowOpen,   // Bölge açılışı (yükselen süpürme)
        WindowClose,  // Bölge kapanışı (alçalan süpürme)
        Steal,        // Drone çarpışması / yük düşmesi
        Explosion,    // Robot ölümü
        Laser,        // Lazer atışı
        Rocket,       // Roket fırlatma
        Emp           // EMP küresi
    }

    private const int RATE = 22050;

    private static readonly Dictionary<Id, AudioClip> clips = new();
    private static AudioSource source;

    public static void Play(Id id, float volume = 0.7f)
    {
        EnsureSource();
        if (source == null) return;

        if (!clips.TryGetValue(id, out AudioClip clip) || clip == null)
        {
            clip = Synthesize(id);
            clips[id] = clip;
        }
        if (clip == null) return;

        // Hafif perde oynaması — aynı ses arka arkaya robotik durmasın
        source.pitch = 1f + Random.Range(-0.06f, 0.06f);
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static void EnsureSource()
    {
        if (source != null) return;

        GameObject go = new GameObject("SfxPlayer");
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.spatialBlend = 0f;   // 2D — kamera nerede olursa olsun duyulur
        source.playOnAwake  = false;

        // Sahnede dinleyici yoksa (kamera prefabında eksikse) sessiz kalmayalım
        if (Object.FindAnyObjectByType<AudioListener>() == null)
            go.AddComponent<AudioListener>();
    }

    // ── Sentez ───────────────────────────────────────────────────────────

    private static AudioClip Synthesize(Id id)
    {
        float[] s = id switch
        {
            Id.Punch      => Mix(Noise(0.08f, 4f, 0.5f), Tone(90f, 90f, 0.09f, 3f, 0.8f)),
            Id.Hit        => Mix(Noise(0.14f, 3f, 0.6f), Tone(160f, 70f, 0.15f, 2.5f, 0.7f)),
            Id.Grab       => Tone(520f, 780f, 0.09f, 2f, 0.5f),
            Id.Deposit    => Mix(Tone(660f, 660f, 0.10f, 2f, 0.4f),
                                 Shift(Tone(880f, 880f, 0.14f, 2f, 0.4f), 0.09f)),
            Id.Announce   => Mix(Tone(880f, 880f, 0.09f, 2f, 0.35f),
                                 Shift(Tone(1174f, 1174f, 0.12f, 2f, 0.35f), 0.10f)),
            Id.WindowOpen  => Tone(220f, 880f, 0.45f, 1.2f, 0.5f),
            Id.WindowClose => Tone(880f, 220f, 0.45f, 1.2f, 0.5f),
            Id.Steal      => Mix(Tone(700f, 320f, 0.22f, 1.5f, 0.5f),
                                 Noise(0.10f, 4f, 0.35f)),
            Id.Explosion  => Mix(Noise(0.75f, 2.2f, 0.9f), Tone(55f, 40f, 0.7f, 1.8f, 0.9f)),
            Id.Laser      => Tone(1400f, 380f, 0.12f, 2f, 0.45f),
            Id.Rocket     => Mix(Noise(0.35f, 1.5f, 0.6f), Tone(140f, 90f, 0.35f, 1.5f, 0.4f)),
            Id.Emp        => Wobble(300f, 9f, 0.35f, 0.5f),
            _             => null
        };

        if (s == null) return null;

        AudioClip clip = AudioClip.Create(id.ToString(), s.Length, 1, RATE, false);
        clip.SetData(s, 0);
        return clip;
    }

    /// <summary>Frekans süpürmeli sinüs tonu, üstel sönümlü.</summary>
    private static float[] Tone(float f0, float f1, float dur,
        float decayPow, float amp)
    {
        int n = Mathf.CeilToInt(dur * RATE);
        float[] s = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float freq = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * freq / RATE;
            s[i] = Mathf.Sin(phase) * amp * Mathf.Pow(1f - t, decayPow);
        }
        return s;
    }

    /// <summary>Alçak geçiren süzgeçli gürültü patlaması.</summary>
    private static float[] Noise(float dur, float decayPow, float amp)
    {
        int n = Mathf.CeilToInt(dur * RATE);
        float[] s = new float[n];
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            lp += (Random.Range(-1f, 1f) - lp) * 0.25f;   // Tek kutuplu süzgeç
            s[i] = lp * amp * Mathf.Pow(1f - t, decayPow);
        }
        return s;
    }

    /// <summary>Frekans titreşimli ton (EMP vınlaması).</summary>
    private static float[] Wobble(float baseFreq, float wobbleHz,
        float dur, float amp)
    {
        int n = Mathf.CeilToInt(dur * RATE);
        float[] s = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t    = (float)i / n;
            float time = (float)i / RATE;
            float freq = baseFreq * (1f + 0.35f *
                Mathf.Sin(2f * Mathf.PI * wobbleHz * time));
            phase += 2f * Mathf.PI * freq / RATE;
            s[i] = Mathf.Sin(phase) * amp * Mathf.Pow(1f - t, 1.5f);
        }
        return s;
    }

    /// <summary>İki örnek dizisini üst üste bindirir (uzun olana göre).</summary>
    private static float[] Mix(float[] a, float[] b)
    {
        float[] longer  = a.Length >= b.Length ? a : b;
        float[] shorter = a.Length >= b.Length ? b : a;

        float[] s = new float[longer.Length];
        for (int i = 0; i < longer.Length; i++)
        {
            float v = longer[i] + (i < shorter.Length ? shorter[i] : 0f);
            s[i] = Mathf.Clamp(v, -1f, 1f);
        }
        return s;
    }

    /// <summary>Örnekleri belirtilen saniye kadar geciktirir (nota sırası).</summary>
    private static float[] Shift(float[] src, float seconds)
    {
        int offset = Mathf.CeilToInt(seconds * RATE);
        float[] s = new float[src.Length + offset];
        for (int i = 0; i < src.Length; i++)
            s[i + offset] = src[i];
        return s;
    }
}
