import json
import os
import pathlib
import platform
import sqlite3
import sys

def fail(message):
    print(json.dumps({"ok": False, "error": message}, ensure_ascii=False))
    sys.exit(1)

def tilde(path, home):
    try:
        relative = path.relative_to(home)
        return "~/" + relative.as_posix() if relative.as_posix() != "." else "~"
    except ValueError:
        return path.as_posix()

def choose_table(tables, needle):
    lowered = needle.lower()
    for t in tables:
        if t.lower() == lowered:
            return t
    for t in tables:
        if lowered in t.lower():
            return t
    return None

def discover_session_store(hermes_home, home):
    candidates = [
        hermes_home / "state.db",
        hermes_home / "state.sqlite",
        hermes_home / "state.sqlite3",
        hermes_home / "store.db",
        hermes_home / "store.sqlite",
        hermes_home / "store.sqlite3",
    ]
    for c in candidates:
        if not c.is_file():
            continue
        try:
            conn = sqlite3.connect(f"file:{c}?mode=ro", uri=True)
            tables = [r[0] for r in conn.execute(
                "SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
            st = choose_table(tables, "sessions")
            mt = choose_table(tables, "messages")
            conn.close()
            if st and mt:
                return {"kind": "sqlite", "path": tilde(c, home),
                        "session_table": st, "message_table": mt}
        except Exception:
            continue
    return None

try:
    home = pathlib.Path.home()
    hermes_home = home / ".hermes"
    user_path = hermes_home / "memories" / "USER.md"
    memory_path = hermes_home / "memories" / "MEMORY.md"
    soul_path = hermes_home / "SOUL.md"
    sessions_dir = hermes_home / "sessions"

    result = {
        "ok": True,
        "home": str(home),
        "hermes_root": str(hermes_home),
        "python_version": platform.python_version(),
        "session_source": None,
        "session_store": None,
        "tracked_files": [],
    }

    if hermes_home.is_dir():
        store = discover_session_store(hermes_home, home)
        if store:
            result["session_store"] = store["path"]
            result["session_source"] = "sqlite"
        elif sessions_dir.is_dir() and list(sessions_dir.glob("*.jsonl")):
            result["session_source"] = "jsonl"
            result["session_store"] = tilde(sessions_dir, home)

    for p in [user_path, memory_path, soul_path]:
        info = {"path": str(p), "exists": p.is_file(), "size": None}
        if info["exists"]:
            try:
                info["size"] = p.stat().st_size
            except OSError:
                pass
        result["tracked_files"].append(info)

    print(json.dumps(result, ensure_ascii=False))
except Exception as exc:
    fail(f"发现远程 Hermes 工作区失败：{exc}")
