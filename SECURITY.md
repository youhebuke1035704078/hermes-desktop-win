# Security Policy

## Supported versions

Hermes Desktop for Windows is distributed through GitHub Releases. Use the latest release whenever possible.

## Update integrity

Every release includes:

- `HermesDesktop.exe`
- `HermesDesktop.exe.sha256`
- `HermesDesktop-<version>-win-x64.zip`

The in-app updater refuses to install a downloaded executable unless its SHA-256 hash matches `HermesDesktop.exe.sha256`.

## SSH trust model

The app connects directly to remote hosts over SSH. On first connection it stores the host key fingerprint in `%APPDATA%\HermesDesktop\known_hosts.json`; later connections must match that fingerprint.

If a host is rebuilt or its SSH host key intentionally changes, remove the matching entry from `known_hosts.json` and reconnect.

## Reporting issues

Please open a GitHub issue with:

- affected version
- operating system version
- reproduction steps
- relevant logs from `%APPDATA%\HermesDesktop\logs`

Do not paste private SSH keys, passphrases, access tokens, or complete remote session transcripts.
