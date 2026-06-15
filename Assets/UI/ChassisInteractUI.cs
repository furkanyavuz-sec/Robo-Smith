// ChassisInteractUI.cs
// Görev: Oyuncu şasiye yaklaşınca E/Q ipuçlarını gösterir.
// Canvas → Screen Space Overlay modunda çalışır.

using UnityEngine;
using TMPro;

public class ChassisInteractUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private PlayerInteraction player;
    [SerializeField] private RobotChassis      chassis;
    [SerializeField] private float             showRadius = 3f;

    [Header("UI Elemanları")]
    [SerializeField] private GameObject panelRoot;   // Ana panel
    [SerializeField] private TMP_Text        eLabel;     // "E: Zırha Ekle"
    [SerializeField] private TMP_Text       qLabel;     // "Q: Silahı Geliştir"

    private void Update()
    {
        if (player == null || chassis == null) return;

        float dist = Vector3.Distance(
            player.transform.position, chassis.transform.position
        );

        bool inRange = dist <= showRadius;

        if (!inRange)
        {
            panelRoot?.SetActive(false);
            return;
        }

        // Oyuncunun elinde uygun malzeme var mı?
        bool canArmor   = chassis.CanInteractArmor(player);
        bool canUpgrade = chassis.CanInteractUpgrade(player);

        bool showPanel = canArmor || canUpgrade;
        panelRoot?.SetActive(showPanel);

        if (eLabel != null)
        {
            eLabel.gameObject.SetActive(canArmor);
            eLabel.text = player.HeldObject != null &&
                          player.HeldObject.TryGetComponent<PickupItem>(out PickupItem i)
                          && i.Type.IsWeapon()
                        ? "E: Silah Tak"
                        : "E: Zırha Ekle";
        }

        if (qLabel != null)
        {
            qLabel.gameObject.SetActive(canUpgrade);

            // Hangi silah upgrade olacak?
            qLabel.text = "Q: Silahı Geliştir";
        }
    }
}