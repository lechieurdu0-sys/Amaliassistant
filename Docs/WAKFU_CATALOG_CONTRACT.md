# Wakfu catalog integration — proposed contract v1

Status: proposal, not wired into runtime. Target game version: 1.94. The actual V05 SQLite schema was not available for inspection in this iteration; no table or column names are assumed.

## Boundary

The catalog is optional, local, read-only and separate from live combat logs. Existing Kikimeter calculations remain authoritative for observed combat. Catalog metadata may enrich display or power a separate simulator; it must never silently replace observed damage or infer an unknown action as a known spell.

## Proposed manifest

A local sidecar JSON contains `contractVersion` (integer, initially 1), `gameVersion` (string, exact exporter version), `exporterVersion` (string), `databaseSha256` (64 hexadecimal characters). The application receives the database path separately from the manifest; no path or SQL is taken from manifest content. A checksum detects mismatch, not authenticity.

Reject unsupported contract versions, malformed manifests and mismatched checksums. Treat unavailable or incompatible catalogs as optional-feature failures, not combat failures. Open SQLite in read-only mode, disable extension loading, use parameterized queries and never execute SQL supplied by the catalog. Verify actual schema before implementing queries. Do not download or publish game assets, database files or private logs.

## Adapter requirements

Expose typed lookup results distinguishing found, unknown, unavailable and incompatible. IDs are opaque until their namespace and meaning are verified against the exporter. Preserve game version and source provenance; do not assume spell IDs equal action IDs. Keep localized labels separate from identity. No database I/O on the WPF dispatcher or per combat event; measure lookup latency and memory before choosing cache policy.

## Acceptance tests (pending implementation)

- Missing catalog: existing combat behavior unchanged.
- Invalid JSON, unsupported version, incorrect checksum: catalog rejected without crashing.
- Unknown ID: no fabricated metadata or damage.
- Synthetic catalog: deterministic lookup and locale fallback once schema is agreed.
- Read-only open: no database modification; no private log or raw game data in repository.
- Existing Windows Debug/Release regression suite remains green.

Next: inspect actual V05 export schema and version metadata, then implement manifest validation and a synthetic fixture before choosing the SQLite adapter. This document does not claim compatibility with the existing export.
