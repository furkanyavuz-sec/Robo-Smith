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

            string text = "E: Zırha Ekle";
            if (player.HeldObject != null &&
                player.HeldObject.TryGetComponent<PickupItem>(out PickupItem i))
            {
                if      (i.Type.IsWeapon()) text = "E: Silah Tak";
                else if (i.Type.IsModule()) text = "E: Modül Tak";
            }
            eLabel.text = text;
        }

        if (qLabel != null)
        {
            qLabel.gameObject.SetActive(canUpgrade);

            if (canUpgrade)
                qLabel.text = UpgradePromptText(chassis, player);
        }
    }

    /// <summary>
    /// Eldeki malzemeyle hangi silahın geliştirileceğini bulup
    /// "Q: Kilic → Lv2" formatında ipucu üretir.
    /// </summary>
    public static string UpgradePromptText(RobotChassis chassis, PlayerInteraction player)
    {
        if (player.HeldObject == null ||
            !player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
            return "Q: Silahı Geliştir";

        RobotStatSheet sheet = chassis.StatSheet;
        for (int i = 0; i < sheet.weaponCount; i++)
        {
            WeaponData w = sheet.equippedWeapons[i];
            if (w == null || w.IsMaxLevel) continue;

            UpgradeLevel next = WeaponUpgradeSystem.GetNextLevel(w);
            if (next != null && next.requiredMaterial == item.Type)
                return next.requiredAmount > 1
                    ? $"Q: {w.weaponName} → Lv{w.upgradeLevel + 1} " +
                      $"({w.upgradeProgress + 1}/{next.requiredAmount})"
                    : $"Q: {w.weaponName} → Lv{w.upgradeLevel + 1}";
        }

        return "Q: Silahı Geliştir";
    }
}