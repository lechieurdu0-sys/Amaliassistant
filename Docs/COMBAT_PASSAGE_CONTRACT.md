# Combat terminology and attribution contract

- A global round (tour de table) is one complete rotation of combatants. A player's individual passage is not a global round.
- An individual passage includes the master and their following summons. Repeated casts and those summons must not reset the master's DPT.
- An observed action by a different player owner or a known unowned AI closes the previous player passage. A delayed effect changes totals only, not the passage.
- The French carryover-seconds message is an individual boundary hint, not a global round marker. Following summons remain in the master's passage. A later direct cast by the same master can establish another observed passage.
- Existing NumberOfTurns is a legacy property name for the number of observed individual passages. DPT is the damage attributed to the current/last observed passage, not a total divided by a global round count.
- No reliable global round number or complete initiative sequence has been established in the supplied logs. Silent/skip-only participants may have no observed action. Do not infer exact global rounds from the number of players who cast. Unnamed carryover events cannot reliably identify a skipped player's passage.
- Current boundary detection uses spell casts, not every possible movement, passive activation, or monster action. Unknown/ambiguous actors are not promoted into a player or used as proof of a monster boundary. This avoids fabricated statistics while retaining known limits.
- Raw user logs remain local; the regression fixtures are synthetic.
