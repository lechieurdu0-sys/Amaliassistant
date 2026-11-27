using System;
using System.Linq;
using System.IO;
using GameOverlay.Kikimeter.Services;
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
        config.Forget("Ghost");
        Check(config.ObservePresence("Ghost") && config.Characters.ContainsKey("Ghost") && !config.IsRemoved("Ghost"), "New presence failed to restore character");
        VerifyLiveEvidence();
        var order = PlayerOrderMembership.Order(new[] {"A","B","C"}, new[] {"Ghost","B","A"}, new[] {"Old","C"});
        Check(order.SequenceEqual(new[] {"B","A","C"}), "Stored preferences created a fourth player");
        Check(PlayerOrderMembership.Order(Array.Empty<string>(), new[] {"Ghost"}).Count == 0, "Empty roster invented a player");
        Check(PlayerOrderMembership.Order(new[] {"A","Ghost"}, new[] {"A"}).Contains("Ghost"), "Actual participant hidden by prior preferences");
    }

    private static void VerifyLiveEvidence()
    {
        string dir = Path.Combine(Path.GetTempPath(), "Amaliassistant-presence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string combat = Path.Combine(dir, "wakfu.log");
        string chat = Path.Combine(dir, "wakfu_chat.log");
        string config = Path.Combine(dir, "characters.json");
        const string join = "[_FL_] fightId=10 Ghost breed : 1 [101] isControlledByAI=false join the fight at 1";
        try
        {
            File.WriteAllText(combat, join + "\n");
            File.WriteAllText(chat, "");
            using (var detector = new LootCharacterDetector(chat, combat, config, startPolling: false))
            {
                detector.PollPresence();
                if (detector.GetConfig().Characters.ContainsKey("Ghost")) throw new InvalidOperationException("History replayed");
                File.AppendAllText(combat, join + "\n");
                detector.PollPresence();
                if (!detector.GetConfig().Characters.ContainsKey("Ghost")) throw new InvalidOperationException("New join missed");
                File.AppendAllText(combat, join + "\n");
                detector.RemoveCharacter("Ghost");
                detector.PollPresence();
                if (detector.GetConfig().Characters.ContainsKey("Ghost")) throw new InvalidOperationException("Unread history restored deleted player");
                File.AppendAllText(chat, "[Information (jeu)] Ghost a rejoint le groupe\n");
                detector.PollPresence();
                if (!detector.GetConfig().Characters.ContainsKey("Ghost")) throw new InvalidOperationException("New group join did not restore player");
                detector.RemoveCharacter("Ghost");
                File.AppendAllText(combat, join);
                detector.RemoveCharacter("Ghost");
                File.AppendAllText(combat, "\n");
                detector.PollPresence();
                if (detector.GetConfig().Characters.ContainsKey("Ghost")) throw new InvalidOperationException("Partial old line restored player");
            }
            using (var detector = new LootCharacterDetector(chat, combat, config, startPolling: false))
            {
                detector.PollPresence();
                if (detector.GetConfig().Characters.ContainsKey("Ghost")) throw new InvalidOperationException("Restart restored history");
                File.AppendAllText(combat, join.Replace("fightId=10", "fightId=11") + "\n");
                detector.PollPresence();
                if (!detector.GetConfig().Characters.ContainsKey("Ghost")) throw new InvalidOperationException("Next fight did not restore player");
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
