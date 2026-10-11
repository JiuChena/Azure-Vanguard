using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 多源加载进度汇聚器：各加载任务登记阶段名与权重，随时上报自身进度（0~1），
    /// 汇聚 Σ(权重×进度)/Σ权重 出单一总进度，经 EventNames.PreloadProgressChanged 广播。
    /// 解决"进度条只看场景加载"的问题——表预热、Addressables、存档等加载源统一进同一根进度条。
    /// 已完成阶段保留在表内（进度按 1 计），保证总进度单调不回退；换一批加载任务前先 Reset。
    /// </summary>
    public class LoadProgressHub
    {
        private static LoadProgressHub instance;
        public static LoadProgressHub Instance => instance ??= new LoadProgressHub();

        /// <summary>单阶段登记项。</summary>
        private class StageEntry
        {
            public float Weight;
            public float Progress;
        }

        // 阶段名 → 登记项（阶段名重复登记视为配置错误，覆盖权重并告警）
        private readonly Dictionary<string, StageEntry> stages = new Dictionary<string, StageEntry>();

        private LoadProgressHub() { }

        /// <summary>
        /// 清空全部登记项；开启新一轮加载任务前调用。
        /// </summary>
        public void Reset()
        {
            stages.Clear();
            Broadcast();
        }

        /// <summary>
        /// 登记一个加载阶段；同名重复登记覆盖权重并告警（进度保留）。
        /// </summary>
        /// <param name="stage">阶段名（如 "scene"/"config"）。</param>
        /// <param name="weight">权重，正数；建议各阶段权重和为 1，非 1 时按相对比例分摊。</param>
        public void Register(string stage, float weight)
        {
            if (string.IsNullOrEmpty(stage) || weight <= 0f)
            {
                Debug.LogError($"[LoadProgressHub] 登记参数无效：stage={stage}, weight={weight}");
                return;
            }

            if (stages.TryGetValue(stage, out StageEntry entry))
            {
                Debug.LogWarning($"[LoadProgressHub] 阶段 {stage} 重复登记，覆盖权重 {entry.Weight} → {weight}");
                entry.Weight = weight;
            }
            else stages[stage] = new StageEntry { Weight = weight, Progress = 0f };

            Broadcast();
        }

        /// <summary>
        /// 上报阶段进度（0~1，自动钳制）；未登记的阶段忽略并告警。
        /// </summary>
        public void Report(string stage, float progress)
        {
            if (!stages.TryGetValue(stage, out StageEntry entry))
            {
                Debug.LogWarning($"[LoadProgressHub] 未登记的阶段上报：{stage}（progress={progress}）");
                return;
            }

            float clamped = Mathf.Clamp01(progress);
            if (clamped <= entry.Progress) return; // 只进不退，忽略重复/回退上报

            entry.Progress = clamped;
            Broadcast();
        }

        /// <summary>
        /// 标记阶段完成（等价 Report(stage, 1)）；登记项保留，总进度保持单调。
        /// </summary>
        public void Complete(string stage)
        {
            Report(stage, 1f);
        }

        /// <summary>
        /// 汇聚总进度：Σ(权重×进度)/Σ权重；无登记项时返回 1（无任务 = 无等待）。
        /// </summary>
        public float TotalProgress
        {
            get
            {
                float weighted = 0f;
                float totalWeight = 0f;
                foreach (KeyValuePair<string, StageEntry> pair in stages)
                {
                    weighted += pair.Value.Weight * pair.Value.Progress;
                    totalWeight += pair.Value.Weight;
                }
                return totalWeight > 0f ? weighted / totalWeight : 1f;
            }
        }

        /// <summary>
        /// 是否全部登记阶段都已完成；无登记项返回 true。
        /// </summary>
        public bool AllComplete
        {
            get
            {
                foreach (KeyValuePair<string, StageEntry> pair in stages)
                {
                    if (pair.Value.Progress < 1f) return false;
                }
                return true;
            }
        }

        /// <summary>进度变化统一出口：广播 PreloadProgressChanged（载荷 = 总进度 0~1）。</summary>
        private void Broadcast()
        {
            EventCenter.Instance.SetEventTrigger(EventNames.PreloadProgressChanged, TotalProgress);
        }
    }
}
