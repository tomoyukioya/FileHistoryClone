# Security Policy

FileHistoryClone handles your personal files, so we take security reports seriously.

## Supported versions

Only the latest release receives security fixes.

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

Instead, use GitHub's private vulnerability reporting: go to the repository's **Security** tab → **Report a vulnerability**. You should receive an initial response within 7 days.

When reporting, please include:

- A description of the issue and its impact
- Steps to reproduce (a minimal `appsettings.json` helps)
- The version you tested

## Scope notes

- FileHistoryClone is a local application: it makes no network connections and collects no telemetry. Reports about data leaving the machine are therefore of particular interest.
- Backups are stored **unencrypted** by design (plain file copies). The confidentiality of the backup destination is the user's responsibility; this is documented behavior, not a vulnerability.
