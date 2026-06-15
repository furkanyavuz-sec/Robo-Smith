// ShieldController.cs
// Görev: İki modlu kalkan sistemi.
// Pasif: Gelen hasarın reflectRatio'sunu saldırgana yansıtır.
// Aktif: BattleRobot FSM'i tehlike anında aktifleştirir,
//        açıkken hasar almaz, cooldown sonrası kapanır.

using UnityEngine;

public class ShieldController : MonoBehaviour
{
    [Header("Aktif Kalkan")]
    [SerializeField] private float activeDuration  = 2f;   // Kaç saniye açık kalır
    [SerializeField] private float activeCooldown  = 8f;   // Yeniden açılma süresi
    [SerializeField] private GameObject shieldVFX;          // Kalkan görsel efekti

    // Pasif yansıtma oranı WeaponData'dan gelir
    private float reflectRatio   = 0.30f;
    private bool  isActive       = false;
    private float activeTimer    = 0f;
    private float cooldownTimer  = 0f;
    private bool  onCooldown     = false;

    private BattleRobot owner;

    public bool  IsActive    => isActive;
    public bool  OnCooldown  => onCooldown;

    public void Initialize(BattleRobot robot, float reflect)
    {
        owner        = robot;
        reflectRatio = reflect;
    }

    private void Update()
    {
        if (isActive)
        {
            activeTimer -= Time.deltaTime;
            if (activeTimer <= 0f) DeactivateShield();
        }

        if (onCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f) onCooldown = false;
        }
    }

    // ── Pasif Yansıtma ───────────────────────────────────────────────────

    /// <summary>
    /// TakeDamage çağrılmadan önce BattleRobot buraya sorar.
    /// True dönerse hasar engellendi + saldırgana yansıtıldı.
    /// </summary>
    public bool TryBlock(int incomingDamage, BattleRobot attacker)
    {
        // Aktif kalkan açıksa tüm hasarı engelle
        if (isActive)
        {
            Debug.Log("<color=cyan>[Kalkan] Aktif kalkan hasarı engelledi!</color>");
            return true;
        }

        // Pasif yansıtma: hasarın bir kısmını geri ver
        if (reflectRatio > 0f && attacker != null)
        {
            int reflected = Mathf.RoundToInt(incomingDamage * reflectRatio);
            attacker.TakeDamage(reflected, WeaponCategory.Defensive);

            Debug.Log($"<color=cyan>[Kalkan] {reflected} hasar yansıtıldı!</color>");
        }

        return false; // Hasarı tamamen engellemedi, normal hasar alınır
    }

    // ── Aktif Kalkan ─────────────────────────────────────────────────────

    /// <summary>BattleRobot FSM tehlike anında çağırır.</summary>
    public bool TryActivate()
    {
        if (isActive || onCooldown) return false;

        isActive    = true;
        activeTimer = activeDuration;

        if (shieldVFX != null) shieldVFX.SetActive(true);

        Debug.Log("<color=cyan>[Kalkan] Aktif kalkan açıldı!</color>");
        return true;
    }

    private void DeactivateShield()
    {
        isActive      = false;
        onCooldown    = true;
        cooldownTimer = activeCooldown;

        if (shieldVFX != null) shieldVFX.SetActive(false);

        Debug.Log("<color=grey>[Kalkan] Aktif kalkan kapandı. " +
                  $"Cooldown: {activeCooldown}s</color>");
    }
}