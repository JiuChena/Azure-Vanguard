using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 波次驱动服务（IBattleWaveDriver 生产实现）：波内时钟推进、到点条目 FIFO 出怪、
    /// 出怪点/路线解析、波完成切波；时钟跟随模拟 Step，暂停/倍速天然生效。
    /// 只读配置与波次表，不改单位状态；出兵动作经 Tick 的 spawnOne 回调外抛。
    /// </summary>
    public sealed class CombatWaveService : IBattleWaveDriver, ICombatSubservice
    {
        // 时间比较容差（浮点累计时钟防边缘漏拍）。
        private const float TimeEpsilon = 0.0001f;

        private readonly struct SpawnRequest
        {
            public readonly string EnemyId;
            public readonly int Level;
            public readonly Vector3 Position;
            public readonly IReadOnlyList<Vector3> Route;

            public SpawnRequest(string enemyId, int level, Vector3 position, IReadOnlyList<Vector3> route)
            {
                EnemyId = enemyId;
                Level = level;
                Position = position;
                Route = route;
            }
        }

        // 波次分组（StageTable 解析结果，按波序）。
        private IReadOnlyList<WaveGroup> groups;

        // 当前波下标（0 基；>= Count 表示全部生成完）。
        private int groupIndex;

        // 当前波内时钟（秒；切波归零）。
        private float waveClock;

        // 每行的时间点游标（与当前波 Rows 一一对应）。
        private int[] cursors = Array.Empty<int>();

        // 到点待创建队列（FIFO）。
        private readonly Queue<SpawnRequest> pending = new Queue<SpawnRequest>();

        // 逐只创建计时（秒）。
        private float spawnTimer;

        // 敌人创建间隔（秒）。
        private float spawnInterval;

        // 已报错的缺失出怪点（防逐帧刷屏）。
        private readonly HashSet<string> missingPoints = new HashSet<string>();

        // 已报错的缺失路线。
        private readonly HashSet<string> missingRoutes = new HashSet<string>();

        // 出怪点映射：pointId → 场景 Transform（实时取 position）。
        private readonly Dictionary<string, Transform> pointMap = new Dictionary<string, Transform>();

        // 路线映射：routeId → 路点序列。
        private readonly Dictionary<string, IReadOnlyList<Vector3>> routeMap =
            new Dictionary<string, IReadOnlyList<Vector3>>();

        /// <summary>波次总数（HUD 波次 X/Y 的分母）。</summary>
        public int TotalWaves => groups?.Count ?? 0;

        /// <summary>当前波号（1 基；全部生成完停在最后一波）。</summary>
        public int CurrentWaveNumber => groups == null ? 0 : Math.Min(groupIndex + 1, groups.Count);

        /// <summary>是否全部波次已生成完（胜负评估输入）。</summary>
        public bool AllSpawned => groups != null && groupIndex >= groups.Count;

        /// <summary>装配波次数据与场景点位/路线；重复调用重置时钟与队列。</summary>
        /// <param name="waveGroups">按波序分组的数据；不允许为 null。</param>
        /// <param name="config">场景配置（出怪点/路线/创建间隔）；不允许为 null。</param>
        public void Begin(IReadOnlyList<WaveGroup> waveGroups, CombatBattleConfig config)
        {
            if (waveGroups == null) throw new ArgumentNullException(nameof(waveGroups));
            if (config == null) throw new ArgumentNullException(nameof(config));

            groups = waveGroups;
            spawnInterval = config.SpawnInterval;
            BuildBindings(config);
            ResetRuntimeState();
        }

        /// <summary>清空波次与绑定（战斗 Reset / 下一场装配前调用）。</summary>
        public void Reset()
        {
            groups = null;
            spawnInterval = 0f;
            pointMap.Clear();
            routeMap.Clear();
            ResetRuntimeState();
        }

        /// <summary>推进一拍：波内时钟 + 到点条目入队 + FIFO 逐只出 + 波完成切波。</summary>
        /// <param name="dt">本拍时长（秒）。</param>
        /// <param name="spawnOne">逐只出怪回调（敌人 id + 等级 + 出怪点 + 路线）。</param>
        public void Tick(float dt, Action<string, int, Vector3, IReadOnlyList<Vector3>> spawnOne)
        {
            if (groups == null || groupIndex >= groups.Count) return;

            WaveGroup group = groups[groupIndex];
            bool queueWasEmpty = pending.Count == 0;

            // 1) 波内时钟推进 + 到点条目整队入队（表内书写顺序）
            waveClock += dt;
            bool allTriggered = true;
            for (int r = 0; r < group.Rows.Count; r++)
            {
                WaveRow row = group.Rows[r];
                IReadOnlyList<WaveSpawnEntry> points = row.TimePoints;
                int cursor = cursors[r];
                Vector3 position = ResolvePoint(row.EnemyCreatePointId);
                IReadOnlyList<Vector3> route = ResolveRoute(row.RouteId);

                while (cursor < points.Count && points[cursor].Time <= waveClock + TimeEpsilon)
                {
                    WaveSpawnEntry entry = points[cursor];
                    for (int u = 0; u < entry.Units.Count; u++)
                    {
                        WaveSpawnEntry.WaveSpawnUnit unit = entry.Units[u];
                        for (int n = 0; n < unit.Count; n++)
                            pending.Enqueue(new SpawnRequest(unit.EnemyId, unit.Level, position, route));
                    }
                    cursor++;
                }

                cursors[r] = cursor;
                if (cursor < points.Count) allTriggered = false;
            }

            // 2) FIFO 逐只出：本轮新入队（队列原为空）时首只立即出，其后按 spawnInterval 节奏
            if (pending.Count > 0)
            {
                if (queueWasEmpty) spawnTimer = 0f;
                spawnTimer -= dt;
                while (pending.Count > 0 && spawnTimer <= 0f)
                {
                    SpawnRequest request = pending.Dequeue();
                    spawnOne?.Invoke(request.EnemyId, request.Level, request.Position, request.Route);
                    spawnTimer += spawnInterval;
                }
            }

            // 3) 波完成判定：全部条目已触发且创建队列已排空 → 切下波（时钟归零）
            if (allTriggered && pending.Count == 0)
            {
                groupIndex++;
                waveClock = 0f;
                cursors = groupIndex < groups.Count ? new int[groups[groupIndex].Rows.Count] : Array.Empty<int>();
            }
        }

        // 重建点位/路线映射：同 id 以首个为准（镜像原线性查找的 first-match 语义）。
        private void BuildBindings(CombatBattleConfig config)
        {
            pointMap.Clear();
            routeMap.Clear();

            if (config.SpawnPoints != null)
            {
                for (int i = 0; i < config.SpawnPoints.Count; i++)
                {
                    SpawnPointBinding binding = config.SpawnPoints[i];
                    if (binding == null || binding.point == null || string.IsNullOrEmpty(binding.pointId)) continue;
                    if (!pointMap.ContainsKey(binding.pointId)) pointMap.Add(binding.pointId, binding.point);
                }
            }

            if (config.Routes != null)
            {
                for (int i = 0; i < config.Routes.Count; i++)
                {
                    EnemyRoute route = config.Routes[i];
                    if (route == null || string.IsNullOrEmpty(route.routeId)) continue;
                    if (!routeMap.ContainsKey(route.routeId)) routeMap.Add(route.routeId, route.points);
                }
            }
        }

        // 只重置运行时游标/队列，不清绑定（Begin 复用）。
        private void ResetRuntimeState()
        {
            groupIndex = 0;
            waveClock = 0f;
            spawnTimer = 0f;
            pending.Clear();
            missingPoints.Clear();
            missingRoutes.Clear();
            cursors = groups != null && groups.Count > 0
                ? new int[groups[0].Rows.Count]
                : Array.Empty<int>();
        }

        // 出怪点解析（实时取 Transform 位置）；未命中报错一次并落 Vector3.zero。
        private Vector3 ResolvePoint(string pointId)
        {
            if (!string.IsNullOrEmpty(pointId) && pointMap.TryGetValue(pointId, out Transform point) && point != null)
                return point.position;

            if (missingPoints.Add(pointId))
                Debug.LogError($"[CombatWave] 出怪点未绑定：pointId={pointId}（请检查 CombatSceneEntry.spawnPoints）");
            return Vector3.zero;
        }

        // 路线解析；空 routeId = 无路线（直奔守点），非空未绑定报错一次并落 null（不拒出兵）。
        private IReadOnlyList<Vector3> ResolveRoute(string routeId)
        {
            if (string.IsNullOrEmpty(routeId)) return null;
            if (routeMap.TryGetValue(routeId, out IReadOnlyList<Vector3> route)) return route;

            if (missingRoutes.Add(routeId))
                Debug.LogError($"[CombatWave] 路线未绑定：routeId={routeId}（请检查 CombatSceneEntry.routes）");
            return null;
        }
    }
}
