"""Temporary, fixed-purpose bundle/report transfer on the Hyper-V private network."""
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from pathlib import Path
import re
import sys

root = Path(sys.argv[1]).resolve()
reports = root / "vm-results"
reports.mkdir(exist_ok=True)

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path != "/FlexComVmTests.zip":
            self.send_error(404)
            return
        bundle = root / "FlexComVmTests.zip"
        self.send_response(200)
        self.send_header("Content-Type", "application/zip")
        self.send_header("Content-Disposition", 'attachment; filename="FlexComVmTests.zip"')
        self.send_header("Content-Length", str(bundle.stat().st_size))
        self.end_headers()
        with bundle.open("rb") as source:
            while chunk := source.read(1024 * 1024):
                self.wfile.write(chunk)

    def do_POST(self):
        name = self.path.removeprefix("/report/")
        if self.client_address[0] != sys.argv[3] or not re.fullmatch(r"[a-zA-Z0-9-]+\.(txt|json|log)", name):
            self.send_error(403)
            return
        length = int(self.headers.get("Content-Length", "0"))
        if not 0 <= length <= 10 * 1024 * 1024:
            self.send_error(413)
            return
        with (reports / name).open("ab" if name == "events.log" else "wb") as output:
            output.write(self.rfile.read(length))
        self.send_response(200)
        self.end_headers()

ThreadingHTTPServer((sys.argv[2], 8765), Handler).serve_forever()
