# Loci redesign — handoff for the next Codex session

Updated: 2026-11-03

## Immediate status

Oly confirmed the temporary AR test controls are visible. The updated Unity Android export is complete at `/home/usr/Documents/LociSpikeExport_Current` and contains `unityLibrary/`, `shared/`, and `gradle.properties`.

The export sync script completed successfully. It copied the Unity folders into `apps/loci-mobile/unity/builds/android`, updated the app's `android/gradle.properties`, and removed Unity's launcher filter from the copied manifest.

**Current action:** List, Stack, Queue, Linked List, Functions, Loops, and all three Selection activities have first AR slices. The Loops landscape sizing issue is unresolved and logged for later review. Two rounds of viewport-fit changes were built and checked on Oly's phone, including a final 98% safe-width / 96% safe-height target; Oly reports the landscape objects still do not appear larger. Stop tuning this without new diagnostic evidence. The Memory table views now use a shared middle angle between the original tilted placement and a camera-square board. Oly confirmed both Data Types/Address Layout and the originally slanted Loop Memory view look successful at this angle. Basic If, Else-if Ladder, and Nested If are implemented and phone-verified, including invalid-input reporting and the reference decision paths. Oly confirmed Memory Overview is readable, Memory Variables is visible in both orientations with a readable lower `ALLOCATED` marker, and List/Queue/Linked List are visible in both orientations. The Linked List portrait chain is readable after reducing excess `NULL` width allowance. Data Types and Address Layout are phone-verified readable in landscape; both tables now use the agreed middle angle, and Address Layout's five rows and marker are visible. Memory Access, Loop Memory, Arrays, Memory Functions, Memory Selection, Memory Scope, Remove Variable, Trace Exercise, Debug Exercise, and Final Assessment are implemented and phone-verified, including question flow, feedback, resulting memory states, score completion, and Reset. Memory Arrays remains distinct from the standalone List topic; Memory Selection remains distinct from the standalone Selection topic.

## User and collaboration requirements

- Oly is new to Unity and works on CachyOS Linux, normally using the fish shell. Give short, numbered instructions and explain the expected success result.
- Work one user-facing step at a time. After asking Oly to perform a step, wait for their output before moving to the next one.
- Preserve existing work and do not invent project facts. Use the source files and approved contract as evidence. Ask Oly if an important product or protocol detail is unclear.
- Each project stage must be committed and pushed to GitHub so Oly's friend can track the work. The shared branch is `codex/loci-ar-modular`.
- Oly explicitly approved the temporary React Native AR test controls. They are only a test harness, not the product UI.
- Do not add or run tests unless Oly asks. List, Stack, Queue, and Linked List behavior has been checked on-device.

## Product boundary — current approved decision

The live physical environment/camera feed is the AR background. The redesign PDF's dark mock background is only a UI mockup; do not use it as the AR background.

React owns all 2D interface, including the app chrome, scanning/placement screens, topic top bar, Operation/Explanation/Status panels, input, and action buttons. Unity owns the AR session, placement, and the topic-specific 3D learning objects and state. This updates the older handoff ownership split.

The two developers reviewed and approved the `topic.action` proposal. React sends `topic.action` with `topicId`, `action`, and optional string `value`. Unity updates its topic state and sends the result using `topic.event`. The contract source is `contracts/messages.schema.json`; human-readable decisions are in `contracts/CONTRACT.md`.

## Repository and project locations

- Monorepo: `/home/usr/Documents/henry/HandoffFiles/LociMonorepo`
- Redesign Unity project: `/home/usr/Documents/henry/HandoffFiles/LociMonorepo/unity-ar`
- React Native host app: `/home/usr/Documents/henry/HandoffFiles/LociMonorepo/apps/loci-mobile`
- Contract source: `/home/usr/Documents/henry/HandoffFiles/LociMonorepo/contracts`
- Unity export sync script: `/home/usr/Documents/henry/HandoffFiles/LociMonorepo/scripts/sync_unity_export.py`
- Older MART_PROG Unity files at the monorepo root (`Assets/`, `ProjectSettings/`) are reference/legacy project content. The active redesign Unity project is `unity-ar/`.
- Git remote: `https://github.com/henryikediego/mart_prog.git`
- Current branch: `codex/loci-ar-modular`

The root `README.md` still describes the older dashboard-based Unity app. For redesign architecture and protocol, use the approved contract and this handoff instead.

## Completed work

### Bridge and contract

- Contract-derived Unity message DTO generation exists at `scripts/generate_unity_message_models.py`.
- Generated C# models are at `unity-ar/Assets/_Project/Scripts/Bridge/LociMessageModels.Generated.cs`.
- The bridge parses and validates shell messages in `unity-ar/Assets/_Project/Scripts/Bridge/SpikeBridge.cs`.
- `topic.action` and examples were added, generated Unity models regenerated, and both developers' review approval recorded.

### AR foundation

- `LociARController` is on the `AR Session` object in `unity-ar/Assets/Scenes/SampleScene.unity`.
- AR is started/stopped by shell messages. Plane candidate detection, manual Place here confirmation, reset, tracking state, and the existing smooth camera-follow behavior are wired.
- Oly tested on a Samsung Galaxy A32: Unity reported `arAvailability: supported`; camera feed started; surface found; placement confirmation worked; reset returned to surface found; `session.stop` stopped the feed.
- Oly confirmed the earlier AR bridge compile-reference errors disappeared after Unity refreshed.

### List first 3D slice

- Pure C# state: `unity-ar/Assets/_Project/Scripts/Topics/List/ListState.cs`.
- 3D renderer and message handler: `unity-ar/Assets/_Project/Scripts/Topics/List/ListTopicController.cs`.
- A dedicated `Loci.Topics.List` assembly exists. The controller is attached to `AR Session` in `SampleScene.unity`.
- The starting values are `10, 20, 30`; the learner-facing capacity is derived from screen size and is calibrated to five values on the Galaxy A32. Supported requests are `Insert`, `Delete`, `Search`, `Traverse`, and `Reset`. On-device validation: `topic.loaded` arrived; after placement, the yellow plane overlay disappeared while the camera remained visible; the values and indexes now appear upright and readable. Oly inserted `40`; the screenshot shows values `10, 20, 30, 40` at indexes `0, 1, 2, 3`, and Unity returned a `topic.event` for `list`, action `Array Insert`, value `40`.
- On `topic.open` for `list`, it spawns the 3D cells and sends `topic.loaded`. On `topic.action`, it updates the model/visuals and emits `topic.event`.
- On-device validation confirms Insert 40 displays a fourth cell at index 3 and emits `topic.event`; Delete removes it, restores values 10, 20, 30 at indexes 0, 1, 2, and emits `topic.event`.
- Search 20 was verified on-device: the `20` cell highlighted and Unity sent `topic.event`.
- Traverse was verified on-device: Unity sent `topic.event` with order `10, 20, 30`, and the search highlight cleared. Reset was verified after inserting `40`: values returned to `10, 20, 30` at indexes `0, 1, 2`, and Unity sent `topic.event`.
- Updated List Search and Traverse to animate one highlight every 0.45 seconds from index 0 toward the end. Search retains the matching highlight and sends `topic.event` when it finds the value; Traverse clears the highlights and then sends the traversal order. Oly refreshed Unity, exported/synced, built successfully, and verified on-device: Traverse highlighted indexes 0, 1, 2 in order, cleared them, and sent order `10, 20, 30`; Search 20 highlighted index 0, then index 1, remained on 20, and sent `topic.event`.
- Updated List capacity and rendering to use `ScreenNodeCapacity`: device-specific readable limit, blocked Insert with failure `topic.event`, and cell/label scaling as count increases. Oly verified five cells fit and a sixth Insert is blocked with the expected failure event on the Galaxy A32.
- After renaming the topic ID to `list`, Oly rebuilt the Android app and confirmed Unity sends `topic.loaded` with `topicId: "list"` and displays the three values `10`, `20`, `30`.
- Oly's last Unity check after this code was added: `refreshed, no red errors`. The on-device 3D topic path sent `topic.loaded`; the visual was not yet acceptable, leading to the display fixes above.

### Stack first modular slice

- Added `StackState` and `StackTopicController` under `unity-ar/Assets/_Project/Scripts/Topics/Stack/`, attached the controller to `AR Session`, and added the `Unity.TextMeshPro` assembly reference.
- Stack defaults to `10, 20, 30`, with vertical bottom-to-top cells and TOP/BOTTOM labels. Its readable capacity is screen-derived and calibrated to five values on the Galaxy A32. `topic.action` supports Push, Pop, Peek, Search, Traverse (top-to-bottom), and Reset, replying with `topic.event`.
- Added temporary phone controls for opening Stack and exercising those operations. Oly refreshed Unity without errors and verified on-device: `topic.loaded` with `topicId: "stack"`; Push 40 adds it above 30; Pop removes it; Peek highlights 30 without changing state; Search 20 highlights it; Traverse clears the highlight and returns `30, 20, 10`; Reset restores `10, 20, 30` with 30 on top. Each action returned `topic.event`.
- Updated Stack Search and Traverse to animate one highlight every 0.45 seconds from the top toward the bottom. Search retains the matching highlight and sends `topic.event` when it finds the value; Traverse clears the highlights and then sends the top-to-bottom order. Oly refreshed Unity, exported/synced, built successfully, and verified on-device: Traverse highlighted `30`, `20`, `10` in order, cleared them, and sent order `30, 20, 10`; Search 20 highlighted `30`, then `20`, remained on 20, and sent `topic.event`.
- Updated Stack capacity and rendering to use `ScreenNodeCapacity`: device-specific readable limit, blocked Push with failure `topic.event`, and vertical scaling as count increases. Oly verified five cells fit and a sixth Push is blocked with the expected failure event on the Galaxy A32.

### Linked List first modular slice

- The approved contract contains topic ID `linked_list`. The older Unity project documents Linked List behavior in `Assets/_Project/Scripts/Modules/LinkedListModule.cs` and `LinkedListController.cs`; this modular slice follows those references for starting chain `10 → 20 → 30 → NULL`, Insert at tail, Delete Head, Search head-to-tail, Traverse head-to-tail, and Reset. Oly changed the node capacity from a fixed five to a display-width-based readable limit, with the Galaxy A32 calibrated to five nodes.
- Added a real singly linked-node data model in `unity-ar/Assets/_Project/Scripts/Topics/LinkedList/LinkedListState.cs`; it tracks Head, Tail, Next references, and Count, rather than representing the concept as an indexed array/list.
- Added `LinkedListTopicController` in the dedicated `Loci.Topics.LinkedList` assembly and attached it to `AR Session`. It renders horizontal nodes, arrows, HEAD/TAIL labels, and the terminal NULL marker. Delete Head animates the head node out; Search visits head-to-tail and keeps the matching node highlighted; Traverse visits each node head-to-tail, clears highlights, then sends the traversal order in `topic.event`.
- Added temporary React test controls using `topicId: "linked_list"` for Open, Insert 40 at tail, Delete Head, Search 20, Traverse, and Reset.
- On-device visual review led to raising the HEAD/TAIL marker offset to 0.09 m and increasing marker font size to 38; Oly confirmed the labels are now legible and lifted above their nodes.
- The first four-node view extended off-screen. The renderer now scales nodes, spacing, and labels as node count grows, and recenters the entire chain including the final link to NULL. Oly confirmed `10 → 20 → 30 → 40 → NULL` fits on screen with HEAD over `10` and TAIL over `40`.
- A later screenshot showed the final arrow overlapping the tail node. Moved it from 0.31 to 0.54 of the node spacing and moved NULL from 0.62 to 0.88 spacing after the tail; re-centered the full chain including this larger terminal link. Oly verified the arrow and NULL sit outside the tail node in the five-node view.
- Implemented screen-based Linked List capacity using the shared safe-area short-side estimate (`min(Screen.safeArea.width, Screen.safeArea.height) / Screen.dpi`), with a 66.7 mm / five-node reference and a five-node fallback when DPI is unavailable. The render scale decreases with node count; Insert fails with a `topic.event` once the device-specific readable limit is reached. Oly approved blocking Insert at that limit and verified five nodes fit while a sixth Insert is blocked with the expected failure event on the Galaxy A32.
- On-device action verification: Insert 40 at tail produced `topic.event`; Delete Head animated `10` out, left `20 → 30 → 40 → NULL` with the endpoints updated, and sent `topic.event`; Reset restored `10 → 20 → 30 → NULL` and sent `topic.event`; Search 20 highlighted `10` then `20`, kept 20 highlighted, and sent `topic.event`; Traverse highlighted `10`, `20`, `30` in head-to-tail order, cleared them, and sent order `10, 20, 30`. The five-node maximum layout and blocked sixth insertion have since been confirmed on the Galaxy A32.

### Queue first modular slice

- Added `QueueState` and `QueueTopicController` under `unity-ar/Assets/_Project/Scripts/Topics/Queue/`, attached the controller to `AR Session`, and added the `Unity.TextMeshPro` assembly reference.
- Queue starts at `10, 20, 30`, capacity five, horizontal front-to-rear cells, with FRONT (blue) and REAR (amber) labels above the ends. Enqueue appends at rear; Dequeue removes front; Peek highlights front; Search and Traverse proceed front-to-rear; Reset restores defaults. Actions emit `topic.event`.
- Added temporary phone controls for Open Queue and Enqueue, Dequeue, Peek, Search, Traverse, and Reset. Oly refreshed Unity, exported/synced, built on Android, and confirmed `topic.loaded` with `topicId: "queue"` plus horizontal `10, 20, 30` cells and clearly readable FRONT/REAR labels. Endpoint labels were enlarged and moved higher for legibility.
- Queue Traverse animates a highlight front-to-rear with a 0.45-second step, clears highlights when complete, then sends `topic.event` with order `10, 20, 30`. Queue Search animates the highlight from FRONT through cells, keeps the match highlighted, then sends `topic.event`. Oly verified both behaviors on the phone, including Search 20 stopping at the matching cell.
- Oly also verified Queue Enqueue (40 added at REAR), Dequeue (10 removed from FRONT), Peek (20 highlighted at FRONT without removal), and Reset (restored 10, 20, 30 with correct endpoints). Unity sent the corresponding `topic.event` for each action.
- Updated Queue capacity and rendering to use `ScreenNodeCapacity`: device-specific readable limit, blocked Enqueue with failure `topic.event`, and horizontal scaling as count increases. Oly verified five cells and the FRONT/REAR markers fit, and a sixth Enqueue is blocked with the expected failure event on the Galaxy A32.

### Functions first modular slice

- Contract topic ID is `functions`; the existing reference `Assets/_Project/Scripts/Modules/FunctionModule.cs` documents the 3D pipeline and `FunctionsController.cs` documents its learner actions. Oly confirmed preserving that behavior: defaults `a = 10`, `b = 20`, return `30`; Set Input accepts two comma-separated integers and changes only the inputs; Add, Subtract, and Multiply update the active function and return value; Reset restores defaults.
- Learners will enter their own values in the final React-owned topic UI. Unity accepts the entered pair through `topic.action` with action `Set Input` and a comma-separated `value`; the temporary test harness's fixed `15,25` button is only a test shortcut, not a product restriction.
- Added pure `FunctionsState` and a dedicated `FunctionsTopicController` in `unity-ar/Assets/_Project/Scripts/Topics/Functions/`. The AR visual is an input → function → return pipeline with `INPUTS`, `FUNCTION`, and `OUTPUT` labels, parameter captions, an operation panel, and directional arrows. Add/Subtract/Multiply pulse the two input boxes, function panel, and output in order before sending `topic.event`; Set Input, Reset, and validation failures also send `topic.event`.
- Attached the controller to `AR Session` in `SampleScene.unity`. Added temporary phone test controls for Open Functions, Set Input 15,25, Add, Subtract, Multiply, and Reset. These controls remain test harness only; React owns final 2D topic UI.
- Unity refreshed without C# compile errors. The only Console error shown was from `Unity.AI.Toolkit.Accounts.Services.Core.Auth.ForceRefreshTokenInternal` in the installed Unity AI Assistant package; its stack trace did not reference Loci code. Oly exported/synced and built the Android app successfully.
- On-device verification: Unity sent `topic.loaded` for `functions` and showed inputs 10, 20, `add(a, b)`, and return 30. Set Input 15,25 changed the inputs but retained return 30 and sent a successful event. Add animated both inputs, function panel, then output and returned 40; Subtract showed `subtract(a, b)` and returned -10; Multiply showed `multiply(a, b)` and returned 375. Each sent a successful `topic.event`. Reset restored 10, 20, `add(a, b)`, and 30 with a successful event.

### Loops first modular slice

- Contract topic ID is `loops`. The existing reference `Assets/_Project/Scripts/Modules/LoopModule.cs` and `LoopsController.cs` define defaults `10, 20, 30`, learner-entered comma-separated whole-number values (one to six), Set Values, Step, Run Loop, and Reset. The six-value maximum is from the existing Loops reference; it is separate from the display-derived capacity used for the data-structure topics.
- Added pure `LoopsState` and a dedicated `LoopsTopicController` under `unity-ar/Assets/_Project/Scripts/Topics/Loops/`. The AR visual is a DATA group with indexes, an active CURRENT marker, a LOOP block narrating the current iteration and running total, arrows, and a RESULT total. Step visits one value and animates that value and the total; Run Loop visits all entered values with pauses, clears CURRENT at completion, and reports the final total. Set Values validates and resets iteration state; Reset restores defaults. Actions report through `topic.event`.
- Attached the controller to `AR Session` in `SampleScene.unity`. Added temporary phone controls with an editable comma-separated value field (default `5,10,15`) plus Open Loops, Set Values, Step, Run Loop, and Reset. The field is test harness only; the final learner-facing topic controls remain React-owned.
- The first phone screenshot showed the DATA and RESULT edges outside the viewport. The React test harness shows portrait guidance and hides it in landscape. The controller watches screen dimensions, camera aspect, and screen orientation, then refits after rotation settles. Its projected fit checks both safe-area width and height. Two update rounds were synced, built, and checked on-device. The last targets 98% safe width and 96% safe height in landscape while keeping portrait capped at `1x`; Oly still saw no visible increase after reinstalling. Leave landscape sizing as a known issue for later review and stop tuning without new diagnostic evidence.

### List, Queue, and Linked List landscape sizing

- Oly asked to apply the portrait guidance and landscape enlargement approach used for other topics to List, Queue, and Linked List. React now shows a topic-specific “For a clearer view … rotate your phone to landscape” banner for these topics in portrait and hides it in landscape.
- Their Unity controllers recalculate a camera-viewport fit while open, with an up-to-`2x` landscape size cap and safe viewport margins. The List/Queue count-based layout scale and all existing readable-node limits remain in place; limits are still based on the device's short screen side and do not increase just because the phone rotates. Linked List fit includes space for the trailing arrow and `NULL` label. After Oly reported that Linked List node values looked too small in portrait, the fit estimate for the trailing `NULL` area was reduced from an overly conservative reservation to improve node/text size without changing the horizontal chain.
- Landscape sizing and orientation were checked on-device for List, Queue, and Linked List. Oly confirmed Memory Variables also appears in both orientations and its `ALLOCATED` marker is readable and farther below the row. The Linked List portrait readability adjustment was refreshed and phone-verified; Oly confirmed it is readable, with the nodes and markers visible.

### Selection Basic If first modular slice

- The approved topic ID is `selection`; its activity IDs are `basic_if`, `else_if_ladder`, and `nested_if`. Begin with `basic_if`.
- The live reference is `Assets/_Project/Scripts/Modules/SelectionController.cs`. Do not copy the parallel state machine in `SelectionModule.cs`; that file is only a display surface and its state is documented as dead code. Basic If defaults are value `75`, condition `>= 50`, and pending result `?`. Its four-rule Toggle Rule cycle is `>= 50`, `< 50`, `== 0`, `!= 0`. Check compares the value using `>=`, `<=`, `>`, `<`, `==`, or `!=`; the result display is `TRUE` or `FALSE`.
- Added `SelectionState` and `SelectionTopicController` in `unity-ar/Assets/_Project/Scripts/Topics/Selection/`, attached to AR Session. The Unity visual is VALUE → CONDITION → RESULT. React owns the 2D controls. The temporary React harness can open Basic If and send Set Value, Set Condition, Check, Toggle Rule, and Reset.
- Oly chose one Study Mode event per action, with invalid values reported as failed events. Do not duplicate the source reference's two Basic If Check rows or omit failed Set Value events. Use the action names Set Value, Set Condition, Check, Toggle Rule, and Reset.
- Basic If was verified on-device: it opened with `75 → value >= 50 → ?`; Check changed the result to `TRUE`; setting value `40` and condition `> 50` then checking returned `FALSE`; Toggle Rule changed the condition to `< 50`; invalid condition `abc` produced one failed event without changing the diagram; Reset restored the defaults. The same one-event-per-action decision applies to all Selection activities.

### Selection Else-if Ladder first modular slice

- The live reference `Assets/_Project/Scripts/Modules/SelectionController.cs` defines two preset ladders. **Grade Rule**: `score >= 80 → A`, `score >= 70 → B`, `score >= 50 → Pass`, else `Fail`. **Performance Rule**: `score >= 90 → Excellent`, `score >= 70 → Good`, `score >= 50 → Average`, else `Needs Improvement`. Evaluation stops at the first true branch; the default score is `75` and default rule is Grade Rule.
- The legacy 3D diagram shows SCORE → a CONDITION block containing the active rule name → RESULT. The detailed condition trace belongs in the action explanation. Implemented `ElseIfLadderState` and `ElseIfLadderController`, both attached through the Selection assembly; React owns the 2D controls.
- Temporary controls open `topicId: "selection"`, `activityId: "else_if_ladder"` and send Set Score, Rule Info, Check, Toggle Rule, and Reset. Invalid Set Score reports a failed event; each supported action sends one event. Check pulses the ladder block, then result, and includes evaluated branches in event details.
- Else-if Ladder was verified on-device: open showed `75 → Grade Rule → ?`; Check returned `B` and stopped after `score >= 70` passed; setting 95 and checking returned `A`; Toggle Rule changed to Performance Rule and cleared the result; checking 95 returned `Excellent`; invalid score was rejected without changing the current diagram; Reset restored score 75, Grade Rule, and `?`.

### Selection Nested If first modular slice

- The live reference defines **Pass Rule** (`score >= 50`, then `attendance >= 75`; outputs `Pass`, `Attendance too low`, or `Fail`) and **Project Eligibility Rule** (`exam >= 80`, then `project >= 70`; outputs `Eligible`, `Project score too low`, or `Exam score too low`). The inner condition is only evaluated when the outer condition is true. Defaults are `65,80` with Pass Rule.
- Implemented `NestedIfState` and `NestedIfController`, attached through the Selection assembly. The diagram shows the two values in the VALUE box, the active preset name in the rule block, then a RESULT box. The result box widens for the reference's longer results. Check pulses the rule block then result and reports whether the inner condition ran.
- Temporary controls open `topicId: "selection"`, `activityId: "nested_if"`; Set Values accepts exactly two comma-separated whole numbers. Rule Info, Check, Toggle Rule, Reset are available. Invalid values produce one failed event; each supported action produces one event.
- Nested If was verified on-device: defaults `65 / 80` with Pass Rule returned `Pass`; `65,50` returned `Attendance too low`; `40,90` returned `Fail` and skipped the inner condition; Toggle Rule changed to Project Eligibility Rule; `90,60` returned `Project score too low`; `90,80` returned `Eligible`; `70,90` returned `Exam score too low` and skipped the inner condition; invalid pair `65,abc` was rejected without changing the diagram; Reset restored `65 / 80`, Pass Rule, and `?`. Selection is complete.

### Memory Management Overview first modular slice

- Approved topic ID is `memory`; the first activity ID is `overview`. The reference `Assets/_Project/Scripts/Modules/MemoryModule.cs` renders an empty table titled `MEMORY TABLE`, with columns `ADDRESS`, `NAME`, `TYPE`, `VALUE`, `SIZE`, and the message `No variables allocated yet.` The old `MemoryController` defaults to Overview when entering the module.
- In the source Unity app, `MemoryModule` has a camera/content fit path (`ARContentAnchor`) and an Overview-only `1.45x` scale. The current LociSpike `LociARController` has no camera-fit helper and places content at a fixed distance. The source table row is about `0.612 m` wide before the Overview multiplier; exact old fit behavior is therefore not available in this AR scene.
- The old source has a porting note that a two-line table reflow was tested on-device and rejected because aligned single-row columns were easier to read. Do not revive that rejected layout without Oly's approval.
- Codex recommended keeping the reference's five columns and adding camera-width fit; Oly asked which approach was recommended, so this recommendation is being used as the working design. `MemoryOverviewController` and a temporary **Open Memory Overview** control render the title, five column headings, and empty-state message. The table uses the source's Overview scale cap (`1.45x`) and shrinks uniformly only as much as needed to fit inside 8% side margins in the current camera viewport.
- Overview is phone-verified: Unity sent `topic.loaded` for `memory`, the full table was visible, and Oly confirmed the headings and empty-state message were readable.

### Memory Management Variables slice

- Oly confirmed the Overview headings and empty-state message are readable on the phone. The next activity in the existing Memory order is `variables`.
- Source supports declarations such as `int age=20`, updates such as `age=25`, and bare-name inspection such as `age`. It preserves an existing variable's type for an untyped update, rejects incompatible values, and shows address, name, type, value, and size. New variable addresses use the source's uniform four-byte index spacing (`0x1000`, `0x1004`, ...); sizes are `int/float 4 bytes`, `double 8 bytes`, `char/bool 1 byte`, and `string varies`.
- Oly chose **Guided prediction** for Predict Step. The five reference statements are `int age = 20;`, `float price = 15.5;`, `char grade = 'A';`, `age = 21;`, and `bool passed = true;`. Each prompt is shown first; the learner enters an answer and presses Predict Step again. The step then executes even for an incorrect answer, highlights the affected row, and reports expected value and correctness. Reset clears both the table and prediction sequence.
- `MemoryVariablesController` is attached to AR Session. Temporary phone controls open Memory Variables and allow direct learner-entered declaration / inspection, guided Predict Step, and Reset. Oly chose a portrait guidance banner asking the learner to rotate the phone to landscape. The Unity table uses a compact content height and a 2.0x landscape scale cap, with camera viewport fit in both orientations. Code and prediction prompts are carried in `topic.event` details for React to display; the 3D statement card was removed to leave room for the table. Android and Unity already allow autorotation across portrait and landscape; the local `ProjectSettings.asset` change is not part of this work. On-device, the portrait banner appeared, the landscape table stayed large after Set/Inspect, and Oly confirmed the values were readable. The `ALLOCATED` marker was too small and close to the row; it is now larger, cyan, and positioned lower.
- Oly confirmed the updated `ALLOCATED` marker is readable and farther below the row in both orientations. Later, reset and check that the empty table remains readable.

### Memory Management Data Types slice

- The existing reference's next Memory activity is `data_types`. Its **Show Types** action displays five rows in the shared memory table: `age / int / 20 / 4 bytes`, `price / float / 15.5 / 4 bytes`, `grade / char / 'A' / 1 byte`, `passed / bool / true / 1 byte`, and `name / string / "Mike" / varies`.
- Addresses follow the reference's size-aware byte offsets: `0x1000`, `0x1004`, `0x1008`, `0x1009`, and `0x100A`. The string displays `varies` but advances the offset by eight bytes, matching the source helper.
- The activity is implemented through `MemoryVariablesController` to reuse the established five-column table and landscape/portrait camera fit. React's temporary controls open `topicId: "memory"`, `activityId: "data_types"` and send **Show Types** / **Reset** actions. The portrait guidance banner also covers Data Types. The displayed examples use one successful `topic.event` for Show Types, following the source's action name.
- The first refresh reported `CS0111` because the controller already had a `TypeSize` helper; the duplicate helper was removed, and the activity now reuses the existing method. Unity refreshed and the app was exported, synced, and installed. The first landscape screenshot showed all five rows but the table was too small to read comfortably. Data Types' landscape scale cap was raised to `3.0x` with camera-fit; Oly reopened it in landscape and confirmed it is readable. Variables retains its verified `2.0x` landscape cap.

### Memory Management Address Layout slice

- The source's next activity is `address_layout`, with **Show Layout** and Reset. It uses the same size-aware addresses as Data Types but replaces the final string row with `level / int / 3`, yielding rows `age / int / 20 / 0x1000`, `price / float / 15.5 / 0x1004`, `grade / char / 'A' / 0x1008`, `passed / bool / true / 0x1009`, and `level / int / 3 / 0x100A`.
- The source highlights the `level` row and displays its marker as `ADDRESS` / `LAYOUT` beneath it. React's temporary controls open `topicId: "memory"`, `activityId: "address_layout"` and send **Show Layout** / **Reset**. Portrait guidance is shown, and the landscape cap uses the Data Types value (`3.0x`) with camera fit.
- Oly reported that the landscape tables for Address Layout and Data Types were too slanted away to read and approved tilting them toward the camera while keeping their position/distance. After those tables became too square to the camera while other Memory tables stayed too slanted, the shared `MemoryVariablesController` was adjusted to a halfway rotation between the placement root and AR camera. Oly refreshed/exported/synced/installed and confirmed the middle angle works on both Data Types/Address Layout and Loop Memory. All activities using this shared table receive the adjustment.

### Memory Management Access slice

- The next source activity is `memory_access`, with the reference **Access Step** trace: `int x = 4;`, `int y = 6;`, `read x`, `read y`, `int sum = x + y;`, then `Memory access trace completed.` The table accumulates `x / int / 4`, `y / int / 6`, and `sum / int / 10` at uniform four-byte addresses (`0x1000`, `0x1004`, `0x1008`). Allocation steps mark the new row as `ALLOCATED`; read steps mark the relevant row as `READ` with its value. The sum detail explains that 4 and 6 were read and added.
- Implemented as `activityId: "memory_access"` inside `MemoryVariablesController`, reusing the established five-column table and row-pulse event flow. The React temporary controls open Memory Access, advance **Access Step**, and reset. The portrait guidance banner includes Memory Access. Each of the five trace steps sends a successful `topic.event` under **Memory Access** with the executed statement and explanation; the following step sends a `Finished` event. Reset clears the table and starts the trace again.
- The first phone check showed the two-line `READ x = 4` marker overlapping the following `y` row. Added vertical spacing after a focused row with a marker and included it in camera-fit sizing. Oly updated the app and confirmed both read markers are clear, `sum` stores `10`, the trace sends Finished, and Reset clears the table and restarts it. Memory Access is phone-verified.

### Memory Management Loop Memory slice

- The source's next activity is `loop_memory`, with a fixed 13-step trace: allocate `numbers[3] = {2, 4, 6}`, set `total = 0`, create `i = 0`, read each array element, add it to total, increment `i` between elements, remove `i` when the loop ends, then mark `total` with `LOOP COMPLETE`. The final total is `12`. Array elements use consecutive size-aware addresses; scalar variables use the normal uniform four-byte address stride.
- The source button is **Loop Step**, but the research log action is **Memory Loop** (the source's activity label is **Loop Memory**). Preserve this log naming difference. Intermediate events include the executed line and source explanation; the final event has result `Finished` and details `Loop memory trace completed`.
- Implemented as `activityId: "loop_memory"` inside `MemoryVariablesController`, using the shared five-column table. Array cells are named `numbers[0]`, `numbers[1]`, and `numbers[2]`; allocation/read/update markers focus the affected row, counter removal shows a global removal marker, and the final total is marked `LOOP COMPLETE`. React's temporary controls open the activity, advance **Loop Step**, and Reset; portrait guidance covers Loop Memory. Oly verified all 13 steps in order, including total `12`, counter removal, the Finished event, and Reset. Oly later confirmed this view looks successful with the shared middle table angle.
- Implemented as `activityId: "loop_memory"` inside `MemoryVariablesController`, using the shared five-column table. Array cells are named `numbers[0]`, `numbers[1]`, and `numbers[2]`; allocation/read/update markers focus the affected row, counter removal shows a global removal marker, and the final total is marked `LOOP COMPLETE`. React's temporary controls open the activity, advance **Loop Step**, and Reset; portrait guidance covers Loop Memory. Oly verified all 13 steps in order, including total `12`, counter removal, the Finished event, and Reset. Loop Memory is phone-verified.

### Memory Management Arrays activity

- The next source activity is `arrays`; the topic contract already assigns that stable ID. Reference input accepts declarations such as `int marks[3]=70,80,65`, element updates such as `marks[2]=85`, and bare element inspection such as `marks[2]`. It supports the standard memory types and allows at most six array values.
- The source does not validate that the bracket count matches the number of entered values. Oly chose to require an exact match so the declaration teaches array length consistently. The redesigned input allows declarations like `int marks[3]=70,80,65`, inferred declarations with comma-separated values, element updates like `marks[2]=85`, and element inspection. It preserves the reference maximum of six values and reports malformed input and type mismatches as failed events without changing the table.
- Implemented as `activityId: "arrays"` in `MemoryVariablesController`. React temporary controls open Arrays and provide learner-entered declaration/update and inspection fields, **Set Array / Update Element**, **Inspect Array Element**, and Reset. The shared five-column table displays each element at a size-aware consecutive address and focuses allocation, update, and inspection rows. Portrait guidance is included. Oly verified a three-element declaration, a single-element update, element inspection, rejected mismatched length, rejected invalid int value, the six-value cap, and Reset. The activity is phone-verified. Oly decided to keep Memory Arrays distinct from the standalone List topic: arrays have declared lengths and indexed consecutive memory locations; List teaches insert, delete, search, and traversal.

### Memory Management Functions slice

- This is the `functions` activity under Memory Management, distinct from the standalone Functions topic. The source **Function Call** trace uses fixed values: `x = 10`, `y = 20`, call `add(x, y)`, create `a:param = 10`, `b:param = 20`, and `sum:local = 30`, return 30, remove the temporary `add()` frame, and store `result = 30`; the next step reports completion. Values are not learner-entered in this reference trace.
- Implemented in `MemoryVariablesController` as `activityId: "functions"`. Function parameters and local are shown as `a:param`, `b:param`, and `sum:local`; the Return marker highlights `sum`, the stored result remains with `x` and `y` after frame cleanup, and a final step sends Finished. Topic events use the source log action **Memory Function Call**. React temporary controls open **Memory Functions**, advance **Function Call**, and Reset; portrait guidance is included. Oly refreshed/exported/synced/installed and verified all nine trace steps, including `FUNCTION FRAME`, `RETURN 30`, frame removal, stored `result = 30`, Finished, and Reset clearing the table. This slice is phone-verified.

### Memory Management Selection slice

- This is the `selection` activity under Memory Management, distinct from the standalone Selection topic. The reference is a fixed five-step `if (score >= 50)` trace: allocate `score = 75`, evaluate the condition TRUE, allocate `result = 1` in the TRUE branch, report the ELSE branch skipped, then report completion.
- Implemented in `MemoryVariablesController` as `activityId: "selection"`. It uses a global CONDITION TRUE marker, highlights the new result row, then shows BRANCH SKIPPED. Each step emits one event with the source action name **Memory Selection**; the final event is Finished. React temporary controls open **Memory Selection**, advance **Run If-Else**, and Reset; portrait guidance is included. Oly refreshed Unity, exported, synced, installed, then confirmed the `score = 75` row, CONDITION TRUE, `result = 1`, BRANCH SKIPPED, Finished, and Reset clearing the table. The activity is phone-verified.

### Memory Management Scope slice

- This is the `scope` activity under Memory Management. The reference has a fixed trace: allocate `x = 10` outside the if block; evaluate `x > 5` as TRUE; allocate `y = 20` inside the block; update `x` to 30 using `y`; remove `y` when the block ends; show that `x = 30` remains; then report completion.
- Implemented in `MemoryVariablesController` as `activityId: "scope"`. Row and global markers distinguish `x` in outer scope, `y` local to the if block, the TRUE condition, the update, removal of `y`, and the surviving `x`. React temporary controls open **Memory Scope**, advance **Scope Step**, and Reset; portrait guidance is included. Oly refreshed Unity, exported, synced, installed, and confirmed each step: `x = 10`, condition TRUE, local `y = 20`, `x = 30`, `y` removed at block end, `x` remains, Finished, and Reset clearing the table. The activity is phone-verified.

### Memory Management Remove Variable slice

- This is the `remove_variable` activity under Memory Management. The reference has a fixed trace: allocate `age / int / 20`, allocate `price / float / 15.5`, remove `age`, show `price` remains, then report completion.
- Implemented in `MemoryVariablesController` as `activityId: "remove_variable"`. The removal marker identifies `age = 20`; the surviving `price` row keeps its original simulated address. React temporary controls open **Remove Variable**, advance **Remove Step**, and Reset; portrait guidance is included. Oly refreshed Unity, exported, synced, installed, and confirmed allocation of `age / int / 20` and `price / float / 15.5`, removal of `age`, the surviving `price`, Finished, and Reset clearing the table. The activity is phone-verified.

### Memory Management Trace Exercise slice

- The reference provides five ordered questions covering variable update (`x = 8`), subtraction (`b = 7`), an else branch (`result = 0`), array update (`marks[1] = 85`), and value copying (`q = 5`). After each answer it shows the resulting memory table and explanation.
- Implemented in `MemoryVariablesController` as `activityId: "trace_exercise"`. React temporary controls show the code/question from Unity, accept a learner answer, and provide Start/Next, Submit, and Reset. Answer comparison follows the existing variable-prediction normalization; empty answers are rejected without advancing, and wrong answers are reported as Incorrect while still showing the resulting memory state and explanation. Oly chose completion immediately after answer five (the old source waited for an extra Next action to detect its wrapped question bank). Oly refreshed Unity, exported, synced, and installed, then answered all five questions correctly (`8`, `7`, `0`, `85`, `5`), saw the corresponding final memory tables, the `5/5` completion event, and Reset clearing the table. The activity is phone-verified.

### Memory Management Debug Exercise slice

- The reference contains four unique questions: identify the line with an int/string type mismatch; identify an invalid array index; identify an invalid char assignment; and identify an invalid bool assignment. Each answer shows the valid resulting memory state and an explanation. The source declares a total of five but wraps and repeats question one to detect exhaustion. Oly chose to finish after the four unique questions and score them out of four.
- Implemented in `MemoryVariablesController` as `activityId: "debug_exercise"`. React temporary controls show each code/question, accept the learner's line-number answer, and provide Start/Next, Submit, and Reset. Empty answers are rejected without advancing; wrong answers show the correct memory state and explanation. Completion occurs after question four with a score out of four. Oly refreshed Unity, exported and synced the files, installed the app, and confirmed all four questions and Reset work. The activity is phone-verified.

### Memory Management Final Assessment slice

- The reference has five ordered questions: update `x` to 8; replace `price` with 12.5; update `marks[0]` to 75; evaluate the `score >= 50` branch and store `result = 1`; and add `a + b` to store `sum = 30`. Each answer is followed by the resulting memory table, a focus marker, and an explanation. Answers use the same normalized comparison as Trace Exercise; wrong answers still show the correct resulting state. The source Final Assessment does not wrap its question bank: after question five, a separate Finish Assessment action reports the score out of five. Preserve that completion flow.
- Implemented in `MemoryVariablesController` as `activityId: "final_assessment"`. React temporary controls provide Start, Next Question, answer entry, Submit, Finish Assessment after the fifth answer, and Reset; empty answers are rejected without advancing. The fifth resulting table stays visible while the learner finishes. Oly refreshed Unity, exported and synced the files, installed the app, and confirmed the assessment exercises and Reset work. The activity is phone-verified.

### Temporary phone test controls

`apps/loci-mobile/App.tsx` contains temporary test controls. They are collapsed by default behind a small **Test controls** button so the camera feed and AR objects remain visible. Expand the panel to send session and placement messages plus:

- **Open List** → `topic.open` with `topicId: "list"`
- **Insert 40** → `topic.action` with `topicId: "list"`, `action: "Insert"`, `value: "40"`
- **Open Memory Overview** → `topic.open` with `topicId: "memory"`, `activityId: "overview"`
- **Open Memory Variables** → `topic.open` with `topicId: "memory"`, `activityId: "variables"`
- **Open Memory Data Types** → `topic.open` with `topicId: "memory"`, `activityId: "data_types"`; **Show Data Types** sends the `Show Types` action, and **Reset Data Types** clears that activity's table.
- **Open Memory Address Layout** → `topic.open` with `topicId: "memory"`, `activityId: "address_layout"`; **Show Address Layout** sends the `Show Layout` action, and **Reset Address Layout** clears the table.
- **Open Memory Access** → `topic.open` with `topicId: "memory"`, `activityId: "memory_access"`; **Access Step** advances the reference trace and **Reset Memory Access** clears it.
- **Open Loop Memory** → `topic.open` with `topicId: "memory"`, `activityId: "loop_memory"`; **Loop Step** advances the reference trace and **Reset Loop Memory** clears it.
- **Open Memory Arrays** → `topic.open` with `topicId: "memory"`, `activityId: "arrays"`; enter an array declaration or element update, then use **Set Array / Update Element**; enter a `name[index]` to inspect; Reset clears the table.
- **Open Memory Functions** → `topic.open` with `topicId: "memory"`, `activityId: "functions"`; **Function Call** advances the reference call/return trace and Reset clears it. This differs from the standalone Functions topic.
- **Open Memory Selection** → `topic.open` with `topicId: "memory"`, `activityId: "selection"`; **Run If-Else** advances the fixed selection trace and Reset clears it. This differs from the standalone Selection topic.
- **Open Memory Scope** → `topic.open` with `topicId: "memory"`, `activityId: "scope"`; **Scope Step** advances the fixed block-scope trace and Reset clears it.
- **Open Remove Variable** → `topic.open` with `topicId: "memory"`, `activityId: "remove_variable"`; **Remove Step** advances the fixed variable-removal trace and Reset clears it.
- **Open Memory Trace Exercise** → `topic.open` with `topicId: "memory"`, `activityId: "trace_exercise"`; Unity sends each ordered code/question prompt, React accepts the answer, and the resulting table and explanation follow. It completes after answer five; Reset restarts the exercise.
- **Open Memory Debug Exercise** → `topic.open` with `topicId: "memory"`, `activityId: "debug_exercise"`; Unity sends each ordered code/question prompt, React accepts the line-number answer, and the resulting table and explanation follow. It completes after four unique questions; Reset restarts the exercise.
- **Open Memory Final Assessment** → `topic.open` with `topicId: "memory"`, `activityId: "final_assessment"`; React presents Unity's five ordered questions, accepts each answer, and shows the resulting memory table. After question five, **Finish Assessment** reports the score; Reset restarts the activity.
- Memory Variables accepts typed declaration/update/inspection text in the temporary input, plus **Predict Step** and **Reset**. The first Predict Step shows the statement prompt; entering a prediction in the same input and pressing again evaluates and executes it.
- **Delete last value** → `topic.action` with `topicId: "list"`, `action: "Delete"`
- **Search 20** → `topic.action` with `topicId: "list"`, `action: "Search"`, `value: "20"`
- **Traverse list** → `topic.action` with `topicId: "list"`, `action: "Traverse"`
- **Reset list** → `topic.action` with `topicId: "list"`, `action: "Reset"`
- Stack controls send `topic.open` / `topic.action` using `topicId: "stack"` for Push, Pop, Peek, Search, Traverse, and Reset.
- Functions controls send `topic.open` / `topic.action` using `topicId: "functions"` for Set Input (`15,25`), Add, Subtract, Multiply, and Reset.
- Selection test controls open `topicId: "selection"` with activity IDs `basic_if`, `else_if_ladder`, or `nested_if`. They include editable inputs and each activity's Set, Rule Info/Set Condition, Check, Toggle Rule, and Reset actions.
- Memory test controls include **Open Memory Overview** → `topic.open` with `topicId: "memory"`, `activityId: "overview"`.
- Loops controls send `topic.open` / `topic.action` using `topicId: "loops"`; the temporary value field sends the learner-entered comma-separated values with Set Values, with separate Step, Run Loop, and Reset controls.
- Linked List controls send `topic.open` / `topic.action` using `topicId: "linked_list"` for Insert, Delete Head, Search, Traverse, and Reset.

The controls appeared after Metro refresh; Oly confirmed this. After Oly confirmed placement and opened List, Unity sent `topic.loaded` with `topicId: "list"`. The original expanded controls obscured much of the camera and 3D view, so they were changed to collapse by default. Oly still needs to visually check the compact layout and report the List 3D values and action event. These controls are not the final React UI and should not be removed until the final React integration is present and the user approves cleanup.

## Recent pushed commits

All are on `codex/loci-ar-modular`:

- `9a48c55 Add temporary List test controls`
- `80ef13c Add comprehensive project handoff`
- `41c1be2 Add modular List 3D topic`
- `0532b83 Record approval of topic action contract`
- `b63abec Propose shell to Unity topic action message`
- `48c2787 Update shell and Unity UI ownership`
- `a582cfc Add temporary AR session test controls`
- `2c8c51e Sync Android host settings from current Unity export`
- `3052f40 Reference AR Subsystems from AR assembly`
- `a8d5b03 Add modular AR session and placement controller`
- Earlier generated bridge/DTO commits: `61a8b94`, `12d47c7`.

## Current Git state and care required

At handoff time, `git status` has one unrelated user/editor change:

` M unity-ar/ProjectSettings/ProjectSettings.asset`

It contains an auto-generated PS4 passcode value. Do not display, copy, commit, or revert it. Leave the user's change untouched. All intended work through the handoff update is committed and pushed.

## React integration handoff readiness

- Unity's modular AR topics and the listed Memory/Selection activities have been implemented and phone-verified, including session/placement, topic loading, learner actions, and topic events. Shared contract files are in `contracts/`; `topics.json` lists stable topic and activity IDs, while `messages.schema.json` and `examples.json` define v1 messages.
- `apps/loci-mobile/App.tsx` still contains a temporary test harness, not the finished product interface. The React-side integration work remains: replace the test panel with the approved shell-owned app/AR/topic UI, connect the existing screens and learner inputs to the contract's `topic.open` / `topic.action` messages, receive Unity `topic.loaded` / `topic.event`, and implement Study Mode research logging / per-Study-ID progress and Product Mode behavior as described in `contracts/CONTRACT.md`. Keep all topic 2D UI in React; Unity owns AR and 3D learning objects.
- Known Unity follow-up: Loops objects still do not visibly enlarge in landscape on Oly's phone after two viewport-fit changes. This remains logged for later investigation; do not delay React integration to continue speculative tuning.
- Memory tables use the verified middle angle between the original slanted pose and a board square to the camera. Oly confirmed it for Data Types/Address Layout and Loop Memory.
- Commit and push each subsequent stage. Preserve the unrelated local `unity-ar/ProjectSettings/ProjectSettings.asset` change; never stage, display, or revert it.

## Build and environment notes

- The phone was previously detected as `RZ8T315SEQF` (Samsung Galaxy A32).
- `npm ci` succeeded in `apps/loci-mobile`; deprecation and low-severity audit notices were observed, not blockers.
- Android build previously succeeded after installing GitHub CLI/auth and supplying the existing ignored `debug.keystore`. Metro showed `Dev server ready`.
- Latest Android build/install completed successfully in 21m 22s, and `com.locispikeapp/.MainActivity` started on the device.
- The current export/sync came from the redesign Unity project and completed successfully.
- `adb` from the agent's tool environment was unavailable due sandbox restrictions; rely on Oly's terminal/device output.
- Do not run tests or lengthy builds without a user request; Oly will provide Unity/phone outputs at each requested checkpoint.
