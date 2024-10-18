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
    public unsafe bool IsQuestComplete(ushort id) => QuestManager.IsQuestComplete(id);
    public unsafe byte GetQuestSequence(ushort id) => QuestManager.GetQuestSequence(id);

    public uint? GetQuestIDByName(string name)
    {
        var matchingRows = Svc.Data.GetExcelSheet<Quest>(Svc.ClientState.ClientLanguage)!.Where(q => !string.IsNullOrEmpty(q.Name) && IsMatch(name, q.Name)).ToList();

        if (matchingRows.Count() > 1)
        {
            matchingRows = [.. matchingRows.OrderByDescending(t => MatchingScore(t.Name, name))];
        }
        return matchingRows.FirstOrDefault()?.RowId;
    }

    public unsafe MonsterNoteRankInfo GetMonsterNoteRankInfo(int index) => MonsterNoteManager.Instance()->RankData[index];

    private static bool IsMatch(string x, string y) => Regex.IsMatch(x, $@"{Regex.Escape(y)}\b");
    private static object MatchingScore(string item, string line)
    {
        var score = 0;

        // primitive matching based on how long the string matches. Enough for now but could need expanding later
        if (line.Contains(item))
            score += item.Length;

        return score;
    }
}
