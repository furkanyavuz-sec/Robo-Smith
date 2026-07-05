// StationProgressSync.cs — MP Faz 2B: İstasyon ilerleme çubuğu senkronu
// Görev: Processor / WeaponCraft / Assembly gibi süreli istasyonların
//   aşama + ilerleme bilgisini server'dan yayınlar; client kendi tarafında
//   StationProgressBar'ı gösterir (bar kodu server'da zaten istasyonun
//   kendi Update'iyle çalışıyor — bu bileşen yalnız client'ı besler).
//   InteractPromptUI de aşamayı buradan okuyup doğru ipucu gösterir.
// Kurulum: MapGenerator.Place her istasyona ekler; IProgressReporter
//   olmayanlarda kendini kapatır. Offline: tamamen pasif.

using Unity.Netcode;
using UnityEngine;

/// <summary>Süreli istasyonların ilerleme raporu sözleşmesi.</summary>
public interface IProgressReporter
{
    /// <summary>0 = boşta, 1 = çalışıyor, 2 = ürün hazır.</summary>
    int ProgressStage { get; }
    float Progress01  { get; }
    float SecondsLeft { get; }
}

public class StationProgressSync : NetworkBehaviour
{
    public const int STAGE_IDLE    = 0;
    public const int STAGE_WORKING = 1;
    public const int STAGE_READY   = 2;

    private readonly NetworkVariable<byte> stageNv =
        new(0, NetworkVariableReadPermission.Everyone,
               NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> progressNv =
        new(0f, NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> secondsLeftNv =
        new(0f, NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

    private IProgressReporter reporter;
    private float pollTimer;
    private float localSecondsLeft;   // Client: senkronlar arası yumuşatma

    private const float POLL_INTERVAL = 0.2f;

    /// <summary>Client ipuçları için senkronlu aşama (InteractPromptUI okur).</summary>
    public int Stage => stageNv.Value;

    private void Awake()
    {
        reporter = GetComponent<IProgressReporter>();
        if (reporter == null) enabled = false;   // Süreli istasyon değil
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            // İstasyonun gerçek durumunu düşük frekansta yayınla
            pollTimer -= Time.deltaTime;
            if (pollTimer > 0f) return;
            pollTimer = POLL_INTERVAL;

            stageNv.Value       = (byte)reporter.ProgressStage;
            progressNv.Value    = reporter.Progress01;
            secondsLeftNv.Value = reporter.SecondsLeft;
            return;
        }

        // Client: bar'ı senkrondan çiz (server kendi barını istasyon
        // Update'iyle çiziyor — burada çizersek çift olurdu)
        if (stageNv.Value == STAGE_WORKING)
        {
            // Senkronlar arasında süreyi yerel akıt — yazı titremesin
            localSecondsLeft = Mathf.Max(0f,
                Mathf.Min(secondsLeftNv.Value, localSecondsLeft - Time.deltaTime));
            if (Mathf.Abs(localSecondsLeft - secondsLeftNv.Value) > 0.4f)
                localSecondsLeft = secondsLeftNv.Value;

            StationProgressBar.Show(gameObject, progressNv.Value, localSecondsLeft);
        }
        else
        {
            localSecondsLeft = secondsLeftNv.Value;
            StationProgressBar.Hide(gameObject);
        }
    }
}
