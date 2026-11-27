using System.Collections.ObjectModel;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Kikimeter.Services;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var cases = new (string Name, Action Run)[]
        {
            ("Active combat preserves players", () => Guard(true, false, false)),
            ("Frozen combat preserves players", () => Guard(false, true, false)),
            ("Reset preserves players", () => Guard(false, false, true)),
            ("Expired outsider removed, group and main preserved", Cleanup),
            ("Main selection is case insensitive", MainSelection),
            ("Sync adds active players only", Sync),
            ("Turn damage reset preserves total", TurnDamage),
            ("Outsider expires strictly after 30 seconds", TimeoutBoundary),
            ("Server change skips cleanup once", ServerCleanup),
            ("Server change skips sync once", ServerSync),
            ("Reset refreshes server baseline", ServerReset),
            ("Catalog manifest preflight", CatalogManifestRegression.Run),
            ("Wakfu catalog authority and ambiguity", WakfuCatalogRegression.Run),
            ("Virtual desktop canvas positioning", OverlayCanvasRegression.Run),
            ("Combat roster comes from log events", CombatRosterRegression.Run),
            ("Combat attribution and ambiguous summons", CombatAttributionRegression.Run),
            ("Incremental log cursor preserves complete lines", LogTailRegression.Run),
            ("Observed French turn boundary and critical spell suffix", ObservedTurnRegression.Run),
            ("Individual passages group summons and distinguish monster actions", IndividualPassageRegression.Run),
            ("Estimated global rounds and participant availability", CombatRoundRegression.Run),
            ("Observed KO and double revival messages", ParticipantStateRegression.Run)
        };
        int failures = 0;
        foreach (var test in cases)
        {
            try { test.Run(); Console.WriteLine("PASS " + test.Name); }
            catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + test.Name + ": " + ex); }
        }
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static PlayerStats Player(string name, long id) => new()
    {
        Name = name, PlayerId = id, IsActive = false,
        LastSeenInCombat = DateTime.Now.AddMinutes(-5)
    };

    private static void Guard(bool active, bool frozen, bool resetting)
    {
        var provider = new Provider { IsCombatActive = active };
        var service = new PlayerManagementService(provider);
        var players = new ObservableCollection<PlayerStats> { Player("Outsider", 1) };
        if (resetting) service.BeginReset();
        service.CleanupPlayersAfterCombat(players, DateTime.Now, freezeAfterCombat: frozen);
        Check(players.Count == 1, "Guard removed a player");
        Check(provider.Reads == 0, "Guard read provider");
    }

    private static void Cleanup()
    {
        var service = new PlayerManagementService(new Provider());
        var main = Player("Main", 1); main.IsMainCharacter = true;
        var group = Player("Group", 2); group.IsInGroup = true;
        var outsider = Player("Outsider", 3);
        var players = new ObservableCollection<PlayerStats> { main, group, outsider };
        service.CleanupPlayersAfterCombat(players, DateTime.Now);
        Check(players.Count == 2 && players.Contains(main) && players.Contains(group), "Unexpected cleanup");
    }

    private static void MainSelection()
    {
        var service = new PlayerManagementService(new Provider());
        var first = Player("First", 1); first.IsMainCharacter = true;
        var second = Player("Second", 2);
        service.SetPlayersCollection(new ObservableCollection<PlayerStats> { first, second });
        PlayerStats? notified = null;
        service.MainCharacterChanged += (_, player) => notified = player;
        service.SetMainCharacter("SECOND");
        Check(!first.IsMainCharacter && second.IsMainCharacter, "Incorrect main flags");
        Check(ReferenceEquals(service.GetMainCharacter(), second) && ReferenceEquals(notified, second), "Incorrect selection or notification");
    }

    private static void Sync()
    {
        var provider = new Provider { IsCombatActive = true };
        provider.Players["1"] = new PlayerData { Id = "1", Name = "Active", IsActive = true, IsInGroup = true };
        provider.Players["2"] = new PlayerData { Id = "2", Name = "Inactive", IsActive = false };
        var service = new PlayerManagementService(provider);
        var players = new ObservableCollection<PlayerStats>();
        service.SyncPlayersWithJson(players);
        Check(players.Count == 1 && players[0].PlayerId == 1 && players[0].IsMainCharacter, "Incorrect sync");
        service.SyncPlayersWithJson(players);
        Check(players.Count == 1, "Duplicate player");
    }

    private static void TimeoutBoundary()
    {
        var clock = new ManualClock();
        var service = new PlayerManagementService(new Provider(), clock);
        var start = clock.GetLocalNow().DateTime;
        var outsider = Player("Outsider", 1);
        outsider.LastSeenInCombat = start;
        var players = new ObservableCollection<PlayerStats> { outsider };
        foreach (var seconds in new[] { 29, 30, 31 })
        {
            clock.Now = clock.Initial.AddSeconds(seconds);
            service.CleanupPlayersAfterCombat(players, start);
            Check(players.Contains(outsider) == (seconds <= 30),
                $"Unexpected expiry at {seconds} seconds");
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Initial { get; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static void TurnDamage()
    {
        var player = new PlayerStats { DamageDealt = 1200, DamageThisTurn = 400 };
        Check(player.DamagePerTurn == 400, "Unexpected current turn damage");
        player.ResetTurnDamage();
        Check(player.DamagePerTurn == 0 && player.DamageDealt == 1200, "Reset changed total damage");
    }

    private static void ServerCleanup()
    {
        var provider = new Provider { Server = "Alpha" };
        var service = new PlayerManagementService(provider);
        service.EndReset();
        var players = new ObservableCollection<PlayerStats> { Player("Outsider", 1) };
        provider.Server = "Beta";
        service.CleanupPlayersAfterCombat(players, DateTime.Now);
        Check(players.Count == 1 && provider.Reads == 0, "Server transition did not skip cleanup");
        service.CleanupPlayersAfterCombat(players, DateTime.Now);
        Check(players.Count == 0 && provider.Reads == 2, "Cleanup did not resume");
    }

    private static void ServerSync()
    {
        var provider = new Provider { Server = "Alpha", IsCombatActive = true };
        var service = new PlayerManagementService(provider);
        service.EndReset();
        provider.Players["1"] = new PlayerData { Id = "1", Name = "Active", IsActive = true };
        var players = new ObservableCollection<PlayerStats>();
        provider.Server = "Beta";
        service.SyncPlayersWithJson(players);
        Check(players.Count == 0 && provider.Reads == 0, "Server transition did not skip sync");
        service.SyncPlayersWithJson(players);
        Check(players.Count == 1 && provider.Reads == 2, "Sync did not resume");
    }

    private static void ServerReset()
    {
        var provider = new Provider { Server = "Alpha", IsCombatActive = true };
        var service = new PlayerManagementService(provider);
        service.EndReset();
        service.BeginReset();
        provider.Server = "Beta";
        var players = new ObservableCollection<PlayerStats>();
        service.SyncPlayersWithJson(players);
        Check(provider.Reads == 0, "Reset did not suspend sync");
        service.EndReset();
        provider.Server = "BETA";
        service.SyncPlayersWithJson(players);
        Check(provider.Reads == 2, "Reset baseline or case-insensitive comparison failed");
    }

    private sealed class Provider : IPlayerDataProvider
    {
        public Dictionary<string, PlayerData> Players { get; } = new();
        public bool IsCombatActive { get; set; }
        public int Reads { get; private set; }
        public string? Server { get; set; }
        public string? GetCurrentServerName() => Server;
        public Dictionary<string, PlayerData> GetCurrentPlayers() { Reads++; return Players; }
        public HashSet<string> GetPlayersInCombat() { Reads++; return new(); }
    }
}
