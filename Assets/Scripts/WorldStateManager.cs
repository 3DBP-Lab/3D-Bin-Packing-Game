using UnityEngine;
using System.Collections.Generic;
using System.Linq; // 需要这个来使用 .Values 和 .Count() 等

// WorldStateManager 负责管理所有已放置包裹的核心数据状态
public class WorldStateManager : MonoBehaviour
{
    // --- 单例模式，方便全局访问 ---
    public static WorldStateManager Instance { get; private set; }

    // --- 模块引用 ---
    // 这个脚本需要验证器来进行最终检查
    // 稍后我们会实现 PlacementValidator.cs，并在这里引用它
    // [SerializeField] private PlacementValidator placementValidator; 

    // --- 内部状态 ---
    // 使用一个“状态栈”来存储历史记录，栈顶永远是当前状态。
    // 这使得“撤销(Undo)”操作变得非常简单。
    private Stack<WorldState> _stateHistory = new Stack<WorldState>();

    // --- 公共属性 ---
    // 外部模块只能通过这个属性安全地“读取”当前状态
    public WorldState CurrentState => _stateHistory.Count > 0 ? _stateHistory.Peek() : null;

    private void Awake()
    {
        // 设置单例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    // 由 SimulationController 调用，用于在任务开始时进行初始化
    public void Initialize(WorldState initialState)
    {
        _stateHistory.Clear();
        _stateHistory.Push(initialState);
        Debug.Log("WorldStateManager 已初始化。");
    }

    // 尝试添加一个新包裹到世界状态中
    public bool TryAddPackage(PackageTemplate packageTemplate, Vector3 position, Quaternion orientation)
    {
        // 在这里进行最终的放置有效性检查
        // 注意: 我们暂时还没有PlacementValidator，所以这里先假设总是有效的
        // bool isValid = placementValidator.IsPlacementValid(packageTemplate, position, orientation, CurrentState);
        bool isValid = true; // 临时替代

        if (!isValid)
        {
            Debug.LogWarning("最终验证失败，包裹未添加。");
            return false;
        }

        // --- 核心逻辑：创建新状态，而不是修改旧状态（不可变性原则） ---
        
        // 1. 获取当前状态作为基础
        WorldState previousState = CurrentState;

        // 2. 复制当前已放置的包裹列表
        var newPlacedPackages = new Dictionary<string, PlacedPackage>(previousState.PlacedPackages);
        var newPlacementOrder = new List<string>(previousState.PlacementOrder); // 复制顺序列表
        // 3. 创建新的包裹实例并添加到新列表中
        var newPackageInstance = new PlacedPackage
        {
            InstanceId = System.Guid.NewGuid().ToString(), // 生成一个唯一的ID
            TemplateId = packageTemplate.TemplateId,
            Position = position,
            Orientation = orientation
        };
        newPlacedPackages.Add(newPackageInstance.InstanceId, newPackageInstance);
        newPlacementOrder.Add(newPackageInstance.InstanceId); // <-- 新增：将新包裹ID添加到顺序末尾

        // 4. 创建一个全新的 WorldState 对象
        var newState = new WorldState(
            previousState.ContainerInfo,
            newPlacedPackages,
            previousState.SequenceNumber + 1
        ){
            PlacementOrder = newPlacementOrder // <-- 将更新后的顺序列表存入新状态
        };

        // 5. 将新状态压入历史栈，它现在成为了“当前状态”
        _stateHistory.Push(newState);

        Debug.Log($"包裹 {newPackageInstance.TemplateId} 已成功放置。当前总数: {newState.PlacedPackages.Count}");
        return true;
    }

    // --- 核心修改 ---
    public PlacedPackage UndoLastPlacement()
    {
        // 至少要保留一个初始的空状态，所以历史记录必须大于1
        if (_stateHistory.Count > 1)
        {
            // 找出最后一个放置的包裹ID
            string lastPackageId = CurrentState.PlacementOrder.LastOrDefault();
            if (lastPackageId != null && CurrentState.PlacedPackages.TryGetValue(lastPackageId, out PlacedPackage undonePackage))
            {
                _stateHistory.Pop(); // 移除当前状态，使上一个状态成为新的当前状态
                Debug.Log($"已撤销对包裹 {undonePackage.TemplateId} 的放置。");
                return undonePackage; // 返回被撤销的包裹数据
            }
        }

        Debug.LogWarning("无法撤销，已是初始状态。");
        return null; // 无法撤销，返回null
    }
}