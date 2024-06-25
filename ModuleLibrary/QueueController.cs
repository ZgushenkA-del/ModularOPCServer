using System;
using System.Collections.Generic;
using System.Threading;

namespace ModuleLibrary;

public class QueueController
{
    private readonly Queue<SharedData> _queue = new();
    private readonly object _lock = new();
    public bool IsEmpty => _queue.IsEmpty;
    public bool IsFilled => _queue.IsFilled;
    public int Count => _queue.Count;
    public static int MaxSize => 50;
    public void Enqueue(SharedData data)
    {
        lock (_lock)
        {
            while (Count >= MaxSize)
            {
                Thread.Sleep(1000);
            }
            _queue.Enqueue(data);
        }
    }

    public SharedData Dequeue()
    {
        lock (_lock)
        {
            var value = _queue.Dequeue();
            return value;
        }
    }
}

public class SharedData(List<EntityNode> entities, string folderName, StatusValues statusCode, DateTime timestamp, ActionTypes actionType = ActionTypes.Auto)
{
    public readonly List<EntityNode> Entities = entities;
    public readonly string FolderName = folderName;
    public readonly StatusValues StatusCode = statusCode;
    public readonly DateTime Timestamp = timestamp;
    public readonly ActionTypes ActionType = actionType;
}

public enum ActionTypes
{
    Auto,
    Add,
    UpdateValues,
    Update,
    Delete,
    Replace
}
