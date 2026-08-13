using UnityEngine;

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

        [Header("Debug (temporary — added to diagnose a 'vertical follow does nothing' report; safe to leave off)")]
        [Tooltip("Logs camera height, computed target height, and this Transform's actual height to the Console/logcat twice a second. Turn on to check whether the target height is responding to camera movement at all, and whether the actual Transform position is tracking that target — this narrows down where a vertical-follow problem actually lives without needing another file exchange.")]
        [SerializeField] private bool debugLogHeight = false;

        public static ARContentAnchor Instance { get; private set; }

        private Vector3 smoothVelocity;
        private float debugLogTimer;

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
