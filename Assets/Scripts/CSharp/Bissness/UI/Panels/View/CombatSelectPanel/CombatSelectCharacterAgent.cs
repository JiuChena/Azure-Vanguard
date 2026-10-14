using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 角色动画状态枚举。
/// </summary>
public enum CombatSelectCharacterState
{
    // 静待/常态待机
    Idle,
    // 漫步/随机行走
    Walk,
    // 受击/互动反馈（被点击）
    Reaction
}

/// <summary>
/// 战斗选择面板角色模型展示控制代理（挂在非战斗展示模型预制体根节点）：
/// 负责管理 Idle、Walk、Reaction 三段 Timeline 的播放与自律状态机轮转。
/// - 平时保持 Idle；
/// - 隔一段随机时间自律切换至 Walk 并持续随机时长，随后切回 Idle；
/// - 玩家点击（UI 触发或 3D 点击）时立即打断当前动作，播放 Reaction 反馈，播完切回 Idle。
/// </summary>
public class CombatSelectCharacterAgent : MonoBehaviour, IPointerClickHandler
{
    [Header("播放器组件与绑定")]
    // 驱动 Timeline 播放的 Director
    [SerializeField] private PlayableDirector director;
    // 角色 Animator 组件（用于轨道重绑）
    [SerializeField] private Animator animator;

    [Header("Timeline 资产")]
    // 常态待机 Timeline
    [SerializeField] private PlayableAsset idleTimeline;
    // 走动 Timeline
    [SerializeField] private PlayableAsset walkTimeline;
    // 玩家点击交互反馈 Timeline
    [SerializeField] private PlayableAsset reactionTimeline;

    [Header("自律行为配置（秒）")]
    // Idle 状态最小持续时长
    [SerializeField] private float minIdleDuration = 8f;
    // Idle 状态最大持续时长
    [SerializeField] private float maxIdleDuration = 16f;
    // Walk 状态最小持续时长
    [SerializeField] private float minWalkDuration = 2.5f;
    // Walk 状态最大持续时长
    [SerializeField] private float maxWalkDuration = 5f;

    // 当前运行状态
    private CombatSelectCharacterState currentState = CombatSelectCharacterState.Idle;
    // 当前状态剩余倒计时计时器
    private float stateTimer;

    private void Awake()
    {
        // 自动解析同物体上的 Director 与 Animator
        if (director == null) director = GetComponent<PlayableDirector>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // 初始切入常态待机
        EnterIdleState();
    }

    private void Update()
    {
        // 倒计时更新与状态机轮转
        if (stateTimer > 0f)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                OnStateTimerExpired();
            }
        }
    }

    #region 对外交互接口

    /// <summary>
    /// 初始化设置 Timeline 资产与 Director 引用。
    /// </summary>
    /// <param name="dir">播放控制器。</param>
    /// <param name="idle">待机 Timeline。</param>
    /// <param name="walk">行走 Timeline。</param>
    /// <param name="reaction">互动反应 Timeline。</param>
    public void Setup(PlayableDirector dir, PlayableAsset idle, PlayableAsset walk, PlayableAsset reaction)
    {
        director = dir != null ? dir : GetComponent<PlayableDirector>();
        idleTimeline = idle;
        walkTimeline = walk;
        reactionTimeline = reaction;
        EnterIdleState();
    }

    /// <summary>
    /// 触发玩家点击反应（播放 Reaction 动画）：打断当前行走或待机，播完后恢复待机。
    /// </summary>
    public void TriggerReaction()
    {
        if (reactionTimeline == null && (director == null || director.playableAsset == null))
        {
            return;
        }

        currentState = CombatSelectCharacterState.Reaction;

        PlayableAsset targetAsset = reactionTimeline != null ? reactionTimeline : director.playableAsset;
        PlayTimeline(targetAsset, DirectorWrapMode.None);

        // 计算反馈动画播放时长，作为停留计时
        float duration = targetAsset != null ? (float)targetAsset.duration : 2f;
        stateTimer = duration > 0.1f ? duration : 2f;
    }

    /// <summary>
    /// 响应 EventSystem 的 3D 指针点击事件。
    /// </summary>
    /// <param name="eventData">指针事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        TriggerReaction();
    }

    #endregion

    #region 内部状态切换

    /// <summary>
    /// 当前状态计时结束时的状态转移逻辑。
    /// </summary>
    private void OnStateTimerExpired()
    {
        switch (currentState)
        {
            case CombatSelectCharacterState.Idle:
                // 待机超时 → 偶尔进入走动态
                EnterWalkState();
                break;

            case CombatSelectCharacterState.Walk:
                // 走动结束 → 返回待机
                EnterIdleState();
                break;

            case CombatSelectCharacterState.Reaction:
                // 反应动作播完 → 返回待机
                EnterIdleState();
                break;
        }
    }

    /// <summary>
    /// 进入待机状态：循环播放 Idle Timeline，随机计算下一次走动前的待机时长。
    /// </summary>
    private void EnterIdleState()
    {
        currentState = CombatSelectCharacterState.Idle;
        PlayTimeline(idleTimeline, DirectorWrapMode.Loop);
        stateTimer = Random.Range(minIdleDuration, maxIdleDuration);
    }

    /// <summary>
    /// 进入行走状态：循环播放 Walk Timeline，随机计算本次走动的持续时长。
    /// </summary>
    private void EnterWalkState()
    {
        if (walkTimeline == null)
        {
            // 若未配置走动动画则继续保持待机
            stateTimer = Random.Range(minIdleDuration, maxIdleDuration);
            return;
        }

        currentState = CombatSelectCharacterState.Walk;
        PlayTimeline(walkTimeline, DirectorWrapMode.Loop);
        stateTimer = Random.Range(minWalkDuration, maxWalkDuration);
    }

    /// <summary>
    /// 底层驱动 Director 播放指定 Timeline，并重绑定 Animator 输出轨道。
    /// </summary>
    /// <param name="asset">待播放的 PlayableAsset。</param>
    /// <param name="wrapMode">循环模式。</param>
    private void PlayTimeline(PlayableAsset asset, DirectorWrapMode wrapMode)
    {
        if (director == null || asset == null) return;

        director.playableAsset = asset;
        director.extrapolationMode = wrapMode;

        // 重新绑定动画轨道至本角色的 Animator
        if (animator != null && asset is TimelineAsset timeline)
        {
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is AnimationTrack)
                {
                    director.SetGenericBinding(track, animator);
                }
            }
        }

        director.time = 0;
        director.Play();
    }

    #endregion
}
