using ModuleLibrary;
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using TypeInfo = Opc.Ua.TypeInfo;

namespace OPCUAServerLibrary;

/// <summary>
/// The measuring point in the following remarks represents the most leaf-level node
/// The current design is that only the measuring point has data, and the rest of the nodes are catalogs
/// </summary>
public class NodeManager : CustomNodeManager2
{
    /// <summary>
    /// The number of configuration modifications is mainly used to identify whether the menu tree has changed. 
    /// If there is a change, the real-time data changes of the corresponding node measuring points of the modified menu tree are not included.
    /// </summary>
    private static NodeManager s_instance = null;
    private static int s_countNodeId = 1;
    private static int NextId { get => ++s_countNodeId; }

    private readonly Dictionary<string, ThreadController> _modules = [];
    private readonly QueueController queueController = new QueueController();
    private ref readonly QueueController QueueController => ref queueController;

    private Thread _moduleWatcherThread;
    private Thread _queueHandler;

    private IList<IReference> _references;
    private List<OpcEntityNode> _entities;
    private readonly Dictionary<string, OpcEntityNode> _spaceNames = [];

    private const string _modulesPath = "..\\Modules\\";
    private const string _dataFolderPath = "..\\DataFolder\\";

    /// <summary>
    /// Set of measuring points, when the real-time data is refreshed, 
    /// directly remove the corresponding measuring points from the dictionary, and modify the value
    /// </summary>
    private readonly Dictionary<string, BaseDataVariableState> _nodeDic = [];

    /// <summary>
    /// Directory collection, required when modifying the menu tree 
    /// (we need to know which menus need to be modified, which need to be added, and which need to be deleted)
    /// </summary>
    private readonly Dictionary<string, FolderState> _folderDic = [];

    public NodeManager(IServerInternal server, ApplicationConfiguration configuration) : base(server, configuration, "http://opcfoundation.org/Quickstarts/ReferenceApplications")
    {
    }

    public static NodeManager GetInstance(IServerInternal server, ApplicationConfiguration configuration)
    {
        if (s_instance == null)
        {
            s_instance = new NodeManager(server, configuration);
        }

        return s_instance;
    }

    public static NodeManager GetInstance()
    {
        if (s_instance == null)
        {
            throw new Exception("Instance of node manager doesnt exists");
        }
        return s_instance;
    }


    /// <summary>
    /// Override the NodeID generation method 
    /// (currently separated by '_', if you need to change, please modify this method)
    /// </summary>
    /// <param name="context"></param>
    /// <param name="node"></param>
    /// <returns></returns>
    public override NodeId New(ISystemContext context, NodeState node)
    {
        if (node is BaseInstanceState instance && instance.Parent != null)
        {
            if (instance.Parent.NodeId.Identifier is string id)
            {
                return new NodeId(id + "_" + instance.SymbolicName, instance.Parent.NodeId.NamespaceIndex);
            }
        }

        return node.NodeId;
    }

    /// <summary>
    /// Override the method of obtaining the node handle
    /// </summary>
    /// <param name="context"></param>
    /// <param name="nodeId"></param>
    /// <param name="cache"></param>
    /// <returns></returns>
    protected override NodeHandle GetManagerHandle(ServerSystemContext context, NodeId nodeId,
        IDictionary<NodeId, NodeState> cache)
    {
        lock (Lock)
        {
            // quickly exclude nodes that are not in the namespace
            if (!IsNodeIdInNamespace(nodeId))
            {
                return null;
            }


            if (!PredefinedNodes.TryGetValue(nodeId, out NodeState node))
            {
                return null;
            }

            NodeHandle handle = new()
            {
                NodeId = nodeId,
                Node = node,
                Validated = true
            };

            return handle;
        }
    }

    /// <summary>
    /// Override the verification method of the node
    /// </summary>
    /// <param name="context"></param>
    /// <param name="handle"></param>
    /// <param name="cache"></param>
    /// <returns></returns>
    protected override NodeState ValidateNode(ServerSystemContext context, NodeHandle handle,
        IDictionary<NodeId, NodeState> cache)
    {
        // not valid if no root
        if (handle == null)
        {
            return null;
        }

        // check if previously validated
        if (handle.Validated)
        {
            return handle.Node;
        }

        // TBD
        return null;
    }

    /// <summary>
    /// Override the creation of the basic directory
    /// </summary>
    /// <param name="externalReferences"></param>
    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out _references))
            {
                externalReferences[ObjectIds.ObjectsFolder] = _references = [];
            }

            try
            {
                //создание необходимых каталогов в файловой системе
                CreateDataFolderIfNotExists();
                CreateModuleFolderIfNotExists();
                // Проверка на присуствие модулей
                CheckModulesOnce();
                // Генерация и добавление стандартных узлов
                GenerateNodesAndUpdateAttributes();
                // Изменение данных в точках измерения в режиме реального времени
                UpdateVariableValue();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Инициализация интерфейса вызова вызывает исключение:" + ex.Message + ex.StackTrace);
                Console.ResetColor();
            }
        }
    }

    private void GenerateNodesAndUpdateAttributes()
    {
        AddMainScadaNodes();

        // Начнем создавать дерево меню узлов
        GeneraterNodes(_references);

        UpdateNodesAttributeFromEntities();
    }

    private void AddMainScadaNodes()
    {
        _entities = [
        /*
        * Класс OpcuaNode определен в следующем формате, связанном с личным бизнесом: 
        * Вы можете изменить соответствующую структуру данных в соответствии с вашими собственными данными.
        * Просто убедитесь, что вы можете четко знать принадлежность каждого узла
        */
        new OpcEntityNode()
        {
            NodeId = 1,
            NodeName = "Корневой узел",
            NodePath = "1",
            NodeType = NodeType.Scada,
            ParentPath = "",
            IsTerminal = false
        }];
    }

    /// <summary>
    /// Сгенерируйте корневой узел 
    /// (поскольку корневой узел требует специальной обработки, это отдельный метод)
    /// </summary>
    /// <param name="nodes"></param>
    /// <param name="references"></param>
    private void GeneraterNodes(IList<IReference> references)
    {
        var list = _entities.Where(d => d.NodeType == NodeType.Scada);
        foreach (var item in list)
        {
            try
            {
                FolderState root = CreateFolder(null, item.NodePath, item.NodeName);
                root.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, root.NodeId));
                root.EventNotifier = EventNotifiers.SubscribeToEvents;
                AddRootNotifier(root);
                CreateNodes(_entities, root, item.NodePath);
                _folderDic.Add(item.NodePath, root);
                // Добавить ссылочное отношение
                AddPredefinedNode(SystemContext, root);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Создание корневого узла OPC-UA вызывает исключение:" + ex.Message);
                Console.ResetColor();
            }
        }
    }

    private static void CreateDataFolderIfNotExists()
    {
        if (!Directory.Exists(_dataFolderPath))
        {
            Directory.CreateDirectory(_dataFolderPath);
        }
    }

    private static void CreateModuleFolderIfNotExists()
    {
        if (!Directory.Exists(_modulesPath))
        {
            Directory.CreateDirectory(_modulesPath);
        }
    }

    private void AddDataToEntities(SharedData data)
    {
        ModuleLibrary.Queue<EntityNode> entitiesQueue = new();

        entitiesQueue.Enqueue(data.Entities);

        while (entitiesQueue.IsFilled)
        {
            var curEntity = entitiesQueue.Dequeue();
            AddOpcEntityNode(data.FolderName, curEntity);
            entitiesQueue.Enqueue(curEntity.Children);
        }
    }

    private void AddOpcEntityNode(string folderName, EntityNode entityNode)
    {
        switch (entityNode.NodeType)
        {
            case NodeStateType.Variable:
                _entities.Add(OpcEntityNode.Leaf(folderName, entityNode, NextId)); break;
            case NodeStateType.Folder:
                _entities.Add(OpcEntityNode.Folder(folderName, entityNode, NextId)); break;
            default:
                break;
        }
    }

    private void UpdateValues(SharedData sharedData)
    {
        Console.WriteLine(sharedData.StatusCode);

        var queue = new ModuleLibrary.Queue<EntityNode>();
        var nodeDic = new Dictionary<string, BaseDataVariableState>(_nodeDic);

        queue.Enqueue(sharedData.Entities);

        Console.WriteLine();
        Console.WriteLine("заполнение значений");

        while (queue.IsFilled)
        {
            var entityNode = queue.Dequeue();
            var entityNodePath = sharedData.FolderName + "\\" + entityNode.Path + (!entityNode.IsParent ? "" : "\\Значение");

            string key = null;

            foreach (var pair in nodeDic)
            {
                var nodePath = pair.Value.BrowseName.ToString().Remove(0, 2);
                if (nodePath.Equals(entityNodePath))
                {
                    key = pair.Key;
                    break;
                }
            }
            if (key is not null && nodeDic.Remove(key))
            {
                var node = _nodeDic[key];
                if (int.TryParse(entityNode.Value, out var intValue))
                    node.Value = intValue;
                else if (float.TryParse(entityNode.Value, out var floatValue))
                    node.Value = floatValue;
                else
                    node.Value = entityNode.Value;
                node.Value = entityNode.Value;
                node.Timestamp = entityNode.TimeStamp;
                node.StatusCode = StatusCodesConverter.GetStatusFrom(entityNode.StatusCode);
                node.ClearChangeMasks(SystemContext, false);
            }

            queue.Enqueue(entityNode.Children);
        }
    }

    private void CreateNodes(List<OpcEntityNode> entities, FolderState parent, string parentPath)
    {
        var list = entities.Where(d => d.ParentPath == parentPath);
        foreach (var node in list)
        {
            try
            {
                if (!node.IsTerminal)
                {
                    FolderState folder = CreateFolder(parent, node.NodePath, node.NodeName);
                    _folderDic.Add(node.NodePath, folder);
                    CreateNodes(entities, folder, node.NodePath);
                }
                else
                {
                    BaseDataVariableState variable = CreateVariable(parent, node.NodePath, node.NodeName,
                        DataTypeIds.Double, ValueRanks.Scalar);
                    // It should be noted here that the directory dictionary uses the directory path as THE KEY
                    // and the measuring point dictionary uses the measuring point ID as THE KEY to facilitate the update of real-TIME DATA.
                    _nodeDic.Add(node.NodeId.ToString(), variable);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Create an OPC-UA child node to trigger an exception:" + ex.Message);
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Create directory
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="path"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    private FolderState CreateFolder(NodeState parent, string path, string name)
    {
        FolderState folder = new(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(path, NamespaceIndex),
            DisplayName = new LocalizedText("en", name),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None
        };

        parent?.AddChild(folder);

        return folder;
    }

    /// <summary>
    /// Create node
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="path"></param>
    /// <param name="name"></param>
    /// <param name="dataType"></param>
    /// <param name="valueRank"></param>
    /// <returns></returns>
    private BaseDataVariableState CreateVariable(NodeState parent, string path, string name, NodeId dataType,
        int valueRank)
    {
        BaseDataVariableState variable = new(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(path, NamespaceIndex),
            DisplayName = new LocalizedText("en", name),
            WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            DataType = dataType,
            ValueRank = valueRank,
            AccessLevel = AccessLevels.CurrentRead,
            UserAccessLevel = AccessLevels.CurrentRead,
            Historizing = false,
            //Value = GetNewValue(variable);
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.Now,
            OnWriteValue = OnWriteDataValue,
        };

        variable.ArrayDimensions = valueRank switch
        {
            ValueRanks.OneDimension => new ReadOnlyList<uint>(new List<uint> { 0 }),
            ValueRanks.TwoDimensions => new ReadOnlyList<uint>(new List<uint> { 0, 0 }),
            _ => variable.ArrayDimensions
        };

        parent?.AddChild(variable);

        return variable;
    }

    /// <summary>
    /// Update node data in real time
    /// </summary>
    private void UpdateVariableValue()
    {
        _moduleWatcherThread = new Thread(CheckModulesAndRepeat);
        _moduleWatcherThread.Start();
        _queueHandler = new Thread(QueueHandlingLoop);
        _queueHandler.Start();
    }

    private void CheckModulesAndRepeat()
    {
        try
        {
            while (true)
            {
                CheckModules();
                Thread.Sleep(10000);
            }
        }
        catch (ThreadInterruptedException)
        {
            Console.WriteLine("module thread has been interrupted!");
        }
    }

    private void CheckModulesOnce()
    {
        CheckModules();
    }

    private void CheckModules()
    {
        Console.WriteLine("Проверка модулей");

        var directories = Directory.GetDirectories(_modulesPath);

        foreach (var directory in directories)
        {
            var directoryName = Path.GetFileName(directory);

            if (_modules.ContainsKey(directoryName)) continue;

            Console.WriteLine(Path.GetFullPath(directory));
            var modules = Directory.GetFiles(directory);

            foreach (var file in modules)
            {
                if (Path.GetExtension(file) != ".dll") continue;

                var filename = Path.GetFileName(file);
                var asm = Assembly.LoadFrom(file);

                Console.WriteLine("\t" + filename);

                foreach (var AsseblyClass in asm.GetTypes())
                {
                    if (typeof(OpcModule) == AsseblyClass) continue;
                    if (typeof(OpcModule).IsAssignableFrom(AsseblyClass))
                    {
                        Console.WriteLine("\t\t" + AsseblyClass);
                        _modules.Add(directoryName, new ThreadController((OpcModule)Activator.CreateInstance(AsseblyClass), QueueController, Path.GetFullPath(file)));
                        _modules[directoryName].Start();
                        Console.WriteLine("\t\tЗапущен\n");
                    }
                }
            }
        }
    }

    private void QueueHandlingLoop()
    {
        try
        {
            Console.WriteLine("Начало проверки очереди");
            while (true)
            {
                if (QueueController.IsFilled)
                {
                    SharedData data = QueueController.Dequeue();
                    Console.WriteLine($"Из очереди: {data.FolderName}");
                    HandleData(data);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }
        catch (ThreadInterruptedException)
        {
            Console.WriteLine("Queue controller has been interrupted!");
        }
    }

    private void HandleData(SharedData sharedData)
    {
        try
        {
            switch (sharedData.ActionType)
            {
                case ActionTypes.Auto:
                    if (!IsFolderAlredyRegistered(sharedData.FolderName))
                        AddNodeParent(sharedData);
                    UpdateNodesFromSharedData(sharedData);
                    break;
                case ActionTypes.Add:
                    AddNodesFromSharedData(sharedData);
                    break;
                case ActionTypes.UpdateValues:
                    UpdateValues(sharedData);
                    break;
                case ActionTypes.Update:
                    UpdateNodesFromSharedData(sharedData);
                    break;
                case ActionTypes.Delete:
                    RemoveNodesByFolderName(sharedData.FolderName);
                    break;
                default:
                    break;
            }
            UpdateNodesAttributeFromEntities();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Регистрация данных из очереди вызвало исключение:" + ex.Message + ex.StackTrace);
            Console.ResetColor();
        }

    }

    private bool IsFolderAlredyRegistered(string folderName)
    {
        if (_spaceNames.Keys.ToList().Contains(folderName))
        {
            return true;
        }
        return false;
    }

    private void AddNodeParent(SharedData sharedData)
    {
        Console.WriteLine($"Added {sharedData.FolderName}");
        var folderNode = GetOpcEntityNodeParent(sharedData.FolderName);
        _entities.Add(folderNode);
        _spaceNames.Add(sharedData.FolderName, folderNode);
    }

    private void UpdateNodesFromSharedData(SharedData sharedData)
    {
        try
        {
            Console.WriteLine($"Обновление узлов {sharedData.FolderName}");
            List<OpcEntityNode> tmpEntities = [.. _entities.Where(d => d.FolderName == sharedData.FolderName && d.NodeType != NodeType.Channel)];
            ModuleLibrary.Queue<EntityNode> entitiesQueue = new();
            entitiesQueue.Enqueue(sharedData.Entities);
            while (entitiesQueue.IsFilled)
            {
                var curEntity = entitiesQueue.Dequeue();
                if (!tmpEntities.Exists(x => x.GetGeneralPath == curEntity.Path))
                {
                    AddOpcEntityNode(sharedData.FolderName, curEntity);
                    entitiesQueue.Enqueue(curEntity.Children);
                    continue;
                }
                var newEntity = tmpEntities.Find(x => x.GetGeneralPath == curEntity.Path);
                tmpEntities.Remove(newEntity);
                _entities.Remove(newEntity);
                newEntity.NodeValue = curEntity.Value;
                newEntity.TimeStamp = curEntity.TimeStamp;
                _entities.Add(newEntity);
                entitiesQueue.Enqueue(curEntity.Children);
            }
            _entities.RemoveAll(x => tmpEntities.Contains(x));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Изменение узлов OPC используя разделяемые данные вызвало исключение" + ex.Message + ex.StackTrace);
            Console.ResetColor();
        }
    }

    private void AddNodesFromSharedData(SharedData sharedData)
    {
        try
        {
            Console.WriteLine($"Добавление узлов {sharedData.FolderName}");
            AddNodeParent(sharedData);
            AddDataToEntities(sharedData);
            UpdateNodesAttributeFromEntities();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Добавление узлов OPC используя разделяемые данные вызвало исключение" + ex.Message + ex.StackTrace);
            Console.ResetColor();
        }
    }

    private void RemoveNodesByFolderName(string folderName)
    {
        if (IsFolderAlredyRegistered(folderName))
        {
            _entities.RemoveAll(x => x.FolderName == folderName);
        }
        _spaceNames.Remove(folderName);
    }

    private static OpcEntityNode GetOpcEntityNodeParent(string folderName)
    {
        return new OpcEntityNode(folderName, NextId);
    }

    /// <summary>
    /// Modify the node tree (add node, delete node, modify node name)
    /// </summary>
    /// <param name="_nodes"></param>
    private void UpdateNodesAttributeFromEntities()
    {
        // Modify or create a root node
        RegisterScadasFromEntities();

        /*
        * Modify or create a directory
        * (here it is designed to have multiple levels of directory, the above is the demonstration data,
        * so I only wrote three levels, in fact, more levels are also possible)
        */
        RegisterFoldersFromEntities();

        /*
        * Modify or create a measuring point
        * Here my data structure uses isTerminal to represent whether it is a measuring point.
        * In actual business, it may need to be adjusted according to its own needs.
        */
        RegisterLeavesFromEntities();

        /*
         * Compare the newly acquired menu list with the original list
         * If the new menu list does not contain the original menu
         * It means that this menu has been deleted and needs to be deleted here.
         */
        RemoveDistictionsFromEntities();
    }

    private void RegisterScadasFromEntities()
    {
        var scadas = _entities.Where(d => d.NodeType == NodeType.Scada);
        foreach (var item in scadas)
        {
            if (!_folderDic.TryGetValue(item.NodePath, out FolderState scadaNode))
            {
                // If the root node does not exist, then the entire tree needs to be created
                FolderState root = CreateFolder(null, item.NodePath, item.NodeName);
                root.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                _references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, root.NodeId));
                root.EventNotifier = EventNotifiers.SubscribeToEvents;
                AddRootNotifier(root);
                CreateNodes(_entities, root, item.NodePath);
                _folderDic.Add(item.NodePath, root);
                AddPredefinedNode(SystemContext, root);
                continue;
            }
            else
            {
                scadaNode.DisplayName = item.NodeName;
                scadaNode.ClearChangeMasks(SystemContext, false);
            }
        }
    }

    private void RegisterFoldersFromEntities()
    {
        var folders = _entities.Where(d => d.NodeType != NodeType.Scada && !d.IsTerminal);
        foreach (var folder in folders)
        {
            RegisterFolder(folder);
        }
    }

    private void RegisterLeavesFromEntities()
    {
        var leaves = _entities.Where(d => d.IsTerminal);
        foreach (var leaf in leaves)
        {
            RegisterLeaf(leaf);
        }
    }

    private void RemoveDistictionsFromEntities()
    {
        List<string> folderPath = [.. _folderDic.Keys];
        List<string> nodePath = [.. _nodeDic.Keys];
        var remNode = nodePath.Except(_entities.Where(d => d.IsTerminal).Select(d => d.NodeId.ToString()));
        foreach (var str in remNode)
        {
            if (_nodeDic.TryGetValue(str, out BaseDataVariableState node))
            {
                var parent = node.Parent;
                parent.RemoveChild(node);
                _nodeDic.Remove(str);
            }
        }

        var remFolder = folderPath.Except(_entities.Where(d => !d.IsTerminal).Select(d => d.NodePath));
        foreach (string str in remFolder)
        {
            if (_folderDic.TryGetValue(str, out FolderState folder))
            {
                var parent = folder.Parent;
                if (parent != null)
                {
                    parent.RemoveChild(folder);
                    _folderDic.Remove(str);
                }
                else
                {
                    RemoveRootNotifier(folder);
                    RemovePredefinedNode(SystemContext, folder, new List<LocalReference>());
                }
            }
        }
    }

    private void RegisterLeaf(OpcEntityNode opcEntityNode)
    {
        if (_nodeDic.TryGetValue(opcEntityNode.NodeId.ToString(), out BaseDataVariableState node))
        {
            node.DisplayName = opcEntityNode.NodeName;
            if (int.TryParse(opcEntityNode.NodeValue, out var intValue))
                node.Value = intValue;
            else if (float.TryParse(opcEntityNode.NodeValue, out var floatValue))
                node.Value = floatValue;
            else
                node.Value = opcEntityNode.NodeValue;
            node.Timestamp = opcEntityNode.TimeStamp;
            node.StatusCode = opcEntityNode.Status;
            node.ClearChangeMasks(SystemContext, false);
        }
        else
        {
            if (_folderDic.TryGetValue(opcEntityNode.ParentPath, out FolderState folder))
            {
                node = CreateVariable(folder, opcEntityNode.NodePath, opcEntityNode.NodeName, DataTypeIds.Double,
                    ValueRanks.Scalar);
                if (int.TryParse(opcEntityNode.NodeValue, out var intValue))
                    node.Value = intValue;
                else if (float.TryParse(opcEntityNode.NodeValue, out var floatValue))
                    node.Value = floatValue;
                else
                    node.Value = opcEntityNode.NodeValue;
                node.Timestamp = opcEntityNode.TimeStamp;
                node.StatusCode = opcEntityNode.Status;
                AddPredefinedNode(SystemContext, node);
                folder.ClearChangeMasks(SystemContext, false);
                _nodeDic.Add(opcEntityNode.NodeId.ToString(), node);
            }
        }
    }

    private void RegisterFolder(OpcEntityNode item)
    {
        if (!_folderDic.TryGetValue(item.NodePath, out FolderState folder))
        {
            var par = GetParentFolderState(_entities, item);
            folder = CreateFolder(par, item.NodePath, item.NodeName);
            AddPredefinedNode(SystemContext, folder);
            par.ClearChangeMasks(SystemContext, false);
            _folderDic.Add(item.NodePath, folder);
        }
        else
        {
            folder.DisplayName = item.NodeName;
            folder.ClearChangeMasks(SystemContext, false);
        }
    }

    public override void DeleteAddressSpace()
    {
        base.DeleteAddressSpace();
        _nodeDic.Clear();
        _entities.Clear();
        _folderDic.Clear();
        _moduleWatcherThread.Interrupt();
        _queueHandler.Interrupt();
        foreach (var item in _modules.Values)
        {
            item.Stop();
        }
        s_instance = null;
    }

    #region WebApi methods
    public Dictionary<string, ThreadController> GetThreadControllers()
    {
        return _modules;
    }

    public ThreadController GetThreadController(string key)
    {
        return _modules[key];
    }

    public Dictionary<string, OpcEntityNode> GetFolders()
    {
        return _spaceNames;
    }

    public void DeleteSpaceName(string spaceName)
    {
        if (!_spaceNames.Keys.ToList().Contains(spaceName))
        {
            throw new Exception("No such spaceName");
        }

        RemoveNodesByFolderName(spaceName);
        UpdateNodesAttributeFromEntities();
    }

    public void StopModuleWatcher()
    {
        if (_moduleWatcherThread.IsAlive)
        {
            _moduleWatcherThread.Interrupt();
        }
        else
        {
            throw new Exception("Module watcher already dead!");
        }

    }

    public void StartModuleWatcher(string spaceName)
    {
        if (!_moduleWatcherThread.IsAlive)
        {
            _moduleWatcherThread.Start();
        }
        else
        {
            throw new Exception("Module watcher already working!");
        }
    }

    public void StopAllModules()
    {
        foreach (var module in _modules.Values)
        {
            try
            {
                module.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    public void StartAllModules()
    {
        foreach (var module in _modules.Values)
        {
            module.Start();
        }
    }

    public List<OpcEntityNode> GetAllNodes()
    {
        return _entities;
    }
    #endregion

    /// <summary>
    /// Create a parent directory (please make sure that the corresponding root directory has been created)
    /// </summary>
    /// <param name="nodes"></param>
    /// <param name="currentNode"></param>
    /// <returns></returns>
    public FolderState GetParentFolderState(IEnumerable<OpcEntityNode> nodes, OpcEntityNode currentNode)
    {
        if (!_folderDic.TryGetValue(currentNode.ParentPath, out FolderState folder))
        {
            var parent = nodes.Where(d => d.NodePath == currentNode.ParentPath).FirstOrDefault();

            if (parent == null)
            {
                parent = _spaceNames[currentNode.ParentPath];
            }

            Console.WriteLine(currentNode.ParentPath);

            if (!string.IsNullOrEmpty(parent.ParentPath))
            {
                var pFol = GetParentFolderState(nodes, parent);
                folder = CreateFolder(pFol, parent.NodePath, parent.NodeName);
                pFol.ClearChangeMasks(SystemContext, false);
                AddPredefinedNode(SystemContext, folder);
                _folderDic.Add(currentNode.ParentPath, folder);
            }
        }

        return folder;
    }

    /// <summary>
    /// Triggered when the client writes a value (bound to the node's write event)
    /// </summary>
    /// <param name="context"></param>
    /// <param name="node"></param>
    /// <param name="indexRange"></param>
    /// <param name="dataEncoding"></param>
    /// <param name="value"></param>
    /// <param name="statusCode"></param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    private ServiceResult OnWriteDataValue(ISystemContext context, NodeState node, NumericRange indexRange,
        QualifiedName dataEncoding,
        ref object value, ref StatusCode statusCode, ref DateTime timestamp)
    {
        BaseDataVariableState variable = node as BaseDataVariableState;
        try
        {
            // Verify data type
            TypeInfo typeInfo = TypeInfo.IsInstanceOfDataType(
                value,
                variable.DataType,
                variable.ValueRank,
                context.NamespaceUris,
                context.TypeTable);

            if (typeInfo == null || typeInfo == TypeInfo.Unknown)
            {
                return StatusCodes.BadTypeMismatch;
            }

            if (typeInfo.BuiltInType == BuiltInType.Double)
            {
                double number = Convert.ToDouble(value);
                value = TypeInfo.Cast(number, typeInfo.BuiltInType);
            }

            return ServiceResult.Good;
        }
        catch (Exception)
        {
            return StatusCodes.BadTypeMismatch;
        }
    }

    /// <summary>
    /// Read historical data
    /// </summary>
    /// <param name="context"></param>
    /// <param name="details"></param>
    /// <param name="timestampsToReturn"></param>
    /// <param name="releaseContinuationPoints"></param>
    /// <param name="nodesToRead"></param>
    /// <param name="results"></param>
    /// <param name="errors"></param>
    public override void HistoryRead(OperationContext context, HistoryReadDetails details,
        TimestampsToReturn timestampsToReturn, bool releaseContinuationPoints,
        IList<HistoryReadValueId> nodesToRead, IList<HistoryReadResult> results, IList<ServiceResult> errors)
    {
        // Suppose the query historical data is all with a time frame
        if (details is not ReadProcessedDetails readDetail || readDetail.StartTime == DateTime.MinValue ||
            readDetail.EndTime == DateTime.MinValue)
        {
            errors[0] = StatusCodes.BadHistoryOperationUnsupported;
            return;
        }

        for (int ii = 0; ii < nodesToRead.Count; ii++)
        {
            int sss = readDetail.StartTime.Millisecond;
            double res = sss + DateTime.Now.Millisecond;
            // The historical data returned here can be of multiple data types,
            // please choose according to the actual business
            Opc.Ua.KeyValuePair keyValue = new()
            {
                Key = new QualifiedName(nodesToRead[ii].NodeId.Identifier.ToString()),
                Value = res
            };
            results[ii] = new HistoryReadResult()
            {
                StatusCode = StatusCodes.Good,
                HistoryData = new ExtensionObject(keyValue)
            };
            errors[ii] = StatusCodes.Good;
            // Remember, if you have already processed the operation of reading historical data,
            // please set Processed to True,
            // so that the OPC-UA library will know that you have processed it and don't need to check it anymore.
            nodesToRead[ii].Processed = true;
        }
    }
}