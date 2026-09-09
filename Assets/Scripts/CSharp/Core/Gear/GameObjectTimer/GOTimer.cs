using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 通用物体存活计时组件。记录出生时刻，按时间轴字典在指定存活秒数后执行动作。
    /// </summary>
    public sealed class GOTimer : MonoBehaviour
    {
        // 时间轴任务：存活秒数 → 到期动作
        private readonly Dictionary<float, Action> _timeline = new Dictionary<float, Action>();

        // 本次激活的出生时刻（Time.time 基准）
        private float _bornTime;

        // 到期的任务时间点，遍历时收集避免修改正在遍历的字典
        private readonly List<float> _expiredKeys = new List<float>(4);

        /// <summary>
        /// 激活时记录出生时刻。
        /// </summary>
        private void OnEnable()
        {
            _bornTime = Time.time;
        }

        /// <summary>
        /// 注册一个存活到点后执行的动作。
        /// </summary>
        /// <param name="survivalSeconds">相对本次出池的存活秒数。</param>
        /// <param name="action">到点执行的动作。</param>
        public void Register(float survivalSeconds, Action action)
        {
            if (action == null || survivalSeconds <= 0f) return;

            _timeline[survivalSeconds] = action;
        }

        /// <summary>
        /// 清空全部时间轴任务并重置出生时刻。调用时机由外部（回池方）决定。
        /// </summary>
        public void Clear()
        {
            _timeline.Clear();
            _expiredKeys.Clear();
            _bornTime = 0f;
        }

        /// <summary>
        /// 每帧检查存活时长，执行到点的任务。
        /// </summary>
        private void Update()
        {
            if (_timeline.Count == 0) return;

            // 收集已到期的时间点
            float elapsed = Time.time - _bornTime;
            foreach (KeyValuePair<float, Action> pair in _timeline)
            {
                if (elapsed >= pair.Key) _expiredKeys.Add(pair.Key);
            }

            // 逐个取出执行；执行中 Clear 清空列表后 while 条件立即不成立，天然避免索引越界
            while (_expiredKeys.Count > 0)
            {
                float key = _expiredKeys[0];
                _expiredKeys.RemoveAt(0);
                if (!_timeline.TryGetValue(key, out Action action)) continue;

                _timeline.Remove(key);
                action.Invoke();
            }
        }
    }
}