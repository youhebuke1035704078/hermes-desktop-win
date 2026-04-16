import json
import os
import pathlib
import sqlite3
import sys

def fail(message):
    print(json.dumps({"ok": False, "error": message}, ensure_ascii=False))
    sys.exit(1)

def choose_table(tables, needle):
    lowered = needle.lower()
    for t in tables:
        if t.lower() == lowered:
            return t
    return None

def choose_column(columns, choices):
    lowered = {c.lower(): c for c in columns}
    for ch in choices:
        if ch.lower() in lowered:
            return lowered[ch.lower()]
    return None

def quote_ident(v):
    return '"' + v.replace('"', '""') + '"'

def stringify(v):
    if v is None:
        return None
    return str(v)

try:
    session_id = stringify(payload.get("session_id"))
    if not session_id:
        fail("The session ID is required.")

    hermes_home = pathlib.Path.home() / ".hermes"
    deleted_session_rows = 0
    deleted_message_rows = 0
    deleted_jsonl = False

    # Try SQLite
    for name in ["state.db", "state.sqlite", "state.sqlite3",
                  "store.db", "store.sqlite", "store.sqlite3"]:
        p = hermes_home / name
        if not p.is_file():
            continue
        try:
            conn = sqlite3.connect(str(p))
            conn.execute("PRAGMA busy_timeout = 2000")
            tables = [r[0] for r in conn.execute(
                "SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
            st = choose_table(tables, "sessions")
            mt = choose_table(tables, "messages")
            if st:
                scols = [r[1] for r in conn.execute(f"PRAGMA table_info({quote_ident(st)})").fetchall()]
                sid_col = choose_column(scols, ["id", "session_id"])
                if sid_col:
                    if mt:
                        mcols = [r[1] for r in conn.execute(f"PRAGMA table_info({quote_ident(mt)})").fetchall()]
                        msid_col = choose_column(mcols, ["session_id", "conversation_id"])
                        if msid_col:
                            with conn:
                                deleted_message_rows = conn.execute(
                                    f"DELETE FROM {quote_ident(mt)} WHERE {quote_ident(msid_col)} = ?",
                                    (session_id,)).rowcount
                    with conn:
                        deleted_session_rows = conn.execute(
                            f"DELETE FROM {quote_ident(st)} WHERE {quote_ident(sid_col)} = ?",
                            (session_id,)).rowcount
            conn.close()
            if deleted_session_rows > 0:
                break
        except Exception:
            continue

    # Try JSONL
    sessions_dir = hermes_home / "sessions"
    if sessions_dir.exists():
        for f in sessions_dir.rglob("*.jsonl"):
            if f.stem == session_id:
                f.unlink()
                deleted_jsonl = True
                break

    if deleted_session_rows <= 0 and not deleted_jsonl:
        fail(f"No session matching '{session_id}' was found to delete.")

    print(json.dumps({"ok": True}, ensure_ascii=False))
except Exception as exc:
    fail(f"Unable to delete the remote Hermes session: {exc}")
