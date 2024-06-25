using ModuleLibrary;
using System;
using System.IO;
using System.Threading;

namespace OPCUAServerLibrary;

public class ThreadController
{
    private readonly OpcModule _module;
    private readonly QueueController _queueController;
    private readonly string _path;
    private Thread _controlledThread;
    public string SpaceName { get => _module.SpaceName; set => _module.SpaceName = value; }
    public TimeSpan Delay { get => _module.Delay; set => _module.Delay = value; }
    public bool IsAlive { get => _controlledThread.IsAlive; }
    public string ModulePath => _path;

    public ThreadController(OpcModule module, QueueController queueController, string path)
    {
        _module = module;
        _queueController = queueController;
        _path = path;
        _controlledThread = new Thread(MainThread);
    }

    private void MainThread()
    {
        try
        {
            _module.LoopSendData(_queueController);
        }
        catch (ThreadInterruptedException)
        {
            Console.WriteLine($"{SpaceName} Interrupted");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public void Start()
    {
        try
        {
            if (!_controlledThread.IsAlive)
            {
                _controlledThread = new Thread(MainThread);
                _controlledThread.Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при старте модуля {Path.GetFileName(_path)}: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        if (IsAlive)
        {
            _controlledThread.Interrupt();
        }
        else
        {
            throw new Exception("Already dead!");
        }
    }
}
