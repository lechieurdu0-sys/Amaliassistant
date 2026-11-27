# Test build safety and retired settings

The CI archive carries AMALIASSISTANT_TEST next to its executable. With the marker present, automatic/manual update entry points and Windows Run registry changes through both existing startup controls are disabled. Normal installations are unchanged. This is not a fully isolated portable profile: AppData, browser cookies and configuration remain shared with the existing application, so back up settings and close the installed version first or use a separate Windows account. Never remove the marker from a test archive.

Corrected scope: sale controls and Reset are restored. Reset clears application character data and Kikimeter state, not game logs or loot history. Chat log paths support sales and character discovery. The legacy character discovery service remains for character configuration; it is not loot tracking.

BreedClassMapper now consults the local master projection when present, matching SpellMetadataProvider. No invented class artwork is added: the repository lacks breed 20.png and numeric icons still need visual verification against their class. A future update may supply an authorized icon. No raw data is published in CI.
