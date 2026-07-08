# Contributing to FileHistoryClone

Thank you for considering a contribution! Bug reports, feature requests, translations, documentation fixes, and pull requests are all welcome.

日本語での Issue / PR も歓迎します。

## Getting set up

```powershell
git clone https://github.com/tomoyukioya/FileHistoryClone.git
cd FileHistoryClone
dotnet build FileHistory/FileHistory.sln
dotnet test FileHistoryTests/FileHistoryTests.csproj
```

Requirements: Windows 10/11 and the .NET 8 SDK. Any editor works; the solution file is `FileHistory/FileHistory.sln`.

## Project layout

| Path | What it is |
| --- | --- |
| `FileHistory/` | The main tray application |
| `FileHistory/Crawler.cs` | Periodic low-priority folder scanner |
| `FileHistory/DirectoryWatcher.cs` | Real-time change detection (`FileSystemWatcher`) |
| `FileHistory/BackupScheduler.cs` | Time-ordered backup queues and copy workers |
| `FileHistory/BackupDb.cs` | LiteDB catalog of files/generations |
| `FileHistory/RetentionWorker.cs` | Automatic cleanup by retention policy |
| `FileHistoryTests/` | MSTest unit/integration tests |

## Guidelines

- **Open an issue first** for larger changes, so we can agree on the direction before you invest time.
- **Tests**: run `dotnet test` before submitting. New behavior should come with a test where practical.
- **Warnings**: the build is warning-free; please keep it that way.
- **UI strings**: never hard-code user-visible text. Add a key to `FileHistory/Strings.resx` (English) **and** `FileHistory/Strings.ja.resx` (Japanese), then use `Strings.Get("Key")`.

## Adding a translation

Localization is resource-based and needs no code changes:

1. Copy `FileHistory/Strings.ja.resx` to `FileHistory/Strings.<culture>.resx` (e.g. `Strings.de.resx`).
2. Translate the `<value>` elements.
3. Build — the satellite assembly is generated automatically, and the language is picked up from the OS (or the `Language` setting).

## Reporting bugs

Please use the bug report template. Logs (default location: `%USERPROFILE%\FileHistoryCloneBackup\Log\app.log`) and your `Settings` section from `appsettings.json` (with personal paths masked) make investigation much faster.
