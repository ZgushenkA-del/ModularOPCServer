using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ModuleLibrary;

public static class XmlParser
{
    private static EntityNode GetEntityNodeFrom(XElement element)
    {
        Queue<Tuple<XElement, EntityNode>> queue = new();
        EntityNode entity = XElementToEntityNode(element);

        var children = element.Elements("entity");

        foreach (var child in children)
            queue.Enqueue(Tuple.Create(child, entity));

        while (queue.IsFilled)
        {
            var (curElem, parent) = queue.Dequeue();
            EntityNode entityNode = XElementToEntityNode(curElem);

            parent.AddChild(entityNode);

            children = curElem.Elements("entity");

            foreach (var child in children)
                queue.Enqueue(Tuple.Create(child, entityNode));
        }
        return entity;
    }

    public static XmlParserResponse GetResponse(string path)
    {
        var xdoc = XDocument.Load(path);

        var elements = xdoc.Element("entities").Elements("entity");


        var timeStamp = DateTime.Parse(xdoc.Element("entities").Attribute("timestamp").Value);
        var statusCode = StatusCodeHandler.GetStatusCode(xdoc.Element("entities").Attribute("statuscode").Value);

        var entityNodes = elements.Select(GetEntityNodeFrom).ToList();

        return new XmlParserResponse(entityNodes, timeStamp, statusCode, path);
    }

    private static EntityNode XElementToEntityNode(XElement element)
    {
        EntityNode entity = new(element.Attribute("name").Value);
        var valueAttribute = element.Attribute("value");

        if (valueAttribute != null)
        {
            entity.Value = valueAttribute.Value;
            entity.NodeType = NodeStateType.Variable;
            if (element.Attribute("statuscode") is not null)
                entity.StatusCode = StatusCodeHandler.GetStatusCode(element.Attribute("statuscode")?.Value);
            else
                entity.StatusCode = StatusValues.Good;
            if (DateTime.TryParse(element.Attribute("timestamp")?.Value, out DateTime result))
                entity.TimeStamp = result;
        }

        return entity;
    }
}

public class XmlParserResponse
{
    public readonly List<EntityNode> Entities;
    public readonly DateTime Date;
    public readonly StatusValues CodeValue;
    public string FilePath;
    public string GetFileName { get => Path.GetFileNameWithoutExtension(FilePath); }

    public XmlParserResponse(List<EntityNode> entities, DateTime date, StatusValues statusCode, string path)
    {
        Entities = entities;
        Date = date;
        CodeValue = statusCode;
        FilePath = path;
    }

    public SharedData GetSharedData { get => new(Entities, GetFileName, CodeValue, Date); }
}