using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 延迟回池的缓冲数据。
    /// </summary>
    public class BufferReturnObjectInfo
    {
        // 待回池的对象引用
        public GameObject obj;

        // 剩余延迟时间（秒），由 Update 每帧递减至零时触发回池
        public float delayTime;

        public BufferReturnObjectInfo(GameObject obj, float delayTime)
        {
            this.obj = obj;
            this.delayTime = delayTime;
        }
    }
}
