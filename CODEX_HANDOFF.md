# Loci redesign — handoff for the next Codex session

Updated: 2026-10-29

## Immediate status

Oly confirmed the temporary AR test controls are visible. The updated Unity Android export is complete at `/home/usr/Documents/LociSpikeExport_Current` and contains `unityLibrary/`, `shared/`, and `gradle.properties`.

The export sync script completed successfully. It copied the Unity folders into `apps/loci-mobile/unity/builds/android`, updated the app's `android/gradle.properties`, and removed Unity's launcher filter from the copied manifest.

**Current action:** Android build/install succeeded (`BUILD SUCCESSFUL in 21m 22s`) and the app launched. Oly confirmed AR placement. The next check is to open Array/List and confirm the initial 3D values appear over the live camera view.

## User and collaboration requirements

- Oly is new to Unity and works on CachyOS Linux, normally using the fish shell. Give short, numbered instructions and explain the expected success result.
- Work one user-facing step at a time. After asking Oly to perform a step, wait for their output before moving to the next one.
- Preserve existing work and do not invent project facts. Use the source files and approved contract as evidence. Ask Oly if an important product or protocol detail is unclear.
- Each project stage must be committed and pushed to GitHub so Oly's friend can track the work. The shared branch is `codex/loci-ar-modular`.
- Oly explicitly approved the temporary React Native AR test controls. They are only a test harness, not the product UI.
- Do not add or run tests unless Oly asks. No test suite has been run for the current Array/List implementation. The Unity compile refresh was confirmed by Oly with “refreshed, no red errors”.

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

### Array/List first 3D slice

- Pure C# state: `unity-ar/Assets/_Project/Scripts/Topics/ArrayList/ArrayListState.cs`.
- 3D renderer and message handler: `unity-ar/Assets/_Project/Scripts/Topics/ArrayList/ArrayListTopicController.cs`.
- A dedicated `Loci.Topics.ArrayList` assembly exists. The controller is attached to `AR Session` in `SampleScene.unity`.
- The starting values are `10, 20, 30`, and the five-value learner-facing capacity follows the legacy Array/List controller. Supported requests are `Insert`, `Delete`, `Search`, `Traverse`, and `Reset`. On-device validation: `topic.loaded` arrived; after placement, the yellow plane overlay disappeared while the camera remained visible; the values and indexes now appear upright and readable. Oly inserted `40`; the screenshot shows values `10, 20, 30, 40` at indexes `0, 1, 2, 3`, and Unity returned a `topic.event` for `array_list`, action `Array Insert`, value `40`.
- On `topic.open` for `array_list`, it spawns the 3D cells and sends `topic.loaded`. On `topic.action`, it updates the model/visuals and emits `topic.event`.
- On-device validation confirms Insert 40 displays a fourth cell at index 3 and emits `topic.event`; Delete removes it, restores values 10, 20, 30 at indexes 0, 1, 2, and emits `topic.event`.
- Search 20 was verified on-device: the `20` cell highlighted and Unity sent `topic.event`.
- Oly's last Unity check after this code was added: `refreshed, no red errors`. The on-device 3D topic path sent `topic.loaded`; the visual was not yet acceptable, leading to the display fixes above.

### Temporary phone test controls

`apps/loci-mobile/App.tsx` contains temporary test controls. They are collapsed by default behind a small **Test controls** button so the camera feed and AR objects remain visible. Expand the panel to send session and placement messages plus:

- **Open Array/List** → `topic.open` with `topicId: "array_list"`
- **Insert 40** → `topic.action` with `topicId: "array_list"`, `action: "Insert"`, `value: "40"`
- **Delete last value** → `topic.action` with `topicId: "array_list"`, `action: "Delete"`
- **Search 20** → `topic.action` with `topicId: "array_list"`, `action: "Search"`, `value: "20"`
- **Traverse list** → `topic.action` with `topicId: "array_list"`, `action: "Traverse"`

The controls appeared after Metro refresh; Oly confirmed this. After Oly confirmed placement and opened Array/List, Unity sent `topic.loaded` with `topicId: "array_list"`. The original expanded controls obscured much of the camera and 3D view, so they were changed to collapse by default. Oly still needs to visually check the compact layout and report the Array/List 3D values and action event. These controls are not the final React UI and should not be removed until the final React integration is present and the user approves cleanup.

## Recent pushed commits

All are on `codex/loci-ar-modular`:

- `9a48c55 Add temporary Array List test controls`
- `80ef13c Add comprehensive project handoff`
- `41c1be2 Add modular Array List 3D topic`
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
2. Refresh the Unity project and confirm no compile errors; export the Android project, sync it into the host app, rebuild/install, then start AR, confirm placement, and open Array/List. Check that plane visualization is gone after placement and values `10`, `20`, `30` fit legibly on their cells. `topic.loaded` was already confirmed.
3. Next, test **Delete** using the temporary controls only if available; expect the last value (`40`) to be removed and a `topic.event` returned. Record the observed message and view before proceeding to Search.
4. Commit/push any subsequent stage. Keep product 2D UI on React and all learning objects in the physical AR scene.

## Build and environment notes

- The phone was previously detected as `RZ8T315SEQF` (Samsung Galaxy A32).
- `npm ci` succeeded in `apps/loci-mobile`; deprecation and low-severity audit notices were observed, not blockers.
- Android build previously succeeded after installing GitHub CLI/auth and supplying the existing ignored `debug.keystore`. Metro showed `Dev server ready`.
- Latest Android build/install completed successfully in 21m 22s, and `com.locispikeapp/.MainActivity` started on the device.
- The current export/sync came from the redesign Unity project and completed successfully.
- `adb` from the agent's tool environment was unavailable due sandbox restrictions; rely on Oly's terminal/device output.
- Do not run tests or lengthy builds without a user request; Oly will provide Unity/phone outputs at each requested checkpoint.
