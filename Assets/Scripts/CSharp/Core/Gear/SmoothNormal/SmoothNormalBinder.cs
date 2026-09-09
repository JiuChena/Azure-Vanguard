using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平滑法线描边绑定组件（通用框架能力）。
/// 挂在角色根节点下的 "Smooth Normal Directions" 子物体上，
/// 持有"平滑克隆网格 ↔ 源网格 ↔ 被替换的 renderer"三列表映射。
/// 由编辑器工具（Tools/NormalSmooth）维护；运行时仅持有引用，无 Update/逻辑。
/// 替代旧的 JSON 映射文件方案，引用跟随 Prefab/场景序列化，天然安全。
/// </summary>
public class SmoothNormalBinder : MonoBehaviour
{
    // 法线合并角度阈值（度），生成平滑网格时使用。
    [SerializeField] private float mergeAngle = 60f;
    // 平滑克隆网格列表：由工具生成的 _SmoothNormal 资产引用。
    [SerializeField] private List<Mesh> smoothedMeshes = new List<Mesh>();
    // 源网格列表：与平滑网格一一对应，用于恢复引用。
    [SerializeField] private List<Mesh> originalMeshes = new List<Mesh>();
    // 被替换共享网格的 Renderer 列表：与源网格一一对应。
    [SerializeField] private List<Renderer> boundRenderers = new List<Renderer>();

    public float MergeAngle => mergeAngle;
    public List<Mesh> SmoothedMeshes => smoothedMeshes;
    public List<Mesh> OriginalMeshes => originalMeshes;
    public List<Renderer> BoundRenderers => boundRenderers;

    public void SetMergeAngle(float angle) => mergeAngle = Mathf.Clamp(angle, 0f, 180f);
    public void ClearBindings()
    {
        smoothedMeshes?.Clear();
        originalMeshes?.Clear();
        boundRenderers?.Clear();
    }
}
