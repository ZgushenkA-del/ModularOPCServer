using System;
using System.Collections.Generic;
using System.Linq;

namespace ModuleLibrary;

public class EntityNode
{
    public string Name { get; set; }
    public string Value { get; set; }
    public DateTime TimeStamp { get; set; }
    public StatusValues StatusCode { get; set; }
    public EntityNode Parent { get; set; }
    public List<EntityNode> Children { get; set; } = [];
    public bool IsParent => Children.Count != 0;
    public bool IsRoot => Parent is null;
    public string Path => (Parent is not null ? Parent.Path + "\\" : string.Empty) + Name;
    public NodeStateType NodeType { get; set; }

    public EntityNode(string name, string value, DateTime dateTime, StatusValues statusCode, EntityNode parent = null) : this(name, value, dateTime, statusCode)
    {
        Parent = parent;
    }

    public EntityNode(string name, EntityNode parent = null) : this(name)
    {
        Parent = parent;
    }

    public EntityNode(string name, string value, DateTime dateTime, StatusValues statusCode) : this(name, value)
    {
        Value = value;
        NodeType = NodeStateType.Variable;
        TimeStamp = DateTime.Now;
        StatusCode = statusCode;
    }

    public EntityNode(string name, string value) : this(name)
    {
        Value = value;
        NodeType = NodeStateType.Variable;
    }

    public EntityNode(string name)
    {
        Name = name;
        NodeType = NodeStateType.Folder;
    }

    public void AddChild(EntityNode child)
    {
        Children.Add(child);
        child.Parent = this;
    }

    public override string ToString()
    {
        return Name + " : " + Value;
    }

    public void Print()
    {
        Console.WriteLine(ToString());
    }

    public void PrintAll()
    {
        Console.WriteLine(ToString());
        Queue<Tuple<int, EntityNode>> queue = new();
        foreach (var child in Children)
        {
            queue.Enqueue(Tuple.Create(1, child));
        }

        while (queue.IsFilled)
        {
            var (depth, curEntity) = queue.Dequeue();
            curEntity.Print(depth);
        }
    }
    public void Print(int depth)
    {
        string result = string.Concat(Enumerable.Repeat("\t", depth)) + ToString();
        Console.WriteLine(result);
        Queue<Tuple<int, EntityNode>> queue = new();
        foreach (var child in Children)
        {
            queue.Enqueue(Tuple.Create(depth + 1, child));
        }

        while (queue.IsFilled)
        {
            var (curDepth, curEntity) = queue.Dequeue();
            curEntity.Print(curDepth);
        }

    }
}

public enum NodeStateType
{
    Folder,
    Variable
}
