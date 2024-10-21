using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.Misc.Commands;

internal class QuestCommands
{
    internal static QuestCommands Instance { get; } = new();

    public List<string> ListAllFunctions()
    {
        var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var list = new List<string>();
        foreach (var method in methods.Where(x => x.Name != nameof(ListAllFunctions) && x.DeclaringType != typeof(object)))
        {
            var parameterList = method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}{(p.IsOptional ? " = " + (p.DefaultValue ?? "null") : "")}");
            list.Add($"{method.ReturnType.Name} {method.Name}({string.Join(", ", parameterList)})");
        }
        return list;
    }

    private static readonly Dictionary<uint, Quest>? QuestSheet = Svc.Data?.GetExcelSheet<Quest>()?.Where(x => x.Id.RawString.Length > 0).ToDictionary(i => i.RowId, i => i);
    public static string GetQuestNameByID(ushort id)
    {
        if (id > 0)
        {
            var digits = id.ToString().Length;
            if (QuestSheet!.Any(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id))
            {
                return QuestSheet!.First(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id).Value.Name.RawString.Replace("", "").Trim();
            }
        }
        return "";
    }

    public unsafe bool IsQuestAccepted(ushort id) => QuestManager.Instance()->IsQuestAccepted(id);
    public unsafe List<uint> GetAcceptedQuests() => Svc.Data.GetExcelSheet<Quest>(Svc.ClientState.ClientLanguage)!.Where(x => IsQuestAccepted((ushort)x.RowId)).Select(x => x.RowId).ToList();
    public unsafe bool IsQuestComplete(ushort id) => QuestManager.IsQuestComplete(id);
    public unsafe byte GetQuestSequence(ushort id) => QuestManager.GetQuestSequence(id);

    private readonly List<SeString> questNames = Svc.Data.GetExcelSheet<Quest>(Svc.ClientState.ClientLanguage)!.Select(x => x.Name).ToList();
    public uint? GetQuestIDByName(string name)
    {
        var matchingRows = questNames.Select((n, i) => (n, i)).Where(t => !string.IsNullOrEmpty(t.n) && IsMatch(name, t.n)).ToList();
        if (matchingRows.Count > 1)
        {
            matchingRows = [.. matchingRows.OrderByDescending(t => MatchingScore(t.n, name))];
        }
        return matchingRows.Count > 0 ? Svc.Data.GetExcelSheet<Quest>(Svc.ClientState.ClientLanguage)!.GetRow((uint)matchingRows.First().i)!.RowId : null;
    }

    public string GetQuestAlliedSociety(uint id) => Svc.Data.GetExcelSheet<Quest>(Svc.ClientState.ClientLanguage)!.FirstOrDefault(x => x.RowId == id)?.BeastTribe.Value?.Name ?? "";

    public unsafe MonsterNoteRankInfo GetMonsterNoteRankInfo(int index) => MonsterNoteManager.Instance()->RankData[index];

    private static bool IsMatch(string x, string y) => Regex.IsMatch(x, $@"\b{Regex.Escape(y)}\b");
    private static object MatchingScore(string item, string line)
    {
        var score = 0;

        // primitive matching based on how long the string matches. Enough for now but could need expanding later
        if (line.Contains(item))
            score += item.Length;

        return score;
    }
}
