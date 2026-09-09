namespace Framework.Gameplay.Abilities
{
    /// <summary>保存 Jump 能力向其他能力公开的运行时数据。</summary>
    public sealed class JumpAbilityRuntimeData : IAbilityRuntimeData
    {
        public float ySpeed;
        public bool isGrounded;
        
        /// <summary>清空 Jump 能力共享数据。</summary>
        public void Reset()
        {
        }
    }
}
