namespace Core.Gear
{
    /// <summary>
    /// 场景名称常量表，所有 SceneManager 加载调用统一引用，消除裸字符串拼写错误。
    /// </summary>
    public static class SceneNames
    {
        // 预加载引导场景（游戏入口，承载 Preloader）。
        public const string Preload = nameof(Preload);
        // 主城场景（预加载完成后进入的第一个场景）。
        public const string Home = nameof(Home);
    }
}
