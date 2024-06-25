using Microsoft.Extensions.Configuration;
using System;
using System.Threading;

namespace OPCUAServerLibrary
{
    public static class EntryPoint
    {
        public static void StartServer()
        {
            var builder = new ConfigurationBuilder().AddJsonFile("appSettings.json", false, true);
            var config = builder.Build();
            OpcuaManagement server = new(config);
            server.CreateServerInstance();
            Console.WriteLine("OpcUa server started . . .");
            while (true)
            {
                Thread.Sleep(100000);
            }
        }
    }
}
