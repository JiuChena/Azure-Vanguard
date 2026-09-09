using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 定时器事件管理器，分别基于缩放与非缩放时间轴触发回调。
    /// </summary>
    public class Timer : MonoBehaviour
    {
        private const double TimeRebaseThreshold = 1_000_000d;
        private const int RebaseBatchSize = 256;

        private static Timer instance;
        // 受缩放定时事件：使用 Time.deltaTime 推进。
        private readonly List<TimerEvent> scaledActions = new List<TimerEvent>();
        // 不受缩放定时事件：使用 Time.unscaledDeltaTime 推进。
        private readonly List<TimerEvent> unscaledActions = new List<TimerEvent>();
        // 受缩放定时器的累计时间。
        private double scaledCurrentTime;
        // 不受缩放定时器的累计时间。
        private double unscaledCurrentTime;
        // 是否正在分批重置两条时间轴上的事件记录。
        private bool isRebasing;
        // 当前时间轴重置协程，用于避免重复启动。
        private Coroutine rebaseCoroutine;

        public static Timer Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject obj = new GameObject();
                    instance = obj.AddComponent<Timer>();
                    obj.name = "TimerEventManager";
                    DontDestroyOnLoad(obj);
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 添加一个定时事件，默认只触发一次。
        /// </summary>
        /// <param name="interval">触发间隔（秒）</param>
        /// <param name="action">触发时执行的回调</param>
        public void AddTimerEvent(float interval, UnityAction action)
        {
            AddTimerEvent(interval, 1, action);
        }

        /// <summary>
        /// 添加一个定时事件。
        /// </summary>
        /// <param name="interval">触发间隔（秒），必须大于 0</param>
        /// <param name="triggerCount">触发次数。1 代表一次，8 代表八次，-1 代表无限次。</param>
        /// <param name="action">触发时执行的回调</param>
        public void AddTimerEvent(float interval, int triggerCount, UnityAction action)
        {
            AddTimerEvent(scaledActions, scaledCurrentTime, interval, triggerCount, action);
        }

        /// <summary>
        /// 添加一个不受时间缩放影响的定时事件，默认只触发一次。
        /// </summary>
        /// <param name="interval">触发间隔（秒），使用非缩放时间推进。</param>
        /// <param name="action">触发时执行的回调。</param>
        public void AddUnscaledTimerEvent(float interval, UnityAction action)
        {
            AddUnscaledTimerEvent(interval, 1, action);
        }

        /// <summary>
        /// 添加一个不受时间缩放影响的定时事件。
        /// </summary>
        /// <param name="interval">触发间隔（秒），必须大于 0。</param>
        /// <param name="triggerCount">触发次数。1 代表一次，8 代表八次，-1 代表无限次。</param>
        /// <param name="action">触发时执行的回调。</param>
        public void AddUnscaledTimerEvent(float interval, int triggerCount, UnityAction action)
        {
            AddTimerEvent(unscaledActions, unscaledCurrentTime, interval, triggerCount, action);
        }

        /// <summary>
        /// 将定时事件加入指定时间基准的事件列表。
        /// </summary>
        /// <param name="targetActions">要写入的定时事件列表。</param>
        /// <param name="recordTime">新事件使用的当前时间点。</param>
        /// <param name="interval">触发间隔（秒），必须大于 0。</param>
        /// <param name="triggerCount">触发次数。1 代表一次，8 代表八次，-1 代表无限次。</param>
        /// <param name="action">触发时执行的回调。</param>
        private void AddTimerEvent(List<TimerEvent> targetActions, double recordTime, float interval, int triggerCount, UnityAction action)
        {
            if (interval <= 0f)
            {
                Debug.LogError("TimerEvent interval must be greater than 0.");
                return;
            }

            if (triggerCount == 0 || triggerCount < -1)
            {
                Debug.LogError("TimerEvent triggerCount must be -1 or greater than 0.");
                return;
            }

            if (action == null)
            {
                Debug.LogError("TimerEvent action can not be null.");
                return;
            }

            targetActions.Add(new TimerEvent(interval, triggerCount, action, recordTime));
        }

        /// <summary>
        /// 推进两条时间轴并触发到期事件。
        /// </summary>
        private void Update()
        {
            scaledCurrentTime += Time.deltaTime;
            unscaledCurrentTime += Time.unscaledDeltaTime;

            if (isRebasing)
            {
                return;
            }

            if (scaledCurrentTime >= TimeRebaseThreshold || unscaledCurrentTime >= TimeRebaseThreshold)
            {
                StartRebase();
                return;
            }

            for (int i = scaledActions.Count - 1; i >= 0; i--)
            {
                TimerEvent timerEvent = scaledActions[i];

                while (scaledCurrentTime - timerEvent.RecordTime >= timerEvent.Interval)
                {
                    timerEvent.RecordTime += timerEvent.Interval;
                    timerEvent.Action.Invoke();

                    if (timerEvent.TriggerCount > 0)
                    {
                        timerEvent.ExecutedCount++;
                        if (timerEvent.ExecutedCount >= timerEvent.TriggerCount)
                        {
                            scaledActions.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            for (int i = unscaledActions.Count - 1; i >= 0; i--)
            {
                TimerEvent timerEvent = unscaledActions[i];

                while (unscaledCurrentTime - timerEvent.RecordTime >= timerEvent.Interval)
                {
                    timerEvent.RecordTime += timerEvent.Interval;
                    timerEvent.Action.Invoke();

                    if (timerEvent.TriggerCount > 0)
                    {
                        timerEvent.ExecutedCount++;
                        if (timerEvent.ExecutedCount >= timerEvent.TriggerCount)
                        {
                            unscaledActions.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 启动两条时间轴的分批重置，避免累计时间过大造成精度损失。
        /// </summary>
        private void StartRebase()
        {
            if (isRebasing)
            {
                return;
            }

            isRebasing = true;
            double scaledRebaseAmount = scaledCurrentTime;
            double unscaledRebaseAmount = unscaledCurrentTime;
            scaledCurrentTime = 0d;
            unscaledCurrentTime = 0d;

            if (rebaseCoroutine != null)
            {
                StopCoroutine(rebaseCoroutine);
            }

            rebaseCoroutine = StartCoroutine(RebaseTimeAsync(
                scaledRebaseAmount,
                unscaledRebaseAmount,
                scaledActions.Count,
                unscaledActions.Count));
        }

        /// <summary>
        /// 分批平移两条时间轴上的事件记录，避免长时间阻塞主线程。
        /// </summary>
        /// <param name="scaledRebaseAmount">受缩放时间轴需要平移的累计时间。</param>
        /// <param name="unscaledRebaseAmount">不受缩放时间轴需要平移的累计时间。</param>
        /// <param name="scaledEventCountSnapshot">开始重置时受缩放事件的数量快照。</param>
        /// <param name="unscaledEventCountSnapshot">开始重置时不受缩放事件的数量快照。</param>
        private System.Collections.IEnumerator RebaseTimeAsync(
            double scaledRebaseAmount,
            double unscaledRebaseAmount,
            int scaledEventCountSnapshot,
            int unscaledEventCountSnapshot)
        {
            int processedCount = 0;
            int safeScaledCount = Mathf.Min(scaledEventCountSnapshot, scaledActions.Count);
            int safeUnscaledCount = Mathf.Min(unscaledEventCountSnapshot, unscaledActions.Count);

            for (int i = 0; i < safeScaledCount; i++)
            {
                scaledActions[i].RecordTime -= scaledRebaseAmount;
                processedCount++;

                if (processedCount >= RebaseBatchSize)
                {
                    processedCount = 0;
                    yield return null;
                }
            }

            for (int i = 0; i < safeUnscaledCount; i++)
            {
                unscaledActions[i].RecordTime -= unscaledRebaseAmount;
                processedCount++;

                if (processedCount >= RebaseBatchSize)
                {
                    processedCount = 0;
                    yield return null;
                }
            }

            rebaseCoroutine = null;
            isRebasing = false;
        }
    }
}
