using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Gear
{
    /// <summary>
    /// 玩家主动探寻交互组件：定期扫描附近可交互物体，管理选项列表与交互按键。
    /// 子类可重写检测方式与对比逻辑扩展。
    /// </summary>
    public class InteractSearcher : MonoBehaviour
    {
        [Header("检测参数")]
        [SerializeField, Tooltip("探寻半径（米）")]
        private float _searchRadius = 3f;

        [SerializeField, Tooltip("探寻间隔（秒）")]
        private float _searchInterval = 0.2f;

        [SerializeField, Tooltip("参与探寻的物理层")]
        private LayerMask _targetLayer = ~0;

        [Header("选项生成")]
        [SerializeField, Tooltip("选项预制体（必须挂 InteractOption 组件）")]
        private InteractOption _optionPrefab;

        [SerializeField, Tooltip("选项生成父节点")]
        private Transform _optionsRoot;

        [SerializeField, Tooltip("选项纵向间距（像素）")]
        private float _optionSpacing = 30f;

        [Header("交互按键")]
        [SerializeField, Tooltip("执行交互的按键")]
        private KeyCode _interactKey = KeyCode.F;

        // 选项字典：GUID → 选项视图实例。
        private readonly Dictionary<int, InteractOption> _options = new Dictionary<int, InteractOption>();

        // 复用扫描命中缓存，避免每帧分配。
        private readonly List<Interactable> _freshInteractables = new List<Interactable>();

        // 复用剔除键缓存。
        private readonly List<int> _staleKeys = new List<int>();

        // 当前选中索引。
        private int _selectedIndex;

        // 扫描计时器。
        private float _searchTimer;

        // 滚轮切换冷却计时器。
        private float _scrollCooldownTimer;

        /// <summary>获取当前交互选项数量。</summary>
        public int OptionCount => _options.Count;

        /// <summary>获取当前选中索引。</summary>
        public int SelectedIndex => _selectedIndex;

        private void Update()
        {
            TickSearch();
            HandleSelectionInput();
            HandleInteractInput();
        }

        /// <summary>
        /// 定期扫描：定时触发范围查询，过滤 Interactable 后双向对比刷新选项列表。
        /// </summary>
        protected virtual void TickSearch()
        {
            _searchTimer -= Time.deltaTime;
            if (_searchTimer > 0f) return;
            _searchTimer = _searchInterval;

            // 收集当前范围内的所有可交互物体。
            _freshInteractables.Clear();
            CollectInteractables(_freshInteractables);

            RefreshOptions(_freshInteractables);
        }

        /// <summary>
        /// 执行范围查询并收集可交互物体。子类可改为射线、Box 或自定义检测。
        /// </summary>
        /// <param name="results">收集到的可交互物体列表。</param>
        protected virtual void CollectInteractables(List<Interactable> results)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _searchRadius, _targetLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Interactable interactable = hits[i].GetComponent<Interactable>();
                if (interactable == null) continue;
                if (!interactable.CanInteract(gameObject)) continue;
                results.Add(interactable);
            }
        }

        /// <summary>
        /// 双向对比刷新选项字典：遍历新结果查字典新增，遍历字典查新结果剔除。
        /// </summary>
        /// <param name="fresh">本次扫描收集到的新可交互物体列表。</param>
        private void RefreshOptions(List<Interactable> fresh)
        {
            // 第一步：遍历新结果，字典中不存在的 GUID 新增选项视图。
            for (int i = 0; i < fresh.Count; i++)
            {
                Interactable interactable = fresh[i];
                if (_options.ContainsKey(interactable.Guid)) continue;

                InteractOption option = CreateOption();
                if (option == null) continue;

                option.Bind(interactable);
                _options.Add(interactable.Guid, option);
            }

            // 第二步：遍历字典，新结果中不存在的 GUID 剔除选项视图。
            _staleKeys.Clear();
            foreach (KeyValuePair<int, InteractOption> pair in _options)
            {
                if (IsInFreshResults(fresh, pair.Key)) continue;
                _staleKeys.Add(pair.Key);
            }

            for (int i = 0; i < _staleKeys.Count; i++)
            {
                if (_options.TryGetValue(_staleKeys[i], out InteractOption option))
                {
                    option.Put();
                    _options.Remove(_staleKeys[i]);
                }
            }

            // 刷新后把索引限制到有效范围。
            ClampSelectedIndex();
        }

        /// <summary>
        /// 从对象池生成选项视图并挂到选项根节点。
        /// </summary>
        /// <returns>生成的选项视图；预制体缺失时返回 null。</returns>
        protected virtual InteractOption CreateOption()
        {
            if (_optionPrefab == null || _optionsRoot == null) return null;

            InteractOption option = InteractOption.Get(_optionPrefab);
            option.transform.SetParent(_optionsRoot, false);

            // 按字典当前数量纵向排列，保持间距。
            Vector3 position = option.transform.localPosition;
            position.y = -_options.Count * _optionSpacing;
            option.transform.localPosition = position;
            return option;
        }

        /// <summary>
        /// 滚轮与上下箭头切换选中索引，并同步所有选项的高亮状态。
        /// </summary>
        protected virtual void HandleSelectionInput()
        {
            if (_options.Count <= 1) return;

            bool changed = false;
            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            scroll /= 120f;

            if (scroll > 0f && _selectedIndex > 0)
            {
                _selectedIndex--;
                changed = true;
                _scrollCooldownTimer = 0.15f;
            }
            else if (scroll < 0f && _selectedIndex < _options.Count - 1)
            {
                _selectedIndex++;
                changed = true;
                _scrollCooldownTimer = 0.15f;
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) && _selectedIndex > 0)
            {
                _selectedIndex--;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) && _selectedIndex < _options.Count - 1)
            {
                _selectedIndex++;
                changed = true;
            }

            if (changed)
                RefreshSelectionVisual();
        }

        /// <summary>
        /// 按下交互键时执行当前选中选项的交互事件。
        /// </summary>
        protected virtual void HandleInteractInput()
        {
            // 无选项或索引无效时直接打断，无可执行目标。
            if (_options.Count == 0) return;
            if (_selectedIndex < 0 || _selectedIndex >= _options.Count) return;

            if (!Input.GetKeyDown(_interactKey)) return;

            InteractOption option = GetSelectedOption();
            if (option == null) return;

            option.PlayClick();
            option.ExecuteInteract();
        }

        /// <summary>
        /// 判断指定 GUID 是否存在于本次新扫描结果中。
        /// </summary>
        /// <param name="fresh">新扫描结果列表。</param>
        /// <param name="guid">需要查询的 GUID。</param>
        /// <returns>存在时返回 true。</returns>
        private bool IsInFreshResults(List<Interactable> fresh, int guid)
        {
            for (int i = 0; i < fresh.Count; i++)
            {
                if (fresh[i].Guid == guid) return true;
            }
            return false;
        }

        /// <summary>
        /// 将选中索引限制到 [0, count-1]；无选项时置为 -1。
        /// </summary>
        private void ClampSelectedIndex()
        {
            if (_options.Count == 0)
            {
                _selectedIndex = -1;
                return;
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _options.Count - 1);
            RefreshSelectionVisual();
        }

        /// <summary>
        /// 同步所有选项视图的选中高亮状态。
        /// </summary>
        private void RefreshSelectionVisual()
        {
            int index = 0;
            foreach (KeyValuePair<int, InteractOption> pair in _options)
            {
                pair.Value.SetSelected(index == _selectedIndex);
                index++;
            }
        }

        /// <summary>
        /// 获取当前选中索引对应的选项视图。
        /// </summary>
        /// <returns>选中选项视图；不存在时返回 null。</returns>
        private InteractOption GetSelectedOption()
        {
            int index = 0;
            foreach (KeyValuePair<int, InteractOption> pair in _options)
            {
                if (index == _selectedIndex) return pair.Value;
                index++;
            }
            return null;
        }
    }
}