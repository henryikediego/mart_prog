# Loci shell–Unity contract v1

This document records the v1 decisions shared by the React Native shell and the Unity AR experience. The machine-readable message schema is in `messages.schema.json`; `topics.json` is the source of truth for topic order, log names, and topic-specific activity IDs. `examples.json` contains valid and invalid messages.

## Ownership and lifecycle

- The shell owns app chrome and the AR-flow screens: camera primer/permission, scanning and placement instructions, tracking-loss guidance, and user-facing error screens. Unity owns the AR session, anchored 3D scene, and topic controls rendered in AR.
- The shell sends `session.start` only after camera permission is granted and the learner opens the AR module. It sends `session.stop` when leaving the AR flow.
- The shell owns module order, quizzes, and progress stored per Study ID. Unity's prior quiz and module-order implementation is retired in the redesigned flow.
- Unity sends `unity.ready` with `arAvailability: "supported"` when the device's ARCore availability check reports support.

## Study and product modes

The shell writes research-log rows only in Study Mode. Product Mode writes no research-log rows. In Study Mode, the shell owns CSV output and supplies the timestamp, version, Study ID, group, session ID, and module log name. Unity supplies learner actions through `topic.event` (`action`, optional `value`, `result`, and `details`). The shell also records app/session lifecycle, quiz, and researcher-area rows in Study Mode.

A session begins when the learner opens an AR module. The shell obtains camera permission before starting Unity.

## Topic activities

`activityId` is optional on `topic.open`. If present, it must be listed under that topic in `topics.json`. The IDs are stable lowercase snake_case forms of the existing Unity labels:

- **Selection:** `basic_if` (Basic If), `else_if_ladder` (Else-if Ladder), `nested_if` (Nested If).
- **Memory:** `overview`, `variables`, `data_types`, `address_layout`, `memory_access`, `loop_memory`, `arrays`, `functions`, `selection`, `scope`, `remove_variable`, `trace_exercise`, `debug_exercise`, `final_assessment`.

The study order retains Linked List as topic 4.

## Display settings

The shell maps its text-size choices to the values already used by the Unity project:

| Shell label | `textScale` |
| --- | ---: |
| Normal | 1.0 |
| Large | 1.15 |
| Extra Large | 1.3 |

## Compatibility behavior

At runtime, a receiver ignores and logs an unknown message type or unknown field so an added field does not crash an older build. The JSON Schema and examples are intentionally stricter for contract review: a message with an unknown type or field is invalid for v1 and fails validation. Optional fields and new message types still require a contract change reviewed by both developers; a breaking change increments `v`.

## Error codes

| Code | Meaning |
| --- | --- |
| E-101 | Camera permission denied. |
| E-102 | Device does not support AR. |
| E-103 | ARCore must be installed or updated. |
| E-104 | Unexpected Unity failure, Unity startup timeout/no `unity.ready`, or a player exit detected by the shell. |
| E-105 | A topic failed to load. |
| E-106 | `topic.open` arrived before a placement anchor exists. |

Tracking loss is represented by `session.state` with `state: "tracking_lost"`, not by an error message.

## Versioning and checks

Both sides use the examples in `examples.json`. A contract change that is not backward-compatible increments `v`; contract changes are reviewed by both developers. The GitHub Actions workflow runs `validate.py` on pushes and pull requests that change `contracts/` or its workflow.
