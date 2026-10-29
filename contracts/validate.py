"""Check examples.json against messages.schema.json. Run: python3 validate.py"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

here = Path(__file__).resolve().parent
schema = json.loads((here / "messages.schema.json").read_text())
examples = json.loads((here / "examples.json").read_text())

Draft202012Validator.check_schema(schema)
validator = Draft202012Validator(schema)

failures = 0
for msg in examples["valid"]:
    errors = list(validator.iter_errors(msg))
    if errors:
        failures += 1
        print("SHOULD PASS but failed:", msg["type"], msg["id"])
        for e in errors[:3]:
            print("   ", e.message[:160])

for case in examples["invalid"]:
    if validator.is_valid(case["message"]):
        failures += 1
        print("SHOULD FAIL but passed:", case["reason"])

topics = json.loads((here / "topics.json").read_text())["topics"]
schema_ids = set(schema["$defs"]["topicId"]["enum"])
file_ids = {t["id"] for t in topics}
if schema_ids != file_ids:
    failures += 1
    print("topics.json and schema topicId differ:", schema_ids ^ file_ids)

print(
    f"{len(examples['valid'])} valid and {len(examples['invalid'])} invalid examples checked, "
    f"{len(topics)} topics; {failures} problem(s)."
)
sys.exit(1 if failures else 0)
