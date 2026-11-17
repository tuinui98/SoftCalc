"""serve.py - Pack the engine and serve the web spike on this computer.

The page needs a web server: a browser does not start a module worker
from a file:// page. This rebuilds engine.zip, checks that Pyodide is
installed (npm install), serves web/ and opens the page:

    python web/serve.py          # http://127.0.0.1:8765/
    python web/serve.py --lan    # also reachable from a phone on the same Wi-Fi

Stop with Ctrl+C.
"""

import argparse
import functools
import http.server
import pathlib
import socket
import sys
import webbrowser

import make_engine

WEB = pathlib.Path(__file__).resolve().parent
PORT = 8765


def lan_address():
    """The address other devices on the network reach this computer at."""
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
        probe.connect(("192.0.2.1", 9))    # no packet is sent; it picks the outgoing interface
        return probe.getsockname()[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--lan", action="store_true", help="listen on the local network too")
    parser.add_argument("--no-open", action="store_true", help="do not open the browser")
    args = parser.parse_args()
    if not (WEB / "node_modules" / "pyodide" / "pyodide.mjs").exists():
        sys.exit("Pyodide is missing: run \"npm install\" in web/ first.")
    make_engine.main()
    host = "0.0.0.0" if args.lan else "127.0.0.1"
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(WEB))
    server = http.server.ThreadingHTTPServer((host, PORT), handler)
    url = f"http://127.0.0.1:{PORT}/"
    print(f"serving {WEB} at {url}")
    if args.lan:
        print(f"on the phone: http://{lan_address()}:{PORT}/")
    if not args.no_open:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("stopped")


if __name__ == "__main__":
    main()
