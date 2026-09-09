using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 提供作者期各轨道共享的 Timeline 会话信息。
    /// </summary>
    internal sealed class BehaviorAuthoringSessionContext
    {
        // 当前作者期会话持有的参与者实例，实例状态只属于本次会话。
        internal readonly System.Collections.Generic.List<IBehaviorAuthoringParticipant> Participants =
            new System.Collections.Generic.List<IBehaviorAuthoringParticipant>();

        /// <summary>当前作者期使用的 Timeline 资产。</summary>
        public TimelineAsset Timeline { get; }
        /// <summary>当前作者期使用的 PlayableDirector。</summary>
        public PlayableDirector Director { get; }
        /// <summary>当前作者期指定的角色根节点。</summary>
        public GameObject ReferenceRoot { get; }

        /// <summary>
        /// 创建作者期会话上下文。
        /// </summary>
        /// <param name="timeline">当前 Timeline 资产；允许为 null。</param>
        /// <param name="director">当前 PlayableDirector；允许为 null。</param>
        /// <param name="referenceRoot">角色根节点；允许为 null。</param>
        public BehaviorAuthoringSessionContext(
            TimelineAsset timeline,
            PlayableDirector director,
            GameObject referenceRoot)
        {
            Timeline = timeline;
            Director = director;
            ReferenceRoot = referenceRoot;
        }
    }

    /// <summary>
    /// 声明具体轨道参与作者期会话的可选契约。
    /// </summary>
    internal interface IBehaviorAuthoringParticipant
    {
        /// <summary>
        /// 在 Timeline 作者期会话开始后准备本轨道需要的预览环境。
        /// </summary>
        /// <param name="context">当前作者期会话共享上下文；不得为 null。</param>
        void BeginAuthoring(BehaviorAuthoringSessionContext context);

        /// <summary>
        /// 在 Timeline 作者期会话结束前清理本轨道临时创建的预览资源。
        /// </summary>
        /// <param name="context">当前作者期会话共享上下文；不得为 null。</param>
        void EndAuthoring(BehaviorAuthoringSessionContext context);
    }
}
