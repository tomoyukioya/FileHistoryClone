# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.0]: https://github.com/tomoyukioya/FileHistoryClone/releases/tag/v1.0.0
