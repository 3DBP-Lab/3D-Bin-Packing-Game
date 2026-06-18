using UnityEngine;
using System.Collections.Generic;

// This file does not get attached to any GameObject.
// It only serves as a central place to define the data structures
// that other scripts (like UIController, WorldStateManager, etc.) will use.





// The "blueprint" for a package type, describing its inherent properties.
[System.Serializable]
public class PackageTemplate
{
    public string TemplateId;
    public Vector3 Dimensions;
    public float Mass;
    public List<string> Constraints;
}

// Represents a package instance that has been successfully placed in the container.
[System.Serializable]
public class PlacedPackage
{
    public string InstanceId;
    public string TemplateId;
    public Vector3 Position;
    public Quaternion Orientation;
}

// Describes the basic properties of the container.
[System.Serializable]
public class Container
{
    public Vector3 Dimensions;
}

// A complete snapshot of the simulation world at a specific moment.
[System.Serializable]
public class WorldState
{
    public Container ContainerInfo;
    public Dictionary<string, PlacedPackage> PlacedPackages;
    public List<string> PlacementOrder; // <-- 新增：用于记录放置顺序的InstanceId列表
    public int SequenceNumber;

    // Constructor to create an initial state
    public WorldState(Container container, Dictionary<string, PlacedPackage> packages, int sequence = 0)
    {
        this.ContainerInfo = container;
        this.PlacedPackages = packages;
        this.PlacementOrder = new List<string>(packages.Keys); // 初始化顺序
        this.SequenceNumber = sequence;
    }
}

// Describes a complete simulation task, loaded from a JSON file.
[System.Serializable]
public class TaskDefinition
{
    public string TaskId;
    public string Description;
    public Container TaskContainer;
    public Dictionary<string, PackageTemplate> PackageCatalog;
    public List<string> PackageQueue;
}