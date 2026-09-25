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

        //打开加载面板；开场动画播放完毕后由面板回调，才开始异步加载主城场景；
        //加载进度由 LoadSceneManager 经 EventNames.LoadSceneProgress 广播，PreloadPanel 监听刷新
        PanelManager.Instance.OpenPanel<PreloadPanel>(UIPath.PRELOADERPANEL, UILayer.Top, panel =>
        {
            //开场动画（溶解入场）播完后再开始加载，避免动画未播完就切场景
            panel.AddDisplayFinishedListener(() =>
            {
                LoadSceneManager.Instance.LoadSceneAsync(SceneNames.Home, () =>
                {
                    PanelManager.Instance.OpenPanel<HomePanel>(UIPath.HOMEPANEL, UILayer.Bot);
                });
            });
        });
    }
}
