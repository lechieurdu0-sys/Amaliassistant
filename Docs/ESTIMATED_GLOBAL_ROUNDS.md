# Estimated global rounds

The original design uses the order of first observed player actions, not a round marker from the game. A return to the first available player after another player or a known monster acts advances the estimated round. Summons stay in their owner's passage. A skipped player does not block the counter. Monsters provide separation evidence even for the last surviving player, without becoming tracked players.

The tracker has distinct available, knocked-out (temporary) and dead (permanently removed from the estimated order) states. KO preserves the initiative slot; revival restores it without clearing combat statistics. Dead players' statistics remain untouched. When the anchor dies, the next available observed player replaces them; if they have not yet acted in the current round, the counter does not advance.

The supplied logs confirm monster 'est hors-combat !' messages only. They do not establish the player-specific KO, permanent death or revival message formats. Accordingly SetState is tested directly, but the parser does not infer a player's death from this ambiguous message. Real state integration awaits verified log syntax. The counter is exposed as EstimatedRound, not yet displayed in the UI.

This is an estimate, not a reconstruction of the game's initiative engine. The first action by a previously silent player does not reveal their actual initiative slot, and a silent anchor or monsters that never log an action may cause missed rounds. Carryover alone is not a global round boundary. Existing DPT remains the current/last individual passage. No raw logs are committed.
