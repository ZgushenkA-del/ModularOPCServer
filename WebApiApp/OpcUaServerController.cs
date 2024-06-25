using OPCUAServerLibrary;

namespace WebApiApp
{
    public class OpcUaServerController
    {
        private OpcuaManagement _opcManager;

        public OpcUaServerController(IConfigurationRoot conf)
        {
            _opcManager = new OpcuaManagement(conf);
        }

        public IResult GetOpcPort()
        {
            try
            {
                return TypedResults.Ok(_opcManager.OpcuaPort);
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult SetOpcPort(int port)
        {
            try
            {
                _opcManager.OpcuaPort = port;
                return TypedResults.Ok(_opcManager.OpcuaPort);
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StartServer()
        {
            try
            {
                _opcManager.CreateServerInstance();
                return TypedResults.Ok(new { Message = "Server successfully started!" });
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StopServer()
        {
            try
            {
                _opcManager.StopServer();
                return TypedResults.Ok(new { Message = "Server successfully stopped!" });
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult GetModules()
        {
            try
            {
                var modules = NodeManager.GetInstance().GetThreadControllers();
                return Results.Json(modules);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StopThread(string key)
        {
            try
            {
                var threadController = NodeManager.GetInstance().GetThreadController(key);
                threadController.Stop();
                return TypedResults.Json(threadController);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StartThread(string key)
        {
            try
            {
                var threadController = NodeManager.GetInstance().GetThreadController(key);
                threadController.Start();
                return TypedResults.Json(threadController);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult GetSpaceNames()
        {
            try
            {
                var result = NodeManager.GetInstance().GetFolders();
                return TypedResults.Json(result);
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult DeleteSpaceName(string spaceName)
        {
            try
            {
                NodeManager.GetInstance().DeleteSpaceName(spaceName);
                return TypedResults.Json(new { message = $"{spaceName} succesfully deleted!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StopAllModules()
        {
            try
            {
                NodeManager.GetInstance().StopAllModules();
                return TypedResults.Json(new { Message = "All modules stopped succesfully" });
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StartAllModules()
        {
            try
            {
                NodeManager.GetInstance().StartAllModules();
                return TypedResults.Json(new { Message = "All modules started succesfully" });
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StartModuleWatcher()
        {
            try
            {
                NodeManager.GetInstance().StartAllModules();
                return TypedResults.Json(new { Message = "Module watcher started succesfully" });
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult StopModuleWatcher()
        {
            try
            {
                NodeManager.GetInstance().StopModuleWatcher();
                return TypedResults.Json(new { Message = "Module watcher stopped succesfully" });
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult GetNodes()
        {
            try
            {
                return TypedResults.Json(NodeManager.GetInstance().GetAllNodes());
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult GetNode(int id)
        {
            try
            {
                var node = NodeManager.GetInstance().GetAllNodes().Find(d => d.NodeId == id);
                if (node == null)
                {
                    return TypedResults.NotFound($"Node with id {id} not found");
                }
                return TypedResults.Json(node);
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }

        public IResult GetOpcServerStatus()
        {
            try
            {
                return TypedResults.Ok(_opcManager.IsAlive);
            }
            catch (Exception ex)
            {
                return TypedResults.Problem(ex.Message);
            }
        }
    }
}
