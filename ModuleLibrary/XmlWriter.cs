using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace ModuleLibrary;

public static class XmlWriter
{
    public static XDocument CreateXmlFile(List<EntityNode> entities, DateTime timeStamp, StatusValues statusCode)
    {
        var xDoc = new XDocument();
        var xLocation = new XElement("entities");

        var xStatusCode = new XElement("statuscode");
        var xValue = new XAttribute("status", statusCode);
        xStatusCode.Add(xValue);
        xLocation.Add(xStatusCode);

        var xTimeStamp = new XElement("timestamp");
        xValue = new XAttribute("time", timeStamp);
        xTimeStamp.Add(xValue);
        xLocation.Add(xTimeStamp);

        foreach (var item in entities)
        {
            AddEntityNodeToXelement(xLocation, item);
        }
        xDoc.Add(xLocation);
        return xDoc;
    }

    public static XDocument CreateXmlFile(XmlParserResponse data)
    {
        var xDoc = new XDocument();
        var xLocation = new XElement("entities");

        var xStatusCode = new XElement("statuscode");
        var xValue = new XAttribute("status", data.CodeValue);
        xStatusCode.Add(xValue);
        xLocation.Add(xStatusCode);

        var xTimeStamp = new XElement("timestamp");
        xValue = new XAttribute("time", data.Date);
        xTimeStamp.Add(xValue);
        xLocation.Add(xTimeStamp);

        foreach (var entity in data.Entities)
        {
            AddEntityNodeToXelement(xLocation, entity);
        }

        xDoc.Add(xLocation);
        return xDoc;
    }

    private static void AddEntityNodeToXelement(XElement xElement, EntityNode entityNode)
    {
        Queue<Tuple<EntityNode, XElement>> entityQueue = new();
        entityQueue.Enqueue(Tuple.Create(entityNode, xElement));

        while (entityQueue.IsFilled)
        {
            var (node, xloc) = entityQueue.Dequeue();
            XElement currentXelement = new("entity");
            currentXelement.Add(new XAttribute("name", node.Name));
            if (node.NodeType is NodeStateType.Variable)
            {
                currentXelement.Add(new XAttribute("value", node.Value));
                currentXelement.Add(new XAttribute("timestamp", node.TimeStamp.ToString()));
                currentXelement.Add(new XAttribute("statuscode", node.StatusCode.ToString()));
            }

            xloc.Add(currentXelement);
            foreach (var item in node.Children)
                entityQueue.Enqueue(Tuple.Create(item, currentXelement));
        }
    }
}
