// WeaponCraftStation.cs
// Görev: Scrapyard ham maddesini alır, işler ve silah item'ı üretir.
// Processor'dan farkı: çıktı bir WeaponItem — RobotChassis'e
// götürülünce stat yerine silah yuvası dolar.

using UnityEngine;

public class WeaponCraftStation : BaseStation
{
    private enum State { Idle, Crafting, Ready }

    [Header("Üretim Tarifi")]
    [SerializeField] private ItemType   inputType;          // Örn: ScrapMetal
    [SerializeField] private ItemType   outputWeaponType;   // Örn: Sword
    [SerializeField] private GameObject outputPrefab;
    [SerializeField] private float      craftDuration = 5f;

    /// <summary>VisualThemeManager gövdeyi silah rengine boyamak için okur.</summary>
    public ItemType OutputWeapon => outputWeaponType;

    [Header("Görsel")]
    [SerializeField] private Transform  displayPoint;
    [SerializeField] private Renderer   progressRenderer;

    private State      state      = State.Idle;
    private float      timer      = 0f;
    private GameObject inputItem  = null;
    private GameObject outputItem = null;

    public override bool CanInteract(PlayerInteraction player)
    {
        switch (state)
        {
            case State.Idle:
                if (player.HeldObject == null) return false;
                if (!player.HeldObject.TryGetComponent<PickupItem>(out PickupItem i))
                    return false;
                return i.Type == inputType;

            case State.Crafting: return false;

            case State.Ready: return player.HeldObject == null;

            default: return false;
        }
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        switch (state)
        {
            case State.Idle:   AcceptInput(player);  break;
            case State.Ready:  GiveOutput(player);   break;
        }
    }

    private void AcceptInput(PlayerInteraction player)
    {
        inputItem = player.HeldObject;
        player.ForceDropFromStation();

        if (inputItem.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        // MP'de NetworkObject parent'lanamaz — yardımcı pozisyona sabitler
        if (displayPoint != null)
            NetworkItem.PlaceAtAnchor(inputItem, displayPoint, Vector3.zero);

        timer = craftDuration;
        state = State.Crafting;

        Debug.Log($"[WeaponCraft] {inputType} → {outputWeaponType} üretiliyor. " +
                  $"Süre: {craftDuration}s");
    }

    private void FinishCrafting()
    {
        if (inputItem != null) { Destroy(inputItem); inputItem = null; }

        Vector3 pos = displayPoint != null
                    ? displayPoint.position
                    : transform.position + Vector3.up * 1.2f;

        outputItem = Instantiate(outputPrefab, pos, Quaternion.identity);

        if (outputItem.TryGetComponent<PickupItem>(out PickupItem item))
            item.SetType(outputWeaponType);

        state = State.Ready;
        StationProgressBar.Hide(gameObject);
        Debug.Log($"[WeaponCraft] {outputWeaponType} hazır!");
    }

    private void GiveOutput(PlayerInteraction player)
    {
        if (outputItem != null)
        {
            outputItem.transform.SetParent(null);
            player.PickupFromStation(outputItem);
            outputItem = null;
        }

        state = State.Idle;
    }

    private void Update()
    {
        if (state != State.Crafting) return;

        timer -= Time.deltaTime;
        float p = 1f - (timer / craftDuration);

        if (progressRenderer != null)
            progressRenderer.material.color = Color.Lerp(Color.yellow, Color.green, p);

        StationProgressBar.Show(gameObject, p, timer);

        if (timer <= 0f) FinishCrafting();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = state switch
        {
            State.Idle     => Color.white,
            State.Crafting => Color.yellow,
            State.Ready    => Color.green,
            _              => Color.white
        };
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.6f, Vector3.one * 0.5f);
    }
}