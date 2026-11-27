# Scope of the modernisation

The active product is the Kikimeter and existing WebView2 browser. Keep the current themes and Windows compatibility.

Loot tracking is retired, not hidden while continuing to run. The main window must not create LootWindow or SaleTracker, including during startup, path changes and menu actions. Legacy source is retained for history; no loot repair or automatic reactivation is planned. Sales notifications are also outside the active scope. Do not delete the user's historical loot data. The game logs are inputs; combat totals must reflect observed events rather than simulated damage.

The existing plugin/updater and other legacy UI paths are not refactored by this commit. The next integration step is to eliminate remaining combat dependencies on the retired loot subsystem and verify the actual combat event source. The multi-monitor fix uses canvas-local virtual desktop coordinates; monitor hot-plug, mixed DPI, non-rectangular layouts and actual hardware remain to be tested.
