using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Core.Gear
{
    /// <summary>
    /// 场景切换管理器，封装异步场景加载，通过 EventCenter 广播加载进度。
    /// </summary>
    public class LoadSceneManager
    {
        private static readonly LoadSceneManager instance = new LoadSceneManager();
        public static LoadSceneManager Instance => instance;

        /// <summary>
        /// 加载场景（内部使用 LoadSceneAsync 保证回调在场景激活后触发）。
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        /// <param name="callback">场景激活后的回调</param>
        public void LoadScene(string sceneName, UnityAction callback = null)
        {
            PublicMono.Instance.StartCoroutine(LoadSceneCoroutine(sceneName, callback));
        }

        /// <summary>
        /// 异步加载场景，每帧通过 EventNames.LoadSceneProgress 广播进度（0~1）。
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        /// <param name="callback">场景激活后的回调</param>
        public void LoadSceneAsync(string sceneName, UnityAction callback = null)
        {
            PublicMono.Instance.StartCoroutine(LoadSceneAsyncCoroutine(sceneName, callback));
        }

        private static IEnumerator LoadSceneCoroutine(string sceneName, UnityAction callback)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                callback?.Invoke();
                yield break;
            }

            while (!op.isDone)
                yield return null;

            callback?.Invoke();
        }

        private static IEnumerator LoadSceneAsyncCoroutine(string sceneName, UnityAction callback)
        {
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOperation == null)
            {
                callback?.Invoke();
                yield break;
            }

            while (!asyncOperation.isDone)
            {
                EventCenter.Instance.SetEventTrigger(EventNames.LoadSceneProgress, asyncOperation.progress);
                yield return null;
            }

            EventCenter.Instance.SetEventTrigger(EventNames.LoadSceneProgress, 1f);
            callback?.Invoke();
        }
    }
}