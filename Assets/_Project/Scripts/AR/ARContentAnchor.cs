using UnityEngine;
using MartProg.Menu;

namespace MartProg.AR
{
    /// <summary>
    /// NOT a Godot port — this is a Unity/AR Foundation-only script. Godot's
    /// app never had real-world AR tracking at all (see camera_bacground.gd
    /// for its 2D camera-compositing approach), so there is no source
    /// behavior to match here.
    ///
    /// HISTORY — this file has gone through three designs; keeping the full
    /// story here since each one was a deliberate response to concrete
    /// on-device feedback, not a false start to be forgotten:
    ///
    /// 1. ORIGINAL BUG (Phase 21, found during Linked List's on-device
    ///    test): every module's 3D content had always been a plain,
    ///    unparented Transform sitting at a fixed world-space position.
    ///    ARCore continuously refines its estimate of the device's pose —
    ///    even with the phone stationary, hand tremor alone feeds enough
    ///    motion data to trigger this — and normally corrects for that
    ///    drift on anything registered as an ARAnchor. Static, unanchored
    ///    content got no such correction, so it visibly slid, most
    ///    noticeably in a session's first several seconds.
    /// 2. FIRST FIX (Phase 21b): create one ARAnchor via
    ///    ARAnchorManager.TryAddAnchorAsync and reparent all module roots
    ///    onto it, so AR Foundation's own per-frame anchor tracking supplied
    ///    the drift correction. Confirmed fixed on-device ("the boxes
    ///    sustain a fixed position").
    /// 3. SOFT-FOLLOW / RECENTER (still Phase 21b): in response to "I think
    ///    it's better to allow the boxes to move with the user's device,"
    ///    content stayed anchored/rock-steady up close but would glide to a
    ///    fresh anchor if the user walked >1.2m away or turned >55° off
    ///    axis. On-device testing showed this didn't read as "moving with
    ///    the device" for the kind of small pans/tilts the user actually
    ///    tried — that was the threshold design working exactly as built
    ///    (small movements are supposed to leave rock-steady content alone),
    ///    but it didn't match what the user pictured. Asked directly
    ///    whether to keep it, make the thresholds more sensitive, or
    ///    redesign toward continuous following — knowing that continuous
    ///    following reintroduces some of the jitter the anchor fix had just
    ///    eliminated — **the user chose continuous following anyway.**
    ///
    /// CURRENT DESIGN — continuous smoothed follow (supersedes the anchor +
    /// recenter approach above):
    ///
    /// Every frame, this script recomputes a target pose a fixed distance
    /// in front of the camera's CURRENT transform (same
    /// flattened-forward-direction math as every earlier version), then
    /// smoothly moves this GameObject's actual pose toward that target —
    /// position via Vector3.SmoothDamp, rotation via an exponentially
    /// eased Slerp — rather than either snapping instantly (jarring) or
    /// only correcting once a large distance/angle threshold is crossed
    /// (the previous design). Content now visibly, continuously drifts
    /// with every phone movement, as requested — the "flexibility" of a
    /// HUD-like object that always stays roughly in front of the user,
    /// not a point fixed in the room.
    ///
    /// A useful side effect of computing the target fresh from the camera's
    /// transform every single frame: it automatically incorporates
    /// whatever pose correction ARCore applied to the camera that frame, so
    /// the original Phase 21 drift bug can't recur here — there's no static
    /// Transform sitting still waiting to fall out of sync, content is
    /// always being freshly recomputed from the current tracked camera
    /// pose. This means ARAnchorManager/ARSession/ARAnchor are no longer
    /// used by this script at all — no anchor creation, no async/await, no
    /// tracking-wait timeout. (If an "AR Anchor Manager" component was
    /// added to XR Origin for the Phase 21b fix, it's simply unused now —
    /// harmless to leave in place or remove.)
    ///
    /// TRADE-OFF, stated plainly (this was flagged before the redesign was
    /// chosen, repeating it here for anyone reading this file cold): this
    /// is no longer genuine world-anchored placement. Content doesn't stay
    /// at a fixed point in the real room the way Phase 21b's anchor fix
    /// made it do — it now always tracks back toward "in front of the
    /// camera." Hand-tremor jitter is DAMPENED by the smoothing below, not
    /// eliminated — `Position Smooth Time` and `Rotation Smooth Speed` are
    /// the two levers to trade responsiveness against smoothness on-device.
    ///
    /// Editor setup is otherwise unchanged from Phase 21b: this component
    /// still lives on a root-level `ARContentAnchor` GameObject, and
    /// `ArrayModule`/`StackModule`/`QueueModule`/`LinkedListModule` still
    /// sit under it as children at local (0,0,0) — that Hierarchy structure
    /// is still useful as a single shared root all four modules position
    /// relative to, independent of how that root's own pose is driven.
    ///
    /// 4. PITCH-BASED VERTICAL FOLLOW (this round): on-device testing of
    ///    continuous follow surfaced that tilting the phone up/down did
    ///    nothing — only physically raising/lowering the device moved
    ///    content vertically, since the target height was computed purely
    ///    from the camera's physical elevation, deliberately ignoring look
    ///    angle. Requested explicitly ("users might want to move their
    ///    devices downward or place them flat in real time" — e.g. angling
    ///    the phone down at a table surface should bring content down to
    ///    meet it). Implemented as a bounded ADDITION to the target height
    ///    only — `pitchTerm = rawForward.y * Placement Distance * Pitch
    ///    Height Influence` — not by tilting the whole content frame to
    ///    match camera pitch. Two deliberate choices here, both to avoid
    ///    the risk flagged before this was approved:
    ///    - Content's own rotation still only tracks flattened (yaw) camera
    ///      direction, same as before — so Stack/Array/etc. stay upright
    ///      and don't visually "topple" as you look down at them, which a
    ///      full pitch-matched rotation would have caused.
    ///    - `rawForward.y` is a plain float in [-1, 1] with no singularity
    ///      at any look angle (unlike the yaw-direction's LookRotation,
    ///      which needs the existing near-vertical fallback below) — so
    ///      this adds pitch sensitivity without adding a new degenerate
    ///      case. The only bound on how far this can move content is
    ///      `Placement Distance` itself (at most, looking straight up or
    ///      down shifts the target by one full Placement Distance).
    ///
    /// 5. FIELD-OF-VIEW MEASUREMENT (Phase 49a — measurement only, no
    ///    layout change yet): every module lays its content out at fixed
    ///    world-space coordinates, so how much of that content a device can
    ///    actually show depends on its physical camera's field of view,
    ///    independent of pixel resolution. That is the root cause behind
    ///    the header-clipping residual Phases 45 and 47 each narrowed
    ///    without closing (Migration Spec §8x/§8z: "No attempt to make the
    ///    layout adapt to actual device field of view"). Before changing
    ///    any layout, this round measures what each real device actually
    ///    shows — the project has been burned repeatedly by fixes built on
    ///    unverified assumptions, and nobody currently knows the Galaxy
    ///    A32's true frustum width or what width the existing tuning
    ///    assumed.
    ///
    ///    **Read from `Camera.projectionMatrix`, not `Camera.fieldOfView`.**
    ///    AR Foundation assigns the camera an explicit projection matrix
    ///    supplied by ARCore each frame; `fieldOfView` is not guaranteed to
    ///    reflect a projection matrix that was set directly, so reading it
    ///    would risk measuring a stale Unity-side value rather than the real
    ///    camera. For a standard symmetric perspective matrix,
    ///    `m00 == 1 / tan(horizontalFov / 2)`, which makes the horizontal
    ///    half-angle exact and cheap to derive.
    ///
    ///    **Reported into the research log, not to the screen or logcat.**
    ///    One row per session, `Action = "AR Frustum"`. Logcat would need
    ///    adb — precisely the friction Phase 48 existed to remove — and an
    ///    on-screen readout would need scene work. Going through the log
    ///    means the measurement travels off-device via Phase 48's own Share
    ///    Log button, and each device's own log carries its own number.
    ///    Worth keeping permanently rather than deleting after this round:
    ///    for a multi-device study, a record of what each participant's
    ///    device could actually display is research metadata, not debug
    ///    scaffolding, and it costs one row per session.
    ///
    ///    The distance-independent number is the horizontal FOV itself —
    ///    visible width at any distance d is `2 * d * tan(hFov / 2)`. The
    ///    worked example logged alongside it uses
    ///    `Placement Distance + Content Distance From Anchor` purely so the
    ///    row is readable without doing the arithmetic by hand.
    ///
    /// 6. FIT TO FIELD OF VIEW (Phase 49b — the actual fix, calibrated
    ///    against 49a's real measurements from the Galaxy A32, SM-A325F):
    ///
    ///    What the measurement found. hFOV 34.8–35.2 deg, giving 0.689 m of
    ///    visible width at 1.10 m. Against that, of the eight modules only
    ///    ONE genuinely overflows — Memory, at 110.7% of available width —
    ///    with Loops next at 96.0% (close enough that any variation clips
    ///    it) and the remaining six between 36% and 90%. Memory also turned
    ///    out to be an outlier in DEPTH, sitting at local Z 0.70 where every
    ///    other module sits at 0.47–0.50: it had already been pushed further
    ///    away to buy view space, and still did not fit. That is why the
    ///    residual survived Phases 45 and 47 — both were throttling text
    ///    growth on a module whose geometry was already over budget.
    ///
    ///    Because only one module actually overflows, a single worst-case
    ///    constant would have shrunk the other seven by ~19% for no reason,
    ///    making text smaller everywhere and working against the
    ///    accessibility text-size feature. So the correction is computed
    ///    PER MODULE from the live measurement above: Memory shrinks, Loops
    ///    barely, and the other six resolve to exactly 1 and render as they
    ///    always have. Shrink-only by decision — the scale is clamped at 1,
    ///    so no device ever renders content larger than the layout was
    ///    authored for.
    ///
    ///    Self-calibrating rather than constant-driven, which matters
    ///    beyond tidiness: it needs no reference device, adapts to text-size
    ///    changes automatically, and is immune to the one gap in 49a's data
    ///    (Memory was never captured at Extra Large, because the log row cap
    ///    cut it off) — whatever the real width turns out to be, it is
    ///    measured rather than assumed.
    ///
    ///    See `ComputeFitScale()` for why the correction is NOT the naive
    ///    available/required ratio, and `MeasureContentAndUpdateFit()` for
    ///    why the measurement is taken in local space.
    ///
    ///    Only the horizontal axis is corrected. In portrait the horizontal
    ///    FOV is the narrow one by roughly a factor of two (34.8 deg against
    ///    69.7 deg vertical on this device), and module content is wider
    ///    than it is tall, so vertical has never been the binding
    ///    constraint.
    /// </summary>
    public class ARContentAnchor : MonoBehaviour
    {
        [Header("AR reference (auto-found if left empty)")]
        [SerializeField] private Transform xrCamera;

        [Header("Placement offset from camera (unvalidated guess — confirm/tune on-device)")]
        [Tooltip("How far in front of the camera (metres) content is targeted, measured along the camera's forward direction flattened to the horizontal plane. Roughly matches every module's own default Ar Distance (0.5).")]
        [SerializeField] private float placementDistance = 0.6f;
        [Tooltip("Vertical offset (metres) applied to the camera's own current height — negative pulls content down toward typical table/floor height instead of eye level.")]
        [SerializeField] private float placementHeightOffset = -0.2f;
        [Tooltip("How much tilting the phone up/down (pitch) additionally shifts content's target height, as a multiplier on (the camera's raw, unflattened forward.y * Placement Distance). 0 = height only follows physical camera elevation, tilting does nothing (the original behavior). 1 = full effect — e.g. looking down ~45° pulls content down by roughly 0.7x Placement Distance, useful for angling the phone down at a table. Does not affect content's own rotation/orientation (still upright, yaw-only) or horizontal position — only where the vertical target sits.")]
        [SerializeField] private float pitchHeightInfluence = 1f;

        [Header("Follow smoothing (unvalidated guess — confirm/tune on-device)")]
        [Tooltip("How long (seconds) position takes to catch up to its target via Vector3.SmoothDamp. Larger = filters out more hand-tremor jitter but content visibly lags behind faster movement; smaller = tracks more tightly but lets more of the original 'swimming' feeling back in. Start small and increase only if jitter is bothersome on-device.")]
        [SerializeField] private float positionSmoothTime = 0.25f;
        [Tooltip("How quickly rotation catches up to its target, as an exponential smoothing rate (per second). Higher = snappier/less lag, lower = smoother but laggier. Rotation uses exponential Slerp rather than SmoothDamp since Unity has no built-in SmoothDamp for rotations.")]
        [SerializeField] private float rotationSmoothSpeed = 6f;

        [Header("Phase 49b — fit content to this device's field of view")]
        [Tooltip("Shrinks content just enough to fit inside what this device's camera can actually show, per module, using the live measurement below. Shrink-only: a module that already fits is left at scale 1 and renders exactly as it did before this phase. Turn off to compare against the uncorrected layout.")]
        [SerializeField] private bool fitContentToFieldOfView = true;
        [Tooltip("Headroom left between content and the frustum edge. 0.02 = fill at most 98% of the visible width. Measured content width is already the widest extent seen across viewing angles, so this is margin on top of a worst case, not on top of an average — which is why a small value is defensible here. Lowered from 0.05 after on-device testing: Memory fitted correctly at 5% but its text had traded a clipping problem for a legibility one, and margin is the only free lever (0.05 -> 0.02 buys Memory about 6% more apparent text size at no layout cost). Raising it shrinks content noticeably faster than you might expect — see the class comment on why the correction is not a simple ratio.")]
        [SerializeField, Range(0f, 0.3f)] private float fitSafetyMargin = 0.02f;
        [Tooltip("How quickly the fit scale eases toward its target, as an exponential rate per second. Prevents a visible snap when a module's measured width first settles.")]
        [SerializeField] private float fitScaleSmoothSpeed = 4f;
        [Tooltip("Hard floor on the fit scale, so a bad or unusual measurement can never shrink content to something unreadable. If a module ever needs more correction than this, it should be fixed in that module's own layout rather than by scaling further.")]
        [SerializeField, Range(0.2f, 1f)] private float minimumFitScale = 0.5f;
        [Tooltip("Ceiling on the fit scale. 1 = shrink-only: content never renders larger than the layout was authored for, which is how this started. Above 1 lets content GROW into space it isn't using, up to this multiple — worth it once a module has been narrowed enough to fit at 1.0 and is leaving screen width empty, which is exactly what happened to Memory after its columns were tightened. Applies to every module, so raising it also enlarges modules that already fit (Stack uses only ~27% of the width, for instance). 1.0 restores the original behaviour exactly. RANGE WIDENED FROM 2 TO 6 AT PHASE 52: the old Range(1, 2) meant a value of 2 was the slider's maximum rather than a chosen number, and Memory sat pinned against it with a third of the frustum still empty — the ceiling was binding and could not be reported as such. Raising this has DIMINISHING returns, not unlimited ones: content sits a fixed depth in front of the anchor, so scaling it up pushes it away almost as fast as it grows. The share of frustum width a module can reach tends to halfWidth / (contentLocalZ * tan(hFOV/2)) as this value rises, however large it gets.")]
        [SerializeField, Range(1f, 6f)] private float maximumFitScale = 1f;
        [Tooltip("Room held back at smaller text sizes so larger ones can grow into it. DEFAULTS TO 0, because measurement showed it was not needed and was doing harm. The concern was that the fit cancels the accessibility control — bigger text making content bigger, the fit shrinking it back. On the populated Memory table that never happened: its measured width barely moves between text settings (0.891 vs 0.894, because Phase 47 caps it), so the fit stays put and text growth passes straight through, giving a natural 1.27x ratio. The apparent cancellation was only in the EMPTY Overview state, where content is small enough that both settings pin to Maximum Fit Scale. Setting this above 0 inflates the extent at smaller text sizes, which for a module already at the frustum limit drives the fit into the Minimum Fit Scale floor: Memory rendered at 0.500 instead of 0.727 at Normal, a 31% loss, buying a ratio it already had. Left in place because a module whose width DOES grow with text scale would genuinely need it.")]
        [SerializeField, Range(0f, 1f)] private float textScaleHeadroom = 0f;

        [Tooltip("How much of the camera's VERTICAL field of view is actually usable for AR content, as a fraction. Not 1.0, because the dashboard's control panel and Summary/Instruction panels cover roughly the lower third of the screen — content behind them is rendered but hidden. Phase 50 added the vertical constraint because two-line Memory records roughly double that module's height, and a horizontal-only fit would have traded a sideways overflow for a downward one. Lower this if a tall module disappears under the panels; raise it toward 1 if there is obvious unused space above and below.")]
        [SerializeField, Range(0.3f, 1f)] private float usableVerticalFraction = 0.65f;

        [Tooltip("How many consecutive smaller measurements (one every 0.5s) before the fit RELEASES its high-water mark and re-sizes to the content actually on screen. The high-water mark exists so a momentary narrow frame mid-animation cannot make content jump; without a release it also means the WIDEST thing a module ever shows governs everything it shows afterwards. Memory is the case that exposed this: its Overview state is 1.45x the table's own width (Overview Scale Multiplier), so entering the module sets the mark at 0.89 m and every activity chosen afterwards is fitted for a width that left the screen long ago, rendering roughly a third smaller than it needs to. 4 samples = 2 seconds, far longer than any animation transient and short enough to feel like part of the transition. 0 restores the old permanently-sticky behaviour.")]
        [SerializeField, Range(0, 12)] private int lowerMeasurementSamplesBeforeRelease = 4;

        [Header("Phase 49a — field-of-view measurement (writes to the research log)")]
        [Tooltip("Records this device's actual camera field of view into the research log, one row per session (Action = \"AR Frustum\"), retrievable off-device via the Researcher Area's Share Log button. Leave ON: for a multi-device study this is a permanent record of what each participant's device could actually display, not temporary debug output, and it costs one row per session.")]
        [SerializeField] private bool logFrustumMeasurement = true;
        [Tooltip("Only used to make the logged row readable — the measurement itself is distance-independent (horizontal FOV). Set this to match the modules' own Ar Distance (0.5 by default) so the logged 'visible width' figure corresponds to where module content actually sits: Placement Distance + this value.")]
        [SerializeField] private float contentDistanceFromAnchor = 0.5f;

        [Header("Debug (temporary — added to diagnose a 'vertical follow does nothing' report; safe to leave off)")]
        [Tooltip("Logs camera height, computed target height, and this Transform's actual height to the Console/logcat twice a second. Turn on to check whether the target height is responding to camera movement at all, and whether the actual Transform position is tracking that target — this narrows down where a vertical-follow problem actually lives without needing another file exchange.")]
        [SerializeField] private bool debugLogHeight = false;

        public static ARContentAnchor Instance { get; private set; }

        private Vector3 smoothVelocity;
        private float debugLogTimer;

        // Phase 49a — frustum measurement state. Sampled repeatedly rather
        // than once, because ARCore does not necessarily have a valid
        // projection matrix on the first frames of a session; resampling and
        // logging only on material change avoids having to guess a "long
        // enough" startup delay, while the row cap keeps a jittery or
        // still-settling matrix from flooding the research log.
        private const int MaxFrustumLogRows = 3;
        private const float FrustumResampleInterval = 2f;
        private const float FrustumChangeThresholdDeg = 1.5f;

        private Camera xrCameraComponent;
        private int frustumRowsLogged;
        private float frustumSampleTimer;
        private float lastLoggedHorizontalFovDeg = -1f;
        private float tanHalfVerticalFov = -1f;

        // Phase 49a — content-width measurement state. Tracked per module
        // and reported as the WIDEST extent observed while that module was
        // open, not a snapshot: Billboard rotates cells to face the camera,
        // so a module's effective width genuinely varies with viewing
        // angle, and a shrink-to-fit correction has to accommodate the
        // worst case rather than whichever frame happened to be sampled.
        private const int MaxContentWidthRows = 16;
        private const float ContentWidthChangeThresholdMetres = 0.02f;

        // Phase 52a. The release row below is capped the same way the
        // content-width row is. Memory has fourteen activities, and a
        // participant moving among them releases the mark on each change —
        // uncapped, one session can bury the rows a researcher actually came
        // to read, in the file the Pilot Test Manual tells them to analyse.
        // Eight is enough to show the pattern and small enough to stay out
        // of the way. Session-wide rather than per-module, matching
        // MaxContentWidthRows.
        private const int MaxFitReleaseRows = 8;

        private int contentWidthRowsLogged;
        private int fitReleaseRowsLogged;
        private string lastMeasuredModule = "";
        private float widestContentThisModule = -1f;
        private float tallestContentThisModule = -1f;
        private float lastLoggedContentWidth = -1f;
        private float lastTextScaleSeen = -1f;

        // Phase 49b — fit-to-frustum state. Content is sampled more often
        // than it is logged: the correction wants to engage promptly when a
        // module opens, while the log only wants a row when the measurement
        // says something new.
        private const float ContentSampleInterval = 0.5f;

        // Consecutive samples measured meaningfully narrower than the
        // current high-water mark. See lowerMeasurementSamplesBeforeRelease.
        private int consecutiveSmallerSamples;

        private float contentSampleTimer;
        private float tanHalfHorizontalFov = -1f;
        private float targetFitScale = 1f;
        private float currentFitScale = 1f;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (xrCamera == null && Camera.main != null) xrCamera = Camera.main.transform;

            if (xrCamera == null)
            {
                Debug.LogError("ARContentAnchor: no camera found — content cannot be placed.");
                return;
            }

            xrCameraComponent = xrCamera.GetComponent<Camera>();

            // Snap immediately to the target pose on the very first frame so
            // content doesn't visibly glide in from an arbitrary starting
            // Transform — matches the existing "appears automatically" UX.
            // Every frame after this one, Update() takes over with smoothing.
            Pose initialPose = ComputeTargetPose();
            transform.SetPositionAndRotation(initialPose.position, initialPose.rotation);
        }

        private void Update()
        {
            if (xrCamera == null) return;

            Pose targetPose = ComputeTargetPose();

            transform.position = Vector3.SmoothDamp(
                transform.position, targetPose.position, ref smoothVelocity, positionSmoothTime);

            float rotationLerpT = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetPose.rotation, rotationLerpT);

            if (debugLogHeight)
            {
                debugLogTimer += Time.deltaTime;
                if (debugLogTimer >= 0.5f)
                {
                    debugLogTimer = 0f;
                    Debug.Log($"ARContentAnchor height debug — camY={xrCamera.position.y:F3} " +
                              $"targetY={targetPose.position.y:F3} actualY={transform.position.y:F3} " +
                              $"pitchInfluence={pitchHeightInfluence:F2}");
                }
            }

            frustumSampleTimer += Time.deltaTime;
            if (frustumSampleTimer >= FrustumResampleInterval)
            {
                frustumSampleTimer = 0f;
                MeasureFrustum();
            }

            contentSampleTimer += Time.deltaTime;
            if (contentSampleTimer >= ContentSampleInterval)
            {
                contentSampleTimer = 0f;
                MeasureContentAndUpdateFit();
            }

            // Phase 49b — eased so the correction settling in never reads as
            // a snap. Applied to localScale only; position and rotation above
            // are untouched by this.
            if (!fitContentToFieldOfView) targetFitScale = 1f;

            if (!Mathf.Approximately(currentFitScale, targetFitScale))
            {
                float fitLerpT = 1f - Mathf.Exp(-fitScaleSmoothSpeed * Time.deltaTime);
                currentFitScale = Mathf.Lerp(currentFitScale, targetFitScale, fitLerpT);
                transform.localScale = Vector3.one * currentFitScale;
            }
        }

        /// <summary>
        /// Phase 49a. Measures what this device's AR camera can actually
        /// show and records it in the research log — see class comment
        /// point 5 for why this reads the projection matrix rather than
        /// Camera.fieldOfView, and why it reports through the log rather
        /// than the screen or logcat.
        ///
        /// Silently returns without logging when the projection matrix
        /// isn't usable yet (ARCore still initialising) or when the value
        /// hasn't materially changed since the last row — so this can be
        /// called repeatedly without needing a guessed startup delay and
        /// without filling the log with near-identical rows.
        /// </summary>
        private void MeasureFrustum()
        {
            if (xrCameraComponent == null) return;

            // For a standard symmetric perspective projection,
            // m00 == 1 / tan(horizontalFov / 2) and m11 == 1 / tan(verticalFov / 2).
            float m00 = xrCameraComponent.projectionMatrix.m00;
            float m11 = xrCameraComponent.projectionMatrix.m11;

            if (m00 <= 0.0001f || float.IsNaN(m00) || float.IsInfinity(m00))
            {
                // Not yet a usable projection matrix — try again next interval.
                return;
            }

            float tanHalfHorizontal = 1f / m00;

            // Stored on every valid sample, not just on the ones that get
            // logged — this is what Phase 49b's fit calculation reads, and it
            // must stay current even once the log has said all it needs to.
            tanHalfHorizontalFov = tanHalfHorizontal;

            float horizontalFovDeg = 2f * Mathf.Atan(tanHalfHorizontal) * Mathf.Rad2Deg;

            if (m11 > 0.0001f && !float.IsNaN(m11) && !float.IsInfinity(m11))
            {
                tanHalfVerticalFov = 1f / m11;
            }

            if (!logFrustumMeasurement || frustumRowsLogged >= MaxFrustumLogRows) return;

            if (lastLoggedHorizontalFovDeg >= 0f &&
                Mathf.Abs(horizontalFovDeg - lastLoggedHorizontalFovDeg) < FrustumChangeThresholdDeg)
            {
                // Same camera as the row already recorded — nothing new to say.
                return;
            }

            float verticalFovDeg = (m11 > 0.0001f && !float.IsNaN(m11) && !float.IsInfinity(m11))
                ? 2f * Mathf.Atan(1f / m11) * Mathf.Rad2Deg
                : 0f;

            // Distance-independent measurement is the FOV itself; this
            // worked example just makes the row readable at a glance.
            float contentDistance = placementDistance + contentDistanceFromAnchor;
            float visibleWidthAtContent = 2f * contentDistance * tanHalfHorizontal;

            lastLoggedHorizontalFovDeg = horizontalFovDeg;
            frustumRowsLogged++;

            ResearchLogger.Instance?.LogEvent(
                "AR Frustum",
                visibleWidthAtContent.ToString("F3") + " m",
                "Measured",
                "Visible width " + visibleWidthAtContent.ToString("F3") + " m at " +
                contentDistance.ToString("F2") + " m (placement " +
                placementDistance.ToString("F2") + " + content " +
                contentDistanceFromAnchor.ToString("F2") + ") | hFOV " +
                horizontalFovDeg.ToString("F1") + " deg | vFOV " +
                verticalFovDeg.ToString("F1") + " deg | tan(hFOV/2) " +
                tanHalfHorizontal.ToString("F4") + " | screen " +
                Screen.width + "x" + Screen.height + " | device " +
                SystemInfo.deviceModel + " | sample " + frustumRowsLogged);
        }

        /// <summary>
        /// Phase 49a. Measures how wide the currently-visible module content
        /// actually is, so the correction in Phase 49b can be calibrated
        /// against real content rather than against Phase 31/32's figures
        /// (Memory ≈0.8 m, Loops ≈0.72 m, Array/List ≈0.54 m) — which
        /// predate the accessibility text-size work entirely, and so
        /// understate width at Large/Extra Large exactly where clipping is
        /// most likely.
        ///
        /// **Measured in this anchor's LOCAL space, deliberately.** Local
        /// extent is unaffected by this anchor's own scale, so a scale
        /// correction applied here later cannot feed back into the
        /// measurement that produced it — no oscillation, and the logged
        /// number stays directly comparable across sessions whatever
        /// correction is in force. At scale 1 the local figure is also the
        /// world figure in metres, which is what makes it directly
        /// comparable to the frustum width in the row above.
        ///
        /// Reports the widest extent seen while a given module was open,
        /// re-armed whenever the module changes, so each module gets its own
        /// row and the binding constraint can be identified from the log
        /// rather than assumed to still be Memory.
        /// </summary>
        private void MeasureContentAndUpdateFit()
        {
            string moduleName = ResearchLogger.Instance != null
                ? ResearchLogger.Instance.ModuleName
                : "";

            float textScale = LandingPageController.CurrentTextScale;

            // Re-arm on either change. Module is obvious. Text scale matters
            // because the high-water mark is deliberately sticky against
            // animation transients — without this reset, dropping from Extra
            // Large back to Normal would leave content shrunk for a width
            // that is no longer on screen.
            if (moduleName != lastMeasuredModule || !Mathf.Approximately(textScale, lastTextScaleSeen))
            {
                lastMeasuredModule = moduleName;
                lastTextScaleSeen = textScale;
                widestContentThisModule = -1f;
                tallestContentThisModule = -1f;
                lastLoggedContentWidth = -1f;
                consecutiveSmallerSamples = 0;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            int counted = 0;

            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;

                Bounds worldBounds = r.bounds;

                // Eight corners of the world-space AABB, each converted into
                // this anchor's local space. Transforming corners rather than
                // the centre/extents keeps the result correct however the
                // anchor is currently rotated.
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? worldBounds.min.x : worldBounds.max.x,
                        (i & 2) == 0 ? worldBounds.min.y : worldBounds.max.y,
                        (i & 4) == 0 ? worldBounds.min.z : worldBounds.max.z);

                    Vector3 local = transform.InverseTransformPoint(corner);

                    if (local.x < minX) minX = local.x;
                    if (local.x > maxX) maxX = local.x;
                    if (local.y < minY) minY = local.y;
                    if (local.y > maxY) maxY = local.y;
                    if (local.z < minZ) minZ = local.z;
                    if (local.z > maxZ) maxZ = local.z;
                }

                counted++;
            }

            if (counted == 0) return;

            float width = maxX - minX;
            if (width <= 0.0001f) return;

            // Measured, rather than assumed from Content Distance From
            // Anchor — this is where content actually sits along the
            // anchor's forward axis, which the fit calculation needs in
            // order to account for scaling pulling content nearer as well
            // as smaller.
            float contentLocalZ = (minZ + maxZ) * 0.5f;

            // Phase 50 — vertical half-extent, using the same centred
            // convention as the horizontal measurement above: half the total
            // span, not the distance from the anchor origin to the furthest
            // edge. The distinction is not academic. Memory's table hangs far
            // below its title, so measuring from the origin reports 0.876 m
            // where the content is really 1.03 m tall — which would demand a
            // fit scale of 0.41, clamp to the 0.5 floor, and render Memory at
            // half size: worse than the problem being solved.
            //
            // Centring is the right assumption because it is achievable:
            // Placement Height Offset and Pitch Height Influence are exactly
            // the controls that sit content vertically in the view, the same
            // way every module centres itself horizontally by construction.
            float halfHeight = (maxY - minY) * 0.5f;

            bool isNewWidest = width > widestContentThisModule;

            if (isNewWidest)
            {
                widestContentThisModule = width;
                consecutiveSmallerSamples = 0;
            }
            else if (lowerMeasurementSamplesBeforeRelease > 0
                     && width < widestContentThisModule - ContentWidthChangeThresholdMetres)
            {
                // Phase 52. Sustained-smaller release. The high-water mark
                // protects against a narrow frame mid-animation; it was not
                // meant to let the widest state a module ever shows govern
                // every state after it. An animation transient lasts a frame
                // or two, so requiring several consecutive smaller samples
                // separates the two cases without needing any module to tell
                // this component that its content changed.
                consecutiveSmallerSamples++;

                if (consecutiveSmallerSamples >= lowerMeasurementSamplesBeforeRelease)
                {
                    float releasedFrom = widestContentThisModule;

                    // Both extents belong to the same content state, so they
                    // are adopted together rather than leaving a stale height
                    // paired with a fresh width.
                    widestContentThisModule = width;
                    tallestContentThisModule = halfHeight;
                    consecutiveSmallerSamples = 0;

                    // Re-arm the log gate so the newly-adopted width is
                    // recorded — this is exactly the event worth having in
                    // the record when a module later looks wrongly sized.
                    lastLoggedContentWidth = -1f;

                    // The release itself always happens; only the reporting
                    // of it is capped. Silently dropping the row is the right
                    // trade — the fit is corrected either way, and the row
                    // exists to explain a sizing surprise, which the first
                    // few occurrences already do.
                    if (fitReleaseRowsLogged < MaxFitReleaseRows)
                    {
                        fitReleaseRowsLogged++;

                        ResearchLogger.Instance?.LogEvent(
                            "AR Fit Released",
                            width.ToString("F3") + " m",
                            "Measured",
                            "Fit released from a stale high-water mark of " +
                            releasedFrom.ToString("F3") + " m to the content now on screen (" +
                            width.ToString("F3") + " m) after " +
                            lowerMeasurementSamplesBeforeRelease + " consecutive smaller samples | module " +
                            (string.IsNullOrEmpty(moduleName) ? "unknown" : moduleName) +
                            " | text scale " + textScale.ToString("F2") +
                            " | release row " + fitReleaseRowsLogged + " of " + MaxFitReleaseRows);
                    }
                }
            }
            else
            {
                consecutiveSmallerSamples = 0;
            }

            if (halfHeight > tallestContentThisModule) tallestContentThisModule = halfHeight;

            // Phase 50 Round 4 — reserve room for larger text settings.
            //
            // Without this, the fit cancels the accessibility control: Extra
            // Large makes content 1.3x bigger, the fit divides by 1.3 to keep
            // it filling the frustum, and the apparent size is identical to
            // Normal. Confirmed on device — the two were indistinguishable.
            //
            // The fix is to size every setting as though it were already at
            // the largest one, by inflating the measured extent in inverse
            // proportion to the current text scale. At Extra Large the factor
            // is 1 and content fills the view; at Normal it is 1.3, so the
            // fit holds content to 1/1.3 of the view and the remaining room
            // is what Extra Large later grows into.
            //
            // Modules with spare room are unaffected, because they are capped
            // by Maximum Fit Scale rather than by the frustum — they keep
            // their Normal size AND gain the scaling. Only a module already
            // at the limit pays, by giving up size at Normal. That is the
            // right way round: an accessibility control that does nothing is
            // worse than content that is smaller at the default setting.
            float headroom = Mathf.Lerp(
                1f,
                LandingPageController.MaxTextScale / Mathf.Max(0.01f, textScale),
                Mathf.Clamp01(textScaleHeadroom));

            // Always recompute against the running worst case, even on
            // samples that don't set a new one — the frustum measurement may
            // only have become available after the width did.
            targetFitScale = ComputeFitScale(
                widestContentThisModule * headroom,
                tallestContentThisModule * headroom,
                contentLocalZ);

            if (!isNewWidest) return;
            if (contentWidthRowsLogged >= MaxContentWidthRows) return;
            if (width <= lastLoggedContentWidth + ContentWidthChangeThresholdMetres) return;

            lastLoggedContentWidth = width;
            contentWidthRowsLogged++;

            ResearchLogger.Instance?.LogEvent(
                "AR Content Width",
                width.ToString("F3") + " m",
                "Measured",
                "Widest content " + width.ToString("F3") + " m (local, scale-1 metres) | " +
                "content local Z " + contentLocalZ.ToString("F3") + " m | text scale " +
                textScale.ToString("F2") + " | renderers " + counted +
                " | half-height " + tallestContentThisModule.ToString("F3") + " m" +
                " | headroom " + headroom.ToString("F2") +
                " | fit scale " + targetFitScale.ToString("F3") +
                " | apparent " + (targetFitScale * textScale).ToString("F2") +
                " | row " + contentWidthRowsLogged);
        }

        /// <summary>
        /// Phase 49b. Returns the uniform scale at which content of the given
        /// local width, sitting at the given local Z, fits inside this
        /// device's measured horizontal frustum with the configured margin.
        /// Clamped to 1 (shrink-only, by decision) and floored at
        /// <c>minimumFitScale</c>.
        ///
        /// **Why this is not simply available / required.** Scaling this
        /// anchor shrinks content AND pulls it nearer, because module content
        /// sits at a local Z offset beneath it. Nearer content subtends a
        /// LARGER angle, so a naive ratio under-corrects — measured on the
        /// Galaxy A32, Memory's naive ratio was 0.903 while the value that
        /// actually fits is 0.812. Solving properly:
        ///
        ///   required:  k*W &lt;= s * 2 * (P + k*Z) * t
        ///   rearranged: k * (W - 2*s*Z*t) &lt;= 2*s*P*t
        ///
        /// where W is content width, Z its local depth, P is Placement
        /// Distance, t is tan(hFOV/2) and s is (1 - margin). Both W and Z are
        /// measured in local space and so are independent of k, which is what
        /// makes this a closed form rather than something needing iteration —
        /// and what stops the correction from feeding back into its own
        /// measurement.
        ///
        /// When the denominator is zero or negative the content fits at any
        /// scale (it grows more slowly than the frustum widens), so no
        /// correction is needed.
        /// </summary>
        private float ComputeFitScale(float contentWidth, float contentHalfHeight, float contentLocalZ)
        {
            if (!fitContentToFieldOfView) return 1f;
            if (tanHalfHorizontalFov <= 0f) return 1f;   // frustum not measured yet
            if (contentWidth <= 0.0001f) return 1f;

            float ceiling = Mathf.Max(1f, maximumFitScale);
            float s = 1f - Mathf.Clamp01(fitSafetyMargin);

            float k = AxisFitScale(contentWidth * 0.5f, contentLocalZ, tanHalfHorizontalFov, s, ceiling);

            // Phase 50 — the vertical axis binds too, now that Memory's
            // records are two lines tall. Whichever axis is tighter wins.
            if (tanHalfVerticalFov > 0f && contentHalfHeight > 0.0001f)
            {
                float usableVertical = tanHalfVerticalFov * Mathf.Clamp01(usableVerticalFraction);
                float kVertical = AxisFitScale(contentHalfHeight, contentLocalZ, usableVertical, s, ceiling);
                k = Mathf.Min(k, kVertical);
            }

            return Mathf.Clamp(k, minimumFitScale, ceiling);
        }

        /// <summary>
        /// Phase 50. One axis of the fit, shared by both so the horizontal
        /// and vertical constraints cannot drift apart.
        ///
        /// Solves <c>k*halfExtent &lt;= s * (P + k*Z) * tanHalf</c> for k. The
        /// half-extent form is what makes this reusable: horizontally it is
        /// half the measured width (content is centred on the anchor by
        /// construction in every module), vertically it is the larger of the
        /// two directions, since content is not vertically centred.
        ///
        /// A non-positive denominator means the content's extent grows more
        /// slowly than the frustum widens as it recedes, so it fits at any
        /// scale — return the ceiling rather than 1, since there is room and
        /// the caller has said whether to use it.
        /// </summary>
        private float AxisFitScale(float halfExtent, float contentLocalZ, float tanHalf, float s, float ceiling)
        {
            if (tanHalf <= 0f || halfExtent <= 0.0001f) return ceiling;

            float denominator = halfExtent - s * contentLocalZ * tanHalf;

            if (denominator <= 0.0001f) return ceiling;

            return (s * placementDistance * tanHalf) / denominator;
        }

        // Godot has no equivalent — pure Unity AR math. Computes a point a
        // fixed distance in front of the camera, flattened to the
        // horizontal plane for HORIZONTAL positioning and rotation (so
        // looking up/down doesn't sweep content sideways or tilt it off
        // upright), at a height offset from the camera's own current
        // height rather than an absolute world height (so this still makes
        // sense regardless of where/how the session started) — PLUS a
        // bounded pitch-driven adjustment to that height, so tilting the
        // phone up/down also moves content vertically (see class comment
        // point 4).
        private Pose ComputeTargetPose()
        {
            // Captured before flattening — used only for the vertical
            // pitch term below, a plain float in [-1, 1] with no
            // singularity at any look angle.
            float rawForwardY = xrCamera.forward.y;

            Vector3 flatForward = xrCamera.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.0001f)
            {
                // Camera looking almost straight up/down — fall back to its
                // up vector so we still get a sensible horizontal direction.
                flatForward = xrCamera.up;
                flatForward.y = 0f;
            }

            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();

            float pitchTerm = rawForwardY * placementDistance * pitchHeightInfluence;

            Vector3 targetPosition = xrCamera.position + flatForward * placementDistance;
            targetPosition.y = xrCamera.position.y + placementHeightOffset + pitchTerm;

            return new Pose(targetPosition, Quaternion.LookRotation(flatForward, Vector3.up));
        }
    }
}
