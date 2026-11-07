#!/usr/bin/env python3
"""Isolated Unity 6.6 preview. SDK adapter is served only on loopback, outside the build."""
from pathlib import Path
from http.server import ThreadingHTTPServer
from urllib.parse import urlsplit, parse_qs
import argparse
import importlib.util

PROJECT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("existing_preview", PROJECT / "builds/Stage2QA/local-preview-server.py")
preview = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preview)


class Handler(preview.PreviewHandler):
    web_root = PROJECT / "builds/Unity66/Yandex"
    qa_root = PROJECT / "builds/Stage2QA"
    official_dev = False

    def serve_file(self, head):
        route = urlsplit(self.path).path
        if self.official_dev and route in ("/", "/index.html") and parse_qs(urlsplit(self.path).query).get("migrationQa") == ["1"]:
            # The official dev adapter's on()/dispatchEvent() are no-ops. These explicit
            # QA controls invoke the production template callbacks, never a replacement
            # pause implementation. They are served only here and never enter the ZIP.
            html = (self.web_root / "index.html").read_text()
            controls = '''<aside style="position:fixed;bottom:2px;left:2px;z-index:20000;background:white;color:black;padding:4px;font:12px sans-serif">
              <span>Migration QA · synthetic platform callbacks</span>
              <button onclick="PauseCallback();document.getElementById('migration-qa-status').textContent='SDK pause callback dispatched'">SDK pause</button>
              <button onclick="ResumeCallback();document.getElementById('migration-qa-status').textContent='SDK resume callback dispatched'">SDK resume</button>
              <span id="migration-qa-status" role="status"></span></aside>'''
            data = html.replace("</body>", controls + "</body>").encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            if not head:
                self.wfile.write(data)
            return
        if self.official_dev and route in ("/sdk.js", "/purchases-catalog.json"):
            host = urlsplit("//" + self.headers.get("Host", "")).hostname
            if host not in ("localhost", "127.0.0.1", "::1"):
                return self.respond(403, b"Loopback hosts only", head=head)
            filename = "sdk.js" if route == "/sdk.js" else "purchases-catalog.json"
            data = (PROJECT / "Migration/Unity66/YandexDev" / filename).read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", "application/javascript" if filename.endswith(".js") else "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            if not head:
                self.wfile.write(data)
            return
        super().serve_file(head)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--official-dev", action="store_true")
    args = parser.parse_args()
    Handler.official_dev = args.official_dev
    port = 8753 if args.official_dev else 8751
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"Unity 6.6 preview: http://localhost:{port}/ ; separate origin/profile; official Yandex dev mocks={args.official_dev}", flush=True)
    server.serve_forever()
