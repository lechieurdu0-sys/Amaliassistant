using System;
using System.Linq;
using GameOverlay.Kikimeter.Core;
using GameOverlay.Kikimeter.Models;

internal static class CombatRoundRegression
{
    public static void Run()
    {
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        var rounds = new CombatRoundTracker();
        void Add(params string[] names)
        { foreach (var name in names) rounds.RegisterPlayer(name); }
        Add("A", "B", "C");
        rounds.ObserveMonsterAction();
        Check(rounds.Round == 0, "Monster opened a player round");
        rounds.ObservePlayerPassage("A");
        rounds.ObservePlayerPassage("A");
        rounds.ObservePlayerPassage("B");
        Check(rounds.Round == 1, "Another player or repeated action advanced global round");
        rounds.ObserveMonsterAction();
        rounds.ObservePlayerPassage("A");
        Check(rounds.Round == 2, "Skipped C blocked return to anchor");

        rounds.Reset(); Add("A", "B");
        rounds.ObservePlayerPassage("A");
        rounds.SetState("A", RoundParticipantState.Dead);
        rounds.ObservePlayerPassage("B");
        Check(rounds.Round == 1 && rounds.Anchor == "B" && !rounds.ObservedOrder.Contains("A"),
              "Death of anchor advanced round prematurely");
        rounds.ObserveMonsterAction();
        rounds.ObservePlayerPassage("B");
        Check(rounds.Round == 2, "Solo survivor did not advance after monster");

        rounds.Reset(); Add("A", "B");
        rounds.ObservePlayerPassage("A");
        rounds.ObservePlayerPassage("B");
        rounds.SetState("A", RoundParticipantState.KnockedOut);
        rounds.ObserveMonsterAction();
        rounds.ObservePlayerPassage("B");
        Check(rounds.Round == 2 && rounds.Anchor == "B" && rounds.ObservedOrder.Contains("A"),
              "KO did not preserve initiative slot");
        rounds.SetState("A", RoundParticipantState.Available);
        rounds.ObservePlayerPassage("A");
        Check(rounds.Round == 2 && rounds.Anchor == "A",
              "Revival falsely incremented round");
        rounds.ObservePlayerPassage("B");
        rounds.ObservePlayerPassage("A");
        Check(rounds.Round == 3, "Revived anchor failed to resume");

        var parser = new LogParser();
        void Line(string text) => parser.ProcessLogLine("[Information (combat)] " + text);
        void Join(string name, int id, bool ai = false) =>
            parser.ProcessLogLine($"[_FL_] fightId=1 {name} breed : 1 [{id}] isControlledByAI={ai.ToString().ToLowerInvariant()} join the fight at 1");
        parser.ProcessLogLine("CREATION DU COMBAT");
        Join("Master", 101);
        Join("Monster", -99, true);
        Line("Master lance le sort Test");
        Line("Master: Invoque un(e) Pet");
        Join("Pet", -1, true);
        Line("Pet lance le sort Test");
        Line("Monster: -20 PV (Feu)");
        Check(parser.EstimatedRound == 1, "Summon advanced global round");
        Line("Monster lance le sort Test");
        Line("Master: -10 PV (Feu)");
        Line("Master lance le sort Test");
        Check(parser.EstimatedRound == 2, "Actual parser did not count solo round after monster");
        parser.ProcessLogLine("[FIGHT] End fight with id 1");
        Check(parser.EstimatedRound == 2, "End fight lost round count");
        parser.ProcessLogLine("CREATION DU COMBAT");
        Check(parser.EstimatedRound == 0, "New fight retained previous round count");
    }
}
