using Opc.Ua;
using Opc.Ua.Configuration;
using System;

namespace OPCUAServerLibrary;

public class OpcuaManagement
{
    public OpcuaServer _server = new();
    private string _opcuaPort;
    private string _opcuaHost;
    private string _opcuaName;
    private ApplicationInstance _appInstance;
    public bool IsAlive { get => _appInstance != null; }

    public int OpcuaPort
    {
        get => int.Parse(_opcuaPort);
        set
        {
            if (IsAlive)
            {
                throw new InvalidOperationException("Server already working!");
            }
            if (!(value < 1 || value > 65535))
            {
                _opcuaPort = value.ToString();
            }
            else
            {
                throw new ArgumentException("Порт должен находиться в пределах от 1 до 65535");
            }
        }
    }

    public OpcuaManagement(Microsoft.Extensions.Configuration.IConfigurationRoot configuration)
    {
        _opcuaPort = configuration["opcSettings:OpcPort"];
        _opcuaHost = configuration["opcSettings:HttpsPort"];
        _opcuaName = configuration["opcSettings:ApplicationUrn"];
    }

    public void CreateServerInstance()
    {
        try
        {
            if (_appInstance == null)
            {
                _server = new OpcuaServer();
                var serverSecurityNone = new ServerSecurityPolicy
                {
                    SecurityMode = MessageSecurityMode.None,
                    SecurityPolicyUri = SecurityPolicies.None
                };

                ServerSecurityPolicy serverSecurityStandard = new();

                var config = new ApplicationConfiguration()
                {
                    ApplicationName = _opcuaName,
                    ApplicationUri = Utils.Format(@$"urn:{System.Net.Dns.GetHostName()}:{_opcuaName}"),
                    ApplicationType = ApplicationType.Server,
                    ServerConfiguration = new ServerConfiguration()
                    {
                        BaseAddresses = { $"opc.tcp://localhost:{_opcuaPort}", $"https://localhost:{_opcuaHost}" },
                        MinRequestThreadCount = 5,
                        MaxRequestThreadCount = 100,
                        MaxQueuedRequestCount = 200,
                        SecurityPolicies = new([serverSecurityNone, serverSecurityStandard])
                    },
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = Utils.Format(@"CN={0}, DC={1}", _opcuaName, System.Net.Dns.GetHostName()) },
                        TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                        TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                        RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                        AutoAcceptUntrustedCertificates = true,
                        AddAppCertToTrustedStore = true
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                    TraceConfiguration = new TraceConfiguration()
                };

                config.Validate(ApplicationType.Server).GetAwaiter().GetResult();
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = e.Error.StatusCode == StatusCodes.BadCertificateUntrusted; };
                }

                _appInstance = new ApplicationInstance
                {
                    ApplicationName = _opcuaName,
                    ApplicationType = ApplicationType.Server,
                    ApplicationConfiguration = config
                };
                //application.CheckApplicationInstanceCertificate(false, 2048).GetAwaiter().GetResult();
                bool certOk = _appInstance.CheckApplicationInstanceCertificate(false, 2048).GetAwaiter().GetResult();
                if (!certOk)
                {
                    Console.WriteLine("Ошибка проверки сертификата!");
                }

                var dis = new DiscoveryServerBase();
                // start the server
                _appInstance.Start(_server).Wait();
            }
            else
            {
                throw new Exception("Server already started!");
            }

        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Запуск сервера OPC-UA вызывает исключение:" + ex.Message);
            Console.ResetColor();
            throw;
        }
    }

    public void StopServer()
    {
        try
        {
            if (_appInstance != null)
            {
                _appInstance.Stop();
                _appInstance = null;
                _server.Dispose();
                _server = null;
                Console.WriteLine("OPCua сервер остановлен");
            }
            else
            {
                Console.WriteLine("_appInstance is null");
                throw new Exception("_appInstance is null");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Остановка сервера вызывает исключение" + ex.Message);
            Console.ResetColor();
            throw;
        }
    }
}
