using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>挂载能力配置列表并按列表顺序驱动纯 C# 能力运行时。</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class GameplayAbilitySystem : MonoBehaviour
    {
        // 当前单位的能力拥有者上下文。
        private AbilityOwnerContext _ownerContext;
        // 能力配置列表：配置资产 → 单位独占的纯 C# 能力运行时。
        [Header("能力配置")]
        [Tooltip("按列表顺序创建并驱动能力运行时；能力之间的执行顺序由此列表决定")]
        [SerializeField] private List<AbilityDefinitionSO> _abilityDefinitions = new List<AbilityDefinitionSO>();
        // 当前单位创建的能力运行时列表。
        private readonly List<AbilityRuntime> _abilities = new List<AbilityRuntime>();
        // 防止销毁流程重复执行。
        private bool _disposed;
        /// <summary>获取当前单位的能力拥有者上下文。</summary>
        public AbilityOwnerContext OwnerContext => _ownerContext;

        /// <summary>创建上下文并根据配置列表初始化能力运行时。</summary>
        private void Awake()
        {
            InitializeAbilities();
        }

        /// <summary>创建上下文并根据配置列表初始化能力运行时。</summary>
        private void InitializeAbilities()
        {
            _ownerContext = new AbilityOwnerContext(gameObject);

            // 按 Inspector 列表顺序创建能力运行时并绑定上下文。
            for (int index = 0; index < _abilityDefinitions.Count; index++)
            {
                AbilityDefinitionSO definition = _abilityDefinitions[index];
                if (definition == null)
                {
                    Debug.LogWarning($"GAS能力列表索引为[{index}]的位置为空，执行失败，请重新检查容器！");
                    continue;
                }
                AbilityRuntime ability = definition.CreateRuntime();
                if (ability == null)
                {
                    Debug.LogWarning($"{definition}所创建的Ability为空，执行失败，请重新检查容器！");
                    continue;
                }
                _abilities.Add(ability);
                ability.AbilityInit(_ownerContext);
            }
        }

        /// <summary>转发能力启用阶段。</summary>
        private void OnEnable()
        {
            if (_ownerContext == null) return;
            for (int index = 0; index < _abilities.Count; index++) _abilities[index].AbilityOnEnable();
        }

        /// <summary>转发能力启动阶段。</summary>
        private void Start()
        {
            if (_ownerContext == null) return;
            for (int index = 0; index < _abilities.Count; index++) _abilities[index].AbilityStart();
        }

        /// <summary>转发能力普通帧阶段。</summary>
        private void Update()
        {
            if (_ownerContext == null) return;
            for (int index = 0; index < _abilities.Count; index++) _abilities[index].AbilityUpdate(Time.deltaTime);
        }

        /// <summary>转发能力固定帧阶段。</summary>
        private void FixedUpdate()
        {
            if (_ownerContext == null) return;
            for (int index = 0; index < _abilities.Count; index++) _abilities[index].AbilityFixedUpdate(Time.fixedDeltaTime);
        }

        /// <summary>转发能力延迟帧阶段。</summary>
        private void LateUpdate()
        {
            if (_ownerContext == null) return;
            for (int index = 0; index < _abilities.Count; index++) _abilities[index].AbilityLateUpdate(Time.deltaTime);
        }

        /// <summary>转发能力禁用阶段。</summary>
        private void OnDisable()
        {
            for (int index = _abilities.Count - 1; index >= 0; index--) _abilities[index].AbilityOnDisable();
        }

        /// <summary>遍历能力配置并绘制各配置提供的 Scene 可视化内容。</summary>
        private void OnDrawGizmos()
        {
            // 编辑器绘制只读取序列化配置，不创建上下文、运行时能力或 Unity 组件。
            for (int index = 0; index < _abilityDefinitions.Count; index++)
            {
                AbilityDefinitionSO definition = _abilityDefinitions[index];
                if (definition == null) continue;
                definition.GizmoDraw(gameObject);
            }
        }

        /// <summary>释放能力组件和上下文。</summary>
        private void OnDestroy()
        {
            if (_disposed) return;
            _disposed = true;
            for (int index = _abilities.Count - 1; index >= 0; index--) _abilities[index].AbilityDispose();
            _abilities.Clear();
            _ownerContext = null;
        }
    }
}
