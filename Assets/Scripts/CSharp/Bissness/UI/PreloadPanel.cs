using Core.Gear;
using UnityEngine;
using UnityEngine.UI;

public class PreloadPanel : PanelBase
{
    // 溶解材质引用；溶解度直接写入该材质的 _DissolveAmount
    [SerializeField] private Material dissolveMaterial;

    // 溶解度（0 完整显示，1 完全消失），变更时同步到材质
    [Range(0f, 1f)]
    [SerializeField] private float dissolveAmount;

    // 材质属性 ID 缓存，避免每次 SetFloat 字符串查找
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    /// <summary>
    /// 溶解度参数：0 完整显示，1 完全消失；赋值即写入溶解材质。
    /// </summary>
    public float DissolveAmount
    {
        get => dissolveAmount;
        set
        {
            dissolveAmount = Mathf.Clamp01(value);
            ApplyDissolve();
        }
    }

    protected override void EventInit()
    {

    }

    protected override void ComponentInit()
    {
        // 初始化时把当前溶解度写入材质
        ApplyDissolve();
    }

    protected override void OnUpdate()
    {
        ApplyDissolve();
    }

    public override void OnEscapePressed() { }
    

    /// <summary>
    /// 将当前溶解度写入溶解材质；材质缺少 _DissolveAmount 属性时由 Unity 静默忽略。
    /// </summary>
    private void ApplyDissolve()
    {
        if (dissolveMaterial == null) return;
        dissolveMaterial.SetFloat(DissolveAmountId, dissolveAmount);
    }
}
