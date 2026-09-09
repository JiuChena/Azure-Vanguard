using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>定义可由 AbilityComponent 创建的能力配置资产。</summary>
    public abstract class AbilityDefinitionSO : ScriptableObject
    {
        /// <summary>创建当前配置对应的单位独占能力运行时。</summary>
        /// <returns>未初始化的能力运行时实例。</returns>
        public abstract AbilityRuntime CreateRuntime();

        /// <summary>绘制当前能力配置在 Scene 窗口中的编辑器可视化内容。</summary>
        /// <param name="owner">能力配置所属的单位对象；为 null 时不执行绘制。</param>
        public virtual void GizmoDraw(GameObject owner) { }
    }
}
