import json
import os
import pathlib
import sys

def fail(message):
    print(json.dumps({"ok": False, "error": message}, ensure_ascii=False))
    sys.exit(1)

try:
    relative_path = payload.get("relative_path", "")
    normalized = pathlib.PurePosixPath(relative_path)
    if normalized.is_absolute() or ".." in normalized.parts or not normalized.parts:
        fail("请求的技能路径无效。")

    root = (pathlib.Path.home() / ".hermes" / "skills").resolve()
    target = (root / pathlib.Path(*normalized.parts) / "SKILL.md").resolve()

    try:
        target.relative_to(root)
    except ValueError:
        fail("请求的技能路径越出 Hermes 技能目录范围。")

    if not target.exists():
        fail(f"No skill exists at {relative_path}.")
    if not target.is_file():
        fail(f"{relative_path} does not resolve to a readable SKILL.md file.")

    content = target.read_text(encoding="utf-8", errors="replace")
    print(json.dumps({
        "ok": True,
        "markdown_content": content,
    }, ensure_ascii=False))
except Exception as exc:
    fail(f"读取远程 Hermes 技能详情失败：{exc}")
