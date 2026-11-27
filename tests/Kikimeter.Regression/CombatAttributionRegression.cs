using System;
using GameOverlay.Kikimeter.Core;

internal static class CombatAttributionRegression
{
    public static void Run()
    {
        var parser = new LogParser();
        void Line(string line) => parser.ProcessLogLine(line);
        void Join(string name, int id, bool ai = false) =>
            Line($"[_FL_] fightId=123 {name} breed : 1 [{id}] isControlledByAI={ai.ToString().ToLowerInvariant()} join the fight at 1");
        void Combat(string message) => Line("[Information (combat)] " + message);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        Line("CREATION DU COMBAT");
        Join("Alpha", 101);
        Join("Beta", 102);
        Combat("Alpha: -10 PV (Feu)");
        var a = parser.PlayerStats["Alpha"];
        var b = parser.PlayerStats["Beta"];
        Check(a.DamageTaken == 10 && a.DamageDealt == 0, "Unknown caster credited to victim");
        Combat("Alpha lance le sort Test");
        Combat("Monster: -100 PV (Feu)");
        Combat("Monster: Enflammé (+1)");
        Combat("Monster lance le sort Test");
        Combat("Alpha: -20 PV (Feu)");
        Combat("Monster: -30 PV (Feu) (Enflammé)");
        Check(a.DamageTaken == 30 && a.DamageDealt == 130 && parser.PlayerStats.Count == 2, "Monster or delayed effect attribution");
        Combat("OtherMonster: -50 PV (Feu) (Enflammé)");
        Check(a.DamageDealt == 130, "Effect leaked across targets");
        Combat("Alpha: Invoque un(e) Pet");
        Join("Pet", -1, true);
        Combat("Pet lance le sort Test");
        Combat("Monster: -40 PV (Feu)");
        Combat("Beta: +25 PV");
        Combat("Beta: 15 Armure");
        Check(a.DamageDealt == 170 && a.DamageBySummon == 40 && a.HealingDone == 25 && a.ShieldGiven == 15, "Summon ownership not shared by damage/heal/shield");
        Combat("Beta: Invoque un(e) Pet");
        Join("Pet", -2, true);
        Combat("Pet lance le sort Test");
        Combat("Monster: -99 PV (Feu)");
        Check(a.DamageDealt == 170 && b.DamageDealt == 0, "Ambiguous summons credited to a player");
        Check(parser.PlayerStats.Count == 2, "AI added to player roster");
    }
}
