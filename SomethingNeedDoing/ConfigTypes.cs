using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SomethingNeedDoing.Macros;
using System;
using System.Collections.Generic;

namespace SomethingNeedDoing;

public interface INode
{
    public string Name { get; set; }
}

public enum Language
{
    Native,
    CSharp,
    Lua,
    //Python,
}

public class MacroNode : INode
{
    public string Name { get; set; } = string.Empty;
    public string Gist { get; set; } = string.Empty;
    public string Contents { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this macro should loop automatically.
    /// </summary>
    public bool CraftingLoop { get; set; } = false;
    public bool IsPostProcess { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating how many loops this macro should run if looping is enabled.
    /// </summary>
    public int CraftLoopCount { get; set; } = 0;
    public Language Language { get; set; } = Language.Native;

    public MacroNode() { }
    public MacroNode(MacroFile file)
    {
        Name = file.Name;
        Contents = file.Contents;
        Language = file.Language;
    }

    public void Run() => Service.MacroManager.EnqueueMacro(this);
}

public class FolderNode : INode
{
    public string Name { get; set; } = string.Empty;

    [JsonProperty(ItemConverterType = typeof(ConcreteNodeConverter))]
    public List<INode> Children { get; } = [];
}

/// <summary>
/// Converts INodes to MacroNodes or FolderNodes.
/// </summary>
public class ConcreteNodeConverter : JsonConverter
{
    public override bool CanRead => true;
    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType) => objectType == typeof(INode);

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var jObject = JObject.Load(reader);
        var jType = jObject["$type"]?.Value<string>();

        if (jType == SimpleName(typeof(MacroNode)))
        {
            var obj = new MacroNode();
            serializer.Populate(jObject.CreateReader(), obj);
            return obj;
        }
        else if (jType == SimpleName(typeof(FolderNode)))
        {
            var obj = new FolderNode();
            serializer.Populate(jObject.CreateReader(), obj);
            return obj;
        }
        else
        {
            throw new NotSupportedException($"Node type \"{jType}\" is not supported.");
        }
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => throw new NotImplementedException();
    private string SimpleName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";
}
