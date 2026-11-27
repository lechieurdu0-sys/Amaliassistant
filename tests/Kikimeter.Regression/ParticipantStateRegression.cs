using System;
using System.Linq;
using GameOverlay.Kikimeter.Core;
using GameOverlay.Kikimeter.Models;

internal static class ParticipantStateRegression
{
    public static void Run()
    {
        var parser = new LogParser();
        var rounds = parser.CombatContext.Rounds;
        void Line(string text) => parser.ProcessLogLine("[Information (combat)] " + text);
        void Join(string name, int id, bool ai = false) =>
            parser.ProcessLogLine($"[_FL_] fightId=1 {name} breed : 1 [{id}] isControlledByAI={ai.ToString().ToLowerInvariant()} join the fight at 1");
        void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        parser.ProcessLogLine("CREATION DU COMBAT");
        Join("A", 101); Join("B", 102); Join("Enemy", -1, true);
        Line("A lance le sort Test");
        Line("Enemy: -100 PV (Feu)");
        Line("B lance le sort Test");
        Line("A: -3\u202f082 PV (Feu)");
        Line("A est KO !");
        Line("A est KO !");
        Check(rounds.GetState("A") == RoundParticipantState.KnockedOut &&
              rounds.Anchor == "B" && rounds.ObservedOrder.SequenceEqual(new[] {"A", "B"}),
              "KO lost initiative slot or duplicate altered state");
        Check(parser.PlayerStats["A"].DamageDealt == 100 &&
              parser.PlayerStats["A"].DamageTaken == 3082 && parser.EstimatedRound == 1,
              "KO changed cumulative stats or round");
        Line("Enemy lance le sort Test");
        Line("B lance le sort Topkaj");
        Check(parser.EstimatedRound == 2, "KO anchor blocked round");
        Line("A: est réanimé (Topkaj)");
        Line("A est ressuscité !");
        Check(rounds.GetState("A") == RoundParticipantState.Available &&
              rounds.Anchor == "A" && parser.EstimatedRound == 2 &&
              rounds.ObservedOrder.SequenceEqual(new[] {"A", "B"}),
              "Double revival duplicated slot or incremented round");
        Check(parser.PlayerStats["A"].HealingDone == 0 &&
              parser.PlayerStats["B"].HealingDone == 0,
              "Revival invented a healing amount");
        Line("A lance le sort Test");
        Line("Enemy: -10 PV (Feu)");
        Check(parser.PlayerStats["A"].DamageDealt == 110 && parser.EstimatedRound == 2,
              "Reanimated player failed to resume or advanced round");
        Line("B lance le sort Test");
        Line("A lance le sort Test");
        Check(parser.EstimatedRound == 3, "Original anchor not restored");
        Line("Enemy est hors-combat !");
        Line("Unknown est KO !");
        Line("Unknown est ressuscité !");
        Check(rounds.GetState("Unknown") == null && rounds.GetState("Enemy") == null &&
              parser.PlayerStats.Count == 2, "Nonplayers entered rotation");
        // Defeat message has no name, but only A is KO.
        Line("A est KO !");
        Line("Vous avez été vaincu(e) !");
        Line("Vous avez été vaincu(e) !");
        Line("A est ressuscité !");
        Check(rounds.GetState("A") == RoundParticipantState.Dead &&
              !rounds.ObservedOrder.Contains("A"), "Dead participant restored");
        Check(parser.PlayerStats["A"].DamageDealt == 110 &&
              parser.PlayerStats["A"].DamageTaken == 3082 &&
              rounds.GetState("B") == RoundParticipantState.Available,
              "Defeat modified cumulative stats or the survivor");
        Line("Enemy lance le sort Test");
        Line("B lance le sort Test");
        Line("Enemy: -12 PV (Feu)");
        Check(parser.PlayerStats["B"].DamageDealt == 12, "Personal defeat ended the fight");

        // A second fight: an unnamed defeat is ambiguous when two players are KO.
        parser.ProcessLogLine("CREATION DU COMBAT");
        Join("A", 101); Join("B", 102); Join("C", 103);
        Line("Vous avez été vaincu(e) !");
        Check(rounds.GetState("A") == RoundParticipantState.Available &&
              rounds.GetState("B") == RoundParticipantState.Available,
              "Defeat without a known KO removed a player");
        Line("A lance le sort Test");
        Line("B lance le sort Test");
        Line("C lance le sort Test");
        Line("A est KO !");
        Line("B est KO !");
        Line("Vous avez été vaincu(e) !");
        Check(rounds.GetState("A") == RoundParticipantState.KnockedOut &&
              rounds.GetState("B") == RoundParticipantState.KnockedOut &&
              rounds.GetState("C") == RoundParticipantState.Available,
              "Ambiguous defeat guessed a victim");
    }
}
