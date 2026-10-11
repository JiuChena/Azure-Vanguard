using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 养成面板角色展示区视图（MVC 的 V）：模型锚点与四个朝向点的引用集合，纯组件持有者不含逻辑。
/// 朝向点由模型展示组件读取（模型水平转向对应点），锚点承接运行时加载出的角色模型实例。
/// </summary>
public class CultivateCharacterDisplayView : MonoBehaviour
{
    //朝向点：正前（默认面向镜头）
    public Transform forward;
    //朝向点：正后
    public Transform back;
    //朝向点：左侧
    public Transform left;
    //朝向点：右侧
    public Transform right;

    //模型锚点：运行时加载的角色模型实例挂载于此
    public Transform pos;
}
