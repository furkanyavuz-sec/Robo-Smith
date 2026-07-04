// PlayerMelee.cs — Oyuncu yumruğu (Boşluk tuşu)
// Görev: Hurdalık Penceresi'nde rakibe (TechnicianBot, MP'de diğer oyuncular)
//   kısa menzilli darbe: isabet alan 1.5 sn sersemler + elindekini düşürür.
//   Ölüm/ceza yok — hızlı, komik, üretim odağını bozmaz.
// Kurulum: prefab düzenlemesi gerekmez — ScrapWindowZone oyuncuya
//   runtime'da iliştirir (EnsurePlayerMelee).

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMelee : MonoBehaviour
{
    [Header("Darbe Ayarları")]
    [SerializeField] private float punchRange    = 1.3f;   // İleri offset
    [SerializeField] private float punchRadius   = 1.1f;   // Etki küresi
    [SerializeField] private float punchCooldown = 0.8f;
    [SerializeField] private float stunDuration  = 1.5f;

    private PlayerController  controller;
    private PlayerInteraction interaction;
    private float cooldownTimer;
    private float stunTimer;
    private bool  stunned;

    public bool IsStunned => stunned;

    private void Awake()
    {
        controller  = GetComponent<PlayerController>();
        interaction = GetComponent<PlayerInteraction>();
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        // Sersemleme sayacı
        if (stunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                stunned = false;
                if (controller != null) controller.enabled = true;
            }
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool punchPressed = keyboard.spaceKey.wasPressedThisFrame;

        // FPV'de Sol Tık da yumruk atar (FPS refleksi) — normal görünümde
        // tıklamalar UI'ya karışmasın diye sadece FPV'de aktif
        Mouse mouse = Mouse.current;
        if (FirstPersonView.IsActive && mouse != null &&
            mouse.leftButton.wasPressedThisFrame)
            punchPressed = true;

        if (punchPressed && cooldownTimer <= 0f)
            Punch();
    }

    private void Punch()
    {
        cooldownTimer = punchCooldown;

        Sfx.Play(Sfx.Id.Punch);
        CameraShake.Add(0.12f);

        Vector3 hitCenter = transform.position + transform.forward * punchRange;
        DamagePopup.Spawn(hitCenter - Vector3.up * 1.2f, "DARBE!",
            new Color(0.95f, 0.85f, 0.10f), 0.8f);

        Collider[] hits = Physics.OverlapSphere(hitCenter, punchRadius);
        foreach (Collider col in hits)
        {
            // Rakip teknisyen
            if (col.TryGetComponent<TechnicianBot>(out TechnicianBot bot))
            {
                Vector3 dir = (bot.transform.position - transform.position);
                dir.y = 0f;
                bot.ReceivePunch(dir.normalized);
                return;
            }

            // MP: diğer oyuncular (kendimiz hariç)
            if (col.TryGetComponent<PlayerMelee>(out PlayerMelee other) &&
                other != this)
            {
                Vector3 dir = (other.transform.position - transform.position);
                dir.y = 0f;
                other.ReceivePunch(dir.normalized);
                return;
            }
        }
    }

    /// <summary>Rakipten darbe: sersemle + elindekini düşür + hafif itilme.</summary>
    public void ReceivePunch(Vector3 knockDir)
    {
        // Drone konsolundayken vurulamaz (kontrol zaten kilitli)
        if (controller != null && !controller.enabled && !stunned) return;

        stunned   = true;
        stunTimer = stunDuration;

        if (controller != null) controller.enabled = false;
        interaction?.ForceDropFromStation();

        transform.position += knockDir * 0.8f;

        Sfx.Play(Sfx.Id.Hit);
        CameraShake.Add(0.4f);   // Darbeyi yiyen benim — ekran sallansın

        DamagePopup.Spawn(transform.position, "SERSEMLEDİ!",
            new Color(0.95f, 0.45f, 0.15f), 1.1f);
    }
}
