# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-07-24

### Added

- **Settings window (GUI)** — choose the backup destination, protected folders, exclusions, timing, and retention without hand-editing `appsettings.json`. Opens from the tray (**Open Settings**) and automatically on first run.
- New advanced settings in `appsettings.json` (no GUI): `RetentionStartupDelay`, `MaxLowPrioritySchedules`, `MaxCopyWorkers`.

### Changed

- **Backup folder names** — `Configuration` → `Database`, `Data` → `BackupFiles` under `{BackupBaseDir}\{User}\{Machine}\`. Existing backups are migrated automatically (one-time rename at startup).
- **Retention policy is now applied immediately** — when a new backup is saved, generations beyond `MaxGenerations` for that file are pruned right away; the periodic scan now mainly handles `RetentionDays` and files that no longer change.
- **Backup cleanup** moved from a main-window button to **Tools → Clean Up Backups**.
- Crawl tuning (`CrawlingIdleTimer`, `CrawlingInterval`) removed from the settings window; still configurable in `appsettings.json`. `CrawlingIdleTimer` default lowered from 600 to 60 seconds, and `CrawlingInterval` is documented as the full-crawl cycle (start-to-start, default 1 day).

### Fixed

- File-level exclude patterns (e.g. `*.tmp`) were ignored for crawler-scheduled backups; they now apply to both the real-time watcher and the crawler.
- The restore folder picker now starts at the file's (or directory's) original location instead of the default folder.
- The installer could hang forever on systems where the WMI service is not running (it used `taskkill` to stop a running instance; now uses PowerShell `Stop-Process`).

## [1.0.0] - 2026-07-08

Initial public release.

### Features

- **Continuous backup** — real-time change detection (`FileSystemWatcher`) plus a low-priority crawler that runs while the PC is idle.
- **Versioned, plain-file backups** — every generation is an ordinary timestamped file you can open in Explorer; no proprietary container.
- **Restore window** — browse generations in a tree, restore files or whole directories, or open any version as a temporary copy to preview it.
- **Retention policy** — optional automatic cleanup by max generations per file and/or age in days (the newest is always kept).
- **Flexible filtering** — per-directory backup intervals, glob exclude patterns, re-include exceptions (`!important.log`), and environment-variable expansion in all paths.
- **Localized UI** — English and Japanese, following the OS language.
- **Windows installer** — per-user (no admin) with an optional "start at sign-in" option, plus portable zips (framework-dependent and self-contained).

[1.1.0]: https://github.com/tomoyukioya/FileHistoryClone/releases/tag/v1.1.0
[1.0.0]: https://github.com/tomoyukioya/FileHistoryClone/releases/tag/v1.0.0
