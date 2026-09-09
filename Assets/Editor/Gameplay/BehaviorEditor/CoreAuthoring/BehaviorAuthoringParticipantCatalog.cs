using System;
using System.Collections.Generic;
using UnityEditor;

namespace BehaviorEditor
{
    /// <summary>
    /// 自动发现并分发作者期轨道参与者。
    /// </summary>
    internal static class BehaviorAuthoringParticipantCatalog
    {
        // 已自动发现的轨道作者期参与者类型。
        private static readonly List<Type> participantTypes = new();

        // 当前目录是否已经完成 TypeCache 扫描。
        private static bool initialized;

        /// <summary>
        /// 调用全部轨道参与者开始准备作者期预览环境。
        /// </summary>
        /// <param name="context">当前作者期会话上下文；不得为 null。</param>
        public static void BeginAuthoring(BehaviorAuthoringSessionContext context)
        {
            if (context == null) return;

            EnsureInitialized();
            context.Participants.Clear();
            for (int index = 0; index < participantTypes.Count; index++)
            {
                IBehaviorAuthoringParticipant participant =
                    (IBehaviorAuthoringParticipant)Activator.CreateInstance(participantTypes[index]);
                if (participant == null) continue;

                context.Participants.Add(participant);
                participant.BeginAuthoring(context);
            }
        }

        /// <summary>
        /// 调用全部轨道参与者清理作者期预览环境。
        /// </summary>
        /// <param name="context">当前作者期会话上下文；不得为 null。</param>
        public static void EndAuthoring(BehaviorAuthoringSessionContext context)
        {
            if (context == null) return;

            EnsureInitialized();
            for (int index = context.Participants.Count - 1; index >= 0; index--)
                context.Participants[index].EndAuthoring(context);

            context.Participants.Clear();
        }

        /// <summary>
        /// 使用 TypeCache 构建所有无参轨道作者期参与者类型。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (initialized) return;

            // 仅在域重载后发现一次，作者期热路径不使用反射。
            foreach (Type participantType in TypeCache.GetTypesDerivedFrom<IBehaviorAuthoringParticipant>())
            {
                if (participantType.IsAbstract || participantType.IsInterface) continue;
                participantTypes.Add(participantType);
            }

            initialized = true;
        }
    }
}
