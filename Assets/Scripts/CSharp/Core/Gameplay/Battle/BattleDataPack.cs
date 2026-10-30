using System.Collections.Generic;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗数据包（①→③ 的装配出口）：⑥从 BattleDataModule 预热结果打包注入 BattleController，
    /// ③对 ① 的依赖收敛为这三个纯数据字典（验证脚本手搓同等结构即可脱离表域推演）。
    /// DeployCosts 的键集 = 编队白名单（部署校验 + 上场名额 = 键数）。
    /// </summary>
    public sealed class BattleDataPack
    {
        // 单位数值（角色键 = unitId，敌人键 = id@等级如 EN_Solider_AR_0001@1）
        public Dictionary<string, BattleUnitStats> Stats { get; } = new Dictionary<string, BattleUnitStats>();

        // 普攻解析包（rate/atkadd 已按等级折算）
        public Dictionary<string, BattleDataModule.NormalAttackData> NormalAttacks { get; }
            = new Dictionary<string, BattleDataModule.NormalAttackData>();

        // 部署费用（键集 = 允许部署的角色白名单）
        public Dictionary<string, int> DeployCosts { get; } = new Dictionary<string, int>();
    }
}
