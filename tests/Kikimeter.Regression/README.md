# Kikimeter regression baseline

Run on Windows with .NET 8 SDK from the repository root:

```powershell
dotnet run --project tests/Kikimeter.Regression/Kikimeter.Regression.csproj
```

Exit code 1 indicates a failing assertion. This dependency-free test runner uses the actual Kikimeter project, not copies of its domain classes. No Wakfu installation, real logs or proprietary data are required. It is deliberately separate from the production solution.

Baseline covers cleanup guards, expired outsiders, main/group preservation, main selection and notification, synchronization and turn damage reset. Exact 30-second boundary tests require an injectable clock. The current comparison is strictly greater than 30 seconds; preserve that behavior during refactoring. Existing group limit logic may retain seven members when the main character is outside the six newest; investigate separately rather than silently change it.

Initial status: source reviewed; execution pending Windows/.NET 8 validation. No production behavior changed.
