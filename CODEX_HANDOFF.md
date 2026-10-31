# Loci redesign — handoff for the next Codex session

Updated: 2026-10-29

## Immediate status

Oly confirmed the temporary AR test controls are visible. The updated Unity Android export is complete at `/home/usr/Documents/LociSpikeExport_Current` and contains `unityLibrary/`, `shared/`, and `gradle.properties`.

The export sync script completed successfully. It copied the Unity folders into `apps/loci-mobile/unity/builds/android`, updated the app's `android/gradle.properties`, and removed Unity's launcher filter from the copied manifest.

**Current action:** List, Stack, and Queue first slices are implemented and verified on the phone. The Linked List slice is implemented and has been exported and installed; the first on-device view showed the HEAD/TAIL labels were too small and too close to the node tops. Their height and font size have been increased. Next, Oly should refresh Unity and verify the labels on-device after export and install.

## User and collaboration requirements

- Oly is new to Unity and works on CachyOS Linux, normally using the fish shell. Give short, numbered instructions and explain the expected success result.
- Work one user-facing step at a time. After asking Oly to perform a step, wait for their output before moving to the next one.
- Preserve existing work and do not invent project facts. Use the source files and approved contract as evidence. Ask Oly if an important product or protocol detail is unclear.
- Each project stage must be committed and pushed to GitHub so Oly's friend can track the work. The shared branch is `codex/loci-ar-modular`.
- Oly explicitly approved the temporary React Native AR test controls. They are only a test harness, not the product UI.
- Do not add or run tests unless Oly asks. List, Stack, and Queue behavior has been checked on-device; the Linked List slice is awaiting Unity refresh and phone verification.

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
- The starting values are `10, 20, 30`, and the five-value learner-facing capacity follows the legacy List controller. Supported requests are `Insert`, `Delete`, `Search`, `Traverse`, and `Reset`. On-device validation: `topic.loaded` arrived; after placement, the yellow plane overlay disappeared while the camera remained visible; the values and indexes now appear upright and readable. Oly inserted `40`; the screenshot shows values `10, 20, 30, 40` at indexes `0, 1, 2, 3`, and Unity returned a `topic.event` for `list`, action `Array Insert`, value `40`.
- On `topic.open` for `list`, it spawns the 3D cells and sends `topic.loaded`. On `topic.action`, it updates the model/visuals and emits `topic.event`.
- On-device validation confirms Insert 40 displays a fourth cell at index 3 and emits `topic.event`; Delete removes it, restores values 10, 20, 30 at indexes 0, 1, 2, and emits `topic.event`.
- Search 20 was verified on-device: the `20` cell highlighted and Unity sent `topic.event`.
- Traverse was verified on-device: Unity sent `topic.event` with order `10, 20, 30`, and the search highlight cleared. Reset was verified after inserting `40`: values returned to `10, 20, 30` at indexes `0, 1, 2`, and Unity sent `topic.event`.
- Updated List Search and Traverse to animate one highlight every 0.45 seconds from index 0 toward the end. Search retains the matching highlight and sends `topic.event` when it finds the value; Traverse clears the highlights and then sends the traversal order. Oly refreshed Unity, exported/synced, built successfully, and verified on-device: Traverse highlighted indexes 0, 1, 2 in order, cleared them, and sent order `10, 20, 30`; Search 20 highlighted index 0, then index 1, remained on 20, and sent `topic.event`.
- After renaming the topic ID to `list`, Oly rebuilt the Android app and confirmed Unity sends `topic.loaded` with `topicId: "list"` and displays the three values `10`, `20`, `30`.
- Oly's last Unity check after this code was added: `refreshed, no red errors`. The on-device 3D topic path sent `topic.loaded`; the visual was not yet acceptable, leading to the display fixes above.

### Stack first modular slice

- Added `StackState` and `StackTopicController` under `unity-ar/Assets/_Project/Scripts/Topics/Stack/`, attached the controller to `AR Session`, and added the `Unity.TextMeshPro` assembly reference.
- Stack defaults to `10, 20, 30`, capacity five, vertical bottom-to-top cells, and TOP/BOTTOM labels. `topic.action` supports Push, Pop, Peek, Search, Traverse (top-to-bottom), and Reset, replying with `topic.event`.
- Added temporary phone controls for opening Stack and exercising those operations. Oly refreshed Unity without errors and verified on-device: `topic.loaded` with `topicId: "stack"`; Push 40 adds it above 30; Pop removes it; Peek highlights 30 without changing state; Search 20 highlights it; Traverse clears the highlight and returns `30, 20, 10`; Reset restores `10, 20, 30` with 30 on top. Each action returned `topic.event`.
- Updated Stack Search and Traverse to animate one highlight every 0.45 seconds from the top toward the bottom. Search retains the matching highlight and sends `topic.event` when it finds the value; Traverse clears the highlights and then sends the top-to-bottom order. Oly refreshed Unity, exported/synced, built successfully, and verified on-device: Traverse highlighted `30`, `20`, `10` in order, cleared them, and sent order `30, 20, 10`; Search 20 highlighted `30`, then `20`, remained on 20, and sent `topic.event`.

### Linked List first modular slice

- The approved contract contains topic ID `linked_list`. The older Unity project documents Linked List behavior in `Assets/_Project/Scripts/Modules/LinkedListModule.cs` and `LinkedListController.cs`; this modular slice follows those references for a five-node limit, starting chain `10 → 20 → 30 → NULL`, Insert at tail, Delete Head, Search head-to-tail, Traverse head-to-tail, and Reset.
- Added a real singly linked-node data model in `unity-ar/Assets/_Project/Scripts/Topics/LinkedList/LinkedListState.cs`; it tracks Head, Tail, Next references, and Count, rather than representing the concept as an indexed array/list.
- Added `LinkedListTopicController` in the dedicated `Loci.Topics.LinkedList` assembly and attached it to `AR Session`. It renders horizontal nodes, arrows, HEAD/TAIL labels, and the terminal NULL marker. Delete Head animates the head node out; Search visits head-to-tail and keeps the matching node highlighted; Traverse visits each node head-to-tail, clears highlights, then sends the traversal order in `topic.event`.
- Added temporary React test controls using `topicId: "linked_list"` for Open, Insert 40 at tail, Delete Head, Search 20, Traverse, and Reset. Unity refresh and phone verification are pending.
- On the first on-device visual review, Oly asked for more legible HEAD/TAIL labels lifted above the nodes. Raised the marker offset to 0.09 m above the node top and increased the marker font size to 38; visual verification is pending.

### Queue first modular slice

- Added `QueueState` and `QueueTopicController` under `unity-ar/Assets/_Project/Scripts/Topics/Queue/`, attached the controller to `AR Session`, and added the `Unity.TextMeshPro` assembly reference.
- Queue starts at `10, 20, 30`, capacity five, horizontal front-to-rear cells, with FRONT (blue) and REAR (amber) labels above the ends. Enqueue appends at rear; Dequeue removes front; Peek highlights front; Search and Traverse proceed front-to-rear; Reset restores defaults. Actions emit `topic.event`.
- Added temporary phone controls for Open Queue and Enqueue, Dequeue, Peek, Search, Traverse, and Reset. Oly refreshed Unity, exported/synced, built on Android, and confirmed `topic.loaded` with `topicId: "queue"` plus horizontal `10, 20, 30` cells and clearly readable FRONT/REAR labels. Endpoint labels were enlarged and moved higher for legibility.
- Queue Traverse animates a highlight front-to-rear with a 0.45-second step, clears highlights when complete, then sends `topic.event` with order `10, 20, 30`. Queue Search animates the highlight from FRONT through cells, keeps the match highlighted, then sends `topic.event`. Oly verified both behaviors on the phone, including Search 20 stopping at the matching cell.
- Oly also verified Queue Enqueue (40 added at REAR), Dequeue (10 removed from FRONT), Peek (20 highlighted at FRONT without removal), and Reset (restored 10, 20, 30 with correct endpoints). Unity sent the corresponding `topic.event` for each action.

### Temporary phone test controls

`apps/loci-mobile/App.tsx` contains temporary test controls. They are collapsed by default behind a small **Test controls** button so the camera feed and AR objects remain visible. Expand the panel to send session and placement messages plus:

- **Open List** → `topic.open` with `topicId: "list"`
- **Insert 40** → `topic.action` with `topicId: "list"`, `action: "Insert"`, `value: "40"`
- **Delete last value** → `topic.action` with `topicId: "list"`, `action: "Delete"`
- **Search 20** → `topic.action` with `topicId: "list"`, `action: "Search"`, `value: "20"`
- **Traverse list** → `topic.action` with `topicId: "list"`, `action: "Traverse"`
- **Reset list** → `topic.action` with `topicId: "list"`, `action: "Reset"`
- Stack controls send `topic.open` / `topic.action` using `topicId: "stack"` for Push, Pop, Peek, Search, Traverse, and Reset.
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

## Exact next sequence

1. Oly confirmed AR placement on the phone.
2. Refresh the Unity project and confirm no compile errors; export the Android project, sync it into the host app, rebuild/install, then start AR, confirm placement, and open List. Check that plane visualization is gone after placement and values `10`, `20`, `30` fit legibly on their cells. `topic.loaded` was already confirmed.
3. List and Stack first AR slices and their supported operations have now been verified on-device. Ask Oly which contracted topic or deliverable to implement next; keep product 2D UI on React and all learning objects in the physical AR scene.
4. Commit/push each subsequent stage.

## Build and environment notes

- The phone was previously detected as `RZ8T315SEQF` (Samsung Galaxy A32).
- `npm ci` succeeded in `apps/loci-mobile`; deprecation and low-severity audit notices were observed, not blockers.
- Android build previously succeeded after installing GitHub CLI/auth and supplying the existing ignored `debug.keystore`. Metro showed `Dev server ready`.
- Latest Android build/install completed successfully in 21m 22s, and `com.locispikeapp/.MainActivity` started on the device.
- The current export/sync came from the redesign Unity project and completed successfully.
- `adb` from the agent's tool environment was unavailable due sandbox restrictions; rely on Oly's terminal/device output.
- Do not run tests or lengthy builds without a user request; Oly will provide Unity/phone outputs at each requested checkpoint.
