# Scope of the modernisation

Active features: Kikimeter, existing WebView2 browser, sale notifications (sound, volume, position, and configured chat log), character settings and recovery/reset controls. Preserve themes and Windows compatibility.

Only loot/butin tracking is retired. No LootWindow or LootTracker may be created by the active app. Sale notifications are independent from loot and remain active. Never delete historical loot data or truncate game logs. Reset clears application character preferences, order and combat display, then resumes reading at the current end of the logs. It intentionally does not reconstruct the combat already underway. Real Windows behavior, multi-monitor gaps and DPI, and actual sale syntax remain to be validated.