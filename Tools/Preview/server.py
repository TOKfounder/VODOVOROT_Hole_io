#!/usr/bin/env python3
"""Loopback-only Stage2 QA server. Never put this helper in a production ZIP."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit
import json
import mimetypes
import shutil

PROJECT_ROOT = Path(__file__).resolve().parents[2]
QA_ROOT = Path(__file__).resolve().parent
WEB_ROOT = PROJECT_ROOT / "builds/ImprovementPass-20261107/Yandex"
BIND = "127.0.0.1"
PORT = 8767


class PreviewHandler(BaseHTTPRequestHandler):
    server_version = "VodovorotLocalPreview/1"
    qa_root = QA_ROOT
    web_root = WEB_ROOT

    def do_GET(self):
        self.serve_file(False)

    def do_HEAD(self):
        self.serve_file(True)

    def reject_method(self):
        self.respond(405, b"Read-only preview: only GET and HEAD are supported.\n",
                     headers={"Allow": "GET, HEAD"})

    do_POST = do_PUT = do_PATCH = do_DELETE = do_OPTIONS = do_TRACE = do_CONNECT = reject_method

    def respond(self, status, body, headers=None, head=False):
        self.send_response(status)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        for name, value in (headers or {}).items():
            self.send_header(name, value)
        self.end_headers()
        if not head:
            self.wfile.write(body)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        super().end_headers()

    def serve_file(self, head):
        # Reject DNS-rebinding Host headers as well as binding only loopback.
        try:
            host = urlsplit("//" + self.headers.get("Host", "")).hostname
        except ValueError:
            host = None
        if host not in ("localhost", "127.0.0.1", "::1"):
            return self.respond(403, b"Loopback preview hosts only.\n", head=head)
        try:
            path = unquote(urlsplit(self.path).path, errors="strict")
        except (UnicodeError, ValueError):
            return self.respond(400, b"Invalid path.\n", head=head)
        if "\x00" in path or "\\" in path or not path.startswith("/"):
            return self.respond(400, b"Invalid path.\n", head=head)
        if any(part in (".", "..") for part in path.split("/")):
            return self.respond(403, b"Traversal is forbidden.\n", head=head)
        if path == "/qa":
            return self.respond(308, b"Use /qa/.\n", {"Location": "/qa/"}, head)
        if path == "/sdk.js":
            base, relative = self.qa_root, "offline-sdk.js"
        elif path.startswith("/qa/"):
            base, relative = self.qa_root, path[4:]
        else:
            base, relative = self.web_root, path[1:]
        base = base.resolve()
        candidate = (base / relative).resolve()
        if not candidate.is_relative_to(base):
            return self.respond(403, b"Traversal is forbidden.\n", head=head)
        if candidate.is_dir():
            candidate = (candidate / "index.html").resolve()
            if not candidate.is_relative_to(base):
                return self.respond(403, b"Traversal is forbidden.\n", head=head)
        if not candidate.is_file():
            if base == self.web_root.resolve() and not (base / "index.html").is_file():
                return self.respond(503, b"The real Unity WebGL build is not ready yet.\n", head=head)
            return self.respond(404, b"File not found; directory listing is disabled.\n", head=head)
        # Explicit .br/.gz files use HTTP decoding. Unity .unityweb uses its own
        # decompression fallback and MUST NOT acquire Content-Encoding here.
        name = candidate.name
        encoding = None
        if name.endswith(".br"):
            name, encoding = name[:-3], "br"
        elif name.endswith(".gz"):
            name, encoding = name[:-3], "gzip"
        if name.endswith(".unityweb"):
            name = name[:-9]
        mime = {
            ".wasm": "application/wasm", ".data": "application/octet-stream",
            ".js": "application/javascript", ".json": "application/json",
            ".html": "text/html; charset=utf-8", ".css": "text/css; charset=utf-8",
            ".svg": "image/svg+xml", ".png": "image/png", ".wav": "audio/wav",
        }.get(Path(name).suffix.lower()) or mimetypes.guess_type(name)[0] or "application/octet-stream"
        try:
            with candidate.open("rb") as stream:
                self.send_response(200)
                self.send_header("Content-Type", mime)
                self.send_header("Content-Length", str(candidate.stat().st_size))
                if encoding:
                    self.send_header("Content-Encoding", encoding)
                self.end_headers()
                if not head:
                    shutil.copyfileobj(stream, self.wfile)
        except (BrokenPipeError, ConnectionResetError):
            pass  # Browser cancelled a build/asset download.

    def log_message(self, format, *args):
        print(json.dumps({"event": "http", "client": self.client_address[0], "message": format % args}), flush=True)


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Loopback-only WebGL preview; no real account, ads, purchases or ranking")
    parser.add_argument("--build", default=str(WEB_ROOT))
    parser.add_argument("--port", type=int, default=PORT)
    args = parser.parse_args()
    PreviewHandler.web_root = Path(args.build).resolve()
    with ThreadingHTTPServer((BIND, args.port), PreviewHandler) as server:
        server.daemon_threads = True
        print(json.dumps({"event": "ready", "url": f"http://{BIND}:{args.port}/",
                          "qa": f"http://{BIND}:{args.port}/",
                          "buildRoot": str(PreviewHandler.web_root), "buildReady": (PreviewHandler.web_root / "index.html").is_file(),
                          "sdk": "localhost-only offline adapter; production SDK disabled"}), flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
