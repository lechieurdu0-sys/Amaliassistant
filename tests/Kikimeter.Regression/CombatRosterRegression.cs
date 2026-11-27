using System;
using GameOverlay.Kikimeter.Core;
using GameOverlay.Kikimeter.Services;

internal static class CombatRosterRegression
{
    public static void Run()
    {
        var parser = new LogParser();
        var provider = new LogParserPlayerDataProvider(parser);
        int joins = 0, removals = 0;
        bool publishedBeforeEvent = true;
        parser.PlayerAdded += (_, p) => { joins++; publishedBeforeEvent &= provider.GetPlayersInCombat().Contains(p.Name); };
        parser.PlayerRemoved += (_, _) => removals++;
        void Join(string name, int id) =>
            parser.ProcessLogLine($"[_FL_] fightId=123 {name} breed : 1 [{id}] isControlledByAI=false join the fight at 1");
        parser.ProcessLogLine("CREATION DU COMBAT");
        Join("Alpha", 101);
        Join("Beta", 102);
        Join("Alpha", 101);
        if (!provider.IsCombatActive || joins != 2 || !publishedBeforeEvent ||
            !provider.GetPlayersInCombat().SetEquals(new[] { "Alpha", "101", "Beta", "102" }))
            throw new InvalidOperationException("Incorrect first roster");
        parser.ProcessLogLine("[Information (combat)] Alpha lance le sort Test");
        parser.ProcessLogLine("[Information (combat)] Monster: -50 PV (Feu)");
        parser.ProcessLogLine("[FIGHT] End fight");
        if (provider.IsCombatActive || provider.GetPlayersInCombat().Count != 0 ||
            parser.PlayerStats["Alpha"].DamageDealt != 50)
            throw new InvalidOperationException("End fight did not preserve totals");
        parser.ProcessLogLine("CREATION DU COMBAT");
        Join("Alpha", 101);
        if (joins != 3 || !provider.GetPlayersInCombat().SetEquals(new[] { "Alpha", "101" }))
            throw new InvalidOperationException("Previous participant leaked into new fight");
        parser.ProcessLogLine("[Information (combat)] Alpha lance le sort Test");
        if (removals != 1 || provider.GetCurrentPlayers().ContainsKey("Beta"))
            throw new InvalidOperationException("Old participant was not removed");
        if (parser.PlayerStats["Alpha"].DamageDealt != 0)
            throw new InvalidOperationException("New combat did not reset totals");
    }
}
