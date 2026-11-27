using System;
using System.Linq;
using Newtonsoft.Json;
using GameOverlay.Kikimeter.Models;

internal static class CharacterRosterRegression
{
    public static void Run()
    {
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        var config = new LootCharacterConfig();
        config.Characters["Ghost"] = true;
        config.MyCharacters.Add("ghost");
        config.ManualCharacters.Add("GHOST");
        config.Forget("gHoSt");
        config = JsonConvert.DeserializeObject<LootCharacterConfig>(JsonConvert.SerializeObject(config))!;
        config.Normalize();
        Check(config.IsRemoved("Ghost") && config.Characters.Count == 0 &&
            config.MyCharacters.Count == 0 && config.ManualCharacters.Count == 0, "Deleted character restored after reload");
        config.Characters["GHOST"] = true;
        config.Normalize();
        Check(config.Characters.Count == 0, "Old history restored removed character");
        config.Allow("Ghost");
        Check(!config.IsRemoved("Ghost"), "Explicit re-add refused");
        var order = PlayerOrderMembership.Order(new[] {"A","B","C"}, new[] {"Ghost","B","A"}, new[] {"Old","C"});
        Check(order.SequenceEqual(new[] {"B","A","C"}), "Stored preferences created a fourth player");
        Check(PlayerOrderMembership.Order(Array.Empty<string>(), new[] {"Ghost"}).Count == 0, "Empty roster invented a player");
        Check(PlayerOrderMembership.Order(new[] {"A","Ghost"}, new[] {"A"}).Contains("Ghost"), "Actual participant hidden by prior preferences");
    }
}
