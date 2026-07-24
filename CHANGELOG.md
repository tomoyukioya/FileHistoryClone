# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-07-08

### Added

- **Settings window (GUI)** — choose the backup destination, protected folders, exclusions, timing, and retention without hand-editing `appsettings.json`. Opens from the tray (**Open Settings**) and automatically on first run.

### Fixed

- File-level exclude patterns (e.g. `*.tmp`) were ignored for crawler-scheduled backups; they now apply to both the real-time watcher and the crawler.
- The restore folder picker now starts at the file's (or directory's) original location instead of the default folder.

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

[1.0.1]: https://github.com/tomoyukioya/FileHistoryClone/releases/tag/v1.0.1
[1.0.0]: https://github.com/tomoyukioya/FileHistoryClone/releases/tag/v1.0.0
