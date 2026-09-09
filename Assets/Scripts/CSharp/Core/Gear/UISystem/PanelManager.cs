using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 集中管理面板生命周期与 Canvas 分层挂点，负责打开、关闭、查询面板。
    /// </summary>
    public class PanelManager
    {
        // 单例实例，静态只读保证首次访问时创建。
        private static readonly PanelManager instance = new PanelManager();
        /// <summary>获取面板管理器单例。</summary>
        public static PanelManager Instance => instance;

        // 已打开面板注册表：面板名 → 面板实例；用于去重与查询。
        private readonly Dictionary<string, PanelBase> panelsDic = new Dictionary<string, PanelBase>();
        // 面板关闭栈：按打开顺序压入，ESC 时弹出栈顶面板。
        private readonly Stack<PanelBase> panelStack = new Stack<PanelBase>();
        // 共享 Canvas 的异步加载任务，首次打开面板时等待完成。
        private readonly Task<ResourceLease<GameObject>> canvasLoadTask;

        // 共享 Canvas 根 Transform，加载完成后缓存。
        public Transform RectTrans_Canvas;
        // 各 UI 层级的挂点 Transform：底层。
        private Transform bot;
        // 各 UI 层级的挂点 Transform：中层。
        private Transform mid;
        // 各 UI 层级的挂点 Transform：顶层。
        private Transform top;
        // 各 UI 层级的挂点 Transform：系统层。
        private Transform system;

        public PanelManager()
        {
            // 预加载共享 Canvas 资源，并监听每帧输入以处理 ESC 关闭。
            canvasLoadTask = AddressableManager.Instance.AcquirePersistentAssetAsync<GameObject>(CorePathDependencies.Addressable_UISystem_Canvas);
            PublicMono.Instance.AddListener(CloseTopPanel);
        }

        /// <summary>
        /// 打开指定类型的面板：校验去重、确保 Canvas、加载资源、实例化到目标层级并注入资源作用域。
        /// </summary>
        /// <typeparam name="T">面板脚本类型。</typeparam>
        /// <param name="panelName">面板资源名与注册名。</param>
        /// <param name="layer">面板挂载的 UI 层级。</param>
        /// <param name="callback">面板打开完成后的回调。</param>
        public async void OpenPanel<T>(string panelName, UILayer layer, UnityAction<T> callback = null) where T : PanelBase
        {
            ResourceScope panelScope = null;
            try
            {
                // 校验面板是否存在实例，避免重复打开。
                if (panelsDic.ContainsKey(panelName)) return;

                // 为字典加入面板实例并校验 CanvasRoot 是否加载完毕。
                panelsDic.Add(panelName, null);
                await EnsureCanvasLoaded();

                // 获取面板挂点层级。
                Transform layerRoot = GetLayerRoot(layer);

                // 创建资源作用域，面板资源随作用域统一释放。
                panelScope = new ResourceScope($"Panel:{panelName}");

                // 加载面板资源。
                ResourceLease<GameObject> panelLease = await AddressableManager.Instance.AcquireAssetAsync<GameObject>(panelName, panelScope);

                // 实例化面板，挂载对应 UILayer 下，规范实例命名。
                GameObject obj = GameObject.Instantiate(panelLease.Asset, layerRoot);
                obj.name = panelName;

                // 获取面板脚本，注入作用域，注册面板名并压入关闭栈。
                T panelScript = obj.GetComponent<T>();
                panelScript.ResourceScope = panelScope;
                panelScript.PanelName = panelName;
                panelsDic[panelName] = panelScript;
                panelStack.Push(panelScript);

                panelScript.DisplayPanel();
                callback?.Invoke(panelScript);
            }
            catch (Exception exception)
            {
                // 打开失败时回滚注册并释放资源作用域。
                Debug.LogException(exception);
                panelsDic.Remove(panelName);
                panelScope?.Dispose();
            }
        }
        
        /// <summary>
        /// 按面板名关闭面板：先查字典找到实例，再转交实例关闭流程。
        /// </summary>
        /// <param name="panelName">需要关闭的面板注册名。</param>
        /// <param name="callback">关闭完成后的回调。</param>
        public void ClosePanel(string panelName, UnityAction callback = null)
        {
            panelsDic.TryGetValue(panelName, out PanelBase panel);
            ClosePanel(panel, callback);
        }

        /// <summary>
        /// 关闭指定面板实例：播退出动画，退出动画放完后由 Timer 延迟统一执行销毁与资源释放。
        /// 面板不在字典中（正在关闭/已关闭/非管理器打开）则忽略，避免重复关闭。
        /// </summary>
        public void ClosePanel(PanelBase panel, UnityAction callback = null)
        {
            // 校验面板实例确实在字典注册中，避免关闭非管理器打开的面板。
            if (panel != null && !string.IsNullOrEmpty(panel.PanelName)
                && panelsDic.TryGetValue(panel.PanelName, out PanelBase registered)
                && registered == panel)
            {
                // 从注册表和关闭栈移除，并播退出动画。
                panelsDic.Remove(panel.PanelName);
                panel.HidePanel();

                // 面板资源释放与销毁：先销毁 GameObject 再释放资源作用域。
                UnityAction destroyAndRelease = () =>
                {
                    if (panel == null) return;
                    panel.DestroyPanel();
                    panel.ResourceScope?.Dispose();
                };

                // 延迟销毁以留出退出动画时间；延迟为 0 时立即销毁。
                if (panel.CloseDelay > 0f) Timer.Instance.AddTimerEvent(panel.CloseDelay, destroyAndRelease);
                else destroyAndRelease();
            }

            callback?.Invoke();
        }

        /// <summary>
        /// 按面板名获取已打开的面板实例。
        /// </summary>
        /// <typeparam name="T">面板脚本类型。</typeparam>
        /// <param name="panelName">面板注册名。</param>
        /// <returns>找到时返回面板实例，否则返回 null 并记录警告。</returns>
        public T GetPanel<T>(string panelName) where T : PanelBase
        {
            if(panelsDic.ContainsKey(panelName)) return panelsDic[panelName] as T;
            else
            {
                Debug.LogWarning($"[{panelName}]面板不存在");
                return null;
            }
        }

        /// <summary>
        /// 激活指定 UI 层级挂点。
        /// </summary>
        /// <param name="layer">需要显示的 UI 层级。</param>
        public void DisplayLayer(UILayer layer)
        {
            Transform layerRoot = GetLayerRoot(layer);
            if (layerRoot != null) layerRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// 隐藏指定 UI 层级挂点。
        /// </summary>
        /// <param name="layer">需要隐藏的 UI 层级。</param>
        public void HideLayer(UILayer layer)
        {
            Transform layerRoot = GetLayerRoot(layer);
            if (layerRoot != null) layerRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 确保共享 Canvas 已实例化并缓存各层级挂点；仅首次打开时执行加载。
        /// </summary>
        private async Task EnsureCanvasLoaded()
        {
            // 已加载则直接返回。
            if (RectTrans_Canvas != null) return;

            // 等待 Canvas 资源异步加载完成。
            ResourceLease<GameObject> canvasLease = await canvasLoadTask;
            if (RectTrans_Canvas != null) return;

            // 加载失败时抛出异常由调用方捕获。
            if (canvasLease == null || canvasLease.Asset == null) throw new Exception("Canvas load failed.");

            // 实例化 Canvas 并设为不随场景销毁。
            GameObject obj = GameObject.Instantiate(canvasLease.Asset);
            RectTrans_Canvas = obj.transform;
            GameObject.DontDestroyOnLoad(obj);

            // 缓存各 UI 层级挂点，供打开面板时定位父节点。
            bot = RectTrans_Canvas.Find(CorePathDependencies.UISystem_Canvas_LayerBot);
            mid = RectTrans_Canvas.Find(CorePathDependencies.UISystem_Canvas_LayerMid);
            top = RectTrans_Canvas.Find(CorePathDependencies.UISystem_Canvas_LayerTop);
            system = RectTrans_Canvas.Find(CorePathDependencies.UISystem_Canvas_LayerSystem);
        }

        /// <summary>
        /// 按 UI 层级返回对应的挂点 Transform。
        /// </summary>
        /// <param name="layer">需要查询的 UI 层级。</param>
        /// <returns>对应层级挂点；未匹配时回退到系统层。</returns>
        private Transform GetLayerRoot(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Bot: return bot;
                case UILayer.Mid: return mid;
                case UILayer.Top: return top;
                case UILayer.System:
                default: return system;
            }
        }

        /// <summary>
        /// 每帧轮询 ESC 键，弹出关闭栈顶部的存活面板并触发其 ESC 逻辑。
        /// </summary>
        private void CloseTopPanel()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // 轮询弹出已销毁（关闭后置空）的面板实例，直到栈顶是存活面板。
            while (panelStack.Count > 0 && panelStack.Peek() == null) panelStack.Pop();
            if (panelStack.Count > 0) panelStack.Peek().OnEscapePressed();   // 面板自身的 ESC 逻辑，默认关闭自己
        }
    }
}
