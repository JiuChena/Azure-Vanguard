using UnityEngine;

/// <summary>用于引用数组与循环检测的内存配置夹具。</summary>
public sealed class BehaviorPickerReferenceFixture : ScriptableObject
{
    // 合成行为资产与自循环配置引用，仅供编辑器测试。
    [Tooltip("测试用的行为资产和自循环引用")]
    public Object[] references;
}
