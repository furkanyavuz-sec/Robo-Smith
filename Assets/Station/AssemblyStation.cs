// AssemblyStation.cs
// Görev: İKİ FARKLI işlenmiş ürünü birleştirip modül üretir.
// Akış: 1. ürünü koy → 2. ürünü koy (geçerli tarif şart) →
//       10 sn montaj → modülü al → şasiye tak.
// Processor'dan farkı: iki girdili, uzun süreli, çıktısı modül item'ı.
// Risk-ödül: iki zincir yönetimi + uzun bekleme = güçlü pasif etki.

using UnityEngine;

public class AssemblyStation : BaseStation
{
    private enum State { Empty, HoldingFirst, Assembling, Ready }

    [Header("Montaj Ayarları")]
    [SerializeField] private float      assembleDuration = 10f;
    [SerializeField] private GameObject outputPrefab;     // PickupItem'lı herhangi bir prefab
    [SerializeField] private Transform  displayPoint;     // Boşsa istasyonun üstü

    [Header("Görsel (Opsiyonel)")]
    [SerializeField] private Renderer progressRenderer;

    // ── Dahili Durum ─────────────────────────────────────────────────────
    private State      state       = State.Empty;
    private ItemType   firstInput;
    private GameObject firstItemObj;
    private ModuleType resultModule = ModuleType.None;
    private float      timer;
    private GameObject outputItem;

    // ── BaseStation Sözleşmesi ───────────────────────────────────────────

    public override bool CanInteract(PlayerInteraction player)
    {
        switch (state)
        {
            case State.Empty:
                return HeldProcessedType(player) != null;

            case State.HoldingFirst:
                ItemType? second = HeldProcessedType(player);
                if (second == null) return false;
                return ModuleCatalog.GetRecipeResult(firstInput, second.Value)
                       != ModuleType.None;

            case State.Assembling:
                return false;

            case State.Ready:
                return player.HeldObject == null;

            default: return false;
        }
    }

    public override void Interact(PlayerInteraction player)
    {
        if (!CanInteract(player)) return;

        switch (state)
        {
            case State.Empty:        AcceptFirst(player);  break;
            case State.HoldingFirst: AcceptSecond(player); break;
            case State.Ready:        GiveOutput(player);   break;
        }
    }

    // ── Akış ─────────────────────────────────────────────────────────────

    private void AcceptFirst(PlayerInteraction player)
    {
        player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item);
        firstInput   = item.Type;
        firstItemObj = player.HeldObject;

        player.ForceDropFromStation();
        PlaceOnDisplay(firstItemObj);

        state = State.HoldingFirst;
        Debug.Log($"[Montaj] 1. ürün: {firstInput}. Şimdi FARKLI bir işlenmiş " +
                  $"ürün getir (geçerli tarif gerekli).");
    }

    private void AcceptSecond(PlayerInteraction player)
    {
        player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item);
        resultModule = ModuleCatalog.GetRecipeResult(firstInput, item.Type);

        GameObject secondObj = player.HeldObject;
        player.ForceDropFromStation();
        Destroy(secondObj);

        if (firstItemObj != null) { Destroy(firstItemObj); firstItemObj = null; }

        timer = assembleDuration;
        state = State.Assembling;

        Debug.Log($"<color=cyan>[Montaj] ⚙️ {ModuleCatalog.TrName(resultModule)} " +
                  $"montajı başladı — {assembleDuration:F0} saniye!</color>");
    }

    private void FinishAssembly()
    {
        StationProgressBar.Hide(gameObject);

        if (outputPrefab == null)
        {
            Debug.LogError("[Montaj] outputPrefab atanmamış!");
            state = State.Empty;
            return;
        }

        Vector3 pos = displayPoint != null
                    ? displayPoint.position
                    : transform.position + Vector3.up * 1.2f;

        outputItem = Instantiate(outputPrefab, pos, Quaternion.identity);

        if (outputItem.TryGetComponent<PickupItem>(out PickupItem item))
            item.SetType(ModuleCatalog.ToItem(resultModule));

        state = State.Ready;
        Debug.Log($"<color=green>[Montaj] ✅ {ModuleCatalog.TrName(resultModule)} " +
                  $"hazır! Al ve şasiye tak.</color>");
    }

    private void GiveOutput(PlayerInteraction player)
    {
        if (outputItem != null)
        {
            outputItem.transform.SetParent(null);
            player.PickupFromStation(outputItem);
            outputItem = null;
        }

        resultModule = ModuleType.None;
        state = State.Empty;
    }

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    private void Update()
    {
        if (state != State.Assembling) return;

        timer -= Time.deltaTime;
        float p = 1f - (timer / assembleDuration);

        if (progressRenderer != null)
            progressRenderer.material.color = Color.Lerp(Color.magenta, Color.green, p);

        StationProgressBar.Show(gameObject, p, timer);

        if (timer <= 0f) FinishAssembly();
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────

    private static ItemType? HeldProcessedType(PlayerInteraction player)
    {
        if (player.HeldObject == null) return null;
        if (!player.HeldObject.TryGetComponent<PickupItem>(out PickupItem item))
            return null;
        return item.Type.IsProcessed() ? item.Type : null;
    }

    private void PlaceOnDisplay(GameObject item)
    {
        if (item == null) return;

        if (item.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
        if (item.TryGetComponent<Collider>(out Collider col))
            col.isTrigger = true;

        if (displayPoint != null)
        {
            item.transform.SetParent(displayPoint);
            item.transform.localPosition = Vector3.zero;
        }
        else
        {
            item.transform.SetParent(transform);
            item.transform.localPosition = Vector3.up * 1.2f;
        }
    }

    /// <summary>UI ipuçları için durum metni.</summary>
    public string GetPromptText(PlayerInteraction player)
    {
        return state switch
        {
            State.Empty        => "E: 1. Ürünü Koy",
            State.HoldingFirst => CanInteract(player)
                                  ? $"E: Birleştir → {PreviewName(player)}"
                                  : $"Farklı ürün gerekli ({firstInput} kondu)",
            State.Assembling   => $"Montaj... {Mathf.CeilToInt(timer)}s",
            State.Ready        => "E: Modülü Al",
            _                  => ""
        };
    }

    private string PreviewName(PlayerInteraction player)
    {
        ItemType? second = HeldProcessedType(player);
        if (second == null) return "?";
        return ModuleCatalog.TrName(
            ModuleCatalog.GetRecipeResult(firstInput, second.Value));
    }
}
