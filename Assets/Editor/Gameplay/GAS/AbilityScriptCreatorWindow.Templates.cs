#if UNITY_EDITOR
namespace Framework.Gameplay.Abilities.Editor
{
    internal sealed partial class AbilityScriptCreatorWindow
    {
        /// <summary>生成能力配置资产脚本内容。</summary>
        private static string BuildAbilitySO(string abilityName)
        {
            string assetName = $"{abilityName}Ability";
            return $@"using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{{
    /// <summary>保存 {abilityName} 能力的静态配置。</summary>
    [CreateAssetMenu(fileName = ""{assetName}"", menuName = ""Framework/Gameplay/Abilities/{abilityName}"")]
    public sealed class {abilityName}AbilitySO : AbilityDefinitionSO
    {{
        /// <summary>创建 {abilityName} 能力运行时。</summary>
        /// <returns>使用当前配置的 {abilityName} 能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {{
            return new {abilityName}AbilityRuntime(this);
        }}
    }}
}}
";
        }

        /// <summary>生成能力运行时数据脚本内容。</summary>
        private static string BuildAbilityRuntimeData(string abilityName)
        {
            return $@"namespace Framework.Gameplay.Abilities
{{
    /// <summary>保存 {abilityName} 能力向其他能力公开的运行时数据。</summary>
    public sealed class {abilityName}AbilityRuntimeData : IAbilityRuntimeData
    {{
        /// <summary>清空 {abilityName} 能力共享数据。</summary>
        public void Reset()
        {{
        }}
    }}
}}
";
        }

        /// <summary>生成能力运行时脚本内容。</summary>
        private static string BuildAbilityRuntime(string abilityName)
        {
            return $@"using Framework.Gameplay.Abilities.Configuration;

namespace Framework.Gameplay.Abilities
{{
    /// <summary>执行 {abilityName} 能力的单位独占运行时逻辑。</summary>
    public sealed class {abilityName}AbilityRuntime : AbilityRuntime
    {{
        // 当前能力的静态配置。
        private readonly {abilityName}AbilitySO _configuration;
        // 当前能力向其他能力公开的运行时数据。
        private {abilityName}AbilityRuntimeData _runtimeData;
        // 当前能力运行时数据在拥有者上下文中的注册键。
        private const AbilityRuntimeDataType RuntimeDataType = AbilityRuntimeDataType.{abilityName};

        /// <summary>创建 {abilityName} 能力运行时并保存配置引用。</summary>
        /// <param name=""configuration"">{abilityName} 能力配置资产。</param>
        public {abilityName}AbilityRuntime({abilityName}AbilitySO configuration)
        {{
            _configuration = configuration;
        }}

        /// <summary>绑定能力拥有者上下文并初始化运行时依赖。</summary>
        /// <param name=""ownerContext"">当前单位的能力拥有者上下文。</param>
        public override void AbilityInit(AbilityOwnerContext ownerContext)
        {{
            base.AbilityInit(ownerContext);
            if (ownerContext == null || ownerContext.Owner == null) return;

            // 创建并注册当前能力向其他能力公开的运行时数据。
            _runtimeData = new {abilityName}AbilityRuntimeData();
            OwnerContext.Register(RuntimeDataType, _runtimeData);
        }}

        /// <summary>清空能力启用前的运行时状态。</summary>
        public override void AbilityOnEnable()
        {{
            _runtimeData?.Reset();
        }}

        /// <summary>执行能力启动阶段。</summary>
        public override void AbilityStart()
        {{
        }}

        /// <summary>执行能力普通帧逻辑。</summary>
        /// <param name=""deltaTime"">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {{
        }}

        /// <summary>执行能力固定帧逻辑。</summary>
        /// <param name=""fixedDeltaTime"">当前固定帧时长，单位：秒。</param>
        public override void AbilityFixedUpdate(float fixedDeltaTime)
        {{
        }}

        /// <summary>执行能力延迟帧逻辑。</summary>
        /// <param name=""deltaTime"">当前帧时长，单位：秒。</param>
        public override void AbilityLateUpdate(float deltaTime)
        {{
        }}

        /// <summary>清理能力禁用时的运行时状态。</summary>
        public override void AbilityOnDisable()
        {{
            _runtimeData?.Reset();
        }}

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {{
            AbilityOnDisable();
            OwnerContext?.Unregister(RuntimeDataType, _runtimeData);
            _runtimeData = null;
            base.AbilityDispose();
        }}
    }}
}}
";
        }
    }
}
#endif
