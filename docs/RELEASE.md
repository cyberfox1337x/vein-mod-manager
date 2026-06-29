# Release Checklist

Use this checklist before uploading a public build.

## Code

- `dotnet restore tests\VeinModManager.SmokeTests\VeinModManager.SmokeTests.csproj` passes.
- `dotnet build tests\VeinModManager.SmokeTests\VeinModManager.SmokeTests.csproj --no-restore` passes.
- `dotnet publish src\VeinModManager\VeinModManager.csproj -c Release -r win-x64 --self-contained true` passes.
- No private local files are staged.
- No generated build or diagnostic output is staged.

## App

- Auto Detect works on a normal Steam install.
- Config save creates a backup.
- `ui_config.lua` import works through the Settings cog.
- Existing generated edits are preserved after saving one new value.

## Branches

- Feature work merged to `dev`.
- `dev` promoted to `staging` after tests.
- `staging` promoted to `main` only after release rehearsal.

## Package

- Include the published app folder.
- Include bundled `ModTemplate`.
- Include README.
- Do not include private local settings or user-specific config.
