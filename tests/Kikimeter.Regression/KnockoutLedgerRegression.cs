using System;
using System.Linq;
using GameOverlay.Kikimeter.Core;
using GameOverlay.Kikimeter.Models;

internal static class KnockoutLedgerRegression
{
    public static void Run()
    {
        var parser = new LogParser();
        var rounds = parser.CombatContext.Rounds;
        var ledger = parser.CombatContext.Knockouts;
        void Raw(string text) => parser.ProcessLogLine(text);
        void Line(string time, string text) => Raw(" INFO " + time + " [AWT-EventQueue-0] (aOr:174) - [Information (combat)] " + text);
        void Join(string name, int id) => Raw($"[_FL_] fightId=1 {name} breed : 1 [{id}] isControlledByAI=false join the fight at 1");
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        Raw("CREATION DU COMBAT");
        Join("A", 101); Join("B", 102); Join("C", 103);
        Line("01:00:01,000", "A lance le sort Test");
        Line("01:00:02,000", "B lance le sort Test");
        Line("01:00:03,000", "C lance le sort Test");
        Line("01:00:04,000", "A est KO !");
        Line("01:00:05,000", "B est KO !");
        Line("01:00:06,000", "C lance le sort Test");
        Line("01:00:07,000", "B: -1 PV (Feu)");
        Line("01:00:08,000", "C lance le sort Test");
        Line("01:00:09,000", "B: -1 PV (Feu)");
        var e = ledger.Snapshot.Single(x => x.Name == "B");
        Check(e.HitCount == 2 && e.KoTime.HasValue && e.LastHitTime.HasValue &&
              e.LastHitSequence > e.KoSequence, "Missing ordered KO evidence");
        Line("01:00:10,000", "Vous avez été vaincu(e) !");
        Check(rounds.GetState("A") == RoundParticipantState.KnockedOut &&
              rounds.GetState("B") == RoundParticipantState.DefeatInferred, "Recent KO impact not resolved");
        Line("01:00:10,001", "Vous avez été vaincu(e) !");
        Check(rounds.GetState("A") == RoundParticipantState.KnockedOut, "Duplicate defeat selected another player");
        int roundBefore = parser.EstimatedRound;
        Line("01:00:11,000", "B: est réanimé (Topkaj)");
        Line("01:00:11,500", "B est ressuscité !");
        Check(rounds.GetState("B") == RoundParticipantState.Available &&
              rounds.ObservedOrder.SequenceEqual(new[] {"A","B","C"}) &&
              ledger.Snapshot.Count == 1 && parser.EstimatedRound == roundBefore,
              "Named revival did not restore initiative slot");
        Line("01:00:12,000", "B est KO !");
        Line("01:00:13,000", "C lance le sort Test");
        Line("01:00:14,000", "A: -1 PV (Feu)");
        Line("01:00:14,100", "B: -1 PV (Feu)");
        Line("01:00:15,000", "Vous avez été vaincu(e) !");
        Check(rounds.GetState("A") == RoundParticipantState.KnockedOut &&
              rounds.GetState("B") == RoundParticipantState.KnockedOut, "Ambiguous AoE guessed death");
        Raw("CREATION DU COMBAT");
        Check(ledger.Snapshot.Count == 0, "KO evidence leaked across fights");
    }
}
