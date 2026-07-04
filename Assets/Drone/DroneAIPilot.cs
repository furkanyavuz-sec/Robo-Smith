// DroneAIPilot.cs — Rakip (kırmızı) drone'un otomatik pilotu
// Görev: Pencere açılınca zorluğa bağlı gecikmeyle kalkar, en yakın serbest
//   ödüle gider, kapınca eve taşır (teslimatı SupplyDrone → DirectorAI işler).
//   Hard'da yük taşıyan oyuncu drone'unu kesmeye (çarpıp düşürtmeye) çalışır.
// Zorluk ayarları kod içindedir — DirectorAI.GetTuning ile aynı felsefe.

using UnityEngine;

[RequireComponent(typeof(SupplyDrone))]
public class DroneAIPilot : MonoBehaviour
{
    // ── Zorluk tablosu (tek denge noktası) ───────────────────────────────
    private struct PilotTuning
    {
        public float speed;        // Uçuş hızı (oyuncu: 7)
        public float reactDelay;   // Pencere açılınca kalkış gecikmesi
        public bool  intercepts;   // Yük taşıyan oyuncu drone'unu kovalar mı
    }

    private static PilotTuning GetTuning(Difficulty d) => d switch
    {
        Difficulty.Easy => new PilotTuning
            { speed = 3.2f, reactDelay = 6f, intercepts = false },
        Difficulty.Hard => new PilotTuning
            { speed = 6.0f, reactDelay = 1f, intercepts = true },
        _ => new PilotTuning   // Normal
            { speed = 4.6f, reactDelay = 2.5f, intercepts = false },
    };

    private SupplyDrone drone;
    private SupplyDrone playerDrone;    // Kesme hedefi (Hard)
    private PilotTuning tuning;
    private float       launchTimer;
    private bool        launched;

    private void Start()
    {
        drone = GetComponent<SupplyDrone>();

        Difficulty diff = MatchData.Instance != null
            ? MatchData.Instance.SelectedDifficulty
            : Difficulty.Normal;
        tuning = GetTuning(diff);

        // Oyuncu drone'unu bul (kesme davranışı için)
        foreach (SupplyDrone d in FindObjectsByType<SupplyDrone>(
                     FindObjectsSortMode.None))
            if (d.IsPlayerTeam) playerDrone = d;
    }

    private void Update()
    {
        DroneRaidZone zone = DroneRaidZone.Instance;
        if (zone == null) return;

        if (!zone.IsOpen)
        {
            // Pencere kapalı: kalkış durumunu sıfırla (eve dönüşü zone tetikler)
            launched    = false;
            launchTimer = tuning.reactDelay;
            return;
        }

        // Kalkış gecikmesi — zorluk hissi buradan gelir
        if (!launched)
        {
            launchTimer -= Time.deltaTime;
            if (launchTimer > 0f) return;

            launched = true;
            drone.BeginAIFlight(tuning.speed);
        }

        if (drone.Mode != SupplyDrone.DroneMode.AI) return;

        drone.SetAITarget(PickTarget(zone));
    }

    private Vector3 PickTarget(DroneRaidZone zone)
    {
        // Yük varsa: eve taşı
        if (drone.IsCarrying) return drone.HomePosition;

        // Hard: oyuncu drone'u yük taşıyorsa kes (çarpma = düşürme)
        if (tuning.intercepts && playerDrone != null &&
            playerDrone.IsCarrying && playerDrone.IsFlying)
            return playerDrone.transform.position;

        // En yakın serbest ödül
        PickupItem best = FindNearestFreeReward(zone);
        if (best != null) return best.transform.position;

        // Ödül kalmadı: evin üstünde bekle
        return drone.HomePosition;
    }

    private PickupItem FindNearestFreeReward(DroneRaidZone zone)
    {
        // TryGrabNearby geniş yarıçapla "en yakın"ı bulmak için uygun değil —
        // ödül konumları üstünden tarama yapar
        PickupItem best = null;
        float bestDist  = float.MaxValue;

        foreach (PickupItem r in zone.FreeRewards())
        {
            float dist = Vector3.Distance(
                drone.transform.position, r.transform.position);
            if (dist < bestDist) { bestDist = dist; best = r; }
        }
        return best;
    }
}
