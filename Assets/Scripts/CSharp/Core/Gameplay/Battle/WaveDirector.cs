using System;
using System.Collections.Generic;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 波次状态机（纯数据驱动）：按注入的波次序列（①WaveRow，⑥装配时按 waveIds 顺序传入，
    /// ③不查表）逐波出兵——进入波立即出第一只，之后按 SpawnInterval 逐只；波内出完进入下一波（波间无间隔）。
    /// 生成动作经 spawnOne 回调外抛（BattleController 生成敌人 state）。
    /// </summary>
    public sealed class WaveDirector
    {
        private readonly IReadOnlyList<WaveRow> waves;
        private readonly float[] intervals;
        private int waveIdx;
        private int spawnedInWave;
        private float timer;

        // 当前波下标（0 基；全部生成完 = waves.Count）
        public int WaveIndex => waveIdx;

        // 是否全部波次已生成完
        public bool AllSpawned => waveIdx >= waves.Count;

        public WaveDirector(IReadOnlyList<WaveRow> waves)
        {
            this.waves = waves ?? throw new ArgumentNullException(nameof(waves));
            intervals = new float[waves.Count];
            for (int i = 0; i < waves.Count; i++)
            {
                intervals[i] = waves[i].SpawnInterval > 0f ? waves[i].SpawnInterval : 0.001f;
            }
            // 首拍立即出第一只：timer 预置到触发阈值
            timer = waves.Count > 0 ? intervals[0] : 0f;
        }

        /// <summary>时间推进：到点逐只外抛 spawnOne（传当前波行）；全部生成完后空转。</summary>
        public void Update(float dt, Action<WaveRow> spawnOne)
        {
            if (spawnOne == null) throw new ArgumentNullException(nameof(spawnOne));
            if (AllSpawned) return;

            // dt 只加一次；波切换时保留上一波溢出的余量（新波阈值补齐即首拍立即出第一只）
            timer += dt;
            while (!AllSpawned && timer >= intervals[waveIdx])
            {
                timer -= intervals[waveIdx];
                spawnOne(waves[waveIdx]);
                spawnedInWave++;

                if (spawnedInWave >= waves[waveIdx].Count)
                {
                    waveIdx++;
                    spawnedInWave = 0;
                    if (!AllSpawned) timer += intervals[waveIdx];
                }
            }
        }
    }
}
