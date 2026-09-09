using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 池化基类。泛型自引用约束（CRTP）让每个派生类持有各自独立的静态对象池，天然隔离不串池。
    /// 继承即用：Get / Put / 延迟回池全部内置，开发者不写任何逻辑也能正常池化；
    /// 需要定制取出/回池行为时，override OnGet / OnPut 即可。
    /// 不含父对象逻辑：对象原地激活/禁用，层级归属由调用方自行决定。
    /// </summary>
    public abstract class Poolable<T> : MonoBehaviour where T : Poolable<T>
    {
        // 每个派生类型各一份队列（泛型封闭后类型不同，队列彼此独立）
        private static readonly Queue<T> poolQueue = new Queue<T>();

        /// <summary>
        /// 从池中获取一个实例：优先复用缓存，池空则实例化预制体。
        /// </summary>
        /// <param name="prefab">预制体，首次使用时用于实例化</param>
        public static T Get(T prefab)
        {
            T item = poolQueue.Count > 0 ? poolQueue.Dequeue() : Instantiate(prefab);
            item.gameObject.SetActive(true);
            item.OnGet(); // 激活后再给定制机会，默认空实现
            return item;
        }

        /// <summary>
        /// 立即回池：置为不激活，等待下次复用。
        /// </summary>
        public void Put()
        {
            OnPut(); // 回池前清理，默认空实现
            gameObject.SetActive(false);
            poolQueue.Enqueue((T)this);
        }

        /// <summary>
        /// 延迟回池：delayTime 秒后自动 Put()。延迟期间对象保持激活（用于播放完特效再回收）。
        /// </summary>
        public void Put(float delayTime)
        {
            if (delayTime <= 0f)
            {
                Put();
                return;
            }

            StartCoroutine(DelayPut(delayTime));
        }

        private IEnumerator DelayPut(float delayTime)
        {
            yield return new WaitForSeconds(delayTime);
            Put();
        }

        /// <summary>取出时调用：重置状态、绑定数据等。默认空实现。</summary>
        protected virtual void OnGet() { }

        /// <summary>回池时调用：清理状态、停止粒子等。默认空实现。</summary>
        protected virtual void OnPut() { }
    }
}
