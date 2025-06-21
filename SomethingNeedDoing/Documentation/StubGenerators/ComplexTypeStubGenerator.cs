using SomethingNeedDoing.Documentation.StubGenerators.Builders;
using System.Reflection;

namespace SomethingNeedDoing.Documentation.StubGenerators;

internal sealed class ComplexTypeStubGenerator(Type type) : StubGenerator
{
    // Static list of generated types to prevent infinite recursion
    private static readonly HashSet<Type> GeneratedTypes = [];

    // White list for base namespace component before recursivley including a type
    private readonly HashSet<string> NamespaceWhitelist = [
        "Dalamud",
        "ECommons",
        "FFXIVClientStructs",
        "Lumina"
    ];

    // Blacklist for types to not include, i.e. not including address pointers
    private static readonly HashSet<Type> ExcludedTypes = [
        typeof(IntPtr),
        typeof(UIntPtr),
        typeof(void*),
        typeof(byte*),
        typeof(void),
    ];

    private readonly Type _type = type;

    protected override StubFile GenerateStub()
    {
        GeneratedTypes.Add(_type);

        var file = new StubFile("classes", $"{ToSnakeCase(_type.Name)}.lua");
        var builder = new Builder();

        builder.AddLine($"-- FQN: {_type.FullName}");
        builder.AddLine("");

        builder.AddLine($"--- @class {_type.Name}");

        foreach (var prop in GetAllProperties(_type))
        {
            if (!ShouldIncludeProperty(prop))
                continue;

            var fieldName = prop.Name;
            builder.AddLine($"--- @field {fieldName} {GetPropertyType(prop)}");

            if (prop.PropertyType.IsEnum)
            {
                new EnumStubGenerator(prop.PropertyType).GetStubFile().Write();
            }

            if (!GeneratedTypes.Contains(prop.PropertyType) && !ShouldIgnoreForStub(prop.PropertyType))
            {
                GeneratedTypes.Add(prop.PropertyType); // To prevent infinite recursion
                new ComplexTypeStubGenerator(prop.PropertyType).GetStubFile().Write();
            }
        }

        builder.AddLine("");
        builder.AddLine($"--- @type {_type.Name}");
        builder.AddLine($"--- @as {_type.Name}");
        builder.AddLine($"{_type.Name} = {{}}");

        file.AddBuilder(builder);
        return file;
    }

    private IEnumerable<PropertyInfo> GetAllProperties(Type type)
    {
        if (type.IsInterface)
        {
            return type.GetProperties()
                .Concat(type.GetInterfaces().SelectMany(i => i.GetProperties()))
                .Distinct();
        }
        else
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Static);
        }
    }

    private string GetPropertyType(PropertyInfo prop)
    {
        var type = LuaTypeConverter.GetLuaType(prop.PropertyType).TypeName;

        return type == "object" ? prop.PropertyType.Name : type;
    }

    private bool ShouldIncludeProperty(PropertyInfo prop)
    {
        if (prop.GetIndexParameters().Length > 0)
            return false;

        var type = prop.PropertyType;

        if (ExcludedTypes.Contains(type))
            return false;

        if (type.IsPrimitive || type.IsEnum)
            return true;

        return !ShouldIgnoreForStub(type);
    }

    private bool ShouldIgnoreForStub(Type type)
    {
        if (type.IsPrimitive || type.IsPointer)
            return true;

        if (type.IsGenericType || type.IsGenericTypeDefinition)
            return true;

        var ns = type.Namespace;
        if (string.IsNullOrEmpty(ns))
            return true;

        return !NamespaceWhitelist.Any(allowed => ns.StartsWith(allowed, StringComparison.Ordinal));
    }
}
