using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Documentation;
using System.Reflection;

namespace SomethingNeedDoing.Gui;

public class ChangelogWindow : Window
{
    private readonly Dictionary<string, List<ChangelogClassGroup>> _versionedGroups = [];
    private readonly List<string> _sortedVersions = [];

    public ChangelogWindow() : base($"{P.Name} - Changelog###{P.Name}_{nameof(ChangelogWindow)}", ImGuiWindowFlags.NoScrollbar)
    {
        Size = new(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
        var asm = typeof(ChangelogWindow).Assembly;
        var allTypes = asm.GetTypes();
        var allEntries = allTypes.SelectMany(type =>
            type.GetCustomAttributes<ChangelogAttribute>().Select(attr => new ChangelogEntry(attr, type.Name, type, "Class"))
            .Concat(type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .SelectMany(member => member.GetCustomAttributes<ChangelogAttribute>()
                    .Select(attr => new ChangelogEntry(attr, type.Name, type, member.MemberType.ToString(), member)))))
            .ToList();
        foreach (var versionGroup in allEntries.GroupBy(e => e.Version))
        {
            var classGroups = versionGroup.GroupBy(e => e.ParentClass)
                .Select(classGroup =>
                {
                    var classType = allTypes.FirstOrDefault(t => t.Name == classGroup.Key);
                    if (classType == null) return null;
                    var members = classGroup.Where(e => e.MemberType != "Class").ToList();
                    return new ChangelogClassGroup
                    {
                        ClassName = classGroup.Key,
                        Members = members.GroupBy(e => e.TargetName)
                            .ToDictionary(g => g.Key, g => new ChangelogMemberEntry
                            {
                                Name = g.Key,
                                ReturnType = g.First().MemberInfo is PropertyInfo pi ? pi.PropertyType : g.First().MemberInfo is MethodInfo mi ? mi.ReturnType : null,
                                Entries = [.. g]
                            })
                    };
                })
                .Where(cg => cg != null)
                .ToList()!;
            _versionedGroups[versionGroup.Key] = classGroups;
        }
        _sortedVersions = [.. _versionedGroups.Keys.OrderByDescending(v => v, new VersionComparer())];

        AddGeneralChangelogs();
        var currentVersion = P.Version;
        var anyChanges = _sortedVersions.Count > 0 && _sortedVersions[0] == currentVersion && _versionedGroups[_sortedVersions[0]].Any(cg => cg.Members.Count > 0);
        if (anyChanges && C.LastSeenVersion != P.Version)
            IsOpen = true;
    }

    private void AddGeneralChangelogs()
    {
        Add("12.8", "Fixed recursive spawning of temporary macros caused by function-level trigger events");
        Add("12.9", "Added pause/resume/stop all commands");
        Add("12.10", "Added a lua stub generator (outputs in config directory, used for script linting) by Faye");
        Add("12.10", "Fixed native macro execution where waits weren't being applied properly");
        Add("12.11", "Fixed /loop");
        Add("12.11", "Fixed craft skip trigger on non craft actions");
        Add("12.13", "Added imports for all enums, now callable via luanet.enum");
        Add("12.14", "Added OnCleanup function for lua scripts.");
        Add("12.15", "Added syntax highlighting to the editor");
        Add("12.16", "Added type change ability in the editor");
        Add("12.17", "Fixed Craftloop, and some action skip/waits");
        Add("12.18", "Fixed autoretainer post process event");
        Add("12.19", "Added more advanced stub generator by Faye");
        Add("12.24", "Fixed remote dependencies and added a file cache system");
        Add("12.25", "Fixed deleting folders. Added renaming folders.");
        Add("12.35", "Added a config module to provide macros with configurable settings, editable outside of code");
        Add("12.41", "Added import from url for new macros.");
        Add("12.53", "Added OnActivePluginsChanged");
        Add("12.64", "Changed list index modifier name from listindex to list");
        Add("12.75", "Added duty started/wiped/completed events");
        Add("12.76", "Fixed git macros resetting configs on update");
        Add("13.41", "Fixed how some lists were handled in the configs, as well as string representations of other types");
        Add("13.51", "Fixed ipc enums not being registered");
        Add("13.59", "Added option to auto show status window on script start. Also fixed the carets not showing in the help menu for Axis fonts.");
        Add("14.02", "Added OnChatMessage metadata filters so it's actually usable. See the wiki for usage.");
    }

    private void Add(string version, string description)
    {
        var attr = new ChangelogAttribute(version, ChangelogType.Changed, description);
        var entry = new ChangelogEntry(attr, "General", typeof(ChangelogWindow), "General", null);

        if (!_versionedGroups.TryGetValue(version, out var classGroups))
        {
            classGroups = [];
            _versionedGroups[version] = classGroups;
        }
        var generalGroup = classGroups.FirstOrDefault(g => g.ClassName == "General");
        if (generalGroup == null)
        {
            generalGroup = new ChangelogClassGroup { ClassName = "General" };
            classGroups.Add(generalGroup);
        }
        if (!generalGroup.Members.TryGetValue("General", out var memberEntry))
        {
            memberEntry = new ChangelogMemberEntry { Name = "General" };
            generalGroup.Members["General"] = memberEntry;
        }
        memberEntry.Entries.Add(entry);

        if (!_sortedVersions.Contains(version))
        {
            _sortedVersions.Add(version);
            _sortedVersions.Sort((a, b) => new VersionComparer().Compare(b, a));
        }
    }

    public override void OnOpen()
    {
        C.LastSeenVersion = P.Version;
        C.Save();
        base.OnOpen();
    }

    public override void Draw()
    {
        foreach (var version in _sortedVersions)
        {
            var flags = P.Version == version ? ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.CollapsingHeader : ImGuiTreeNodeFlags.CollapsingHeader;
            using (ImRaii.PushColor(ImGuiCol.Text, EzColor.Green.Vector4, flags.HasFlag(ImGuiTreeNodeFlags.DefaultOpen)))
                if (!ImGui.CollapsingHeader($"Version {version}", flags)) continue;
            var classGroups = _versionedGroups[version];
            var classGroupDict = classGroups.ToDictionary(cg => cg.ClassName, cg => cg);
            var usedAsReturnType = classGroups.SelectMany(cg => cg.Members.Values)
                .Select(m => m.ReturnType?.Name).Where(rt => !string.IsNullOrEmpty(rt)).ToHashSet();
            var rootClassNames = classGroups.Where(cg => !usedAsReturnType.Contains(cg.ClassName) || cg.Members.Count == 0)
                .Select(cg => cg.ClassName).ToList();
            var allReachableTypes = new HashSet<string>();
            foreach (var root in rootClassNames)
            {
                var visited = new HashSet<string>();
                CollectReachableTypes(root, classGroupDict, visited);
                foreach (var t in visited) allReachableTypes.Add(t);
            }
            foreach (var root in rootClassNames)
            {
                if (!classGroupDict.TryGetValue(root, out var classGroup)) continue;
                using var tree = ImRaii.TreeNode(classGroup.ClassName, ImGuiTreeNodeFlags.DefaultOpen);
                if (!tree) continue;
                var visited = new HashSet<string> { classGroup.ClassName };
                foreach (var memberEntry in classGroup.Members.Values)
                    DrawMemberWithReturnTypeRecursive(memberEntry, classGroupDict, visited, allReachableTypes);
            }
            foreach (var classGroup in classGroups)
            {
                if (rootClassNames.Contains(classGroup.ClassName) || allReachableTypes.Contains(classGroup.ClassName) || classGroup.Members.Count == 0) continue;
                using var tree = ImRaii.TreeNode(classGroup.ClassName, ImGuiTreeNodeFlags.DefaultOpen);
                if (!tree) continue;
                var visited = new HashSet<string> { classGroup.ClassName };
                foreach (var memberEntry in classGroup.Members.Values)
                    DrawMemberWithReturnTypeRecursive(memberEntry, classGroupDict, visited, allReachableTypes);
            }
        }
    }

    private static void CollectReachableTypes(string className, Dictionary<string, ChangelogClassGroup> classGroupDict, HashSet<string> visited)
    {
        if (!classGroupDict.TryGetValue(className, out var classGroup) || !visited.Add(className)) return;
        foreach (var member in classGroup.Members.Values)
            if (member.ReturnType is { Name.Length: > 0, Name: var name } && classGroupDict.ContainsKey(name))
                CollectReachableTypes(name, classGroupDict, visited);
    }

    private static void DrawMemberWithReturnTypeRecursive(ChangelogMemberEntry memberEntry, Dictionary<string, ChangelogClassGroup> classGroupDict, HashSet<string> visited, HashSet<string> allReachableTypes)
    {
        var returnType = memberEntry.ReturnType;
        var returnTypeName = returnType?.Name;
        var hasReturnTypeData = returnType != null && classGroupDict.ContainsKey(returnTypeName) && classGroupDict[returnTypeName].Members.Count > 0 && !visited.Contains(returnTypeName);
        var label = memberEntry.Name + (returnType != null ? $" → {LuaTypeConverter.GetLuaType(returnType).TypeName}" : "");

        // I can't wait for something to legitimately be named general and mess this up
        if (memberEntry.Name == "General" && classGroupDict.TryGetValue("General", out var cg) && cg.Members["General"] == memberEntry)
        {
            foreach (var entry in memberEntry.Entries)
                if (!string.IsNullOrEmpty(entry.Description))
                    using (ImRaii.TextWrapPos(0f))
                        ImGui.TextUnformatted($"{entry.Description}");
            return;
        }

        if (hasReturnTypeData)
        {
            using var tree = ImRaii.TreeNode(label, ImGuiTreeNodeFlags.DefaultOpen);
            if (tree)
            {
                foreach (var entry in memberEntry.Entries)
                    if (!string.IsNullOrEmpty(entry.Description))
                        using (ImRaii.TextWrapPos(0f))
                            ImGui.TextUnformatted($"{entry.ChangeType} {entry.Description}");
                visited.Add(returnTypeName!);
                foreach (var subMember in classGroupDict[returnTypeName!].Members.Values)
                    DrawMemberWithReturnTypeRecursive(subMember, classGroupDict, visited, allReachableTypes);
                visited.Remove(returnTypeName!);
            }
        }
        else
        {
            ImGui.BulletText(label);
            foreach (var entry in memberEntry.Entries)
                if (!string.IsNullOrEmpty(entry.Description))
                    using (ImRaii.TextWrapPos(0f))
                        ImGui.TextUnformatted($"{entry.ChangeType} {entry.Description}");
        }
    }

    private class ChangelogClassGroup
    {
        public string ClassName = string.Empty;
        public Dictionary<string, ChangelogMemberEntry> Members = [];
    }

    private class ChangelogMemberEntry
    {
        public string Name = string.Empty;
        public Type? ReturnType;
        public List<ChangelogEntry> Entries = [];
    }

    private class ChangelogEntry(ChangelogAttribute attr, string parentClass, Type declaringType, string memberType, MemberInfo? memberInfo = null)
    {
        public string Version = attr.Version;
        public ChangelogType ChangeType = attr.ChangeType;
        public string ParentClass = parentClass;
        public string TargetName = memberInfo?.Name ?? parentClass;
        public string? Description = attr.Description;
        public string MemberType = memberType;
        public Type? DeclaringType = declaringType;
        public MemberInfo? MemberInfo = memberInfo;
    }

    private class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xParts = x.Split('.');
            var yParts = y.Split('.');

            var maxLength = Math.Max(xParts.Length, yParts.Length);

            for (var i = 0; i < maxLength; i++)
            {
                var xPart = i < xParts.Length ? xParts[i] : "0";
                var yPart = i < yParts.Length ? yParts[i] : "0";

                if (int.TryParse(xPart, out var xNum) && int.TryParse(yPart, out var yNum))
                {
                    if (xNum != yNum)
                        return xNum.CompareTo(yNum);
                }
                else
                {
                    // If parsing fails, fall back to string comparison
                    var stringCompare = string.Compare(xPart, yPart, StringComparison.Ordinal);
                    if (stringCompare != 0)
                        return stringCompare;
                }
            }

            return 0;
        }
    }
}
