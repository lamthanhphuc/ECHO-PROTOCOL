"""Verify the exact same policy bundle ships in API, Unity and Web. No files are changed."""
from pathlib import Path
import hashlib
import json
import sys
root = Path(__file__).resolve().parent.parent
paths = ["EchoProtocol.Backend/src/EchoProtocol.Api/Legal/policies.json", "KLTN/Assets/Resources/Legal/policies.json", "EchoProtocol.Web/src/content/policies.json"]
contents = [(root / path).read_text(encoding="utf-8-sig").replace("\r\n", "\n").encode("utf-8") for path in paths]
if len(set(contents)) != 1:
    sys.exit("Policy bundles differ. Update all three from the reviewed source before building.")
bundle = json.loads(contents[0])
assert {doc["id"] for doc in bundle["documents"]} == {"terms", "privacy", "community", "transactions"}
assert all(doc["bodyVi"].strip() and doc["bodyEn"].strip() and doc["version"] for doc in bundle["documents"])
hash_value = hashlib.sha256(contents[0]).hexdigest()
if hash_value not in (root / "EchoProtocol.Web/src/app/legal/page.tsx").read_text(encoding="utf-8"):
    sys.exit("Web offline policy hash is stale.")
print("Policy bundle consistent:", bundle["bundleVersion"], hash_value)
