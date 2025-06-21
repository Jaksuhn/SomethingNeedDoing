using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Documentation;
using SomethingNeedDoing.Documentation.StubGenerators;
using System.IO;
using System.Reflection;

namespace SomethingNeedDoing.Services;

public class StubGeneratorService
{
    public StubGeneratorService(LuaDocumentation luaDocs)
    {
        CleanUp();

        Svc.Log.Debug("Generating stubs");

        Svc.Log.Debug("Getting refernced types from lua documentation");
        var registry = GetAllReferencedTypes(luaDocs);

        // Global stubs
        Svc.Log.Debug("Adding global helper stub");
        new GlobalStubGenerator().GetStubFile().Write();

        // Generate enum stubs
        Svc.Log.Debug("Adding referenced enum stubs");
        foreach (var enumType in registry.Where(t => t.IsEnum))
        {
            if (enumType == null)
            {
                continue;
            }

            new EnumStubGenerator(enumType).GetStubFile().Write();
        }

        // Generate wrapper stubs
        Svc.Log.Debug("Adding referenced wrapper stubs");
        foreach (var wrapperType in registry.Where(t => t.IsWrapper()))
        {
            if (wrapperType == null)
            {
                continue;
            }

            new WrapperStubGenerator(wrapperType).GetStubFile().Write();
        }

        // Generate module stubs
        Svc.Log.Debug("Adding module stubs");
        foreach (var module in luaDocs.GetModules())
        {
            if (module.Key == "IPC")
                continue;

            new ModuleStubGenerator(module.Key, module.Value).GetStubFile().Write();
        }

        // Generate IPC module stubs
        Svc.Log.Debug("Adding IPC module stubs");
        new IpcStubGenerator(luaDocs).GetStubFile().Write();

        // Expose Vector type stubs
        Svc.Log.Debug("Adding vector stubs");
        new ClassStubGenerator(typeof(Vector2)).WithDocumentationLine("requires the import of \"System.Numerics\"").GetStubFile().Write();
        new ClassStubGenerator(typeof(Vector3)).WithDocumentationLine("requires the import of \"System.Numerics\"").GetStubFile().Write();
        new ClassStubGenerator(typeof(Vector4)).WithDocumentationLine("requires the import of \"System.Numerics\"").GetStubFile().Write();

        // Complex but well used class stubs
        Svc.Log.Debug("Adding Svc stubs");
        new ComplexTypeStubGenerator(typeof(Svc)).GetStubFile().Write();
    }

    private void CleanUp()
    {
        var configDir = Svc.PluginInterface.GetPluginConfigDirectory();

        var stubsDir = Path.Combine(configDir, "stubs");
        var legacyFile = Path.Combine(configDir, "snd-stubs.lua");

        if (Directory.Exists(stubsDir))
        {
            try
            {
                Directory.Delete(stubsDir, recursive: true);
                Svc.Log.Debug($"Deleted stub directory: {stubsDir}");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Failed to delete stub directory '{stubsDir}': {ex}");
            }
        }

        if (File.Exists(legacyFile))
        {
            try
            {
                File.Delete(legacyFile);
                Svc.Log.Debug($"Deleted legacy stub file: {legacyFile}");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Failed to delete legacy stub file '{legacyFile}': {ex}");
            }
        }
    }

    private IEnumerable<Type> GetAllNestedTypes(Type type)
    {
        var stack = new Stack<Type>();
        var visited = new HashSet<Type>();

        stack.Push(type);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == null || !visited.Add(current))
                continue;

            yield return current;

            if (current.IsGenericType)
                foreach (var arg in current.GetGenericArguments())
                    stack.Push(arg);

            if (current.IsArray)
                stack.Push(current.GetElementType()!);

            if (Nullable.GetUnderlyingType(current) is Type nullableType)
                stack.Push(nullableType);

            if (current.BaseType != null && current.BaseType != typeof(object))
                stack.Push(current.BaseType);
        }
    }

    private IEnumerable<Type> GetAllReferencedTypes(LuaDocumentation luaDocs)
    {
        var seen = new HashSet<Type>();

        void AddType(Type? type)
        {
            if (type == null) return;

            foreach (var nested in GetAllNestedTypes(type))
            {
                if (seen.Add(nested))
                {
                    if (typeof(IWrapper).IsAssignableFrom(nested))
                    {
                        foreach (var prop in nested.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            if (prop.GetCustomAttribute<LuaDocsAttribute>() != null)
                                AddType(prop.PropertyType);

                        foreach (var method in nested.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (method.GetCustomAttribute<LuaDocsAttribute>() == null)
                                continue;

                            AddType(method.ReturnType);
                            foreach (var param in method.GetParameters())
                                AddType(param.ParameterType);
                        }
                    }
                }
            }
        }

        foreach (var module in luaDocs.GetModules())
        {
            foreach (var doc in module.Value)
            {
                AddType(doc.ReturnType.Type);

                if (doc.Parameters != null)
                    foreach (var param in doc.Parameters)
                        AddType(param.Type.Type);
            }
        }

        return seen;
    }
}
