# MART_PROG (Unity)

**Mobile Augmented Reality Technology for Programming Concept Visualization**

Educational mobile AR application (thesis research) that teaches core
computer-science concepts through interactive 3D visualizations over a
live camera feed, with a persistent six-panel dashboard and CSV research
logging.

This is the Unity rebuild of the original Godot 4.7 / GDScript project.
The migration is a **translation of validated, tested behaviour** — the
functional specification is the source of truth, not a place to redesign.

## Environment

| | |
|---|---|
| Engine | Unity 6.3 LTS (6000.3.x) |
| AR | AR Foundation 6.x + Google ARCore XR Plugin (Android) |
| Language | C# |
| Input | Unity Input System package |
| Primary platform | Android first; iOS to follow |
| Dev OS | Linux |

## The eight modules

1. Array/List — indexed storage
2. Stack — LIFO
3. Queue — FIFO
4. Linked List — nodes linked head-to-tail, ending in NULL
5. Functions — inputs → function → output
6. Loops — iterating and accumulating a running total
7. Selection — conditional logic (Basic If / Else-if Ladder / Nested If)
8. Memory — how data lives in memory (13 activities)

## The dashboard (six panels)

Summary · Instruction · Operation · Explanation · Status · Progress.
Reusable across every module. Operation and Explanation are dynamic,
never identical, never blank.

## Rebuild order

1. Project + AR Foundation; live camera feed with a placeholder object on a plane
2. Dashboard UI prefab + DashboardController
3. ResearchLogger service (CSV)
4. Array/List module end-to-end (establishes the per-module pattern) + its quiz
5. Stack, Queue, Linked List
6. Functions, Loops
7. Selection (single authoritative state — no text-content label lookups)
8. Memory (all 13 activities)
9. Cross-module QA pass

## Research log CSV format

`Timestamp,StudentID,SessionID,Module,Action,Value,Result,Details`
