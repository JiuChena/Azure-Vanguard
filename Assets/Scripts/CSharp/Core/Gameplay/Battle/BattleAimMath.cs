using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 子弹锁头散射数学（纯函数，供 PooledSpawnExecuteSO 与验证脚本共用）：
    /// 目标 head 点加 XYZ 随机散射（scatter 半径内球）后减去枪口位置得发射方向。
    /// </summary>
    public static class BattleAimMath
    {
        /// <summary>
        /// 解析锁头发射方向：目标点 + 内球随机散射（scatter ≤ 0 不加散射）− 枪口 → 归一化方向。
        /// </summary>
        /// <param name="muzzle">枪口世界位置。</param>
        /// <param name="aimPoint">目标 head 世界位置。</param>
        /// <param name="scatter">XYZ 散射半径（米）；0 = 精准锁头。</param>
        /// <returns>发射方向（单位向量）；枪口与目标重合时回退正向。</returns>
        public static Vector3 ResolveDirection(Vector3 muzzle, Vector3 aimPoint, float scatter)
        {
            Vector3 aim = scatter > 0f ? aimPoint + Random.insideUnitSphere * scatter : aimPoint;
            Vector3 dir = aim - muzzle;
            return dir.sqrMagnitude > 1e-9f ? dir.normalized : Vector3.forward;
        }
    }
}
