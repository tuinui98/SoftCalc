"""check_help_links.py - Every link out in the help still opens (owner 2026-11-26: before each release).

Reads help/*.md and web/app/help/*.md, finds each [text](https://...) link
(the More: lines), fetches it, and prints its status and page title. Exits
1 when one does not answer 200, so a moved or dead reference page is
found before a release, not by a user.

    python tools/check_help_links.py
"""

import http.client
import pathlib
import re
import sys
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
FOLDERS = [ROOT / "help", ROOT / "web" / "app" / "help"]
LINK = re.compile(r"\]\((https?://[^)\s]+)\)")
TITLE = re.compile(r"<title>(.*?)</title>", re.IGNORECASE | re.DOTALL)
AGENT = "SoftCalc-help-link-check"
TIMEOUT_S = 20


def links():
    """{url: [files]} for every link in the help."""
    found = {}
    for folder in FOLDERS:
        for path in sorted(folder.glob("*.md")):
            for url in LINK.findall(path.read_text(encoding="utf-8")):
                found.setdefault(url, []).append(path.relative_to(ROOT).as_posix())
    return found


def check(url):
    """(status, title) of one page; status 0 when it could not be reached."""
    request = urllib.request.Request(url, headers={"User-Agent": AGENT})
    try:
        with urllib.request.urlopen(request, timeout=TIMEOUT_S) as answer:
            try:
                page = answer.read(200_000).decode("utf-8", "replace")
            except http.client.IncompleteRead as cut:    # a page cut short still has its title
                page = cut.partial.decode("utf-8", "replace")
            title = TITLE.search(page)
            return answer.status, " ".join(title.group(1).split()) if title else ""
    except urllib.error.HTTPError as error:
        return error.code, ""
    except (urllib.error.URLError, TimeoutError) as error:
        return 0, str(error)


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")    # page titles hold any letter
    failed = 0
    found = links()
    for url, files in found.items():
        status, title = check(url)
        failed += status != 200
        print(f"{status:3}  {url}  {title[:70]}  ({', '.join(sorted(set(files)))})")
    print(f"{len(found)} links, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
