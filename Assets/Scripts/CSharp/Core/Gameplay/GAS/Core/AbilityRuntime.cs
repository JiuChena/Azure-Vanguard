namespace Framework.Gameplay.Abilities
{
    /// <summary>定义由 AbilityComponent 驱动的纯 C# 能力运行时。</summary>
    public abstract class AbilityRuntime
    {
        // 当前能力所属单位的上下文。
        protected AbilityOwnerContext OwnerContext { get; private set; }

        /// <summary>绑定能力拥有者上下文并初始化运行时依赖。</summary>
        /// <param name="ownerContext">当前单位的能力拥有者上下文。</param>
        public virtual void AbilityInit(AbilityOwnerContext ownerContext)
        {
            OwnerContext = ownerContext;
        }

        /// <summary>执行能力启用阶段。</summary>
        public virtual void AbilityOnEnable() { }

        /// <summary>执行能力启动阶段。</summary>
        public virtual void AbilityStart() { }

        /// <summary>执行能力普通帧阶段。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public virtual void AbilityUpdate(float deltaTime) { }

        /// <summary>执行能力固定帧阶段。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public virtual void AbilityFixedUpdate(float fixedDeltaTime) { }

        /// <summary>执行能力延迟帧阶段。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public virtual void AbilityLateUpdate(float deltaTime) { }

        /// <summary>执行能力禁用阶段。</summary>
        public virtual void AbilityOnDisable() { }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public virtual void AbilityDispose()
        {
            OwnerContext = null;
        }
    }
}
