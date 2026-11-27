using System;
using GameOverlay.Kikimeter.Core;

internal static class IndividualPassageRegression
{
    public static void Run()
    {
        var parser = new LogParser();
        void Raw(string line) => parser.ProcessLogLine(line);
        void Line(string message) => Raw("[Information (combat)] " + message);
        void Join(string name, int id, bool ai = false) =>
            Raw($"[_FL_] fightId=1 {name} breed : 1 [{id}] isControlledByAI={ai.ToString().ToLowerInvariant()} obstacleId : -1 join the fight at {{Point3 : (0, 0, 0)}}");
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        Raw("CREATION DU COMBAT");
        Join("Master", 101);
        Join("Ally", 102);
        Join("Silent", 103);
        Join("Monster", -100, true);
        var master = parser.PlayerStats["Master"];
        var ally = parser.PlayerStats["Ally"];
        var silent = parser.PlayerStats["Silent"];

        Line("Master lance le sort Test");
        Line("Monster: -100 PV (Feu)");
        Line("Master: Invoque un(e) PetA");
        Join("PetA", -1, true);
        Line("Master: Invoque un(e) PetB");
        Join("PetB", -2, true);
        Line("39 secondes reportées pour le tour suivant.");
        Line("PetA lance le sort Test");
        Line("Monster: -20 PV (Feu)");
        Line("PetB lance le sort Test");
        Line("Monster: -30 PV (Feu)");
        Check(master.NumberOfTurns == 1 && master.DamageThisTurn == 150 &&
              master.DamageBySummon == 50, "Summons split the master's passage");

        Line("Monster lance le sort Test");
        Line("Master: -7 PV (Terre)");
        Check(master.NumberOfTurns == 1 && master.DamageThisTurn == 150 &&
              master.DamageTaken == 7 && parser.CombatContext.ActivePassageOwner == null,
              "Monster did not close player passage");
        Line("Master lance le sort Test");
        Line("Monster: -40 PV (Feu)");
        Check(master.NumberOfTurns == 2 && master.DamageThisTurn == 40 &&
              master.DamageDealt == 190, "Returning owner did not begin new passage");

        Line("Ally lance le sort Test");
        Line("Monster: -12 PV (Feu)");
        Check(master.NumberOfTurns == 2 && ally.NumberOfTurns == 1 &&
              silent.NumberOfTurns == 0 && master.DamageThisTurn == 40,
              "Another player changed unrelated counters");
        Line("Monster: Enflammé (+1)");
        Line("Monster lance le sort Test");
        Line("Monster: -5 PV (Feu) (Enflammé)");
        Check(ally.DamageDealt == 17 && ally.NumberOfTurns == 1 &&
              parser.CombatContext.ActivePassageOwner == null,
              "Delayed effect opened a passage");
        Raw("[FIGHT] End fight with id 1");
        Check(master.DamageDealt == 190 && ally.DamageDealt == 17 && silent.NumberOfTurns == 0,
              "Combat end modified observed totals");
    }
}
