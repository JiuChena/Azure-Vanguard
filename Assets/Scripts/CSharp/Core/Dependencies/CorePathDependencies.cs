/// <summary>
/// 核心模块资源加载依赖
/// </summary>
public static class CorePathDependencies
{
    #region UISystem

    //Addressable（地址以 AddressableAssetsData 中 UI-Basic 组的实际配置为准）
    public static readonly string Addressable_UISystem_Canvas = "Canvas";

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
