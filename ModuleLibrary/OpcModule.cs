using System;
using System.Collections.Generic;
using System.Threading;

namespace ModuleLibrary;

public abstract class OpcModule
{
    protected abstract List<EntityNode> DataList { get; set; }
    protected virtual DateTime TimeStamp { get; set; }
    protected virtual StatusValues StatusCode { get; set; }
    public abstract string SpaceName { get; set; }
    public abstract TimeSpan Delay { get; set; }
    //Объект для передачи на сервер
    public virtual SharedData GetSharedData => new(DataList, SpaceName, StatusCode, TimeStamp);
    //Заполнения списка данных
    protected abstract void FillDataList();
    //Цикл работы модуля внутри сервера
    public virtual void LoopSendData(QueueController queue)
    {
        while (true)
        {
            FillDataList();
            PushData(queue);
            Thread.Sleep(Delay);
        }
    }
    //Метод добавляющий данные в очередь сервера на обработку
    protected virtual void PushData(QueueController queue)
    {
        try
        {
            queue.Enqueue(GetSharedData);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}