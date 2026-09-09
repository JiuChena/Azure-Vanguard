using System.Collections;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;

public class Preloader : MonoBehaviour
{
    void Start()
    {
        //网络传输信息注册初始化
        MessageTool.Init();
    }
}
