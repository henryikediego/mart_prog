"""Validate contract examples and cross-file topic/activity identifiers."""
import json
import re
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

here = Path(__file__).resolve().parent
schema = json.loads((here / "messages.schema.json").read_text())
examples = json.loads((here / "examples.json").read_text())
topics_document = json.loads((here / "topics.json").read_text())
topics = topics_document["topics"]

Draft202012Validator.check_schema(schema)
schema_validator = Draft202012Validator(schema)
failures = 0

schema_topic_ids = set(schema["$defs"]["topicId"]["enum"])
topic_ids = [topic["id"] for topic in topics]
if schema_topic_ids != set(topic_ids):
    failures += 1
    print("topics.json and schema topicId differ:", schema_topic_ids ^ set(topic_ids))
if len(topic_ids) != len(set(topic_ids)):
    failures += 1
    print("topics.json contains duplicate topic ids")

activity_ids_by_topic = {}
for topic in topics:
    activities = topic.get("activities", [])
    if not isinstance(activities, list):
        failures += 1
        print("activities must be a list for topic:", topic["id"])
        continue
    activity_ids = []
    for activity in activities:
        if not isinstance(activity, dict) or not {"id", "title"} <= activity.keys():
            failures += 1
            print("activity must have id and title for topic:", topic["id"])
            continue
        activity_id = activity["id"]
        if not isinstance(activity_id, str):
            failures += 1
            print("activity id must be a string for topic:", topic["id"])
            continue
        if not re.fullmatch(r"[a-z0-9]+(?:_[a-z0-9]+)*", activity_id):
            failures += 1
            print("activity id must be lowercase snake_case for topic:", topic["id"])
        activity_ids.append(activity_id)
    if len(activity_ids) != len(set(activity_ids)):
        failures += 1
        print("duplicate activity ids for topic:", topic["id"])
    activity_ids_by_topic[topic["id"]] = set(activity_ids)


def is_valid_contract_message(message):
    if not schema_validator.is_valid(message):
        return False
    if message["type"] == "topic.open":
        payload = message["payload"]
        activity_id = payload.get("activityId")
        if activity_id is not None:
            allowed = activity_ids_by_topic.get(payload["topicId"], set())
            return activity_id in allowed
    return True


for message in examples["valid"]:
    if not is_valid_contract_message(message):
        failures += 1
        print("SHOULD PASS but failed:", message["type"], message["id"])

for case in examples["invalid"]:
    if is_valid_contract_message(case["message"]):
        failures += 1
        print("SHOULD FAIL but passed:", case["reason"])

print(
    f"{len(examples['valid'])} valid and {len(examples['invalid'])} invalid examples checked, "
    f"{len(topics)} topics; {failures} problem(s)."
)
sys.exit(1 if failures else 0)
