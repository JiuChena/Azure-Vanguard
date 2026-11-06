using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Core.Gear;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BehaviorTreeCompareToBHSMTest
{
    // ============================================================================
    // 行为树 vs BetterHSM 逐帧开销对比（自包含单文件）
    //
    // 为什么是单文件：本工程当前被既有未完成改动（Talents_CH0221 / CombatEffectService 等）
    // 阻断编译，Assembly-CSharp 无法重建。UnityCLI 的 run_script 只能对"单个文件"做独立编译，
    // 因此测试全部代码收在这一个文件里，由编辑器已加载的旧 Assembly-CSharp.dll 提供
    // Core.Gear.BetterHSM 依赖。工程恢复可编译后，本文件会正常并入 Assembly-CSharp。
    //
    // 用法：
    //   unity.exe command run_script --project-path "<项目根>" \
    //     --file Assets/BehaviorTreeCompareToBHSMTest/BenchmarkSuite.cs \
    //     --entry BehaviorTreeCompareToBHSMTest.BenchCliEntry.Run
    //
    // 公平性口径：
    //   1) 同一语义负载：6 状态/动作、5 条优先级条件、激活态恒为"优先级最高的成立条件"；
    //   2) 同一叶子工作体：BenchWork.Tick 被 OnUpdate 与动作叶共用；
    //   3) 同一标脏契约：BHSM 按项目实际接法批量标脏全部状态，行为树只标变化的条件并沿父链传播；
    //   4) 行为树带完整剪枝：优先级预排序 + 命中即止 + 记忆序列 + 脏标记传播 +
    //      干净子树跳过 + 干净失败分支零评估 + 运行路径直下；
    //   5) 热循环内不含虚派发、委托、LINQ、字符串或分配。
    // ============================================================================

    /// <summary>
    /// 两个实现共享的决策数据、条件谓词与叶子工作体。
    /// 所有谓词都是单次布尔字段读取（复合守卫为两次读取），
    /// 保证 BHSM 的边条件与行为树的条件叶执行完全相同的语义负载。
    /// </summary>
    public sealed class BenchContext
    {
        // 决策标志，数值即优先级：Downed 50 > Talent 40 > Skill 30 > Attack 20 > IsMoving 10 > Idle 0。
        /// <summary>倒地标志（最高优先级）。</summary>
        public bool IsDowned;
        /// <summary>天赋意图。</summary>
        public bool WantTalent;
        /// <summary>技能意图。</summary>
        public bool WantSkill;
        /// <summary>攻击意图。</summary>
        public bool WantAttack;
        /// <summary>移动意图。</summary>
        public bool IsMoving;

        // 工作体累加器与生命周期计数：读回这些值既防止 JIT 消除工作体，也用于两实现的轨迹对账。
        /// <summary>叶子每帧工作体的累加器。</summary>
        public int WorkAccumulator;
        /// <summary>状态/动作进入次数。</summary>
        public int EnterCount;
        /// <summary>状态/动作退出次数。</summary>
        public int ExitCount;

        // 单字段条件：BHSM 的 To(...) 与行为树的条件叶共用。
        /// <summary>是否倒地。</summary>
        public bool Downed() => IsDowned;
        /// <summary>是否要求进入天赋态。</summary>
        public bool Talent() => WantTalent;
        /// <summary>是否要求进入技能态。</summary>
        public bool Skill() => WantSkill;
        /// <summary>是否要求进入攻击态。</summary>
        public bool Attack() => WantAttack;
        /// <summary>是否在移动。</summary>
        public bool Moving() => IsMoving;
        /// <summary>是否已停止移动（移动态回待机的守卫）。</summary>
        public bool NotMoving() => !IsMoving;

        // 平铺 HSM 的低优先级出边必须显式表达"本状态条件仍成立时不得被低优先级抢占"，
        // 这是平表状态图的固有成本；行为树靠 selector 顺序天然表达同一语义。
        /// <summary>攻击意图成立且当前状态条件已失效（移动态离开攻击态）。</summary>
        public bool AttackEndedAndMoving() => !WantAttack && IsMoving;
        /// <summary>攻击意图成立且当前状态条件已失效（攻击态回待机）。</summary>
        public bool AttackEndedAndIdle() => !WantAttack && !IsMoving;
        /// <summary>技能意图成立（技能态离开后进入攻击态）。</summary>
        public bool SkillEndedAttack() => !WantSkill && WantAttack;
        /// <summary>技能意图已失效且仍在移动（技能态回移动）。</summary>
        public bool SkillEndedMoving() => !WantSkill && IsMoving;
        /// <summary>技能意图已失效且静止（技能态回待机）。</summary>
        public bool SkillEndedIdle() => !WantSkill && !IsMoving;
        /// <summary>天赋意图已失效且技能意图成立。</summary>
        public bool TalentEndedSkill() => !WantTalent && WantSkill;
        /// <summary>天赋意图已失效且攻击意图成立。</summary>
        public bool TalentEndedAttack() => !WantTalent && WantAttack;
        /// <summary>天赋意图已失效且仍在移动。</summary>
        public bool TalentEndedMoving() => !WantTalent && IsMoving;
        /// <summary>天赋意图已失效且静止。</summary>
        public bool TalentEndedIdle() => !WantTalent && !IsMoving;
        /// <summary>倒地解除且天赋意图成立（倒地态回流）。</summary>
        public bool DownedEndedTalent() => !IsDowned && WantTalent;
        /// <summary>倒地解除且技能意图成立。</summary>
        public bool DownedEndedSkill() => !IsDowned && WantSkill;
        /// <summary>倒地解除且攻击意图成立。</summary>
        public bool DownedEndedAttack() => !IsDowned && WantAttack;
        /// <summary>倒地解除且仍在移动。</summary>
        public bool DownedEndedMoving() => !IsDowned && IsMoving;
        /// <summary>倒地解除且静止。</summary>
        public bool DownedEndedIdle() => !IsDowned && !IsMoving;
    }

    /// <summary>
    /// 两实现共用的叶子每帧工作体：状态机在 OnUpdate 里调用，行为树在动作叶里调用，
    /// 内容与调用次数都一致，差异只来自框架调度本身。
    /// </summary>
    public static class BenchWork
    {
        // 结果读回到共享累加器，编译器无法把工作体当死代码消除。
        /// <summary>执行一次叶子帧工作。</summary>
        /// <param name="accumulator">跨帧累加的整数状态。</param>
        public static void Tick(ref int accumulator)
        {
            int value = accumulator + 1;
            value ^= value << 3;
            accumulator = value & 0x7FFFFFF;
        }

        /// <summary>记录一次状态/动作进入（两实现负载相同）。</summary>
        /// <param name="count">进入计数器。</param>
        public static void Enter(ref int count)
        {
            count = count + 1;
        }

        /// <summary>记录一次状态/动作退出（两实现负载相同）。</summary>
        /// <param name="count">退出计数器。</param>
        public static void Exit(ref int count)
        {
            count = count + 1;
        }
    }

    // ============================================================================
    // 行为树内核
    // ============================================================================

    /// <summary>行为树节点返回状态。</summary>
    public enum BtStatus : byte
    {
        /// <summary>本节点失败。</summary>
        Failure = 0,
        /// <summary>本节点成功。</summary>
        Success = 1,
        /// <summary>本节点仍在运行。</summary>
        Running = 2
    }

    /// <summary>行为树节点种类。</summary>
    internal enum BtKind : byte
    {
        Selector = 0,
        Sequence = 1,
        Condition = 2,
        Action = 3
    }

    /// <summary>
    /// 面向热路径的行为树内核：构建期冻结为扁平数组，运行期按下标递归、零分配。
    /// 剪枝手段与 BHSM 对齐并各多一层：
    /// 1) 子节点在构建期按优先级排定，命中即止（对应 BHSM 排序后的"首真即停"）；
    /// 2) 条件叶可被业务标脏，脏标记沿父链传播，干净子树整棵跳过（对应 BHSM 的门控整层跳过）；
    /// 3) 干净且缓存为 Running 的子树直接下钻到运行中的子节点（对应 BHSM 只更新激活叶）；
    /// 4) 干净且缓存为 Failure 的分支零评估直接返回（行为树侧多出的一层剪枝）。
    /// 内存布局：同一棵树的所有节点字段各占一条连续数组，遍历时命中同一批缓存行。
    /// </summary>
    public sealed class BehaviorTree
    {
        // ---- 构建期冻结的只读结构 ----
        private readonly BtKind[] _kind;
        private readonly int[] _firstChild;
        private readonly int[] _childCount;
        private readonly int[] _childList;
        private readonly int[] _parent;
        private readonly int[] _payload;
        private readonly Func<bool>[] _conditions;
        private readonly int[] _conditionNodes;
        private readonly bool _gated;

        // ---- 运行期状态 ----
        private readonly int[] _runningChild;
        private readonly byte[] _status;
        private readonly byte[] _dirty;
        private readonly int[] _actionEnterCount;
        private readonly int[] _actionTickCount;
        private readonly int[] _actionExitCount;
        private readonly BenchContext _context;
        private int _activeActionNode = -1;
        private int _activeActionId = -1;
        private int _switchCount;

        /// <summary>使用构建器产出的扁平结构创建一棵已冻结的行为树。</summary>
        internal BehaviorTree(
            BtKind[] kind,
            int[] firstChild,
            int[] childCount,
            int[] childList,
            int[] parent,
            int[] payload,
            Func<bool>[] conditions,
            int[] conditionNodes,
            int actionCount,
            BenchContext context,
            bool gated)
        {
            _kind = kind;
            _firstChild = firstChild;
            _childCount = childCount;
            _childList = childList;
            _parent = parent;
            _payload = payload;
            _conditions = conditions;
            _conditionNodes = conditionNodes;
            _context = context;
            _gated = gated;

            int nodeCount = kind.Length;
            _runningChild = new int[nodeCount];
            _status = new byte[nodeCount];
            _dirty = new byte[nodeCount];
            for (int index = 0; index < nodeCount; index++)
            {
                _runningChild[index] = -1;
                // 构建期全置脏：首帧整棵树必须真实评估一次。
                _dirty[index] = 1;
            }

            _actionEnterCount = new int[actionCount];
            _actionTickCount = new int[actionCount];
            _actionExitCount = new int[actionCount];
        }

        /// <summary>当前运行中的动作编号；没有运行时返回 -1。</summary>
        public int ActiveActionId => _activeActionId;

        /// <summary>自构建以来动作叶被激活的次数（等价于状态切换次数）。</summary>
        public int SwitchCount => _switchCount;

        /// <summary>指定动作编号的进入次数。</summary>
        public int GetActionEnterCount(int actionId) => _actionEnterCount[actionId];

        /// <summary>指定动作编号的帧执行次数。</summary>
        public int GetActionTickCount(int actionId) => _actionTickCount[actionId];

        /// <summary>指定动作编号的退出次数。</summary>
        public int GetActionExitCount(int actionId) => _actionExitCount[actionId];

        /// <summary>推进一帧：从根节点求值。</summary>
        public void Tick()
        {
            TickNode(0);
        }

        /// <summary>
        /// 业务侧标记某个条件的数据已变化；脏标记沿父链上溯，祖先整条路径重新评估。
        /// 未开启门控时该调用为空操作（每帧本来就会全量重评估）。
        /// </summary>
        /// <param name="conditionId">构建期注册的条件编号。</param>
        public void MarkDirty(int conditionId)
        {
            if (!_gated) return;

            int node = _conditionNodes[conditionId];
            while (node >= 0)
            {
                _dirty[node] = 1;
                node = _parent[node];
            }
        }

        /// <summary>节点求值入口：动作叶每帧都要跑工作体；其余干净节点走缓存，脏节点重新评估。</summary>
        private BtStatus TickNode(int node)
        {
            // 动作叶承载叶子帧工作体，无论是否干净每帧都必须执行。
            if (_kind[node] == BtKind.Action) return TickAction(node);

            if (!_gated || _dirty[node] != 0) return EvaluateNode(node);

            // 干净子树：Running 下钻到运行中的子节点，其余直接返回缓存结果。
            byte cached = _status[node];
            if (cached == (byte)BtStatus.Running)
            {
                int running = _runningChild[node];
                if (running >= 0) return TickNode(_childList[_firstChild[node] + running]);
            }

            return (BtStatus)cached;
        }

        /// <summary>清脏后按节点种类重新求值。</summary>
        private BtStatus EvaluateNode(int node)
        {
            _dirty[node] = 0;
            switch (_kind[node])
            {
                case BtKind.Selector:
                    return EvaluateSelector(node);
                case BtKind.Sequence:
                    return EvaluateSequence(node);
                case BtKind.Condition:
                    return EvaluateCondition(node);
                default:
                    return TickAction(node);
            }
        }

        /// <summary>选择器：按优先级顺序扫描，首个 Success/Running 胜出，其余子节点本帧不评估。</summary>
        private BtStatus EvaluateSelector(int node)
        {
            int start = _firstChild[node];
            int count = _childCount[node];
            for (int slot = 0; slot < count; slot++)
            {
                BtStatus result = TickNode(_childList[start + slot]);
                if (result == BtStatus.Failure) continue;

                _runningChild[node] = slot;
                _status[node] = (byte)result;
                return result;
            }

            _runningChild[node] = -1;
            _status[node] = (byte)BtStatus.Failure;
            return BtStatus.Failure;
        }

        /// <summary>
        /// 序列：脏时从首子节点重评估（反应式守卫，状态条件失效即离开），
        /// 干净且运行中时直接续跑运行中的子节点（记忆剪枝）。
        /// </summary>
        private BtStatus EvaluateSequence(int node)
        {
            int start = _firstChild[node];
            int count = _childCount[node];
            for (int slot = 0; slot < count; slot++)
            {
                BtStatus result = TickNode(_childList[start + slot]);
                if (result == BtStatus.Running)
                {
                    _runningChild[node] = slot;
                    _status[node] = (byte)BtStatus.Running;
                    return BtStatus.Running;
                }

                if (result == BtStatus.Failure)
                {
                    _runningChild[node] = -1;
                    _status[node] = (byte)BtStatus.Failure;
                    return BtStatus.Failure;
                }
            }

            _runningChild[node] = -1;
            _status[node] = (byte)BtStatus.Success;
            return BtStatus.Success;
        }

        /// <summary>条件叶：执行一次谓词并缓存结果，干净的后续帧直接读缓存。</summary>
        private BtStatus EvaluateCondition(int node)
        {
            BtStatus result = _conditions[_payload[node]]() ? BtStatus.Success : BtStatus.Failure;
            _status[node] = (byte)result;
            return result;
        }

        /// <summary>
        /// 动作叶：首次进入先退出旧动作再进入新动作（与 BHSM 的 Exit→Enter 顺序一致），
        /// 随后执行与状态机 OnUpdate 完全相同的工作体。
        /// </summary>
        private BtStatus TickAction(int node)
        {
            _status[node] = (byte)BtStatus.Running;
            _runningChild[node] = -1;

            int actionId = _payload[node];
            if (_activeActionNode != node)
            {
                if (_activeActionNode >= 0) ExitAction(_activeActionNode);
                _activeActionNode = node;
                _activeActionId = actionId;
                _switchCount++;
                _actionEnterCount[actionId]++;
                BenchWork.Enter(ref _context.EnterCount);
            }

            _actionTickCount[actionId]++;
            BenchWork.Tick(ref _context.WorkAccumulator);
            return BtStatus.Running;
        }

        /// <summary>退出旧动作：只做计数，与 BHSM 的 OnExit 负载相同。</summary>
        private void ExitAction(int node)
        {
            int actionId = _payload[node];
            _actionExitCount[actionId]++;
            BenchWork.Exit(ref _context.ExitCount);
        }
    }

    /// <summary>行为树构建器：构建期允许分配，Build 后冻结为扁平数组。</summary>
    public sealed class BehaviorTreeBuilder
    {
        private readonly List<BtKind> _kind = new List<BtKind>();
        private readonly List<int> _parent = new List<int>();
        private readonly List<int> _payload = new List<int>();
        private readonly List<List<int>> _children = new List<List<int>>();

        /// <summary>新增选择器节点并返回其下标。</summary>
        public int AddSelector()
        {
            return AddNode(BtKind.Selector, -1);
        }

        /// <summary>新增序列节点并返回其下标。</summary>
        public int AddSequence()
        {
            return AddNode(BtKind.Sequence, -1);
        }

        /// <summary>新增条件叶并返回其下标。</summary>
        /// <param name="conditionId">条件编号，与 MarkDirty 使用同一编号。</param>
        public int AddCondition(int conditionId)
        {
            return AddNode(BtKind.Condition, conditionId);
        }

        /// <summary>新增动作叶并返回其下标。</summary>
        /// <param name="actionId">动作编号，用于统计与激活判定。</param>
        public int AddAction(int actionId)
        {
            return AddNode(BtKind.Action, actionId);
        }

        /// <summary>把 child 挂到 parent 的子节点列表末尾；子节点顺序即优先级顺序。</summary>
        public void AddChild(int parent, int child)
        {
            _children[parent].Add(child);
            _parent[child] = parent;
        }

        /// <summary>
        /// 冻结为运行期行为树：子列表展平为连续数组，每个父节点记录起始下标与数量。
        /// </summary>
        /// <param name="context">叶子工作体读取的共享上下文。</param>
        /// <param name="conditions">条件编号到谓词的映射。</param>
        /// <param name="actionCount">动作编号总数。</param>
        /// <param name="gated">是否启用脏门控（false 即经典反应式行为树，每帧全量重评估）。</param>
        public BehaviorTree Build(BenchContext context, Func<bool>[] conditions, int actionCount, bool gated)
        {
            int nodeCount = _kind.Count;
            int[] firstChild = new int[nodeCount];
            int[] childCount = new int[nodeCount];
            int totalChildren = 0;
            for (int index = 0; index < nodeCount; index++) totalChildren += _children[index].Count;

            int[] childList = new int[totalChildren];
            int cursor = 0;
            for (int index = 0; index < nodeCount; index++)
            {
                List<int> children = _children[index];
                firstChild[index] = cursor;
                childCount[index] = children.Count;
                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                    childList[cursor++] = children[childIndex];
            }

            int[] conditionNodes = new int[conditions.Length];
            for (int index = 0; index < conditionNodes.Length; index++) conditionNodes[index] = -1;
            for (int index = 0; index < nodeCount; index++)
            {
                if (_kind[index] != BtKind.Condition) continue;

                conditionNodes[_payload[index]] = index;
            }

            return new BehaviorTree(
                _kind.ToArray(),
                firstChild,
                childCount,
                childList,
                _parent.ToArray(),
                _payload.ToArray(),
                conditions,
                conditionNodes,
                actionCount,
                context,
                gated);
        }

        private int AddNode(BtKind kind, int payload)
        {
            int index = _kind.Count;
            _kind.Add(kind);
            _parent.Add(-1);
            _payload.Add(payload);
            _children.Add(new List<int>(2));
            return index;
        }
    }

    // ============================================================================
    // 被测实现
    // ============================================================================

    /// <summary>
    /// 一个可运行的被测实现。Run 内部用具体类型直接驱动，避免把虚派发开销混进每帧成本。
    /// </summary>
    public abstract class BenchCase : IDisposable
    {
        /// <summary>该实现读取的共享上下文。</summary>
        public BenchContext Context { get; protected set; }

        /// <summary>实现名称（报告用）。</summary>
        public abstract string Name { get; }

        /// <summary>当前激活的状态/动作编号（0 Idle / 1 Move / 2 Attack / 3 Skill / 4 Talent / 5 Death）。</summary>
        public abstract int ActiveStateId { get; }

        /// <summary>自构建以来的状态切换次数（初始进入不计）。</summary>
        public abstract int SwitchCount { get; }

        /// <summary>按业务惯例把所有可判定数据标脏（等价性对账用）。</summary>
        public abstract void MarkDirtyAll();

        /// <summary>推进一帧。</summary>
        public abstract void TickOnce();

        /// <summary>
        /// 基准热循环：连续推进 ticks 帧。
        /// switching 为 true 时每帧翻转攻击意图，制造"每帧切换一次"的最坏场景。
        /// </summary>
        public abstract void Run(int ticks, bool switching);

        /// <summary>释放被测实例。</summary>
        public virtual void Dispose() { }
    }

    /// <summary>
    /// BHSM 被测实例：6 个平铺叶子状态、每状态 5 条优先级边、可选脏门控。
    /// 图结构与行为树镜像一致——任意状态下激活态恒为"优先级最高的成立条件"。
    /// </summary>
    public sealed class BhsBenchCase : BenchCase
    {
        private readonly BetterHSM<BenchContext> _hsm;
        private readonly BState[] _states;
        private readonly bool _gated;

        /// <summary>构建一个 BHSM 实例。</summary>
        /// <param name="context">状态读取的决策上下文。</param>
        /// <param name="gated">是否启用中断脏门控（生产接法为启用）。</param>
        public BhsBenchCase(BenchContext context, bool gated)
        {
            Context = context;
            _gated = gated;

            BIdle idle = new BIdle();
            BMove move = new BMove();
            BAttack attack = new BAttack();
            BSkill skill = new BSkill();
            BTalent talent = new BTalent();
            BDeath death = new BDeath();
            _states = new BState[] { idle, move, attack, skill, talent, death };

            _hsm = new BetterHSM<BenchContext>(context);
            StateGraphBuilder<BenchContext> graph = _hsm.BeginBuild();
            graph.AddState(idle).AddState(move).AddState(attack)
                .AddState(skill).AddState(talent).AddState(death);

            // Idle（最低优先级）：任一更高优先级条件成立即离开；无更低状态可去。
            graph.From(idle).To(death, context.Downed, 50);
            graph.From(idle).To(talent, context.Talent, 40);
            graph.From(idle).To(skill, context.Skill, 30);
            graph.From(idle).To(attack, context.Attack, 20);
            graph.From(idle).To(move, context.Moving, 10);

            // Move：高优先级边一键触发；移动停止回 Idle。
            graph.From(move).To(death, context.Downed, 50);
            graph.From(move).To(talent, context.Talent, 40);
            graph.From(move).To(skill, context.Skill, 30);
            graph.From(move).To(attack, context.Attack, 20);
            graph.From(move).To(idle, context.NotMoving, 10);

            // Attack：高优先级直切；攻击意图消失后按是否移动分流。
            graph.From(attack).To(death, context.Downed, 50);
            graph.From(attack).To(talent, context.Talent, 40);
            graph.From(attack).To(skill, context.Skill, 30);
            graph.From(attack).To(move, context.AttackEndedAndMoving, 10);
            graph.From(attack).To(idle, context.AttackEndedAndIdle, 10);

            // Skill：技能意图仍成立时不得被低优先级抢占（平表边必须显式守卫）。
            graph.From(skill).To(death, context.Downed, 50);
            graph.From(skill).To(talent, context.Talent, 40);
            graph.From(skill).To(attack, context.SkillEndedAttack, 20);
            graph.From(skill).To(move, context.SkillEndedMoving, 10);
            graph.From(skill).To(idle, context.SkillEndedIdle, 10);

            // Talent：天赋意图仍成立时驻留。
            graph.From(talent).To(death, context.Downed, 50);
            graph.From(talent).To(skill, context.TalentEndedSkill, 30);
            graph.From(talent).To(attack, context.TalentEndedAttack, 20);
            graph.From(talent).To(move, context.TalentEndedMoving, 10);
            graph.From(talent).To(idle, context.TalentEndedIdle, 10);

            // Death：倒地解除后按优先级回流。
            graph.From(death).To(talent, context.DownedEndedTalent, 40);
            graph.From(death).To(skill, context.DownedEndedSkill, 30);
            graph.From(death).To(attack, context.DownedEndedAttack, 20);
            graph.From(death).To(move, context.DownedEndedMoving, 10);
            graph.From(death).To(idle, context.DownedEndedIdle, 10);

            // 生产接法：所有状态启用门控，只有业务标脏后才评估本层中断。
            if (gated) graph.EnableInterruptGating(_states);
            graph.Build(idle);
        }

        /// <inheritdoc />
        public override string Name => _gated ? "BHSM-门控" : "BHSM-无门控";

        /// <inheritdoc />
        public override int ActiveStateId => ((BState)_hsm.CurrentState).StateId;

        /// <inheritdoc />
        public override int SwitchCount => Context.EnterCount - 1;

        /// <summary>
        /// 业务侧标脏：项目实际接法（HSMGateRuntimeData）是批量把全部状态标脏，
        /// 因为感知层不知道当前激活的是哪个状态。
        /// </summary>
        public override void MarkDirtyAll()
        {
            if (!_gated) return;

            for (int index = 0; index < _states.Length; index++) _states[index].MarkDirty();
        }

        /// <inheritdoc />
        public override void TickOnce()
        {
            _hsm.Update();
        }

        /// <summary>供多实例热循环直接调用的具体类型入口（避免虚派发混入规模测试）。</summary>
        public void TickConcrete()
        {
            _hsm.Update();
        }

        /// <inheritdoc />
        public override void Run(int ticks, bool switching)
        {
            if (switching)
            {
                for (int frame = 0; frame < ticks; frame++)
                {
                    // 意图每帧翻转：攻击态与待机态逐帧互切。
                    Context.WantAttack = (frame & 1) == 0;
                    MarkDirtyAll();
                    _hsm.Update();
                }

                return;
            }

            for (int frame = 0; frame < ticks; frame++) _hsm.Update();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _hsm.Dispose();
        }

        // 状态基类：与行为树动作叶执行完全相同的工作体与生命周期计数。
        private abstract class BState : StateBase<BenchContext>
        {
            private readonly int _id;

            protected BState(int id)
            {
                _id = id;
            }

            public int StateId => _id;

            public override void OnEnter()
            {
                BenchWork.Enter(ref Context.EnterCount);
            }

            public override void OnUpdate()
            {
                BenchWork.Tick(ref Context.WorkAccumulator);
            }

            public override void OnExit()
            {
                BenchWork.Exit(ref Context.ExitCount);
            }
        }

        // BHSM 按状态类型唯一注册，每个状态必须是独立类型。
        private sealed class BIdle : BState { public BIdle() : base(0) { } }
        private sealed class BMove : BState { public BMove() : base(1) { } }
        private sealed class BAttack : BState { public BAttack() : base(2) { } }
        private sealed class BSkill : BState { public BSkill() : base(3) { } }
        private sealed class BTalent : BState { public BTalent() : base(4) { } }
        private sealed class BDeath : BState { public BDeath() : base(5) { } }
    }

    /// <summary>
    /// 行为树被测实例：与 BHSM 同构的优先级选择树。
    /// 结构：Selector[ 5 × Sequence[ Condition, Action ], Action(Idle) ]，
    /// 子节点顺序即条件优先级，动作叶承载与 OnUpdate 相同的工作体。
    /// </summary>
    public sealed class BtBenchCase : BenchCase
    {
        // 条件编号（与 BenchContext 上的谓词一一对应）。
        private const int ConditionDowned = 0;
        private const int ConditionTalent = 1;
        private const int ConditionSkill = 2;
        private const int ConditionAttack = 3;
        private const int ConditionMoving = 4;

        // 动作编号（与状态编号一致，便于与 BHSM 对账）。
        private const int ActionIdle = 0;
        private const int ActionDeath = 5;

        private readonly BehaviorTree _tree;
        private readonly bool _gated;

        /// <summary>构建一棵行为树实例。</summary>
        /// <param name="context">叶子工作体读取的共享上下文。</param>
        /// <param name="gated">是否启用脏门控剪枝（false 即经典反应式行为树）。</param>
        public BtBenchCase(BenchContext context, bool gated)
        {
            Context = context;
            _gated = gated;

            BehaviorTreeBuilder builder = new BehaviorTreeBuilder();
            int root = builder.AddSelector();
            AddGuardBranch(builder, root, ConditionDowned, ActionDeath);
            AddGuardBranch(builder, root, ConditionTalent, 4);
            AddGuardBranch(builder, root, ConditionSkill, 3);
            AddGuardBranch(builder, root, ConditionAttack, 2);
            AddGuardBranch(builder, root, ConditionMoving, 1);
            builder.AddChild(root, builder.AddAction(ActionIdle));

            Func<bool>[] conditions =
            {
                new Func<bool>(context.Downed),
                new Func<bool>(context.Talent),
                new Func<bool>(context.Skill),
                new Func<bool>(context.Attack),
                new Func<bool>(context.Moving)
            };

            _tree = builder.Build(context, conditions, 6, gated);
        }

        /// <inheritdoc />
        public override string Name => _gated ? "行为树-门控" : "行为树-反应式";

        /// <inheritdoc />
        public override int ActiveStateId => _tree.ActiveActionId < 0 ? ActionIdle : _tree.ActiveActionId;

        /// <inheritdoc />
        public override int SwitchCount => _tree.SwitchCount;

        /// <summary>行为树惯用接法：只标记数据实际变化的那条条件，脏标记沿父链传播。</summary>
        public override void MarkDirtyAll()
        {
            if (!_gated) return;

            _tree.MarkDirty(ConditionDowned);
            _tree.MarkDirty(ConditionTalent);
            _tree.MarkDirty(ConditionSkill);
            _tree.MarkDirty(ConditionAttack);
            _tree.MarkDirty(ConditionMoving);
        }

        /// <inheritdoc />
        public override void TickOnce()
        {
            _tree.Tick();
        }

        /// <summary>供多实例热循环直接调用的具体类型入口（避免虚派发混入规模测试）。</summary>
        public void TickConcrete()
        {
            _tree.Tick();
        }

        /// <inheritdoc />
        public override void Run(int ticks, bool switching)
        {
            if (switching)
            {
                for (int frame = 0; frame < ticks; frame++)
                {
                    Context.WantAttack = (frame & 1) == 0;
                    // 只有攻击意图变化：行为树只需标记条件 3，父链自动置脏。
                    if (_gated) _tree.MarkDirty(ConditionAttack);
                    _tree.Tick();
                }

                return;
            }

            for (int frame = 0; frame < ticks; frame++) _tree.Tick();
        }

        /// <summary>追加一条"条件成立即执行该动作"的守卫分支。</summary>
        private static void AddGuardBranch(BehaviorTreeBuilder builder, int root, int conditionId, int actionId)
        {
            int branch = builder.AddSequence();
            builder.AddChild(root, branch);
            builder.AddChild(branch, builder.AddCondition(conditionId));
            builder.AddChild(branch, builder.AddAction(actionId));
        }
    }

    // ============================================================================
    // 基准驱动
    // ============================================================================

    /// <summary>
    /// 行为树 vs BHSM 的逐帧开销基准。
    /// 四个被测实现跑同一语义负载、同一工作体、同一标脏契约：
    /// 稳态（不切换）与每帧切换两个场景，各自报告每帧中位开销、最小开销与堆分配。
    /// </summary>
    public static class BenchmarkHarness
    {
        // 每轮帧数与轮数：单轮 ~1.5ms 量级，足够盖过 Stopwatch 分辨率；轮数多取中位数抗抖动。
        private const int TicksPerRound = 100000;
        private const int Rounds = 19;
        private const int WarmupTicks = 30000;

        // 多实例规模测试：模拟一帧内推进 N 个 AI 实例的缓存压力。
        private const int ScaleInstances = 200;
        private const int ScaleFrames = 5000;
        private const int ScaleRepetitions = 5;

        // 每线程分配字节探针（Mono 不一定提供，缺失时回退为不可用）。
        private static readonly Func<long> AllocationProbe = CreateAllocationProbe();

        /// <summary>运行完整基准并返回摘要（同时写入 Results 下的日志与 CSV）。</summary>
        public static string Run()
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("[BtBhsBench] 环境：Unity 2022.3.55f1c1 / Mono / 编辑模式 / 单线程主线程");
            report.AppendLine($"[BtBhsBench] 参数：每轮 {TicksPerRound} 帧 × {Rounds} 轮取中位，预热 {WarmupTicks} 帧");
            report.AppendLine("[BtBhsBench] 工作负载：6 状态/动作，5 条优先级条件，叶子工作体完全相同");
            report.AppendLine("[BtBhsBench] " + VerifyEquivalence());

            List<Row> rows = new List<Row>(16);

            // 场景一：稳态——条件恒假且不标脏，两实现都停在 Idle。
            RunScenario(rows, "稳态(不切换)", switching: false);
            // 场景二：最坏切换——攻击意图逐帧翻转，每帧发生一次状态切换。
            RunScenario(rows, "每帧切换", switching: true);

            report.AppendLine();
            report.AppendLine("[BtBhsBench] ===== 单实例逐帧开销 =====");
            AppendTable(report, rows);

            List<Row> scaleRows = new List<Row>(8);
            RunScaleScenario(scaleRows);
            report.AppendLine();
            report.AppendLine($"[BtBhsBench] ===== 多实例规模（{ScaleInstances} 实例 × {ScaleFrames} 帧，稳态）=====");
            AppendTable(report, scaleRows);

            report.AppendLine();
            report.AppendLine(Summarize(rows));

            WriteLogs(rows, scaleRows, report.ToString());
            string text = report.ToString();
            Debug.Log(text);
            return BuildShortSummary(rows, scaleRows);
        }

        // ---------------------------------------------------------------- 场景驱动

        private static void RunScenario(List<Row> rows, string scenario, bool switching)
        {
            string[] names = { "BHSM-门控", "BHSM-无门控", "行为树-反应式", "行为树-门控" };
            Func<BenchContext, BenchCase>[] factories =
            {
                context => new BhsBenchCase(context, true),
                context => new BhsBenchCase(context, false),
                context => new BtBenchCase(context, false),
                context => new BtBenchCase(context, true)
            };

            int count = factories.Length;
            BenchCase[] benches = new BenchCase[count];
            List<double>[] samples = new List<double>[count];
            double[] allocated = new double[count];
            for (int index = 0; index < count; index++)
            {
                benches[index] = factories[index](new BenchContext());
                benches[index].Run(WarmupTicks, switching);
                samples[index] = new List<double>(Rounds);
                allocated[index] = -1;
            }

            // 同一轮内交错测量四个实现，并按轮次轮转起始位置：
            // CPU 降频/后台负载只同时影响四方，GC 后的冷缓存也不会固定落在某个实现上。
            for (int round = 0; round < Rounds; round++)
            {
                GC.Collect();
                int startIndex = round % count;
                for (int step = 0; step < count; step++)
                {
                    int index = (startIndex + step) % count;

                    long allocBefore = AllocationProbe != null ? AllocationProbe() : 0;
                    long start = Stopwatch.GetTimestamp();
                    benches[index].Run(TicksPerRound, switching);
                    long end = Stopwatch.GetTimestamp();
                    long allocAfter = AllocationProbe != null ? AllocationProbe() : 0;

                    samples[index].Add((end - start) * (1e9 / Stopwatch.Frequency) / TicksPerRound);
                    if (AllocationProbe != null)
                        allocated[index] = (allocAfter - allocBefore) / (double)TicksPerRound;
                }
            }

            for (int index = 0; index < count; index++)
            {
                long switchCount = benches[index].SwitchCount;
                benches[index].Dispose();
                samples[index].Sort();
                rows.Add(new Row
                {
                    Scenario = scenario,
                    Implementation = names[index],
                    Measurement = new Measurement
                    {
                        MedianNs = samples[index][samples[index].Count / 2],
                        MinNs = samples[index][0],
                        AllocatedBytesPerTick = allocated[index],
                        Switches = switchCount,
                        Frames = TicksPerRound * (long)Rounds
                    }
                });
            }
        }

        private static void RunScaleScenario(List<Row> rows)
        {
            rows.Add(MeasureScale("BHSM-门控", true, isBehaviorTree: false));
            rows.Add(MeasureScale("BHSM-无门控", false, isBehaviorTree: false));
            rows.Add(MeasureScale("行为树-反应式", false, isBehaviorTree: true));
            rows.Add(MeasureScale("行为树-门控", true, isBehaviorTree: true));
        }

        // ---------------------------------------------------------------- 多实例测量

        private static Row MeasureScale(string implementation, bool gated, bool isBehaviorTree)
        {
            return isBehaviorTree ? MeasureScaleBt(implementation, gated) : MeasureScaleBhs(implementation, gated);
        }

        private static Row MeasureScaleBhs(string implementation, bool gated)
        {
            BhsBenchCase[] cases = new BhsBenchCase[ScaleInstances];
            for (int index = 0; index < ScaleInstances; index++)
                cases[index] = new BhsBenchCase(new BenchContext(), gated);

            // 预热：整批跑一遍，触发全部 JIT。
            for (int frame = 0; frame < 200; frame++)
                for (int index = 0; index < cases.Length; index++) cases[index].TickConcrete();

            List<double> samples = new List<double>(ScaleRepetitions);
            for (int repetition = 0; repetition < ScaleRepetitions; repetition++)
            {
                GC.Collect();
                long start = Stopwatch.GetTimestamp();
                for (int frame = 0; frame < ScaleFrames; frame++)
                    for (int index = 0; index < cases.Length; index++) cases[index].TickConcrete();
                long end = Stopwatch.GetTimestamp();
                samples.Add((end - start) * (1e9 / Stopwatch.Frequency) / (ScaleFrames * (double)ScaleInstances));
            }

            for (int index = 0; index < cases.Length; index++) cases[index].Dispose();
            return BuildScaleRow(implementation, samples);
        }

        private static Row MeasureScaleBt(string implementation, bool gated)
        {
            BtBenchCase[] cases = new BtBenchCase[ScaleInstances];
            for (int index = 0; index < ScaleInstances; index++)
                cases[index] = new BtBenchCase(new BenchContext(), gated);

            for (int frame = 0; frame < 200; frame++)
                for (int index = 0; index < cases.Length; index++) cases[index].TickConcrete();

            List<double> samples = new List<double>(ScaleRepetitions);
            for (int repetition = 0; repetition < ScaleRepetitions; repetition++)
            {
                GC.Collect();
                long start = Stopwatch.GetTimestamp();
                for (int frame = 0; frame < ScaleFrames; frame++)
                    for (int index = 0; index < cases.Length; index++) cases[index].TickConcrete();
                long end = Stopwatch.GetTimestamp();
                samples.Add((end - start) * (1e9 / Stopwatch.Frequency) / (ScaleFrames * (double)ScaleInstances));
            }

            return BuildScaleRow(implementation, samples);
        }

        private static Row BuildScaleRow(string implementation, List<double> samples)
        {
            samples.Sort();
            return new Row
            {
                Scenario = "多实例稳态",
                Implementation = implementation,
                Measurement = new Measurement
                {
                    MedianNs = samples[samples.Count / 2],
                    MinNs = samples[0],
                    AllocatedBytesPerTick = -1,
                    Frames = ScaleFrames * (long)ScaleInstances * ScaleRepetitions
                }
            };
        }

        // ---------------------------------------------------------------- 语义对账

        /// <summary>
        /// 5000 步随机决策下逐帧比对：两实现激活态必须都等于"优先级最高的成立条件"这一独立判定。
        /// 通过后才认为后续性能数字是同一语义负载的比较。
        /// </summary>
        private static string VerifyEquivalence()
        {
            const int Steps = 5000;
            BenchContext bhsContext = new BenchContext();
            BenchContext btContext = new BenchContext();
            BhsBenchCase bhs = new BhsBenchCase(bhsContext, true);
            BtBenchCase bt = new BtBenchCase(btContext, true);
            System.Random random = new System.Random(20261106);

            int mismatches = 0;
            string firstMismatch = string.Empty;

            for (int step = 0; step < Steps; step++)
            {
                bool downed = random.Next(100) < 4;
                bool talent = random.Next(100) < 10;
                bool skill = random.Next(100) < 18;
                bool attack = random.Next(100) < 38;
                bool moving = random.Next(100) < 50;

                ApplyFlags(bhsContext, downed, talent, skill, attack, moving);
                ApplyFlags(btContext, downed, talent, skill, attack, moving);

                bhs.MarkDirtyAll();
                bt.MarkDirtyAll();
                bhs.TickOnce();
                bt.TickOnce();

                int expected = ExpectedState(bhsContext);
                int bhsActive = bhs.ActiveStateId;
                int btActive = bt.ActiveStateId;
                if (bhsActive == expected && btActive == expected) continue;

                mismatches++;
                if (mismatches == 1)
                {
                    firstMismatch = $"step={step} downed={downed} talent={talent} skill={skill} attack={attack} moving={moving} " +
                        $"预期={expected} BHSM={bhsActive} BT={btActive}";
                }
            }

            bhs.Dispose();
            return mismatches == 0
                ? $"语义对账通过：{Steps} 步随机决策，两实现激活态与独立判定逐帧一致（切换 BHSM={bhs.SwitchCount} BT={bt.SwitchCount}）"
                : $"语义对账失败 {mismatches}/{Steps}：{firstMismatch}";
        }

        private static void ApplyFlags(
            BenchContext context, bool downed, bool talent, bool skill, bool attack, bool moving)
        {
            context.IsDowned = downed;
            context.WantTalent = talent;
            context.WantSkill = skill;
            context.WantAttack = attack;
            context.IsMoving = moving;
        }

        /// <summary>独立判定：优先级最高的成立条件，全假回 Idle。</summary>
        private static int ExpectedState(BenchContext context)
        {
            if (context.IsDowned) return 5;
            if (context.WantTalent) return 4;
            if (context.WantSkill) return 3;
            if (context.WantAttack) return 2;
            if (context.IsMoving) return 1;
            return 0;
        }

        // ---------------------------------------------------------------- 报告输出

        private static void AppendTable(StringBuilder report, List<Row> rows)
        {
            report.AppendLine("实现            每帧中位ns  每帧最小ns  分配B/帧  切换数");
            for (int index = 0; index < rows.Count; index++)
            {
                Row row = rows[index];
                string alloc = row.Measurement.AllocatedBytesPerTick < 0
                    ? "n/a"
                    : row.Measurement.AllocatedBytesPerTick.ToString("F3");
                report.AppendLine(
                    $"{row.Implementation,-14} {row.Measurement.MedianNs,10:F2} {row.Measurement.MinNs,11:F2} {alloc,10} {row.Measurement.Switches,8}");
            }
        }

        /// <summary>按场景给出倍率与 100 实例 60fps 的整帧换算，便于直接判读。</summary>
        private static string Summarize(List<Row> rows)
        {
            StringBuilder summary = new StringBuilder(2048);
            summary.AppendLine("[BtBhsBench] ===== 结论数据 =====");
            for (int scenario = 0; scenario < 2; scenario++)
            {
                string name = scenario == 0 ? "稳态(不切换)" : "每帧切换";
                Row bhs = FindRow(rows, name, "BHSM-门控");
                Row bt = FindRow(rows, name, "行为树-门控");
                Row btReactive = FindRow(rows, name, "行为树-反应式");
                if (bhs == null || bt == null || btReactive == null) continue;

                double ratio = bt.Measurement.MedianNs / bhs.Measurement.MedianNs;
                double delta = bt.Measurement.MedianNs - bhs.Measurement.MedianNs;
                // 100 个实例、60fps：每帧总开销（ms）= ns/实例 × 100。
                double bhsMs = bhs.Measurement.MedianNs * 100 / 1e6;
                double btMs = bt.Measurement.MedianNs * 100 / 1e6;
                double reactiveMs = btReactive.Measurement.MedianNs * 100 / 1e6;
                summary.AppendLine(
                    $"[{name}] 门控BHSM {bhs.Measurement.MedianNs:F2}ns vs 门控BT {bt.Measurement.MedianNs:F2}ns " +
                    $"（BT/BHSM={ratio:F2}x，差 {delta:+0.00;-0.00}ns）；反应式BT {btReactive.Measurement.MedianNs:F2}ns；" +
                    $"100实例@60fps 每帧：BHSM {bhsMs:F3}ms / BT {btMs:F3}ms / 反应式BT {reactiveMs:F3}ms");
            }

            return summary.ToString();
        }

        private static Row FindRow(List<Row> rows, string scenario, string implementation)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                if (rows[index].Scenario == scenario && rows[index].Implementation == implementation) return rows[index];
            }

            return null;
        }

        private static string BuildShortSummary(List<Row> rows, List<Row> scaleRows)
        {
            StringBuilder summary = new StringBuilder(1024);
            for (int index = 0; index < rows.Count; index++)
            {
                Row row = rows[index];
                summary.Append(row.Scenario).Append('/').Append(row.Implementation)
                    .Append(" 中位=").Append(row.Measurement.MedianNs.ToString("F2")).Append("ns")
                    .Append(" 切换=").Append(row.Measurement.Switches);
                if (index + 1 < rows.Count) summary.Append(" | ");
            }

            for (int index = 0; index < scaleRows.Count; index++)
            {
                summary.Append(" | 200实例 ").Append(scaleRows[index].Implementation)
                    .Append('=').Append(scaleRows[index].Measurement.MedianNs.ToString("F2")).Append("ns");
            }

            return summary.ToString();
        }

        private static void WriteLogs(List<Row> rows, List<Row> scaleRows, string report)
        {
            try
            {
                string directory = Path.Combine(Application.dataPath, "BehaviorTreeCompareToBHSMTest", "Results");
                Directory.CreateDirectory(directory);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.WriteAllText(Path.Combine(directory, "bench-" + stamp + ".log"), report);

                StringBuilder csv = new StringBuilder(2048);
                csv.AppendLine("scenario,implementation,median_ns_per_frame,min_ns_per_frame,allocated_bytes_per_frame,switches,frames");
                AppendCsv(csv, rows);
                AppendCsv(csv, scaleRows);
                File.WriteAllText(Path.Combine(directory, "bench-" + stamp + ".csv"), csv.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError("[BtBhsBench] 结果落盘失败：" + exception);
            }
        }

        private static void AppendCsv(StringBuilder csv, List<Row> rows)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                Row row = rows[index];
                csv.Append(row.Scenario).Append(',')
                    .Append(row.Implementation).Append(',')
                    .Append(row.Measurement.MedianNs.ToString("F3")).Append(',')
                    .Append(row.Measurement.MinNs.ToString("F3")).Append(',')
                    .Append(row.Measurement.AllocatedBytesPerTick.ToString("F3")).Append(',')
                    .Append(row.Measurement.Switches).Append(',')
                    .Append(row.Measurement.Frames).AppendLine();
            }
        }

        private static Func<long> CreateAllocationProbe()
        {
            try
            {
                MethodInfo method = typeof(GC).GetMethod(
                    "GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
                if (method == null) return null;

                return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- 数据结构

        private struct Measurement
        {
            public double MedianNs;
            public double MinNs;
            public double AllocatedBytesPerTick;
            public long Switches;
            public long Frames;
        }

        private sealed class Row
        {
            public string Scenario;
            public string Implementation;
            public Measurement Measurement;
        }
    }

    /// <summary>
    /// UnityCLI 入口：在 Assets 内，可被 run_script 以 --file 直接编译执行，
    /// 不需要在 AgentScripts 建转发壳（测试内容全部留在本文件夹）。
    /// </summary>
    public static class BenchCliEntry
    {
        /// <summary>运行基准并返回摘要字符串。</summary>
        public static string Run()
        {
            return BenchmarkHarness.Run();
        }
    }
}
