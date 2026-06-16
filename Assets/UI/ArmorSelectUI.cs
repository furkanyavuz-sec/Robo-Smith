using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ArmorSelectUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private PlayerInteraction player;
    [SerializeField] private RobotChassis[]    chassisList;
    [SerializeField] private float             showRadius = 4f;

    [Header("Panel")]
    [SerializeField] private GameObject        panelRoot;
    [SerializeField] private TextMeshProUGUI   titleText;
    [SerializeField] private TextMeshProUGUI   armorNameText;
    [SerializeField] private TextMeshProUGUI   armorDescText;
    [SerializeField] private TextMeshProUGUI   navigationHint;
    [SerializeField] private Image             armorIcon;

    [Header("Zirh Renkleri")]
    [SerializeField] private Color heavyPlateColor    = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color reactiveArmorColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color energyShieldColor  = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color empResistColor     = new Color(0.6f, 0.2f, 1f);

    private ArmorType[] armorTypes =
    {
        ArmorType.HeavyPlate,
        ArmorType.ReactiveArmor,
        ArmorType.EnergyShield,
        ArmorType.EMPResistance
    };

    private int selectedIndex = 0;
    private RobotChassis activeChassis = null;
    private void Start()
    {
        panelRoot?.SetActive(false);
    }

    private void Update()
{
    if (player == null) return;
    if (chassisList == null || chassisList.Length == 0) return;

    RobotChassis closest     = null;
    float        closestDist = float.MaxValue;

    foreach (RobotChassis c in chassisList)
    {
        if (c == null) continue;
        float dist = Vector3.Distance(
            player.transform.position, c.transform.position);

        if (dist < closestDist)
        {
            closestDist = dist;
            closest     = c;
        }
    }

    bool inRange = closest != null && closestDist <= showRadius
                && player.HeldObject == null;

    if (inRange)
    {
        if (activeChassis != closest)
        {
            activeChassis = closest;

            for (int i = 0; i < armorTypes.Length; i++)
                if (armorTypes[i] == activeChassis.EquippedArmor)
                    selectedIndex = i;

            RefreshPanel();
        }

        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            selectedIndex = (selectedIndex + 1) % armorTypes.Length;
            RefreshPanel();
        }

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            activeChassis.SetArmor(armorTypes[selectedIndex]);
            Debug.Log($"[ArmorSelectUI] Zirh secildi: {armorTypes[selectedIndex]}");
        }
    }
    else
    {
        activeChassis = null;
        if (panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);
    }
}

    private void RefreshPanel()
{
    if (activeChassis == null) return;  // ← BU SATIRI EKLE

    ArmorType current = armorTypes[selectedIndex];

    if (titleText != null)
        titleText.text = "ZIRH SEC";

    if (armorNameText != null)
        armorNameText.text = current switch
        {
            ArmorType.HeavyPlate    => "Agir Plaka",
            ArmorType.ReactiveArmor => "Reaktif Zirh",
            ArmorType.EnergyShield  => "Enerji Kalkani",
            ArmorType.EMPResistance => "EMP Direnci",
            _                       => "—"
        };

    if (armorDescText != null)
        armorDescText.text = ArmorResistanceTable.GetDescription(current);

    if (navigationHint != null)
        navigationHint.text = $"[ Tab ] Gecis  |  [ F ] Sec\n" +
                              $"{selectedIndex + 1}/{armorTypes.Length}";

    Color panelColor = current switch
    {
        ArmorType.HeavyPlate    => heavyPlateColor,
        ArmorType.ReactiveArmor => reactiveArmorColor,
        ArmorType.EnergyShield  => energyShieldColor,
        ArmorType.EMPResistance => empResistColor,
        _                       => Color.white
    };

    if (armorIcon     != null) armorIcon.color     = panelColor;
    if (armorNameText != null) armorNameText.color = panelColor;
}
}