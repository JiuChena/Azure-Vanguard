using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存区域检测（感知）能力的静态配置。</summary>
    [CreateAssetMenu(fileName = "PerceptionAbility", menuName = "Framework/Gameplay/Abilities/Perception")]
    public sealed class PerceptionAbilitySO : AbilityDefinitionSO
    {
        // 移动判定距离：目的地与当前位置小于该值视为到位（Idle⇄Move 仲裁口径，与 ③ 移动指令对齐）。
        [Tooltip("移动判定距离：③ 意图目的地与当前位置小于该值视为到位；建议与 NavMeshAgent 停止距离同量级")]
        [SerializeField] private float _moveArriveDistance = 0.2f;

        /// <summary>获取移动判定距离（到位阈值）。</summary>
        public float MoveArriveDistance => _moveArriveDistance;

        /// <summary>创建感知能力运行时。</summary>
        /// <returns>使用当前配置的感知能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new PerceptionAbilityRuntime(this);
        }
    }
}
