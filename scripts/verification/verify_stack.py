#!/usr/bin/env python3
"""
End-to-end verification of the Docker Compose stack through the load balancer (default http://localhost:8080).

Only the Python standard library and the `docker` CLI are needed. The scripts never read `.env`: the Stripe test key
is used by the Subscriptions container, not by this script.

Modes (they can be combined; `all` runs the three):
  base       Routes, contract of the gateway, ownership and webhook rejection. Read-only, safe to repeat.
  isolation  QAS-09: stops the Subscriptions container and checks that the core keeps answering. Starts it again.
  replicas   Stops one core replica and then one gateway replica, measures what a client sees, and starts them again.

Usage:
  python scripts/verification/verify_stack.py base
  python scripts/verification/verify_stack.py all --report reports/verification.json

The test users (`ganadero`, `maria`) and their password come from the Development seed of the core. The run needs the
stack started with `docker compose up -d` in Development mode.
"""
import argparse
import json
import os
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request

BASE = os.environ.get("ANITEC_BASE_URL", "http://localhost:8080")
PASSWORD = os.environ.get("ANITEC_SEED_PASSWORD", "anitec123")
COMPOSE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

results = []   # (name, passed, detail)
measures = {}  # name -> dict with numbers measured in this run


def call(method, path, token=None, body=None, headers=None, timeout=15):
    """Returns (status, parsed body or raw text, response headers, seconds). Status 0 means no HTTP answer at all."""
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, method=method, data=data)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            raw = r.read().decode()
            status, hdrs = r.status, dict(r.headers)
    except urllib.error.HTTPError as e:
        raw, status, hdrs = e.read().decode(), e.code, dict(e.headers)
    except Exception as e:  # connection refused, timeout...
        return 0, str(e), {}, time.perf_counter() - started
    elapsed = time.perf_counter() - started
    try:
        return status, json.loads(raw) if raw else None, hdrs, elapsed
    except ValueError:
        return status, raw, hdrs, elapsed


def check(name, cond, detail=""):
    results.append((name, bool(cond), detail))
    print(("OK   " if cond else "FAIL ") + name + (f"  [{detail}]" if detail else ""), flush=True)


def docker(*args):
    return subprocess.run(["docker", "compose", *args], cwd=COMPOSE_DIR, capture_output=True, text=True)


def service_health(service):
    """Health text of a compose service from `docker inspect`, '' when it is not running."""
    ps = docker("ps", "-q", service).stdout.strip()
    if not ps:
        return ""
    out = subprocess.run(["docker", "inspect", "-f", "{{.State.Status}}/{{if .State.Health}}{{.State.Health.Status}}{{end}}", ps],
                         capture_output=True, text=True).stdout.strip()
    return out


def wait_healthy(service, seconds=90):
    end = time.time() + seconds
    while time.time() < end:
        if service_health(service) == "running/healthy":
            return True
        time.sleep(1)
    return False


def wait_status(path, token, expected, seconds=60):
    """Waits until a route answers the expected status. Returns the seconds it took, or None."""
    started = time.time()
    while time.time() - started < seconds:
        s = call("GET", path, token, timeout=5)[0]
        if s == expected:
            return time.time() - started
        time.sleep(0.5)
    return None


def login(user):
    s, d, _, _ = call("POST", "/api/v1/authentication/sign-in", body={"username": user, "password": PASSWORD})
    if s != 200 or not isinstance(d, dict):
        sys.exit(f"Cannot sign in as '{user}' (status {s}). Is the stack running in Development mode?")
    return d["token"], d["id"]


def burst(path, token, count, pause=0.05):
    """Sequential requests; returns a list of (status, seconds)."""
    out = []
    for _ in range(count):
        s, _, _, t = call("GET", path, token, timeout=15)
        out.append((s, t))
        time.sleep(pause)
    return out


def summarize(samples):
    codes = {}
    for s, _ in samples:
        codes[s] = codes.get(s, 0) + 1
    times = sorted(t for _, t in samples)
    return {
        "requests": len(samples),
        "statuses": {str(k): v for k, v in sorted(codes.items())},
        "failed": sum(v for k, v in codes.items() if k != 200),
        "median_ms": round(statistics.median(times) * 1000, 1),
        "p95_ms": round(times[int(len(times) * 0.95) - 1] * 1000, 1) if len(times) >= 20 else None,
        "max_ms": round(times[-1] * 1000, 1),
    }


# ----------------------------------------------------------------------------------------------------------------
def mode_base():
    print("\n== base: routes, contract of the gateway, ownership, webhook ==")
    tg, idg = login("ganadero")
    tm, idm = login("maria")
    check("two users sign in through lb -> gateway -> core", idg != idm, f"ids {idg}, {idm}")

    s, _, _, _ = call("GET", "/health")
    check("/health through the load balancer -> 200", s == 200, str(s))

    s, d, _, _ = call("GET", "/gateway/status")
    healthy = s == 200 and all(x["health"] == "Healthy" for c in d["clusters"] for x in c["destinations"])
    check("/gateway/status lists every destination as Healthy", healthy, str(s))

    s, plans, h, _ = call("GET", "/api/v1/subscription-plans", tg)
    prices = sorted(p["price"] for p in plans) if s == 200 else []
    check("plans with token -> 200 and the 3 seeded plans (49, 99, 129 PEN)", prices[:3] == [49, 99, 129], f"{s}, {prices}")
    check("the answer carries X-Correlation-Id", bool(h.get("X-Correlation-Id")))

    s, _, h, _ = call("GET", "/api/v1/animals", tg, headers={"X-Correlation-Id": "verify-0001"})
    check("a well-formed correlation id sent by the client is kept", h.get("X-Correlation-Id") == "verify-0001", str(h.get("X-Correlation-Id")))

    s, d, _, _ = call("GET", "/api/v2/animals", tg)
    check("unsupported API version -> 400 with a documented error", s == 400, str(s))

    s, _, _, _ = call("GET", "/api/v1/subscription-plans")
    check("plans without token -> 401 (answer of Subscriptions)", s == 401, str(s))

    s, _, _, _ = call("GET", "/api/v1/herds", tg)
    check("core route through the gateway (herds) -> 200", s == 200, str(s))

    s, _, _, _ = call("GET", f"/api/v1/subscriptions/users/{idg}/active", tm)
    check("another user's active subscription -> 404 (QAS-08)", s == 404, str(s))
    s, _, _, _ = call("GET", f"/api/v1/subscriptions/users/{idg}/payments", tm)
    check("another user's payments -> 404 (QAS-08)", s == 404, str(s))
    s, _, _, _ = call("GET", f"/api/v1/subscriptions/users/{idg}/payments", tg)
    check("own payments -> 200", s == 200, str(s))

    s, d, _, _ = call("POST", "/api/v1/subscriptions/stripe/webhook", None, {"id": "evt_fake", "type": "x"})
    check("webhook without a valid signature -> 400", s == 400, str(s))
    s, d, _, _ = call("POST", "/api/v1/subscriptions/stripe/webhook", None, {"id": "evt_fake", "type": "x"},
                      headers={"Stripe-Signature": "t=1,v1=00"})
    check("webhook with a wrong signature -> 400", s == 400, str(s))

    owners = set()
    for _ in range(12):
        owners.add(call("GET", "/health")[2].get("X-Upstream"))
    check("the load balancer alternates between the two gateways", len(owners) == 2, ", ".join(sorted(o for o in owners if o)))


def mode_isolation():
    print("\n== isolation (QAS-09): Subscriptions stopped, core must keep answering ==")
    tg, idg = login("ganadero")
    plans = "/api/v1/subscription-plans"
    core = "/api/v1/herds"

    check("before: both services answer 200", call("GET", plans, tg)[0] == 200 and call("GET", core, tg)[0] == 200)

    docker("stop", "subscriptions")
    try:
        # Give the active health check (every 5 s) a chance to notice; the first requests are part of the measurement.
        during_core, during_subs, health = [], [], []
        for _ in range(60):
            s, _, _, t = call("GET", core, tg)
            during_core.append((s, t))
            s2, d2, h2, t2 = call("GET", plans, tg)
            during_subs.append((s2, t2, d2))
            health.append(call("GET", "/health")[0])
            time.sleep(0.1)
        core_sum = summarize(during_core)
        check("core routes keep answering 200 while Subscriptions is down (60 of 60)", core_sum["failed"] == 0, json.dumps(core_sum))
        check("/health keeps answering 200", all(c == 200 for c in health), f"{health.count(200)} of {len(health)}")
        statuses = {s for s, _, _ in during_subs}
        check("Subscriptions routes answer a controlled 502/503, never an unhandled error", statuses <= {502, 503}, str(sorted(statuses)))
        last = during_subs[-1]
        check("the error is a JSON problem that names the service", isinstance(last[2], dict) and last[2].get("service") == "subscriptions", str(last[2])[:120])
        check("the error carries a correlation id", bool(call("GET", plans, tg)[2].get("X-Correlation-Id")))
        settled = [t for s, t, _ in during_subs[-30:]]
        measures["isolation"] = {
            "core_during_outage": core_sum,
            "subscriptions_error_median_ms_after_detection": round(statistics.median(settled) * 1000, 1),
            "subscriptions_error_max_ms_first_30": round(max(t for _, t, _ in during_subs[:30]) * 1000, 1),
        }
    finally:
        docker("start", "subscriptions")
    ok = wait_healthy("subscriptions", 90)
    check("Subscriptions container is healthy again", ok)
    took = wait_status(plans, tg, 200, 60)
    check("the plans route answers 200 again through the gateway", took is not None, f"{took:.1f} s after the container was healthy" if took is not None else "no recovery in 60 s")
    if took is not None:
        measures.setdefault("isolation", {})["recovery_seconds_after_healthy"] = round(took, 1)


def mode_replicas():
    print("\n== replicas: one core replica and one gateway replica fail ==")
    tg, idg = login("ganadero")
    path = "/api/v1/herds"

    base = summarize(burst(path, tg, 40))
    check("baseline: 40 of 40 requests 200", base["failed"] == 0, json.dumps(base))
    measures["baseline"] = base

    # ---- one core replica
    docker("stop", "api1")
    try:
        samples = burst(path, tg, 150, pause=0.1)
        s = summarize(samples)
        first_fail = next((i for i, (st, _) in enumerate(samples) if st != 200), None)
        check("core replica api1 stopped: requests keep succeeding", s["failed"] <= 3,
              f"failed {s['failed']} of {s['requests']}, first failure at request {first_fail}")
        measures["one_core_replica_down"] = {**s, "first_failure_at_request": first_fail}
        status = call("GET", "/gateway/status")[1]
        api1 = next(x for c in status["clusters"] if c["cluster"] == "core" for x in c["destinations"] if x["destination"] == "api1")
        check("the gateway reports api1 as Unhealthy", api1["health"] == "Unhealthy", json.dumps(api1))
    finally:
        docker("start", "api1")
    check("api1 healthy again", wait_healthy("api1", 120))
    time.sleep(6)  # one active health-check interval
    api1 = next(x for c in call("GET", "/gateway/status")[1]["clusters"] if c["cluster"] == "core" for x in c["destinations"] if x["destination"] == "api1")
    check("the gateway puts api1 back in rotation", api1["health"] == "Healthy", json.dumps(api1))

    # ---- one gateway replica (nginx restarts are NOT needed here: the IPs do not change when a container is stopped)
    docker("stop", "gateway1")
    try:
        samples = burst(path, tg, 100, pause=0.1)
        s = summarize(samples)
        check("gateway replica gateway1 stopped: requests keep succeeding through gateway2", s["failed"] == 0, json.dumps(s))
        measures["one_gateway_replica_down"] = s
    finally:
        docker("start", "gateway1")
    check("gateway1 healthy again", wait_healthy("gateway1", 90))
    # After a connection error nginx keeps that gateway out of the rotation for its fail_timeout (10 s by default), so the
    # return is not immediate. This measures how long it takes. (A RECREATED container can get a new IP; nginx resolves
    # names only at startup, so that case needs `docker compose restart lb`, see README.)
    started = time.time()
    owners = set()
    while time.time() - started < 40 and len(owners) < 2:
        owners = {call("GET", "/health")[2].get("X-Upstream") for _ in range(6)} - {None}
        time.sleep(1)
    back = time.time() - started
    check("both gateways answer again through the load balancer", len(owners) == 2,
          f"{back:.0f} s after gateway1 was healthy; " + ", ".join(sorted(owners)))
    measures["one_gateway_replica_down"]["seconds_until_back_in_nginx_rotation"] = round(back)


# ----------------------------------------------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("modes", nargs="+", choices=["base", "isolation", "replicas", "all"])
    ap.add_argument("--report", help="write the results and measurements to this JSON file")
    args = ap.parse_args()
    modes = ["base", "isolation", "replicas"] if "all" in args.modes else args.modes

    for m in modes:
        {"base": mode_base, "isolation": mode_isolation, "replicas": mode_replicas}[m]()

    passed = sum(1 for _, ok, _ in results if ok)
    print(f"\n{passed} of {len(results)} checks passed")
    if measures:
        print(json.dumps(measures, indent=2))
    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, "w", encoding="utf-8") as f:
            json.dump({"base_url": BASE, "checks": [{"name": n, "passed": ok, "detail": d} for n, ok, d in results],
                       "measures": measures}, f, indent=2)
    sys.exit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
