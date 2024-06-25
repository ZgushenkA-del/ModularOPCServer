using System.Text.Json.Serialization;

namespace OPCUAServerLibrary;

public class OpcuaNode
{
    /// <summary>
    /// Node path (splicing step by step)
    /// </summary>
    public string NodePath { get; set; }
    /// <summary>
    /// Parent node path (splicing step by step)
    /// </summary>
    public string ParentPath { get; set; }
    /// <summary>
    /// Node number 
    /// (the node number in this business system is not completely unique, 
    /// but all measuring point Ids are different)
    /// </summary>
    public int NodeId { get; set; }
    /// <summary>
    /// Node name (display name)
    /// </summary>
    public string NodeName { get; set; }
    /// <summary>
    /// Is it an endpoint (the bottom child node)
    /// </summary>
    public bool IsTerminal { get; set; }
    /// <summary>
    /// Node type
    /// </summary>
    [JsonIgnore]
    public NodeType NodeType { get; set; }
}
public enum NodeType
{
    /// <summary>
    /// Root node
    /// </summary>
    Scada = 1,
    /// <summary>
    /// Table of contents
    /// </summary>
    Channel = 2,
    /// <summary>
    /// Table of contents
    /// </summary>
    Device = 3,
    /// <summary>
    /// Measuring point
    /// </summary>
    Measure = 4
}
