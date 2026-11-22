#!/usr/bin/env python3
"""Read-only live smoke test for Azure XML pilot. Never echo credentials or account data."""
import json
import os
import subprocess
import urllib.error
import urllib.parse
import urllib.request
import http.cookiejar

RESOURCE_GROUP = "badritradersPOC"
APP = "badritraders"
ROOT = "https://badritraders-hga6f6amdubqbhfb.southindia-01.azurewebsites.net"
ACCOUNTS = [
    ("OWNER", "owner", "Owner"),
    ("SALESMAN", "salesperson", "Salesperson"),
    ("RETAILER", "retailer", "Retailer"),
]

def call(path, method="GET", data=None, opener=None):
    hdr = {"Content-Type": "application/json"} if data is not None else {}
    req = urllib.request.Request(
        ROOT + path, data=json.dumps(data).encode("utf-8") if data is not None else None,
        headers=hdr, method=method)
    opener = opener or urllib.request.build_opener()
    with opener.open(req, timeout=30) as response:
        raw = response.read(150000)
        return response.status, (json.loads(raw) if raw else {})

code, health = call("/health")
assert code == 200 and health.get("version") == "pilot-r2", "Unexpected /health response"
print("PASS: Azure /health returned Badri XML pilot-r2.")

try:
    call("/api/state")
    raise AssertionError("Anonymous financial API returned success")
except urllib.error.HTTPError as error:
    assert error.code == 401, f"Expected HTTP 401, got {error.code}"
print("PASS: Anonymous financial API denied (HTTP 401).")

# CLI query result is kept inside Python process memory; not printed, logged or exported.
res = subprocess.run(
    ["az", "webapp", "config", "appsettings", "list", "-g", RESOURCE_GROUP, "-n", APP,
     "--query", "[].{name:name,value:value}", "-o", "json"],
    check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
config = {row["name"]: row.get("value") or "" for row in json.loads(res.stdout)}
assert config.get("BADRI_XML_PATH") == "/home/badri-data/tradeflow.xml"
for suffix, username, expected_role in ACCOUNTS:
    secret = config.get("BADRI_PASS_" + suffix)
    assert secret and len(secret) >= 14, "Missing/weak Azure account secret: " + suffix
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    status, session = call("/api/auth/login", "POST", {"username": username,"password": secret}, opener)
    assert status == 200 and session.get("role") == expected_role, f"Role login failed: {expected_role}"
    status, verified = call("/api/auth/session", opener=opener)
    assert status == 200 and verified.get("role") == expected_role, f"Session failed: {expected_role}"
    status, state = call("/api/state", opener=opener)
    assert status == 200 and state.get("role") == expected_role, f"Role dashboard failed: {expected_role}"
    if expected_role == "Retailer":
        assert "suppliers" not in state and "purchases" not in state, "Retailer must not see supplier data"
        assert all(x.get("retailer") == "Kumar Maligai" for x in state.get("orders", []))
    if expected_role == "Salesperson":
        assert "supplierPayables" not in state.get("totals", {}), "Salesperson should not see supplier totals"
    print(f"PASS: {expected_role} login, session and role dashboard.")

print("PASS: three independent roles authenticated. No changes to financial records.")
