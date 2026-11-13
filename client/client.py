#!/usr/bin/env python3
"""Bazar.com command-line client (Python standard library only).

Usage:
    python3 client/client.py search <topic>
    python3 client/client.py info <id>
    python3 client/client.py purchase <id>
    python3 client/client.py restock <id> <n>     # talks to the catalog service directly
    python3 client/client.py demo                 # scripted end-to-end demonstration

Base URLs (flags or environment variables):
    --frontend / FRONTEND_URL   default http://localhost:5000
    --catalog  / CATALOG_URL    default http://localhost:4000   (restock and demo checks only)
"""
import argparse
import json
import os
import sys
import threading
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime

FRONTEND = "http://localhost:5000"
CATALOG = "http://localhost:4000"


def log(msg):
    """Timestamped client-side log line."""
    print(f"[{datetime.now().strftime('%H:%M:%S.%f')[:-3]}] {msg}", flush=True)


def call(method, url):
    """Send a request without a body and return (status_code, parsed_json_or_None).

    HTTP error statuses are returned (not raised); connection problems return (None, None).
    """
    return call_json(method, url, None)


def call_json(method, url, payload):
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", "application/json")
    elif method == "POST":
        req.add_header("Content-Length", "0")
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            status, body = resp.status, resp.read()
    except urllib.error.HTTPError as e:
        status, body = e.code, e.read()
    except (urllib.error.URLError, OSError) as e:
        log(f"ERROR: cannot reach {url}: {e}")
        return None, None
    try:
        return status, json.loads(body) if body else None
    except ValueError:
        return status, {"message": body.decode(errors="replace")}


# ---------------------------------------------------------------- commands

def cmd_search(topic):
    log(f"GET {FRONTEND}/search/{topic}")
    status, body = call("GET", f"{FRONTEND}/search/{urllib.parse.quote(topic)}")
    if status != 200:
        print(f"  search failed (HTTP {status}): {body}")
        return False
    print(f"\n  Books about '{topic}':")
    if not body:
        print("    (none)")
    else:
        print(f"    {'ID':<4} Title")
        print(f"    {'--':<4} {'-' * 50}")
        for b in body:
            print(f"    {b['id']:<4} {b['title']}")
    print()
    return True


def cmd_info(item):
    log(f"GET {FRONTEND}/info/{item}")
    status, body = call("GET", f"{FRONTEND}/info/{item}")
    if status != 200:
        print(f"  info failed (HTTP {status}): {body.get('message') if body else ''}\n")
        return False
    print(f"\n  Book #{body['id']}")
    print(f"    Title   : {body['title']}")
    print(f"    Topic   : {body['topic']}")
    print(f"    Price   : ${body['price']}")
    print(f"    In stock: {body['quantity']}\n")
    return True


def cmd_purchase(item, quiet=False):
    """Returns True on success. quiet=True suppresses per-call logging (used by threads)."""
    if not quiet:
        log(f"POST {FRONTEND}/purchase/{item}")
    status, body = call("POST", f"{FRONTEND}/purchase/{item}")
    if status == 200:
        print(f"  bought book {body['title']}  (order #{body['orderId']})", flush=True)
        return True
    msg = body.get("message") if isinstance(body, dict) else body
    print(f"  purchase of item {item} FAILED (HTTP {status}): {msg}", flush=True)
    return False


def cmd_restock(item, n):
    log(f"PUT {CATALOG}/update/{item}  delta=+{n}")
    status, body = call_json("PUT", f"{CATALOG}/update/{item}", {"delta": int(n)})
    if status != 200:
        print(f"  restock failed (HTTP {status}): {body}\n")
        return False
    print(f"  restocked '{body['title']}': stock is now {body['quantity']}\n")
    return True


# ---------------------------------------------------------------- demo

def stock_of(item):
    status, body = call("GET", f"{FRONTEND}/info/{item}")
    return body["quantity"] if status == 200 else None


def banner(text):
    print("\n" + "=" * 70 + f"\n  {text}\n" + "=" * 70)


def cmd_demo():
    banner("1. Search by topic")
    cmd_search("distributed systems")
    cmd_search("undergraduate school")

    banner("2. Info on every book")
    for i in (1, 2, 3, 4):
        cmd_info(i)

    banner("3. Purchases")
    cmd_purchase(2)
    cmd_purchase(2)
    cmd_purchase(3)
    cmd_info(2)
    log("Purchasing an unknown item (expect 404):")
    cmd_purchase(99)

    banner("4. Out-of-stock scenario")
    # Make book 4 have exactly 2 copies, then buy 3 times: the third must fail.
    current = stock_of(4)
    status, _ = call_json("PUT", f"{CATALOG}/update/4", {"quantity": 2})
    log(f"Set stock of book 4 to 2 via catalog (was {current}, HTTP {status})")
    for _ in range(3):
        cmd_purchase(4)
    cmd_info(4)

    banner("5. Restock")
    cmd_restock(4, 5)
    cmd_purchase(4)
    cmd_info(4)

    banner("6. Concurrent purchases (no overselling)")
    item, available, workers = 1, 5, 20
    call_json("PUT", f"{CATALOG}/update/{item}", {"quantity": available})
    log(f"Stock of book {item} set to {available}; launching {workers} simultaneous purchases")
    results = []
    lock = threading.Lock()
    start = threading.Barrier(workers)

    def worker():
        start.wait()  # release all threads at the same moment
        ok = cmd_purchase(item, quiet=True)
        with lock:
            results.append(ok)

    threads = [threading.Thread(target=worker) for _ in range(workers)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    successes = sum(results)
    final = stock_of(item)
    print(f"\n  requests sent    : {workers}")
    print(f"  stock available  : {available}")
    print(f"  successful buys  : {successes}")
    print(f"  failed (no stock): {workers - successes}")
    print(f"  final stock      : {final}")
    ok = successes == available and final == 0
    print(f"  RESULT           : {'PASS - no overselling, stock never negative' if ok else 'FAIL'}\n")
    return ok


def main():
    global FRONTEND, CATALOG
    p = argparse.ArgumentParser(description="Bazar.com client")
    p.add_argument("--frontend", default=os.environ.get("FRONTEND_URL", FRONTEND))
    p.add_argument("--catalog", default=os.environ.get("CATALOG_URL", CATALOG))
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("search").add_argument("topic")
    sub.add_parser("info").add_argument("id")
    sub.add_parser("purchase").add_argument("id")
    r = sub.add_parser("restock")
    r.add_argument("id")
    r.add_argument("n")
    sub.add_parser("demo")
    a = p.parse_args()

    FRONTEND, CATALOG = a.frontend.rstrip("/"), a.catalog.rstrip("/")
    if a.cmd == "search":
        ok = cmd_search(a.topic)
    elif a.cmd == "info":
        ok = cmd_info(a.id)
    elif a.cmd == "purchase":
        ok = cmd_purchase(a.id)
    elif a.cmd == "restock":
        ok = cmd_restock(a.id, a.n)
    else:
        ok = cmd_demo()
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
