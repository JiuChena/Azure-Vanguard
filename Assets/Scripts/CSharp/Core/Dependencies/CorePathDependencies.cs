/// <summary>
/// 核心模块资源加载依赖
/// </summary>
public static class CorePathDependencies
{
    #region UISystem

    //Addressable
    public static readonly string Addressable_UISystem_Canvas = "UI-Basic-Canvas";

    //Canvas 预制下各 UI 层挂点名称
    public static readonly string UISystem_Canvas_LayerBot = "Bot";
    public static readonly string UISystem_Canvas_LayerMid = "Mid";
    public static readonly string UISystem_Canvas_LayerTop = "Top";
    public static readonly string UISystem_Canvas_LayerSystem = "System";

    #endregion

    #region Scenes

    //Scenes
    public static readonly string Addressable_Scenes_Preload = "Preload";

    #endregion
}
