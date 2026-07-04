// DroneConsole.cs — Garajdaki drone kumanda istasyonu
// Görev: Çekirdek bölge penceresi aktifken oyuncu E ile konsola geçer;
//   PlayerController + PlayerInteraction kapanır, kamera drone'a kilitlenir.
//   Çıkış: drone tarafında E/ESC (SupplyDrone.HandlePilotInput) veya
//   pencere kapanınca DroneRaidZone.ForceReturnHome üzerinden.
// Kurulum: MapGenerator SupplyBin prefabını klonlayıp bileşeni bununla
//   değiştirir (Montaj İstasyonu deseniyle aynı) — layer/collider hazır gelir.

using UnityEngine;

public class DroneConsole : BaseStation
{
    [Header("Bağlantılar (MapGenerator bağlar)")]
    [SerializeField] private SupplyDrone drone;

    private PlayerController  pilotController;
    private PlayerInteraction pilotInteraction;
    private Transform         pilotTransform;
    private bool              inUse;

    public override bool CanInteract(PlayerInteraction player)
    {
        if (inUse)                    return false;
        if (player.HeldObject != null) return false;   // Eli boş olmalı
        if (drone == null)            return false;
        if (drone.Mode != SupplyDrone.DroneMode.Docked) return false;

        // Konsol sadece pencere anonsu/açıkken çalışır
        return DroneRaidZone.Instance != null &&
               DroneRaidZone.Instance.DroneUsable;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        inUse            = true;
        pilotInteraction = player;
        pilotController  = player.GetComponent<PlayerController>();
        pilotTransform   = player.transform;

        // Oyuncuyu konsola kilitle
        if (pilotController  != null) pilotController.enabled  = false;
        pilotInteraction.enabled = false;

        // Kamera drone'a geçer, kontrol drone'da
        drone.BeginPiloting(this);
        CameraController.Instance?.SetTarget(drone.transform);

        Debug.Log("[DroneConsole] Drone kontrolü alındı.");
    }

    /// <summary>SupplyDrone (E/ESC) veya DroneRaidZone (pencere kapandı) çağırır.</summary>
    public void EndPiloting()
    {
        if (!inUse) return;
        inUse = false;

        drone?.EndPiloting();

        if (pilotController  != null) pilotController.enabled  = true;
        if (pilotInteraction != null) pilotInteraction.enabled = true;

        if (pilotTransform != null)
            CameraController.Instance?.SetTarget(pilotTransform);

        pilotController  = null;
        pilotInteraction = null;
        pilotTransform   = null;

        Debug.Log("[DroneConsole] Drone kontrolü bırakıldı — oyuncu serbest.");
    }
}
