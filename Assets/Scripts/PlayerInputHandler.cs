using UnityEngine;
using System.Linq;

public enum GraspingMode
{
    Center,
    FrontEdge,
    BackEdge,
    LeftEdge,
    RightEdge,
    FrontLeftCorner,
    FrontRightCorner,
    BackLeftCorner,
    BackRightCorner
}

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Module References")]
    [SerializeField] private SimulationController simController;
    [SerializeField] private WorldStateManager worldStateManager;
    [SerializeField] private UIController uiController;

    [Header("Control Settings")]
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private LayerMask placedPackageLayerMask;
    [SerializeField] private float gridSize = 1.0f;
    [SerializeField] private float probeHeight = 100f;

    [Header("End Effector Settings")]
    [SerializeField] private Vector3 effectorSize = new Vector3(17, 0.1f, 17);

    private bool _isHoldingPackage = false;
    private bool _inputEnabled = true;
    private PackageTemplate _currentPackageTemplate;
    private Camera _mainCamera;
    
    private GraspingMode _graspingMode = GraspingMode.Center;

    private Vector3 _ghostPackagePosition;
    private Quaternion _ghostPackageOrientation;
    private Vector3 _ghostEffectorPosition;
    private Quaternion _ghostEffectorOrientation = Quaternion.identity;

    private Vector3[] _localPackageVertices = new Vector3[8];

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    public void AssignNewPackage(PackageTemplate packageTemplate)
    {
        _currentPackageTemplate = packageTemplate;
        _isHoldingPackage = true;
        _ghostPackageOrientation = Quaternion.identity;
        _graspingMode = GraspingMode.Center;
        
        Vector3 halfSize = _currentPackageTemplate.Dimensions / 2f;
        _localPackageVertices = new Vector3[8] {
            new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
            new Vector3(-halfSize.x, halfSize.y, -halfSize.z), new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
            new Vector3(halfSize.x, halfSize.y, -halfSize.z), new Vector3(halfSize.x, -halfSize.y, halfSize.z),
            new Vector3(-halfSize.x, halfSize.y, halfSize.z), new Vector3(halfSize.x, halfSize.y, halfSize.z)
        };
        
        HandleMouseAndKeyInput();
    }

    public void DisableInput()
    {
        _inputEnabled = false;
        if (uiController != null) uiController.HideGhosts();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backspace) && _inputEnabled)
        {
            simController.RequestUndo();
            return;
        }

        if (!_inputEnabled || !_isHoldingPackage) return;

        HandleMouseAndKeyInput();
        bool isValidPlacement = CheckPlacementValidity();
        
        uiController.UpdateGhosts(
            _ghostPackagePosition, _ghostPackageOrientation, _currentPackageTemplate, 
            _ghostEffectorPosition, _ghostEffectorOrientation, effectorSize, 
            isValidPlacement);

        HandlePlacementConfirmationInput(isValidPlacement);
    }
    
    private void HandleMouseAndKeyInput()
    {
        // 步骤 1: 优先处理旋转输入
        if (Input.GetKeyDown(KeyCode.LeftArrow)) _ghostPackageOrientation = Quaternion.Euler(0, 90, 0) * _ghostPackageOrientation;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) _ghostPackageOrientation = Quaternion.Euler(0, -90, 0) * _ghostPackageOrientation;
        if (Input.GetKeyDown(KeyCode.UpArrow)) _ghostPackageOrientation = Quaternion.Euler(90, 0, 0) * _ghostPackageOrientation;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) _ghostPackageOrientation = Quaternion.Euler(-90, 0, 0) * _ghostPackageOrientation;
        if (Input.GetKeyDown(KeyCode.Tab)) _graspingMode = (GraspingMode)(((int)_graspingMode + 1) % 9);

        // 步骤 2: 确定端拾器的“锚点”
        Ray cameraRay = _mainCamera.ScreenPointToRay(Input.mousePosition);
        Vector3 anchorPoint = _ghostEffectorPosition;
        if (Physics.Raycast(cameraRay, out RaycastHit cameraHit, Mathf.Infinity, groundLayerMask))
        {
            float snappedX = Mathf.Round(cameraHit.point.x / gridSize) * gridSize;
            float snappedZ = Mathf.Round(cameraHit.point.z / gridSize) * gridSize;
            float surfaceY = 0f;
            if (Physics.Raycast(new Vector3(snappedX, probeHeight, snappedZ), Vector3.down, out RaycastHit probeHit, Mathf.Infinity, groundLayerMask))
            {
                surfaceY = probeHit.point.y;
            }
            anchorPoint = new Vector3(snappedX, surfaceY, snappedZ);
        }
        
        // --- 步骤 3: 核心几何解算 (已重写) ---
        
        // A. 找到旋转后包裹的“新顶面”信息
        GetTopFaceInfo(_ghostPackageOrientation, _currentPackageTemplate.Dimensions, 
            out Vector3 topFaceWorldNormal, out Vector3 topFaceCenterWorldOffset, 
            out Vector3 topFaceAxisU, out Vector3 topFaceAxisV, 
            out Vector2 topFaceDimensions);

        // B. 在新顶面上计算抓取偏移
        Vector2 graspOffset2D = GetGraspingOffset2D(topFaceDimensions, effectorSize);
        Vector3 graspOffsetOnFace = topFaceAxisU * graspOffset2D.x + topFaceAxisV * graspOffset2D.y;
        
        // C. 计算从包裹中心到端拾器中心的总世界偏移
        Vector3 totalWorldOffset = topFaceCenterWorldOffset + graspOffsetOnFace;

        // D. 反向计算包裹位置
        float minYOfRotatedShape = _localPackageVertices.Min(v => (_ghostPackageOrientation * v).y);
        float correctedPackageY = anchorPoint.y - minYOfRotatedShape;
        _ghostPackagePosition = new Vector3(anchorPoint.x - totalWorldOffset.x, correctedPackageY, anchorPoint.z - totalWorldOffset.z);

        // E. 最终确定端拾器位置
        _ghostEffectorPosition = _ghostPackagePosition + totalWorldOffset;
        _ghostEffectorPosition.y += effectorSize.y / 2f; // 确保端拾器中心在包裹顶面上方
    }

    private void GetTopFaceInfo(Quaternion rotation, Vector3 dimensions, out Vector3 worldNormal, out Vector3 worldCenterOffset, out Vector3 axisU, out Vector3 axisV, out Vector2 faceDimensions)
    {
        Vector3[] localNormals = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        int topFaceIndex = -1;
        float maxDot = -2f;

        for (int i = 0; i < localNormals.Length; i++)
        {
            float dot = Vector3.Dot(rotation * localNormals[i], Vector3.up);
            if (dot > maxDot)
            {
                maxDot = dot;
                topFaceIndex = i;
            }
        }
        
        worldNormal = rotation * localNormals[topFaceIndex];
        
        switch (topFaceIndex)
        {
            case 0: // Top (+Y)
                worldCenterOffset = rotation * (Vector3.up * dimensions.y / 2f);
                axisU = rotation * Vector3.right; axisV = rotation * Vector3.forward;
                faceDimensions = new Vector2(dimensions.x, dimensions.z);
                break;
            case 1: // Bottom (-Y)
                worldCenterOffset = rotation * (Vector3.down * dimensions.y / 2f);
                axisU = rotation * Vector3.right; axisV = rotation * Vector3.back;
                faceDimensions = new Vector2(dimensions.x, dimensions.z);
                break;
            case 2: // Right (+X)
                worldCenterOffset = rotation * (Vector3.right * dimensions.x / 2f);
                axisU = rotation * Vector3.forward; axisV = rotation * Vector3.up;
                faceDimensions = new Vector2(dimensions.z, dimensions.y);
                break;
            case 3: // Left (-X)
                worldCenterOffset = rotation * (Vector3.left * dimensions.x / 2f);
                axisU = rotation * Vector3.back; axisV = rotation * Vector3.up;
                faceDimensions = new Vector2(dimensions.z, dimensions.y);
                break;
            case 4: // Forward (+Z)
                worldCenterOffset = rotation * (Vector3.forward * dimensions.z / 2f);
                axisU = rotation * Vector3.right; axisV = rotation * Vector3.up;
                faceDimensions = new Vector2(dimensions.x, dimensions.y);
                break;
            default: // Back (-Z)
                worldCenterOffset = rotation * (Vector3.back * dimensions.z / 2f);
                axisU = rotation * Vector3.left; axisV = rotation * Vector3.up;
                faceDimensions = new Vector2(dimensions.x, dimensions.y);
                break;
        }
    }
    
    private Vector2 GetGraspingOffset2D(Vector2 faceDims, Vector3 effectorDims)
    {
        Vector2 offset = Vector2.zero;
        Vector2 packageHalf = faceDims / 2f;
        Vector2 effectorHalf = new Vector2(effectorDims.x, effectorDims.z) / 2f; // Use effector's XZ as its face dimensions

        switch (_graspingMode)
        {
            case GraspingMode.Center: offset = Vector2.zero; break;
            case GraspingMode.FrontEdge: offset.y = packageHalf.y - effectorHalf.y; break; // Z axis on face
            case GraspingMode.BackEdge: offset.y = -packageHalf.y + effectorHalf.y; break;
            case GraspingMode.LeftEdge: offset.x = -packageHalf.x + effectorHalf.x; break; // X axis on face
            case GraspingMode.RightEdge: offset.x = packageHalf.x - effectorHalf.x; break;
            case GraspingMode.FrontLeftCorner: offset = new Vector2(-packageHalf.x + effectorHalf.x, packageHalf.y - effectorHalf.y); break;
            case GraspingMode.FrontRightCorner: offset = new Vector2(packageHalf.x - effectorHalf.x, packageHalf.y - effectorHalf.y); break;
            case GraspingMode.BackLeftCorner: offset = new Vector2(-packageHalf.x + effectorHalf.x, -packageHalf.y + effectorHalf.y); break;
            case GraspingMode.BackRightCorner: offset = new Vector2(packageHalf.x - effectorHalf.x, -packageHalf.y + effectorHalf.y); break;
        }
        return offset;
    }

    private bool CheckPlacementValidity()
    {
        if (!IsVolumeValid(_ghostPackagePosition, _currentPackageTemplate.Dimensions / 2f, _ghostPackageOrientation, _localPackageVertices)) return false;
        if (!IsVolumeValid(_ghostEffectorPosition, effectorSize / 2f, _ghostEffectorOrientation, null)) return false;
        return true;
    }

    private bool IsVolumeValid(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3[] localVertices)
    {
        Vector3 containerHalfSize = simController.ContainerDimensions / 2f;
        if (localVertices == null)
        {
            localVertices = new Vector3[8] { new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z), new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z), new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z), new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z), new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z), new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z), new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z), new Vector3(halfExtents.x, halfExtents.y, halfExtents.z) };
        }
        foreach (var vertex in localVertices)
        {
            Vector3 worldVertex = center + (orientation * vertex);
            if (Mathf.Abs(worldVertex.x) > containerHalfSize.x || worldVertex.y > containerHalfSize.y * 2 || worldVertex.y < 0 || Mathf.Abs(worldVertex.z) > containerHalfSize.z)
            {
                return false;
            }
        }
        Vector3 overlapCheckHalfSize = halfExtents * 0.999f;
        if (Physics.OverlapBox(center, overlapCheckHalfSize, orientation, placedPackageLayerMask).Length > 0)
        {
            return false;
        }
        return true;
    }

    private void HandlePlacementConfirmationInput(bool isValidPlacement)
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (isValidPlacement)
            {
                bool success = worldStateManager.TryAddPackage(_currentPackageTemplate, _ghostPackagePosition, _ghostPackageOrientation);
                if (success)
                {
                    _isHoldingPackage = false;
                    uiController.HideGhosts();
                    simController.OnPackagePlacementSuccess();
                } else { Debug.LogError("放置失败。"); }
            }
        }
    }
}