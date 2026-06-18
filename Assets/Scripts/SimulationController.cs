using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

// 辅助类，用于直接匹配JSON中每个元素的数据结构
public class PackageJsonData
{
    public float length;
    public float width;
    public float height;
}

// 辅助类，用于导出纯净的坐标数据
public class Vector3Data
{
    public float x, y, z;
    public Vector3Data(Vector3 v) { x = v.x; y = v.y; z = v.z; }
}

// 新的导出数据结构，包含两个角点
public class PlacedPackageExportData
{
    public string TemplateId;
    public Vector3Data MinCorner; // 包裹上靠近原点的角点
    public Vector3Data MaxCorner; // 包裹上远离原点的体对角线角点
}

public enum SimulationState
{
    Ready,
    Running,
    Finished
}

public class SimulationController : MonoBehaviour
{
    [Header("Module References")]
    [SerializeField] private WorldStateManager worldStateManager;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private UIController uiController;
    
    [Header("Container Geometry References")]
    [SerializeField] private Transform containerTransform; 
    [SerializeField] private Transform wallBackTransform;
    [SerializeField] private Transform WallRightTransform;
    [SerializeField] private Transform floorTransform;

    [Header("Task Settings")]
    [SerializeField] private string taskFileName = "packages.json";

    public Vector3 ContainerDimensions => _currentTask?.TaskContainer?.Dimensions ?? Vector3.zero;

    private TaskDefinition _currentTask;
    private SimulationState _currentState;
    private int _packageQueueIndex;

    private void Start()
    {
        // 增加对新引用的检查
        if (containerTransform == null || wallBackTransform == null || WallRightTransform == null || floorTransform == null)
        {
            Debug.LogError("错误：请在Inspector中链接Container的所有几何体引用 (Container, Wall_Back, Wall_Left, Floor)！");
            return;
        }
        
        TaskDefinition task = LoadTaskFromJson(taskFileName); 
        if (task != null)
        {
            LoadTask(task);
            StartSimulation();
        }
        else
        {
            Debug.LogError($"无法从文件 {taskFileName} 加载任务，仿真中止。");
        }
    }
    // --- 新增：悔棋的核心处理方法 ---
    public void RequestUndo()
    {
        // 尝试从状态管理器撤销上一步
        PlacedPackage undonePackage = worldStateManager.UndoLastPlacement();

        // 如果成功撤销 (undonePackage 不为 null)
        if (undonePackage != null)
        {
            // 1. 将包裹供给队列的索引减一
            _packageQueueIndex--;

            // 2. 命令UI控制器根据“新的当前状态”（即上一个状态）重画场景
            uiController.RenderWorldState(worldStateManager.CurrentState);

            // 3. 从任务目录中找到被撤销包裹的模板信息
            if (_currentTask.PackageCatalog.TryGetValue(undonePackage.TemplateId, out PackageTemplate packageTemplate))
            {
                // 4. 命令玩家输入处理器重新“拿起”这个包裹
                playerInputHandler.AssignNewPackage(packageTemplate);

                // 5. 更新UI的“下一个包裹”预览
                uiController.UpdateNextPackagePreview(packageTemplate);
            }
        }
    }
    private TaskDefinition LoadTaskFromJson(string fileName)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(filePath))
        {
            Debug.LogError($"错误：在StreamingAssets文件夹中找不到文件 {fileName}");
            return null;
        }

        string jsonString = File.ReadAllText(filePath);
        List<PackageJsonData> packageDataList = JsonConvert.DeserializeObject<List<PackageJsonData>>(jsonString);

        if (packageDataList == null)
        {
            Debug.LogError($"错误：JSON文件 {fileName} 格式不正确。");
            return null;
        }

        var task = new TaskDefinition
        {
            TaskId = Path.GetFileNameWithoutExtension(fileName),
            Description = $"从 {fileName} 加载的任务，共 {packageDataList.Count} 个包裹。",
            TaskContainer = new Container { Dimensions = new Vector3(40, 80, 47) },
            PackageCatalog = new Dictionary<string, PackageTemplate>(),
            PackageQueue = new List<string>()
        };

        foreach (var packageData in packageDataList)
        {
            string templateId = $"BOX-{packageData.width}x{packageData.height}x{packageData.length}";
            
            if (!task.PackageCatalog.ContainsKey(templateId))
            {
                task.PackageCatalog.Add(templateId, new PackageTemplate
                {
                    TemplateId = templateId,
                    Dimensions = new Vector3(packageData.width, packageData.height, packageData.length)
                });
            }
            task.PackageQueue.Add(templateId);
        }
        return task;
    }

    public void LoadTask(TaskDefinition task)
    {
        _currentTask = task;
        _packageQueueIndex = 0;
        var initialPackages = new Dictionary<string, PlacedPackage>();
        var initialState = new WorldState(_currentTask.TaskContainer, initialPackages, 0);
        worldStateManager.Initialize(initialState);
        uiController.Initialize(_currentTask);
        _currentState = SimulationState.Ready;
        Debug.Log($"任务 '{_currentTask.TaskId}' 已加载，准备就绪。");
    }

    public void StartSimulation()
    {
        if (_currentState != SimulationState.Ready)
        {
            Debug.LogError("错误：仿真只有在准备就绪状态下才能开始。");
            return;
        }
        _currentState = SimulationState.Running;
        Debug.Log("仿真开始！");
        PresentNextPackageToPlayer();
    }

    public void OnPackagePlacementSuccess()
    {
        if (_currentState != SimulationState.Running) return;
        Debug.Log($"包裹 {_packageQueueIndex + 1} / {_currentTask.PackageQueue.Count} 放置成功。");
        uiController.RenderWorldState(worldStateManager.CurrentState);
        _packageQueueIndex++;
        PresentNextPackageToPlayer();
    }

    private void PresentNextPackageToPlayer()
    {
        if (_packageQueueIndex >= _currentTask.PackageQueue.Count)
        {
            EndSimulation("所有包裹均已放置。");
            return;
        }
        string nextPackageTemplateId = _currentTask.PackageQueue[_packageQueueIndex];
        if (_currentTask.PackageCatalog.TryGetValue(nextPackageTemplateId, out PackageTemplate packageTemplate))
        {
            playerInputHandler.AssignNewPackage(packageTemplate);
            uiController.UpdateNextPackagePreview(packageTemplate);
        }
        else
        {
            EndSimulation($"错误：包裹ID '{nextPackageTemplateId}' 在目录中未找到。");
        }
    }
    
    private void EndSimulation(string reason)
    {
        if (_currentState == SimulationState.Finished) return;
        _currentState = SimulationState.Finished;
        playerInputHandler.DisableInput();
        uiController.ShowSimulationEndScreen(reason);
        Debug.Log($"仿真结束: {reason}");
        ExportPlacementData();
    }

    private void ExportPlacementData()
    {
        WorldState finalState = worldStateManager.CurrentState;
        if (finalState == null || finalState.PlacementOrder.Count == 0)
        {
            Debug.Log("没有包裹被放置，无需导出。");
            return;
        }

        // 精确计算坐标原点 (容器内角)
        float originX = WallRightTransform.position.x + WallRightTransform.localScale.x / 2f;
        float originY = floorTransform.position.y + floorTransform.localScale.y / 2f;
        float originZ = wallBackTransform.position.z + wallBackTransform.localScale.z / 2f;
        
        Vector3 originPoint = new Vector3(originX, originY, originZ);
        Debug.Log($"使用容器内角作为坐标原点进行导出，其世界坐标为: {originPoint}");

        var packagesToExport = new List<PlacedPackageExportData>();
        
        foreach (string instanceId in finalState.PlacementOrder)
        {
            if (finalState.PlacedPackages.TryGetValue(instanceId, out PlacedPackage package))
            {
                if (_currentTask.PackageCatalog.TryGetValue(package.TemplateId, out PackageTemplate template))
                {
                    // Vector3 halfDimensions = template.Dimensions / 2f;
                    Vector3 halfDimensions = new Vector3(-template.Dimensions.x, template.Dimensions.y, template.Dimensions.z) / 2f;
                    Vector3 localMinCorner = -halfDimensions;
                    Vector3 localMaxCorner = halfDimensions;

                    Vector3 worldMinCorner = package.Position + (package.Orientation * localMinCorner);
                    Vector3 worldMaxCorner = package.Position + (package.Orientation * localMaxCorner);
                    
                    Debug.Log($"包裹{instanceId} 左下世界坐标为: {worldMinCorner}，右上世界坐标为：{worldMaxCorner}");

                    Vector3 relativeMinCorner = new Vector3(worldMinCorner.z - originPoint.z, originPoint.x - worldMinCorner.x, worldMinCorner.y - originPoint.y) ;
                    Vector3 relativeMaxCorner = new Vector3(worldMaxCorner.z - originPoint.z, originPoint.x - worldMaxCorner.x, worldMaxCorner.y - originPoint.y) ;

                    var exportData = new PlacedPackageExportData
                    {
                        TemplateId = package.TemplateId,
                        MinCorner = new Vector3Data(relativeMinCorner),
                        MaxCorner = new Vector3Data(relativeMaxCorner)
                    };
                    packagesToExport.Add(exportData);
                }
            }
        }

        string jsonString = JsonConvert.SerializeObject(packagesToExport, Formatting.Indented);
        string fileName = $"placement_data_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            File.WriteAllText(filePath, jsonString);
            Debug.Log($"<color=lime>成功导出放置数据到: {filePath}</color>");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"导出文件失败: {e.Message}");
        }
    }
}