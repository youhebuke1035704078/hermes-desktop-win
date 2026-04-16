# Architecture

This document covers the internal architecture of Hermes Desktop for Windows.

---

## Layered design

```
┌─────────────────────────────────────────────┐
│  Views (XAML)                               │
│  MainWindow, ConnectionManagerView, ...     │
├─────────────────────────────────────────────┤
│  ViewModels (CommunityToolkit.Mvvm)         │
│  MainViewModel, SessionBrowserViewModel, ...│
├─────────────────────────────────────────────┤
│  Services                                   │
│  SshTransport, RemotePythonScriptExecutor,  │
│  FileEditorService, SessionBrowserService...│
├─────────────────────────────────────────────┤
│  SSH.NET                                    │
│  SshConnectionPool, SshClient, ShellStream  │
├─────────────────────────────────────────────┤
│  Remote Host                                │
│  python3 executing embedded scripts         │
│  ~/.hermes/state.db, SKILL.md, USER.md, ... │
└─────────────────────────────────────────────┘
```

---

## Dependency injection

All services and view models are wired in `App.xaml.cs` via `Microsoft.Extensions.DependencyInjection`.

**Singletons** (one instance for the app's lifetime):
- `SshConnectionPool` &mdash; manages SSH connections
- `ISshTransport` &rarr; `SshTransport` &mdash; command execution
- `IRemoteScriptExecutor` &rarr; `RemotePythonScriptExecutor` &mdash; Python script runner
- `IConnectionStore` &rarr; `ConnectionStore` &mdash; persists profiles to JSON
- `IRemoteHermesService`, `IFileEditorService`, `ISessionBrowserService`, `IUsageBrowserService`, `ISkillBrowserService` &mdash; domain services
- `SshConfigParser` &mdash; `~/.ssh/config` importer
- `MainViewModel` &mdash; navigation hub

**Transient** (new instance per navigation):
- `ConnectionManagerViewModel`, `OverviewViewModel`, `FileEditorViewModel`, `SessionBrowserViewModel`, `UsageBrowserViewModel`, `SkillBrowserViewModel`, `TerminalViewModel`

---

## Navigation

`MainViewModel` owns a `SelectedSection` enum and a `CurrentContentViewModel` property. When the section changes:

1. Check if the file editor has unsaved changes (`IsDirty`).
2. If dirty, show the discard-changes dialog. User can stay or discard.
3. Resolve the new ViewModel from the DI container.
4. Set `CurrentContentViewModel`. WPF's implicit `DataTemplate` matching in `App.xaml` renders the correct View.

The window title updates to `"{Section} - {Label} - Hermes Desktop"`.

---

## SSH transport

### Connection pool

`SshConnectionPool` maintains a `ConcurrentDictionary<Guid, PooledConnection>` keyed by profile ID.

- **Double-checked locking**: first check without lock, then acquire `SemaphoreSlim`, check again, create if needed.
- **Auth method building**: tries profile-specific key first, then standard keys from `~/.ssh/`.
- **Eviction**: on `SshConnectionException`, the broken connection is removed and the caller retries.
- **Terminal connections**: get their own dedicated `SshClient` (not pooled) because `ShellStream` ties up the channel.

### Command execution

`SshTransport.ExecuteCommandAsync()`:
1. Get or create a pooled `SshClient`.
2. `client.CreateCommand(command)` with a configurable timeout (default 30s).
3. Execute on a `Task.Run` thread to avoid blocking the UI.
4. Return `SshCommandResult(ExitCode, StdOut, StdErr, Duration)`.
5. On `SshConnectionException`: evict from pool, rethrow.

---

## Remote script execution

`RemotePythonScriptExecutor` loads Python scripts from embedded resources, injects parameters, and sends them over SSH.

### Script format

All scripts expect a `payload` global dictionary and print JSON to stdout. Success responses include `"ok": true`. Error responses include `"ok": false, "error": "..."`.

### Parameter injection

```python
import json as _json
payload = _json.loads('{"key": "value"}')
# ... rest of script
```

### Transport

```bash
printf '%s' '<base64_script>' | base64 -d | python3 -
```

The base64 encoding avoids shell escaping issues. `printf '%s'` prevents echo interpretation.

### Embedded scripts

| Script | Purpose |
|--------|---------|
| `discover_hermes.py` | Find Hermes root, session store, tracked files |
| `query_sessions.py` | List sessions with pagination and search |
| `query_session_detail.py` | Fetch full transcript for a session |
| `query_usage.py` | Aggregate token usage statistics |
| `discover_skills.py` | List all SKILL.md files with frontmatter |
| `read_skill_detail.py` | Read full SKILL.md content |
| `read_file.py` | Read file content + SHA-256 hash |
| `write_file.py` | Atomic write with hash-based conflict detection |
| `delete_session.py` | Delete session from SQLite + JSONL |

---

## File editing & conflict detection

The file editor uses optimistic locking:

1. **Load**: read remote file, compute SHA-256 hash, store as `ContentHash`.
2. **Edit**: user modifies text locally. `IsDirty = content != originalContent`.
3. **Save**: send new content + `expected_content_hash` to `write_file.py`.
4. **Server check**: script re-reads the file, computes current hash, compares. If hashes differ, returns error.
5. **On conflict**: user sees a yellow warning banner and must reload before saving.
6. **Atomic write**: `tempfile.mkstemp` + `os.replace` + `os.fsync` on remote.

---

## Terminal

### Components

- `TerminalViewModel` &mdash; manages tab collection, creates SSH shell sessions
- `TerminalTabViewModel` &mdash; owns one `ShellStreamSession` + one `TerminalControl`
- `TerminalControl` &mdash; WPF `UserControl` hosting `WebView2` with xterm.js
- `terminal-bridge.js` &mdash; client-side bridge between xterm.js and C#

### Data flow

```
Remote shell <-> SSH.NET ShellStream <-> TerminalControl (C#) <-> WebView2 JS interop <-> xterm.js
```

**Output** (remote to screen):
- `ShellStream.ReadAsync()` reads raw bytes
- C# base64-encodes them
- `ExecuteScriptAsync("terminalWrite('...')")` passes to JS
- JS decodes and calls `terminal.write(Uint8Array)`

**Input** (keyboard to remote):
- xterm.js `onData` fires with typed characters
- JS base64-encodes and calls `window.chrome.webview.postMessage()`
- C# `WebMessageReceived` handler decodes and writes to `ShellStream`

**Resize**:
- `ResizeObserver` in JS detects container size changes
- JS posts `{type: "resize", cols, rows}` to C#
- C# calls `SendWindowChangeRequest` on the SSH channel (via reflection for SSH.NET 2025 compatibility)

### Tab lifecycle

- Each tab owns a dedicated `SshClient` + `ShellStream`
- `TerminalControl` instances persist across tab switches (hidden, not destroyed)
- Connection health is monitored every 2 seconds
- On disconnect: shows status text + "Reconnect" button
- Reconnect creates a new `ShellStreamSession` and re-attaches to the control

---

## Theming

`ThemeManager` reads `HKCU\...\Personalize\AppsUseLightTheme` from the Windows registry.

Two resource dictionaries define 19 named brushes each:
- `LightTheme.xaml` &mdash; light backgrounds, dark text
- `DarkTheme.xaml` &mdash; VS Code-inspired dark palette

The MainWindow uses `DynamicResource` bindings (`WindowBackground`, `SidebarBackground`, `TextPrimary`, etc.) so themes can be switched at runtime.

---

## Markdown rendering

`MarkdownControl` wraps WebView2 with [marked.js](https://marked.js.org/). Used in the Skills detail view.

1. WebView2 loads `Assets/Markdown/markdown.html`
2. Skill markdown content is base64-encoded in C#
3. JS function `renderMarkdown(base64, isDark)` decodes and renders via `marked.parse()`
4. CSS variables switch between dark and light styles

---

## Usage charts

`SimpleBarChart` is a pure-XAML bar chart control. No external charting library.

- Uses `UniformGrid` with one column per data point
- Bar height proportional to max value
- Yellow-to-red gradient based on value intensity
- Tooltips show session name and token count
- Renders the last 100 sessions from the usage query
