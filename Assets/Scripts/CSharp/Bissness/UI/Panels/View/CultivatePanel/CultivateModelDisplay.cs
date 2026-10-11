using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 养成页角色模型展示组件：挂在非战斗模型预制体根（与 PlayableDirector 同物体），
/// 序列化指定各页的 Timeline 与朝向（数组下标即页号）；面板切页时按页切换播放内容并水平转向对应方位点。
/// </summary>
public class CultivateModelDisplay : MonoBehaviour
{
    //驱动播放的 Director：缺省自动取同物体组件
    [SerializeField] private PlayableDirector director;

    //各页展示配置：数组下标即页号（0 属性/1 武器/2 饰品/3 技能，与 CultivatePanelView.pages 对齐）；越界页停播且不转向
    [SerializeField] private CultivateModelPageShow[] pages;

    /// <summary>
    /// 实例化时补齐 Director 引用：预制体未拖 Director 时取同物体组件。
    /// </summary>
    private void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
    }

    #region 对外接口

    /// <summary>
    /// 按页展示：先水平转向该页方位点，再切换 Timeline 播放；该页未配置（下标越界或 Timeline 为空）时停播且不转向。
    /// </summary>
    /// <param name="pageIndex">页面下标（0 属性/1 武器/2 饰品/3 技能，即 <see cref="pages"/> 数组下标）。</param>
    /// <param name="displayView">展示区视图，提供四个朝向点；为 null 时不转向。</param>
    public void Play(int pageIndex, CultivateCharacterDisplayView displayView)
    {
        if (director == null) return;

        //下标命中且配置了 Timeline 才动作：未配置页保持原姿态并停播
        CultivateModelPageShow show = pages != null && pageIndex >= 0 && pageIndex < pages.Length
            ? pages[pageIndex]
            : null;
        if (show == null || show.timeline == null)
        {
            director.Stop();
            return;
        }

        FaceDirection(show.lookAt, displayView);
        director.playableAsset = show.timeline;
        director.Play();
    }

    /// <summary>停播 Director（面板关闭/切换角色时兜底调用）。</summary>
    public void Stop()
    {
        if (director != null) director.Stop();
    }

    #endregion

    #region Private

    /// <summary>水平转向：模型根只绕 Y 轴转，使前向指向目标方位点（俯仰不变，避免模型倾倒）。</summary>
    private void FaceDirection(CultivateLookDirection direction, CultivateCharacterDisplayView displayView)
    {
        Transform target = direction switch
        {
            CultivateLookDirection.Front => displayView != null ? displayView.forward : null,
            CultivateLookDirection.Back => displayView != null ? displayView.back : null,
            CultivateLookDirection.Left => displayView != null ? displayView.left : null,
            CultivateLookDirection.Right => displayView != null ? displayView.right : null,
            _ => null,
        };
        if (target == null) return;

        //水平面投影后取前向；与方位点重合时无方向可言，不转
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(delta);
    }

    #endregion
}

/// <summary>角色展示朝向：看向展示区四个朝向点中的哪一个。</summary>
public enum CultivateLookDirection
{
    Front, // 正前
    Back,  // 正后
    Left,  // 左侧
    Right, // 右侧
}

/// <summary>单页展示配置：数组下标即页号，本元素只描述该页的 Timeline 与角色朝向。</summary>
[System.Serializable]
public class CultivateModelPageShow
{
    //该页播放的 Timeline；为空时该页停播（恢复静立姿态）
    public TimelineAsset timeline;
    //该页角色看向的方位（对应展示区的朝向点）
    public CultivateLookDirection lookAt = CultivateLookDirection.Front;
}
