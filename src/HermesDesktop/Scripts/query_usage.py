import json
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
    if isinstance(v, bytes):
        return v.decode("utf-8", errors="replace")
    return str(v)

def sanitize_title(v):
    text = stringify(v)
    if not text:
        return None
    text = text.replace("\n", " ").replace("\r", " ").strip()
    if text.lower().startswith("<think>"):
        return None
    return text[:120] if text else None

try:
    home = pathlib.Path.home()
    hermes_home = home / ".hermes"

    conn = None
    for name in ["state.db", "state.sqlite", "state.sqlite3",
                  "store.db", "store.sqlite", "store.sqlite3"]:
        p = hermes_home / name
        if p.is_file():
            try:
                c = sqlite3.connect(f"file:{p}?mode=ro", uri=True)
                tables = [r[0] for r in c.execute(
                    "SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
                st = choose_table(tables, "sessions")
                if st:
                    conn = c
                    session_table = st
                    break
                c.close()
            except Exception:
                continue

    if conn is None:
        print(json.dumps({
            "ok": True,
            "state": "unavailable",
            "session_count": 0,
            "input_tokens": 0,
            "output_tokens": 0,
            "top_sessions": [],
            "top_models": [],
            "recent_sessions": [],
            "message": "No readable session store found.",
        }, ensure_ascii=False))
        sys.exit(0)

    cols = [r[1] for r in conn.execute(f"PRAGMA table_info({quote_ident(session_table)})").fetchall()]
    low = {c.lower(): c for c in cols}

    sid_col = choose_column(cols, ["id", "session_id"])
    title_col = choose_column(cols, ["title", "summary", "name"])
    started_col = choose_column(cols, ["started_at", "created_at", "timestamp"])
    model_col = choose_column(cols, ["model"])

    input_expr = f"COALESCE(SUM({quote_ident(low['input_tokens'])}), 0)" if "input_tokens" in low else "0"
    output_expr = f"COALESCE(SUM({quote_ident(low['output_tokens'])}), 0)" if "output_tokens" in low else "0"
    input_val = f"COALESCE({quote_ident(low['input_tokens'])}, 0)" if "input_tokens" in low else "0"
    output_val = f"COALESCE({quote_ident(low['output_tokens'])}, 0)" if "output_tokens" in low else "0"

    totals = conn.execute(
        f"SELECT COUNT(*), {input_expr}, {output_expr} FROM {quote_ident(session_table)}"
    ).fetchone() or (0, 0, 0)

    # Top sessions
    top_sessions = []
    if sid_col:
        q = (f"SELECT {quote_ident(sid_col)}, "
             f"{quote_ident(title_col) if title_col else 'NULL'}, "
             f"{input_val}, {output_val}, ({input_val} + {output_val}) "
             f"FROM {quote_ident(session_table)} ORDER BY 5 DESC LIMIT 10")
        for r in conn.execute(q).fetchall():
            sid = stringify(r[0])
            if not sid:
                continue
            top_sessions.append({
                "id": sid,
                "title": sanitize_title(r[1]) or sid,
                "input_tokens": int(r[2] or 0),
                "output_tokens": int(r[3] or 0),
                "total_tokens": int(r[4] or 0),
            })

    # Top models
    top_models = []
    if model_col:
        mq = (f"SELECT COALESCE(NULLIF(TRIM({quote_ident(model_col)}), ''), 'Unknown'), "
              f"COUNT(*), SUM({input_val} + {output_val}) "
              f"FROM {quote_ident(session_table)} "
              f"GROUP BY 1 ORDER BY 3 DESC LIMIT 5")
        for r in conn.execute(mq).fetchall():
            top_models.append({
                "model": stringify(r[0]) or "Unknown",
                "session_count": int(r[1] or 0),
                "total_tokens": int(r[2] or 0),
            })

    # Recent sessions (last 100, oldest-first for charts)
    recent_sessions = []
    if sid_col:
        rq = (f"SELECT {quote_ident(sid_col)}, "
              f"{quote_ident(title_col) if title_col else 'NULL'}, "
              f"{input_val}, {output_val}, ({input_val} + {output_val}) "
              f"FROM {quote_ident(session_table)} ")
        if started_col:
            rq += f"ORDER BY {quote_ident(started_col)} DESC "
        rq += "LIMIT 100"
        for r in reversed(conn.execute(rq).fetchall()):
            sid = stringify(r[0])
            if not sid:
                continue
            recent_sessions.append({
                "id": sid,
                "title": sanitize_title(r[1]) or sid,
                "input_tokens": int(r[2] or 0),
                "output_tokens": int(r[3] or 0),
                "total_tokens": int(r[4] or 0),
            })

    conn.close()
    print(json.dumps({
        "ok": True,
        "state": "available",
        "session_count": int(totals[0] or 0),
        "input_tokens": int(totals[1] or 0),
        "output_tokens": int(totals[2] or 0),
        "top_sessions": top_sessions,
        "top_models": top_models,
        "recent_sessions": recent_sessions,
    }, ensure_ascii=False))
except Exception as exc:
    fail(f"读取远程 Hermes 用量失败：{exc}")
