using System;
using GameOverlay.Kikimeter.Core;

internal static class ObservedTurnRegression
{
    public static void Run()
    {
        // Synthetic identities and amounts; syntax verified against local French Wakfu logs.
        var parser = new LogParser();
        void Line(string message) => parser.ProcessLogLine("[Information (combat)] " + message);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        parser.ProcessLogLine("CREATION DU COMBAT");
        parser.ProcessLogLine("[_FL_] fightId=1 Player breed : 20 [101] isControlledByAI=false obstacleId : -1 join the fight at {Point3 : (0, 0, 0)}");
        var player = parser.PlayerStats["Player"];
        Line("Player lance le sort Candela (Critiques)");
        Check(parser.CombatContext.CurrentSpell == "Candela", "Critical annotation polluted catalog lookup");
        Line("Monster: -10 PV (Feu)");
        Line("Player lance le sort Candela");
        Line("Monster: -20 PV (Feu)");
        Check(player.NumberOfTurns == 1 && player.DamageThisTurn == 30, "Repeated spell counted as a new turn");
        Line("39 secondes reportées pour le tour suivant.");
        Line("Player: 1 PW");
        Line("Player: Applique l'aura Bellaura Pourpre");
        Line("Player: -3 PV (Eau)");
        Check(player.DamageDealt == 30 && player.DamageTaken == 3, "Stale caster after turn boundary");
        Line("Player lance le sort Drosera");
        Line("Monster: -7 PV (Lumière) (Feu)");
        Check(player.NumberOfTurns == 2 && player.DamageThisTurn == 7 && player.DamageDealt == 37, "Consecutive solo turns merged");
        parser.ProcessLogLine("[FIGHT] End fight with id 1");
        Line("Combat terminé, cliquez ici pour rouvrir l'écran de fin de combat.");
        Check(player.DamageDealt == 37, "Repeated end marker changed totals");
    }
}
