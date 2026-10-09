#!/usr/bin/env python3
"""Shared leaderboard for Night Supermarket — one file, no dependencies.

    python3 Tools/leaderboard_server.py            # listens on http://0.0.0.0:8787
    python3 Tools/leaderboard_server.py 9000       # another port

Then in the game set the URL once (Unity Console / any C# entry point, or the Editor menu item
"Night Supermarket > Leaderboard > Set server URL…"):

    PlayerPrefs.SetString("ns-board-url", "http://<this machine>:8787");

Protocol — plain text, the same line format the game stores locally (name|points|grade|date):
    GET  /shift/<n>          -> the board for shift n, best first, 10 lines max
    POST /shift/<n>  <lines> -> merge the posted lines in, reply with the merged board
Scores live in leaderboard.json next to this script. Names: letters, digits, space, _ and -, 16 max.
"""
import json
import os
import re
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

STORE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "leaderboard.json")
CAPACITY = 10
NAME_OK = re.compile(r"[^A-Za-z0-9 _-]")


def clean(name):
    name = NAME_OK.sub("", (name or "").strip())[:16]
    return name or "mannequin"


def load():
    try:
        with open(STORE, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def save(data):
    tmp = STORE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    os.replace(tmp, STORE)


def merge(board, lines):
    """board: list of [name, points, grade, date]. Higher score per name wins (case-insensitive)."""
    best = {e[0].lower(): e for e in board}
    for raw in lines.splitlines():
        parts = raw.strip().split("|")
        if len(parts) < 2:
            continue
        try:
            points = int(parts[1])
        except ValueError:
            continue
        entry = [clean(parts[0]), points, parts[2] if len(parts) > 2 else "", parts[3] if len(parts) > 3 else ""]
        key = entry[0].lower()
        if key not in best or best[key][1] < points:
            best[key] = entry
    merged = sorted(best.values(), key=lambda e: -e[1])[:CAPACITY]
    return merged


def serialize(board):
    return "".join(f"{e[0]}|{e[1]}|{e[2]}|{e[3]}\n" for e in board)


class Handler(BaseHTTPRequestHandler):
    def _shift(self):
        m = re.fullmatch(r"/shift/(\d+)/?", self.path)
        return m.group(1) if m else None

    def _reply(self, code, text):
        body = text.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        shift = self._shift()
        if shift is None:
            return self._reply(404, "use /shift/<n>\n")
        self._reply(200, serialize(load().get(shift, [])))

    def do_POST(self):
        shift = self._shift()
        if shift is None:
            return self._reply(404, "use /shift/<n>\n")
        length = min(int(self.headers.get("Content-Length") or 0), 64 * 1024)
        lines = self.rfile.read(length).decode("utf-8", "replace")
        data = load()
        data[shift] = merge(data.get(shift, []), lines)
        save(data)
        self._reply(200, serialize(data[shift]))

    def log_message(self, fmt, *args):
        sys.stderr.write("%s %s\n" % (self.command, self.path))


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8787
    print(f"Night Supermarket leaderboard on http://0.0.0.0:{port}  (store: {STORE})")
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()
