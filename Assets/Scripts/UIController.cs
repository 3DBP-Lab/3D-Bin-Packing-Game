using UnityEngine;
using System.Collections.Generic;
using System.Linq; // <-- THIS IS THE FIX
using TMPro;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    [Header("3D Object Prefabs")]
    [SerializeField] private GameObject packagePrefab; 
    [SerializeField] private GameObject effectorPrefab; // 新增：端拾器的预制件
    [SerializeField] private Material ghostPackageMaterialValid;
    [SerializeField] private Material ghostEffectorMaterialValid; // <-- 新增：端拾器的有效材质（例如，绿色）
    [SerializeField] private Material ghostMaterialInvalid;
    [SerializeField] private Material solidPackageMaterial;

    [Header("2D UI Elements")]
    [SerializeField] private TextMeshProUGUI taskInfoText;
    [SerializeField] private TextMeshProUGUI nextPackageInfoText;
    [SerializeField] private GameObject simulationEndScreen;

    private GameObject _ghostPackageInstance;
    private GameObject _ghostEffectorInstance; // 新增：追踪“幽灵”端拾器
    private Dictionary<string, GameObject> _placedPackageObjects = new Dictionary<string, GameObject>();
    private TaskDefinition _currentTask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void Initialize(TaskDefinition task)
    {
        _currentTask = task;
        DisplayTaskInfo(task.Description);
    }
    
    public void RenderWorldState(WorldState state)
    {
        // 检查数据中的所有包裹
        foreach (var packageData in state.PlacedPackages.Values)
        {
            // 如果场景中还没有这个包裹的GameObject，就创建一个
            if (!_placedPackageObjects.ContainsKey(packageData.InstanceId))
            {
                GameObject newSolidPackage = Instantiate(packagePrefab, packageData.Position, packageData.Orientation);
                newSolidPackage.name = $"Package_{packageData.TemplateId}_{packageData.InstanceId.Substring(0, 4)}";
                
                if (_currentTask != null && _currentTask.PackageCatalog.TryGetValue(packageData.TemplateId, out PackageTemplate template))
                {
                    newSolidPackage.transform.localScale = template.Dimensions;
                }

                Renderer renderer = newSolidPackage.GetComponent<Renderer>();
                if (renderer != null && solidPackageMaterial != null)
                {
                    renderer.material = solidPackageMaterial;
                }

                // --- 关键修改 ---
                // 将新创建的实体包裹设置到“PlacedPackage”图层，以便下次射线可以探测到它
                newSolidPackage.layer = LayerMask.NameToLayer("PlacedPackage");
                // ----------------

                _placedPackageObjects.Add(packageData.InstanceId, newSolidPackage);
            }
        }

        // --- 步骤 2: 移除所有不在当前状态中的包裹 (悔棋的关键) ---
        // 找出那些在场景中存在，但在“新的当前状态”数据中已不存在的包裹
        List<string> packagesToRemove = _placedPackageObjects.Keys.Except(state.PlacedPackages.Keys).ToList();
        
        foreach (var instanceId in packagesToRemove)
        {
            if (_placedPackageObjects.TryGetValue(instanceId, out GameObject objectToDestroy))
            {
                Destroy(objectToDestroy); // 从场景中销毁游戏对象
                _placedPackageObjects.Remove(instanceId); // 从追踪字典中移除
            }
        }
    }

    // --- 新增/修改的方法 ---

    // 扩展原方法，使其能同时更新包裹和端拾器
    public void UpdateGhosts(Vector3 packagePos, Quaternion packageRot, PackageTemplate packageTmpl,
                             Vector3 effectorPos, Quaternion effectorRot, Vector3 effectorSize,
                             bool isValid)
    {
        // 更新包裹
        if (_ghostPackageInstance == null)
        {
            _ghostPackageInstance = Instantiate(packagePrefab);
            _ghostPackageInstance.name = "Ghost Package";
            _ghostPackageInstance.layer = LayerMask.NameToLayer("Ghost");
        }
        _ghostPackageInstance.SetActive(true);
        _ghostPackageInstance.transform.SetPositionAndRotation(packagePos, packageRot);
        _ghostPackageInstance.transform.localScale = packageTmpl.Dimensions;
        
        // 更新端拾器
        if (_ghostEffectorInstance == null)
        {
            _ghostEffectorInstance = Instantiate(effectorPrefab);
            _ghostEffectorInstance.name = "Ghost Effector";
            _ghostEffectorInstance.layer = LayerMask.NameToLayer("Ghost");
        }
        _ghostEffectorInstance.SetActive(true);
        _ghostEffectorInstance.transform.SetPositionAndRotation(effectorPos, effectorRot);
        _ghostEffectorInstance.transform.localScale = effectorSize;

        // --- 核心修改：根据有效性分别为包裹和端拾器设置材质 ---
        Renderer packageRenderer = _ghostPackageInstance.GetComponent<Renderer>();
        Renderer effectorRenderer = _ghostEffectorInstance.GetComponent<Renderer>();

        if (isValid)
        {
            // 如果位置有效，各自使用自己的“有效”材质
            if (packageRenderer != null) packageRenderer.material = ghostPackageMaterialValid;
            if (effectorRenderer != null) effectorRenderer.material = ghostEffectorMaterialValid;
        }
        else
        {
            // 如果位置无效，则统一使用“无效”的红色材质以示警告
            if (packageRenderer != null) packageRenderer.material = ghostMaterialInvalid;
            if (effectorRenderer != null) effectorRenderer.material = ghostMaterialInvalid;
        }
    }

    // 隐藏所有“幽灵”对象
    public void HideGhosts()
    {
        if (_ghostPackageInstance != null) _ghostPackageInstance.SetActive(false);
        if (_ghostEffectorInstance != null) _ghostEffectorInstance.SetActive(false);
    }
    public void DisplayTaskInfo(string info)
    {
        if(taskInfoText != null)
        {
            taskInfoText.text = info;
        }
    }

    public void UpdateNextPackagePreview(PackageTemplate packageTemplate)
    {
        if(nextPackageInfoText != null)
        {
            nextPackageInfoText.text = $"下一个包裹:\nID: {packageTemplate.TemplateId}\n尺寸: {packageTemplate.Dimensions.x}x{packageTemplate.Dimensions.y}x{packageTemplate.Dimensions.z}";
        }
    }
    
    public void ShowSimulationEndScreen(string reason)
    {
        if(simulationEndScreen != null)
        {
            simulationEndScreen.SetActive(true);
        }
    }
}