// StationSparks.cs — Çalışan istasyonda kıvılcım efekti
// Görev: IProgressReporter olan istasyonlarda (Processor, WeaponCraft,
//   Assembly) işleme sürerken (stage 1) turuncu kıvılcım pınarı oynatır.
//   MP client'ta da çalışır — StationProgressSync stage'i aynalıyor.
// Kurulum: MapGenerator istasyonlara StationProgressSync ile birlikte ekler.

using UnityEngine;

public class StationSparks : MonoBehaviour
{
    private IProgressReporter reporter;
    private ParticleSystem    sparks;

    private void Start()
    {
        reporter = GetComponent<IProgressReporter>();
        if (reporter == null) { enabled = false; return; }

        sparks = Fx.SparkLoop(transform, new Vector3(0f, 1.15f, 0f),
            new Color(1f, 0.72f, 0.22f));
    }

    private void Update()
    {
        bool working = reporter.ProgressStage == 1;

        if (working && !sparks.isPlaying)      sparks.Play();
        else if (!working && sparks.isPlaying) sparks.Stop();
    }
}
