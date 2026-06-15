    // EMPEffect.cs
// Görev: BattleRobot'a eklenir, EMP çarptığında robotu
//        belirli süre tamamen dondurur.
// BattleRobot her Update başında IsFrozen kontrolü yapar.

using UnityEngine;

public class EMPEffect : MonoBehaviour
{
    private float     freezeTimer    = 0f;
    private bool      isFrozen       = false;
    private Renderer  bodyRenderer;

    // Shader property
    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock propBlock;

    public bool IsFrozen => isFrozen;

    private void Awake()
    {
        bodyRenderer = GetComponentInChildren<Renderer>();
        propBlock    = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (!isFrozen) return;

        freezeTimer -= Time.deltaTime;

        // Dondurulmuş efekt: mavi titreme
        float pulse = Mathf.PingPong(Time.time * 4f, 1f);
        ApplyFreezeColor(Color.Lerp(Color.blue, Color.cyan, pulse));

        if (freezeTimer <= 0f)
            Unfreeze();
    }

    /// <summary>EMP çarptığında BattleRobot çağırır.</summary>
    public void ApplyFreeze(float duration)
    {
        isFrozen    = true;
        freezeTimer = duration;

        Debug.Log($"<color=blue>[EMP] {gameObject.name} {duration}s donduruldu!</color>");
    }

    private void Unfreeze()
    {
        isFrozen = false;
        ApplyFreezeColor(Color.white); // Normal renge dön
        Debug.Log($"<color=cyan>[EMP] {gameObject.name} EMP etkisinden çıktı.</color>");
    }

    private void ApplyFreezeColor(Color color)
    {
        if (bodyRenderer == null) return;
        bodyRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(PropColor, color);
        bodyRenderer.SetPropertyBlock(propBlock);
    }
}