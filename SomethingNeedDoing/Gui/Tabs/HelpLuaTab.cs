using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Documentation;
using System.Reflection;

namespace SomethingNeedDoing.Gui.Tabs;
public class HelpLuaTab(LuaDocumentation luaDocs)
{
    public void DrawTab()
    {
        using var child = ImRaii.Child(nameof(HelpLuaTab));
        ImGuiUtils.Section("Lua Scripting", () => ImGui.TextWrapped($"Below are all of the functions and properties provided by the framework. Click any to copy the full call path to clipboard."));

        foreach (var module in luaDocs.GetModules())
        {
            if (module.Key == "IPC")
            {
                ImGuiUtils.Section(module.Key, () =>
                {
                    var groupedFunctions = module.Value
                        .GroupBy(f => f.ModuleName.Contains('.') ? f.ModuleName.Split('.')[1] : "Root");

                    if (groupedFunctions.FirstOrDefault(g => g.Key == "Root") is { } rootFunctions)
                        rootFunctions.Each(f => DrawFunction(f));

                    foreach (var submodule in groupedFunctions.Where(g => g.Key != "Root"))
                        DrawSubmodule(submodule);
                }, true, UiBuilder.MonoFont);
            }
            else
                ImGuiUtils.Section(module.Key, () => module.Value.EachWithIndex((f, i) => DrawFunction(f, i)), true, UiBuilder.MonoFont);
        }
    }

    private void DrawSubmodule(IGrouping<string, LuaFunctionDoc> submodule)
    {
        ImGuiEx.TextCopy(ImGuiColors.DalamudViolet, submodule.Key, submodule.Key);
        ImGuiUtils.CollapsibleSection(submodule.Key, () => submodule.Each(f => DrawFunction(f)));
        ImGui.Separator();
    }

    private void DrawFunction(LuaFunctionDoc function, int? index = null)
    {
        using var functionId = ImRaii.PushId(function.FunctionName);

        var isMethod = function.IsMethod;
        var isProperty = !isMethod && function.FunctionName.Contains('.');
        var isField = !isMethod && !isProperty;

        var (displaySignature, copySignature) = GetSignatures(function);

        if (isMethod)
            FunctionText(function.FunctionName, function.Parameters, copySignature);
        else
            ImGuiEx.TextCopy(ImGuiColors.DalamudViolet, displaySignature, copySignature);

        if (function.ReturnType.TypeName != "void")
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"→ {function.ReturnType}");
        }

        if (function.ReturnType.Type?.IsEnum == true || function.ReturnType.Type?.IsWrapper() == true ||
            (function.ReturnType is { TypeName: "table", GenericArguments.Count: > 0 } &&
             function.ReturnType.GenericArguments[0].Type?.IsWrapper() == true))
        {
            ImGuiUtils.CollapsibleSection($"{function.FunctionName}_return_{index ?? 0}", () =>
            {
                if (function.ReturnType.Type?.IsEnum == true)
                    DrawEnumValues(function.ReturnType.Type, $"{function.FunctionName}_return_{index ?? 0}");
                else if (function.ReturnType.Type?.IsWrapper() == true)
                    DrawWrapperProperties(function.ReturnType.Type.Name, $"{function.FunctionName}_return_{index ?? 0}", copySignature);
                else if (function.ReturnType is { TypeName: "table", GenericArguments.Count: > 0 })
                {
                    var elementType = function.ReturnType.GenericArguments[0];
                    if (elementType.Type?.IsWrapper() == true)
                        DrawWrapperProperties(elementType.Type.Name, $"{function.FunctionName}_return_{index ?? 0}", copySignature);
                }
            });
        }

        DrawExamples(function.Examples);
        ImGui.Separator();
    }

    private (string DisplaySignature, string CopySignature) GetSignatures(LuaFunctionDoc function)
    {
        var isMethod = function.IsMethod;
        if (!isMethod)
            return (function.FunctionName, $"{function.ModuleName}.{function.FunctionName}");

        return (
            $"{function.FunctionName}({string.Join(", ", function.Parameters.Select(p => $"{p.Name}: {p.Type}"))})",
            $"{function.ModuleName}.{function.FunctionName}({string.Join(", ", function.Parameters.Select(p => p.Name))})"
        );
    }

    private void DrawExamples(string[]? examples)
    {
        if (examples is not { Length: > 0 }) return;

        foreach (var ex in examples)
        {
            if (!string.IsNullOrEmpty(ex))
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Example:");
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, ex);
            }
        }
    }

    private void FunctionText(string functionName, List<(string Name, LuaTypeInfo Type, string? Description)> parameters, string fullChain)
    {
        ImGuiEx.TextCopy(ImGuiColors.DalamudViolet, functionName, fullChain);
        ImGui.SameLine(0, 0);
        ImGui.TextUnformatted("(");
        ImGui.SameLine(0, 0);

        for (var i = 0; i < parameters.Count; i++)
        {
            var (name, type, _) = parameters[i];
            ImGui.TextColored(ImGuiColors.DalamudOrange, type.ToString());
            ImGui.SameLine();
            ImGui.TextUnformatted(name);

            if (i < parameters.Count - 1)
            {
                ImGui.SameLine(0, 0);
                ImGui.Text(", ");
                ImGui.SameLine(0, 0);
            }
        }

        ImGui.SameLine(0, 0);
        ImGui.Text(")");
    }

    private void DrawWrapperProperties(string wrapperTypeName, string id, string parentChain = "")
    {
        var wrapperType = typeof(HelpLuaTab).Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == wrapperTypeName && typeof(IWrapper).IsAssignableFrom(t));

        if (wrapperType == null || !typeof(IWrapper).IsAssignableFrom(wrapperType))
            return;

        var wrapperProperties = wrapperType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttributes(typeof(LuaDocsAttribute), true).Length != 0)
            .ToList();

        var wrapperMethods = wrapperType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes(typeof(LuaDocsAttribute), true).Length != 0)
            .ToList();

        using var _ = ImRaii.PushId(id);

        if (wrapperProperties.Count > 0)
        {
            using var __ = ImRaii.PushIndent();
            foreach (var (prop, index) in wrapperProperties.WithIndex())
            {
                if (prop.Name == "Item" && prop.GetIndexParameters() is { Length: > 0 }) continue;

                var docs = prop.GetCustomAttributes(typeof(LuaDocsAttribute), true).Cast<LuaDocsAttribute>().FirstOrDefault();
                var fullChain = string.IsNullOrEmpty(parentChain) ? prop.Name : $"{parentChain}.{prop.Name}";

                ImGuiEx.TextCopy(ImGuiColors.DalamudOrange, prop.Name, fullChain);
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudGrey, $"→ {LuaTypeConverter.GetLuaType(prop.PropertyType)}");

                if (prop.PropertyType.IsWrapper() || prop.PropertyType.IsList() || prop.PropertyType.IsEnum)
                {
                    ImGuiUtils.CollapsibleSection($"{id}_prop_{index}", () =>
                    {
                        if (prop.PropertyType.IsWrapper())
                            DrawWrapperProperties(prop.PropertyType.Name, $"{id}_prop_{index}", fullChain);
                        else if (prop.PropertyType.IsList())
                        {
                            var elementType = prop.PropertyType.GetGenericArguments()[0];
                            if (elementType.IsWrapper())
                                DrawWrapperProperties(elementType.Name, $"{id}_prop_{index}", fullChain);
                        }
                        else if (prop.PropertyType.IsEnum)
                            DrawEnumValues(prop.PropertyType, $"{id}_prop_{index}");
                    });
                }

                if (docs?.Description != null)
                    ImGui.TextWrapped(docs.Description);
            }
        }

        if (wrapperMethods.Count > 0)
        {
            if (wrapperProperties.Count > 0)
                ImGui.Spacing();

            using var __ = ImRaii.PushIndent();
            foreach (var (method, index) in wrapperMethods.WithIndex())
            {
                var docs = method.GetCustomAttributes(typeof(LuaDocsAttribute), true).Cast<LuaDocsAttribute>().FirstOrDefault();
                var parameters = method.GetParameters().Select(p => (
                    Name: p.Name ?? "unk",
                    TypeInfo: LuaTypeConverter.GetLuaType(p.ParameterType),
                    Description: (string?)null
                )).ToList();
                var copySignature = parameters.Count > 0 ? $"{method.Name}({string.Join(", ", parameters.Select(p => p.TypeInfo.TypeName))})" : method.Name;

                var fullChain = string.IsNullOrEmpty(parentChain) ? copySignature : $"{parentChain}:{copySignature}";
                FunctionText(method.Name, parameters, fullChain);
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudGrey, $"→ {LuaTypeConverter.GetLuaType(method.ReturnType)}");

                if (method.ReturnType.IsWrapper() || method.ReturnType.IsList() || method.ReturnType.IsEnum)
                {
                    ImGuiUtils.CollapsibleSection($"{id}_method_{index}", () =>
                    {
                        if (method.ReturnType.IsWrapper())
                            DrawWrapperProperties(method.ReturnType.Name, $"{id}_method_{index}", fullChain);
                        else if (method.ReturnType.IsList())
                        {
                            var elementType = method.ReturnType.GetGenericArguments()[0];
                            if (elementType.IsWrapper())
                                DrawWrapperProperties(elementType.Name, $"{id}_method_{index}", fullChain);
                        }
                        else if (method.ReturnType.IsEnum)
                            DrawEnumValues(method.ReturnType, $"{id}_method_{index}");
                    });
                }

                if (docs?.Description != null)
                    ImGui.TextWrapped(docs.Description);
            }
        }
    }

    private void DrawEnumValues(Type enumType, string id)
    {
        if (!enumType.IsEnum) return;

        using var _ = ImRaii.PushId(id);
        using var __ = ImRaii.PushIndent();
        foreach (var value in Enum.GetValues(enumType))
        {
            var name = Enum.GetName(enumType, value);
            if (name == null) continue;

            ImGuiEx.TextCopy(ImGuiColors.DalamudOrange, name, name);
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"= {value}");
        }
    }
}
