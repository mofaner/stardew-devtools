#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Convert a MediaWiki XML export from Stardew Valley Wiki (Modding:* pages)
into clean, structured Markdown files under docs/custom/wiki/<slug>.md.

Usage:
    python scripts/import_wiki_xml.py [path/to/export.xml] [--out docs/custom/wiki]
"""

import argparse
import html
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Optional, Tuple


SKIP_EXACT_TITLES = {
    "Modding:Community",
    "Modding:Editing XNB files",
    "Modding:Using XNB mods",
    "Modding:Mod compatibility",
    "Modding:Migrate to Stardew Valley 1.3",
    "Modding:Migrate XNB changes to Stardew Valley 1.3",
    "Modding:Migrate to Stardew Valley 1.3.36",
    "Modding:Migrate to Stardew Valley 1.4",
    "Modding:Migrate to Stardew Valley 1.5",
    "Modding:Migrate to Stardew Valley 1.5.5",
    "Modding:Migrate to SMAPI 2.0",
    "Modding:Migrate to SMAPI 3.0",
    "Modding:Migrate to Harmony 2.0",
}

SKIP_PREFIXES = (
    "User:",
    "Template:",
    "Modding:Player Guide/",
    "Modding:Installing SMAPI",
    "Modding:Mod compatibility/",
    "Modding:Using XNB mods/",
)

SKIP_SUFFIXES = (
    "/header",
    "/entry",
    "/barchart",
    "/cmd",
    "/event",
    "/xnb",
)


def should_skip_page(title: str, text: str) -> Tuple[bool, str]:
    if not title or not text.strip():
        return True, "empty"
    if text.lstrip().upper().startswith("#REDIRECT"):
        return True, "redirect"
    if title in SKIP_EXACT_TITLES:
        return True, "excluded_outdated_or_non_dev"
    for pfx in SKIP_PREFIXES:
        if title.startswith(pfx):
            return True, f"prefix:{pfx}"
    for sfx in SKIP_SUFFIXES:
        if title.endswith(sfx):
            return True, f"suffix:{sfx}"
    return False, ""


def title_to_slug(title: str) -> str:
    s = title
    if s.startswith("Modding:"):
        s = s[len("Modding:"):]
    s = s.lower()
    s = re.sub(r"[^a-z0-9]+", "-", s).strip("-")
    return s or "page"


def title_to_wiki_url(title: str) -> str:
    underscored = title.replace(" ", "_")
    return f"https://stardewvalleywiki.com/{underscored}"


def split_top_level_pipes(inner: str) -> List[str]:
    """Split template content on `|` while respecting nested `{{...}}`, `[[...]]`, and tags."""
    parts: List[str] = []
    buf: List[str] = []
    i = 0
    n = len(inner)
    brace_depth = 0
    bracket_depth = 0
    in_pre = False

    while i < n:
        if inner.startswith("<syntaxhighlight", i) or inner.startswith("<source", i) or inner.startswith("<pre", i):
            in_pre = True
        elif inner.startswith("</syntaxhighlight>", i) or inner.startswith("</source>", i) or inner.startswith("</pre>", i):
            in_pre = False

        if not in_pre:
            if inner.startswith("{{", i):
                brace_depth += 1
                buf.append("{{")
                i += 2
                continue
            if inner.startswith("}}", i) and brace_depth > 0:
                brace_depth -= 1
                buf.append("}}")
                i += 2
                continue
            if inner.startswith("[[", i):
                bracket_depth += 1
                buf.append("[[")
                i += 2
                continue
            if inner.startswith("]]", i) and bracket_depth > 0:
                bracket_depth -= 1
                buf.append("]]")
                i += 2
                continue
            if inner[i] == "|" and brace_depth == 0 and bracket_depth == 0:
                parts.append("".join(buf))
                buf = []
                i += 1
                continue

        buf.append(inner[i])
        i += 1

    parts.append("".join(buf))
    return parts


def parse_template_args(parts: List[str]) -> Tuple[List[str], Dict[str, str]]:
    positional: List[str] = []
    named: Dict[str, str] = {}
    for p in parts[1:]:
        eq = p.find("=")
        # Check that `=` isn't inside a tag or multi-line block before key
        if eq > 0 and "\n" not in p[:eq] and "<" not in p[:eq] and "{{" not in p[:eq]:
            key = p[:eq].strip()
            val = p[eq + 1:].strip()
            named[key] = val
        else:
            positional.append(p.strip())
    return positional, named


def truncate_collapse_content(raw_content: str, max_lines: int = 15) -> str:
    lines = raw_content.strip().splitlines()
    if len(lines) <= max_lines + 3:
        return raw_content.strip()

    kept = lines[:max_lines]
    # Check if we cut inside an open <syntaxhighlight> / <source> / <pre> / ``` block
    joined_kept = "\n".join(kept)
    closing = ""
    if ("<syntaxhighlight" in joined_kept and "</syntaxhighlight>" not in joined_kept):
        closing = "\n  // ... (truncated sample; query `sdv-dev data` for full vanilla entries)\n</syntaxhighlight>"
    elif ("<source" in joined_kept and "</source>" not in joined_kept):
        closing = "\n  // ... (truncated sample)\n</source>"
    elif ("<pre" in joined_kept and "</pre>" not in joined_kept):
        closing = "\n... (truncated sample)\n</pre>"
    elif joined_kept.count("```") % 2 == 1:
        closing = "\n  // ... (truncated sample)\n```"
    else:
        closing = "\n*(... truncated long sample ...)*"

    return joined_kept + closing


def render_single_template(raw_inner: str) -> str:
    parts = split_top_level_pipes(raw_inner)
    if not parts:
        return ""

    tname = parts[0].strip()
    tname_lower = tname.lower()
    pos, named = parse_template_args(parts)

    # 1. Parameter tokens: {{t|X}} -> <X>, {{o|X}} -> [X]
    if tname_lower == "t":
        arg = pos[0] if pos else named.get("1", "")
        return f"<{arg}>"
    if tname_lower == "o":
        arg = pos[0] if pos else named.get("1", "")
        return f"[{arg}]"

    # 2. Console command entry: {{/cmd | command=... | params=... | desc=... | example=...}}
    if tname_lower == "/cmd":
        cmd = named.get("command", "").strip()
        prms = named.get("params", "").strip()
        desc = named.get("desc", "").strip()
        example = named.get("example", "").strip()

        syntax_params = prms.replace(",", "") if prms else ""
        primary_cmd = cmd.split(",")[0].strip()
        syntax = f"{primary_cmd} {syntax_params}".strip()

        out = [f"## Command: `{cmd}`"]
        out.append(f"- **Syntax**: `{syntax}`")
        if desc:
            out.append(f"- **Description**: {desc}")
        if example:
            out.append(f"- **Example**:\n{example}")
        return "\n".join(out) + "\n"

    # 3. SMAPI Event entry: {{/event | group=... | name=... | desc=... | arg name 1=...}}
    if tname_lower == "/event":
        group = named.get("group", "").strip()
        name = named.get("name", "").strip()
        desc = named.get("desc", "").strip()
        example = named.get("example", "").strip()

        full_ev = f"helper.Events.{group}.{name}" if group else name
        out = [f"## Event: `{full_ev}`"]
        if desc:
            out.append(desc)

        arg_lines = []
        for idx in range(1, 15):
            an = named.get(f"arg name {idx}", "").strip()
            at = named.get(f"arg type {idx}", "").strip()
            ad = named.get(f"arg desc {idx}", "").strip()
            if an or ad:
                type_part = f" ({at})" if at else ""
                arg_lines.append(f"  - {an}{type_part}: {ad}")
        if arg_lines:
            out.append("- **Event Arguments**:\n" + "\n".join(arg_lines))
        if example:
            out.append(f"- **Example**:\n{example}")
        return "\n\n".join(out) + "\n"

    # 4. Collapsible blocks: {{collapse | Label | content=...}} or positional {{collapse | Label | ...}}
    if tname_lower == "collapse":
        label = pos[0] if pos else named.get("title", named.get("1", "Example"))
        body = named.get("content", pos[1] if len(pos) > 1 else "")
        truncated = truncate_collapse_content(body, max_lines=15)
        return f"\n**{label} (Sample)**:\n{truncated}\n"

    # 5. Simple formatting / note / main / anchor templates
    if tname_lower in ("main", "see also"):
        targets = ", ".join(pos)
        return f"> *{tname.capitalize()}: {targets}*"
    if tname_lower == "anchor":
        return ""
    if tname_lower in ("quote", "blockquote"):
        q = pos[0] if pos else named.get("text", "")
        return f"> {q}"
    if tname_lower.startswith("modding:") or tname_lower.endswith("/header"):
        return ""
    if tname_lower in ("tokentable", " clear", "clear", "toc limit", "toclimit"):
        return ""

    # Preserve Content Patcher tokens like {{ModId}}, {{i18n:...}}, {{HasMod:...}}, {{Random:...}} inside inline `<code>`
    if not pos and not named and (":" in tname or tname in ("ModId", "Target", "TargetWithoutPath", "FromFile", "Season", "Weather", "Time", "Day", "DayEvent", "DayOfWeek", "PlayerName", "FarmName")):
        return "{{" + raw_inner + "}}"

    # Generic fallback for unknown minor inline templates (e.g. {{key|E}}, {{version|1.6}})
    if pos:
        return " ".join(pos)
    if named:
        return " ".join(f"{k}={v}" for k, v in named.items())
    return "{{" + raw_inner + "}}"


def expand_all_templates(wikitext: str) -> str:
    """Recursively expand balanced `{{ ... }}` templates from innermost to outermost,
    ignoring `{{...}}` inside `<syntaxhighlight>` / `<source>` / `<pre>` / `<nowiki>` blocks,
    while allowing `{{t|X}}` and `{{o|X}}` inside inline `<code>` / `<samp>` tags!"""
    protected: List[str] = []

    def stash(m: re.Match) -> str:
        idx = len(protected)
        protected.append(m.group(0))
        return f"\x00CODEBLOCK_{idx}\x00"

    text = re.sub(
        r"<(syntaxhighlight|source|pre|nowiki)\b[^>]*>.*?</\1>",
        stash,
        wikitext,
        flags=re.DOTALL | re.IGNORECASE,
    )

    # Iterative stack-based expansion of balanced {{ ... }}
    max_passes = 8
    for _ in range(max_passes):
        if "{{" not in text:
            break
        out: List[str] = []
        stack: List[List[str]] = []
        i = 0
        n = len(text)
        changed = False

        while i < n:
            if text.startswith("{{", i):
                stack.append([])
                i += 2
                continue
            if text.startswith("}}", i) and stack:
                inner_raw = "".join(stack.pop())
                inner_restored = restore_stashed(inner_raw, protected)
                rendered = render_single_template(inner_restored)
                rendered_safe = re.sub(
                    r"<(syntaxhighlight|source|pre|nowiki)\b[^>]*>.*?</\1>",
                    stash,
                    rendered,
                    flags=re.DOTALL | re.IGNORECASE,
                )
                if rendered_safe != "{{" + inner_raw + "}}":
                    changed = True
                if stack:
                    stack[-1].append(rendered_safe)
                else:
                    out.append(rendered_safe)
                i += 2
                continue

            if stack:
                stack[-1].append(text[i])
            else:
                out.append(text[i])
            i += 1

        # Flush any unclosed `{{` literally
        while stack:
            unclosed = "{{" + "".join(stack.pop())
            if stack:
                stack[-1].append(unclosed)
            else:
                out.append(unclosed)

        text = "".join(out)
        if not changed:
            break

    return restore_stashed(text, protected)


def restore_stashed(text: str, protected: List[str]) -> str:
    def repl(m: re.Match) -> str:
        idx = int(m.group(1))
        return protected[idx] if 0 <= idx < len(protected) else m.group(0)

    prev = None
    cur = text
    while prev != cur and "\x00CODEBLOCK_" in cur:
        prev = cur
        cur = re.sub(r"\x00CODEBLOCK_(\d+)\x00", repl, cur)
    return cur


def clean_cell_text(cell: str) -> str:
    c = cell.strip()
    # Strip optional leading cell attributes like `style="..." | actual text` (only if not inside [[link|label]] or `code`)
    m = re.match(r'^(?:(?:style|class|align|valign|width|colspan|rowspan|scope)\s*=\s*(?:"[^"]*"|\'[^\']*\'|\S+)\s*)+\|\s*(.*)$', c, flags=re.DOTALL | re.IGNORECASE)
    if m:
        c = m.group(1).strip()

    c = re.sub(r"<br\s*/?>", "; ", c, flags=re.IGNORECASE)
    c = re.sub(r"\s*\n+\s*", " ", c)
    c = c.replace("|", "\\|")
    return c.strip()


def convert_single_wikitable(table_body: str) -> str:
    # If this wikitable was just a wrapper around `{{/cmd}}` or `{{/event}}` templates, unwrap it and preserve all expanded entries!
    if "## Command:" in table_body or "## Event:" in table_body or "### Command:" in table_body or "### Event:" in table_body:
        kept_lines = []
        for raw_line in table_body.splitlines():
            s = raw_line.strip()
            if s.startswith("{|") or s.startswith("|}") or s.startswith("|-") or s.startswith("!"):
                continue
            kept_lines.append(raw_line)
        return "\n" + "\n".join(kept_lines).strip() + "\n"

    rows: List[Tuple[bool, List[str]]] = []
    cur_cells: List[Tuple[bool, str]] = []

    def flush_row():
        nonlocal cur_cells
        if cur_cells:
            is_hdr = all(h for h, _ in cur_cells)
            rows.append((is_hdr, [clean_cell_text(val) for _, val in cur_cells]))
            cur_cells = []

    for raw_line in table_body.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("{|") or line.startswith("|+"):
            continue
        if line.startswith("|}"):
            break
        if line.startswith("|-"):
            flush_row()
            continue
        if line.startswith("!"):
            content = line[1:].strip()
            for part in re.split(r"\s*!!\s*", content):
                cur_cells.append((True, part))
        elif line.startswith("|"):
            content = line[1:].strip()
            for part in re.split(r"\s*\|\|\s*", content):
                cur_cells.append((False, part))
        else:
            # Continuation of previous cell
            if cur_cells:
                is_h, prev_val = cur_cells[-1]
                cur_cells[-1] = (is_h, prev_val + " " + line)

    flush_row()
    if not rows:
        return ""

    col_count = max(len(r[1]) for r in rows)
    if col_count == 0:
        return ""

    md_lines: List[str] = []
    header_emitted = False

    for idx, (is_hdr, cells) in enumerate(rows):
        padded = cells + [""] * (col_count - len(cells))
        row_str = "| " + " | ".join(padded) + " |"
        md_lines.append(row_str)
        if idx == 0:
            md_lines.append("| " + " | ".join(["---"] * col_count) + " |")
            header_emitted = True

    if not header_emitted:
        return ""
    return "\n" + "\n".join(md_lines) + "\n"


def convert_wikitables(text: str) -> str:
    # Handle non-nested `{| ... |}` blocks iteratively
    pattern = re.compile(r"\{\|[^\n]*\n(.*?)\n\|\}", flags=re.DOTALL)
    for _ in range(5):
        if "{|" not in text:
            break
        new_text = pattern.sub(lambda m: convert_single_wikitable(m.group(1)), text)
        if new_text == text:
            break
        text = new_text
    return text


def convert_wikitext_to_markdown(title: str, raw_wikitext: str) -> str:
    s = raw_wikitext

    # 1. Strip comments and non-content directives
    s = re.sub(r"<!--.*?-->", "", s, flags=re.DOTALL)
    s = re.sub(r"<noinclude>.*?</noinclude>", "", s, flags=re.DOTALL | re.IGNORECASE)
    s = re.sub(r"</?includeonly>", "", s, flags=re.IGNORECASE)
    s = re.sub(r"__(?:TOC|NOTOC|FORCETOC|NOEDITSECTION)__", "", s)

    # 2. Expand templates (`{{/cmd}}`, `{{/event}}`, `{{t|X}}`, `{{o|X}}`, `{{collapse}}`, etc.)
    s = expand_all_templates(s)

    # 3. Protect fenced / syntaxhighlight blocks during inline Wiki formatting
    code_blocks: List[str] = []

    def stash_code_block(lang: str, body: str) -> str:
        idx = len(code_blocks)
        clean_lang = (lang or "").strip().lower()
        clean_body = html.unescape(body.strip("\r\n"))
        code_blocks.append(f"\n```{clean_lang}\n{clean_body}\n```\n")
        return f"\x00FENCED_{idx}\x00"

    s = re.sub(
        r"<(?:syntaxhighlight|source)(?:\s+[^>]*?lang\s*=\s*['\"]?([a-zA-Z0-9+#\-]+)['\"]?[^>]*)?>(.*?)</(?:syntaxhighlight|source)>",
        lambda m: stash_code_block(m.group(1) or "", m.group(2)),
        s,
        flags=re.DOTALL | re.IGNORECASE,
    )
    s = re.sub(
        r"<pre[^>]*>(.*?)</pre>",
        lambda m: stash_code_block("text", m.group(1)),
        s,
        flags=re.DOTALL | re.IGNORECASE,
    )

    # 4. Inline code tags: <code> / <samp> / <tt> -> `...`
    s = re.sub(
        r"<(?:code|samp|tt)[^>]*>(.*?)</(?:code|samp|tt)>",
        lambda m: "`" + html.unescape(m.group(1).strip()) + "`",
        s,
        flags=re.DOTALL | re.IGNORECASE,
    )

    # 5. Convert `{| class="wikitable" ... |}` into Markdown tables
    s = convert_wikitables(s)

    # 6. Convert headings (`======` down to `==`)
    for level in range(6, 1, -1):
        eq = "=" * level
        hashes = "#" * level
        s = re.sub(rf"^{eq}\s*(.+?)\s*{eq}\s*$", rf"{hashes} \1", s, flags=re.MULTILINE)

    # 7. Convert MediaWiki definition lists (`; Term : Desc`) & bullet depths (`**`, `***`, `##`)
    s = re.sub(r"^;\s*([^\n:]+?)\s*:\s*(.+)$", r"- **\1**: \2", s, flags=re.MULTILINE)
    s = re.sub(r"^;\s*(.+)$", r"**\1**", s, flags=re.MULTILINE)
    s = re.sub(r"^:\s*(.+)$", r"  \1", s, flags=re.MULTILINE)
    s = re.sub(r"^(\*{2,})\s+", lambda m: "  " * (len(m.group(1)) - 1) + "- ", s, flags=re.MULTILINE)
    s = re.sub(r"^\*\s+", "- ", s, flags=re.MULTILINE)

    # 8. Convert links: [[File:...]] removal, [[Target|Label]] -> Label, [[Target]] -> Target, [http... Label] -> [Label](http...)
    s = re.sub(r"\[\[(?:File|Image):[^\]]+\]\]", "", s, flags=re.IGNORECASE)
    s = re.sub(r"\[\[Category:[^\]]+\]\]", "", s, flags=re.IGNORECASE)
    s = re.sub(r"\[\[([^|\]]+)\|([^\]]+)\]\]", r"\2", s)
    s = re.sub(r"\[\[([^\]]+)\]\]", r"\1", s)
    s = re.sub(r"\[(https?://[^\s\]]+)\s+([^\]]+)\]", r"[\2](\1)", s)

    # 9. Bold / Italic & remaining minor HTML cleanup
    s = s.replace("'''", "**").replace("''", "*")
    s = re.sub(r"<br\s*/?>", "\n", s, flags=re.IGNORECASE)
    s = re.sub(r"</?(?:div|span|small|sup|sub|blockquote|nowiki|dfn)[^>]*>", "", s, flags=re.IGNORECASE)
    s = html.unescape(s)

    # 10. Restore fenced code blocks
    def restore_fenced(m: re.Match) -> str:
        idx = int(m.group(1))
        return code_blocks[idx] if 0 <= idx < len(code_blocks) else m.group(0)

    s = re.sub(r"\x00FENCED_(\d+)\x00", restore_fenced, s)

    # Normalize excessive blank lines
    s = re.sub(r"\n{3,}", "\n\n", s).strip()

    wiki_url = title_to_wiki_url(title)
    underscored_alias = title.replace("Modding:", "").replace(" ", "_")
    header = (
        f"> **文档来源**: Stardew Valley Official Wiki ({wiki_url})\n\n"
        f"# {title} (`{underscored_alias}`)\n\n"
    )
    return header + s + "\n"


def convert_wiki_xml(xml_path: Path, out_dir: Path) -> Tuple[int, int]:
    if not xml_path.is_file():
        raise FileNotFoundError(f"XML file not found: {xml_path}")

    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    tree = ET.parse(str(xml_path))
    root = tree.getroot()
    ns = {"mw": root.tag.split("}")[0].strip("{")} if "}" in root.tag else {}

    page_tag = "mw:page" if ns else "page"
    title_tag = "mw:title" if ns else "title"
    text_path = "mw:revision/mw:text" if ns else "revision/text"

    written = 0
    skipped = 0

    for page in root.findall(page_tag, ns):
        t_el = page.find(title_tag, ns)
        txt_el = page.find(text_path, ns)
        title = (t_el.text or "").strip() if t_el is not None else ""
        wikitext = (txt_el.text or "") if txt_el is not None else ""

        skip, _ = should_skip_page(title, wikitext)
        if skip:
            skipped += 1
            continue

        md = convert_wikitext_to_markdown(title, wikitext)
        slug = title_to_slug(title)
        out_file = out_dir / f"{slug}.md"
        out_file.write_text(md, encoding="utf-8", newline="\n")
        written += 1

    return written, skipped


def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    default_xml = repo_root / "raw" / "stardew-wiki-modding-export.xml"
    default_out = repo_root / "docs" / "custom" / "wiki"

    parser = argparse.ArgumentParser(description="Import Stardew Valley Modding Wiki XML export into clean Markdown files.")
    parser.add_argument("xml_path", nargs="?", default=str(default_xml), help="Path to MediaWiki exported .xml file")
    parser.add_argument("--out", default=str(default_out), help="Output directory (default: docs/custom/wiki)")
    args = parser.parse_args()

    xml_path = Path(args.xml_path)
    out_dir = Path(args.out)

    written, skipped = convert_wiki_xml(xml_path, out_dir)
    print(f"Converted {written} Wiki modding pages -> {out_dir} (skipped {skipped} non-dev/outdated/template pages).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
