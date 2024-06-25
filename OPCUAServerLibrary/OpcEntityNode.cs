using ModuleLibrary;
using Opc.Ua;
using System;
using System.Text.Json.Serialization;

namespace OPCUAServerLibrary;

public class OpcEntityNode : OpcuaNode
{
    public string NodeValue { get; set; }
    public DateTime TimeStamp { get; set; }
    [JsonInclude]
    public readonly string FolderName;
    [JsonIgnore]
    public StatusCode Status { get; set; }
    [JsonIgnore]
    public string GetGeneralPath { get => NodePath.Replace(FolderName + "\\", "").Replace("\\Значение", ""); }

    public OpcEntityNode() : base() { }

    public OpcEntityNode(string folderName, int id)
    {
        NodeId = id;
        NodeName = folderName;
        NodePath = folderName;
        FolderName = folderName;
        NodeType = NodeType.Channel;
        ParentPath = "1";
        IsTerminal = false;
    }

    public OpcEntityNode(EntityNode node, string folderName, int id)
    {
        NodeId = id;
        NodeName = node.Name;
        NodePath = node.Path;
        FolderName = folderName;
        ParentPath = node.Parent is null ? folderName : folderName + "\\" + node.Parent.Path;
    }

    public static OpcEntityNode Folder(string folderName, EntityNode entityNode, int id)
    {
        return new OpcEntityNode(entityNode, folderName, id)
        {
            NodePath = folderName + "\\" + entityNode.Path,
            IsTerminal = false,
            NodeType = NodeType.Device
        };
    }

    public static OpcEntityNode Leaf(string folderName, EntityNode entityNode, int id)
    {
        return new OpcEntityNode(entityNode, folderName, id)
        {
            NodePath = folderName + "\\" + entityNode.Path,
            IsTerminal = true,
            NodeValue = entityNode.Value,
            NodeType = NodeType.Measure,
            Status = StatusCodesConverter.GetStatusFrom(entityNode.StatusCode),
            TimeStamp = entityNode.TimeStamp
        };
    }
}
