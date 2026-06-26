namespace SomethingNeedDoing.Core;

/// <summary>
/// Represents a configuration item for a macro.
/// </summary>
public class MacroConfigItem : IEquatable<MacroConfigItem>
{
    /// <summary>
    /// Gets or sets the current value of the config item.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(ConfigValueConverter))]
    public object Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default value of the config item.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(ConfigValueConverter))]
    public object DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the config item.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collapsible section header to group this config under in the settings UI.
    /// </summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the config item.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Type Type { get; set; } = typeof(string);

    /// <summary>
    /// Gets or sets the type name for JSON serialization.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("Type")]
    [Newtonsoft.Json.JsonProperty(nameof(Type))]
    public string TypeName
    {
        get => TypeToString(Type);
        set => Type = ParseTypeFromString(value);
    }

    /// <summary>
    /// Gets or sets the minimum value for numeric types.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(ConfigValueConverter))]
    public object? MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value for numeric types.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(ConfigValueConverter))]
    public object? MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the validation pattern for string types (regex).
    /// </summary>
    public string? ValidationPattern { get; set; }

    /// <summary>
    /// Gets or sets the validation error message.
    /// </summary>
    public string? ValidationMessage { get; set; }

    /// <summary>
    /// Gets or sets the available choices for choice-type config items.
    /// </summary>
    public List<string> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this is a choice list (select from predefined options) or a dynamic list (add/remove items).
    /// Only applies when Type is "list". Defaults to false (dynamic list).
    /// </summary>
    public bool IsChoice { get; set; } = false;

    public bool IsValueDefault()
    {
        if (Value == null && DefaultValue == null)
            return true;
        if (Value == null || DefaultValue == null)
            return false;
        return Type switch
        {
            var t when t == typeof(int) => Convert.ToInt32(Value) == Convert.ToInt32(DefaultValue),
            var t when t == typeof(float) || t == typeof(double) => Math.Abs(Convert.ToSingle(Value) - Convert.ToSingle(DefaultValue)) < 0.0001f,
            var t when t == typeof(bool) => Convert.ToBoolean(Value) == Convert.ToBoolean(DefaultValue),
            var t when t == typeof(List<string>) => IsChoice ? AreChoiceListsEqual(Value, DefaultValue) : AreListsEqual(Value, DefaultValue),
            _ => string.Equals(Value.ToString() ?? "", DefaultValue.ToString() ?? "", StringComparison.Ordinal),
        };
    }

    private static bool AreListsEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        var list1 = ConvertToListOfStrings(value1);
        var list2 = ConvertToListOfStrings(value2);

        return list1.Count == list2.Count && list1.SequenceEqual(list2, StringComparer.Ordinal);
    }

    private static List<string> ConvertToListOfStrings(object? value)
    {
        return value switch
        {
            List<string> stringList => stringList,
            List<object> objectList => [.. objectList.Select(x => x?.ToString() ?? string.Empty)],
            _ => []
        };
    }

    private static bool AreChoiceListsEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        var currentChoice = value1?.ToString() ?? "";
        var defaultChoice = value2?.ToString() ?? "";

        return string.Equals(currentChoice, defaultChoice, StringComparison.Ordinal);
    }

    private static string TypeToString(Type type)
    {
        return type switch
        {
            var t when t == typeof(bool) => "bool",
            var t when t == typeof(int) => "int",
            var t when t == typeof(float) => "float",
            var t when t == typeof(List<string>) => "list",
            _ => "string"
        };
    }

    private static Type ParseTypeFromString(string typeString)
    {
        return typeString.ToLower() switch
        {
            "bool" or "boolean" => typeof(bool),
            "int" or "integer" or "number" => typeof(int),
            "float" or "double" => typeof(float),
            "list" => typeof(List<string>),
            _ => typeof(string)
        };
    }

    public bool Equals(MacroConfigItem? other)
    {
        if (other is null)
            return false;

        return string.Equals(NormalizeObject(DefaultValue), NormalizeObject(other.DefaultValue), StringComparison.Ordinal) &&
               Description == other.Description &&
               Section == other.Section &&
               Type == other.Type &&
               string.Equals(NormalizeObject(MinValue), NormalizeObject(other.MinValue), StringComparison.Ordinal) &&
               string.Equals(NormalizeObject(MaxValue), NormalizeObject(other.MaxValue), StringComparison.Ordinal) &&
               ValidationPattern == other.ValidationPattern &&
               ValidationMessage == other.ValidationMessage &&
               IsChoice == other.IsChoice &&
               Choices.SequenceEqual(other.Choices);
    }

    public override bool Equals(object? obj) => obj is MacroConfigItem other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NormalizeObject(DefaultValue), StringComparer.Ordinal);
        hash.Add(Description, StringComparer.Ordinal);
        hash.Add(Section, StringComparer.Ordinal);
        hash.Add(Type);
        hash.Add(NormalizeObject(MinValue), StringComparer.Ordinal);
        hash.Add(NormalizeObject(MaxValue), StringComparer.Ordinal);
        hash.Add(ValidationPattern, StringComparer.Ordinal);
        hash.Add(ValidationMessage, StringComparer.Ordinal);
        hash.Add(IsChoice);
        foreach (var choice in Choices)
            hash.Add(choice, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static string NormalizeObject(object? value) => value?.ToString() ?? string.Empty;
}

/// <summary>
/// Custom JSON converter for config values to handle different types properly.
/// </summary>
public class ConfigValueConverter : Newtonsoft.Json.JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(object);

    public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case Newtonsoft.Json.JsonToken.String:
                return reader.Value?.ToString() ?? string.Empty;
            case Newtonsoft.Json.JsonToken.Integer:
                return Convert.ToInt32(reader.Value);
            case Newtonsoft.Json.JsonToken.Float:
                return Convert.ToSingle(reader.Value);
            case Newtonsoft.Json.JsonToken.Boolean:
                return Convert.ToBoolean(reader.Value);
            case Newtonsoft.Json.JsonToken.StartArray:
                var list = new List<string>();
                reader.Read();
                while (reader.TokenType != Newtonsoft.Json.JsonToken.EndArray)
                {
                    if (reader.TokenType == Newtonsoft.Json.JsonToken.String)
                        list.Add(reader.Value?.ToString() ?? string.Empty);
                    reader.Read();
                }
                return list;
            case Newtonsoft.Json.JsonToken.Null:
                return string.Empty;
            default:
                return reader.Value?.ToString() ?? string.Empty;
        }
    }

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        switch (value)
        {
            case List<string> list:
                writer.WriteStartArray();
                foreach (var item in list)
                    writer.WriteValue(item);
                writer.WriteEndArray();
                break;
            case string str:
                writer.WriteValue(str);
                break;
            case int i:
                writer.WriteValue(i);
                break;
            case float f:
                writer.WriteValue(f);
                break;
            case double d:
                writer.WriteValue(d);
                break;
            case bool b:
                writer.WriteValue(b);
                break;
            default:
                writer.WriteValue(value.ToString());
                break;
        }
    }
}
