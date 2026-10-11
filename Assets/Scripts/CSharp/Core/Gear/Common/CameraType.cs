namespace Core.Gear
{
    /// <summary>
    /// 相机类型键：注册与查询相机时使用的枚举键，按项目需要在此扩充取值。
    /// </summary>
    public enum CameraType
    {
        // 主相机：场景渲染。
        Main,
        // UI 相机：Canvas 渲染。
        UI,
    }
}
