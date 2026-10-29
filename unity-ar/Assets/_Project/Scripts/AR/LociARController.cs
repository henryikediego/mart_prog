using System.Collections;
using System.Collections.Generic;
using Loci.Bridge;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace Loci.AR
{
    /// <summary>
    /// Connects the shell contract to the AR session, plane raycast, and the
    /// shared content root. Shell-owned screens remain outside Unity.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class LociARController : MonoBehaviour
    {
        private static readonly List<ARRaycastHit> RaycastHits = new List<ARRaycastHit>();

        [Header("Scene references")]
        [SerializeField] private ARSession arSession;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private GameObject contentRoot;

        [Header("Candidate surface")]
        [SerializeField] private Vector2 reticleViewportPoint = new Vector2(0.5f, 0.5f);

        [Header("Smooth camera follow (kept from the existing app)")]
        [SerializeField, Min(0.05f)] private float placementDistance = 0.6f;
        [SerializeField] private float placementHeightOffset = -0.2f;
        [SerializeField] private float pitchHeightInfluence = 1f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.25f;
        [SerializeField, Min(0.01f)] private float rotationSmoothSpeed = 6f;

        private ARRaycastManager raycastManager;
        private ARCameraManager cameraManager;
        private ARCameraBackground cameraBackground;
        private Camera arCamera;
        private Pose candidatePose;
        private Vector3 followVelocity;
        private LociSessionState currentState = LociSessionState.Idle;
        private LociTrackingReason currentReason;
        private bool hasReason;
        private bool availabilityChecked;
        private bool unityReadySent;
        private bool sessionStartRequested;
        private int sessionRequestVersion;
        private bool sessionRunning;
        private bool hasTracked;
        private bool candidateKnown;
        private bool candidateValid;
        private bool placed;

        public static LociARController Instance { get; private set; }
        public Transform ContentRoot => contentRoot != null ? contentRoot.transform : null;
        public bool IsPlaced => placed;

        private void Awake()
        {
            Instance = this;

            if (arSession == null)
                arSession = FindAnyObjectByType<ARSession>();
            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();
            if (xrOrigin == null)
                xrOrigin = FindAnyObjectByType<XROrigin>();

            if (xrOrigin != null)
            {
                arCamera = xrOrigin.Camera;
                if (arCamera != null)
                {
                    cameraManager = arCamera.GetComponent<ARCameraManager>();
                    cameraBackground = arCamera.GetComponent<ARCameraBackground>();
                }
                raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
                if (raycastManager == null)
                    raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
                raycastManager.enabled = false;
            }

            if (cameraManager != null)
                cameraManager.enabled = false;
            if (cameraBackground != null)
                cameraBackground.enabled = false;

            if (arSession != null)
                arSession.enabled = false;
            if (planeManager != null)
                planeManager.enabled = false;

            if (contentRoot == null)
                contentRoot = new GameObject("Loci Content Root");
            contentRoot.SetActive(false);
        }

        private void OnEnable()
        {
            SpikeBridge.ShellCommandReceived += OnShellCommand;
            ARSession.stateChanged += OnARSessionStateChanged;
        }

        private void Start()
        {
            StartCoroutine(CheckAvailabilityAndReportReady());
        }

        private void OnDisable()
        {
            SpikeBridge.ShellCommandReceived -= OnShellCommand;
            ARSession.stateChanged -= OnARSessionStateChanged;
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!sessionRunning || ARSession.state != ARSessionState.SessionTracking)
                return;

            if (placed)
            {
                FollowCameraSmoothly();
                return;
            }

            UpdatePlacementCandidate();
        }

        private IEnumerator CheckAvailabilityAndReportReady()
        {
            yield return ARSession.CheckAvailability();
            availabilityChecked = true;
            SendUnityReady(MapAvailability(ARSession.state));

            if (sessionStartRequested)
                StartARSession();
        }

        private void OnShellCommand(LociMessageEnvelope message, ILociMessagePayload payload)
        {
            switch (message.Type)
            {
                case LociMessageTypes.SessionStart:
                    if (sessionRunning || sessionStartRequested)
                        break;
                    sessionRequestVersion++;
                    sessionStartRequested = true;
                    SetState(LociSessionState.Starting);
                    if (availabilityChecked)
                        StartARSession();
                    break;

                case LociMessageTypes.SessionStop:
                    StopARSession();
                    break;

                case LociMessageTypes.PlacementConfirm:
                    ConfirmPlacement();
                    break;

                case LociMessageTypes.PlacementReset:
                    ResetPlacement();
                    break;

                case LociMessageTypes.TopicOpen:
                    if (!placed)
                    {
                        SendError(
                            LociErrorCode.E106,
                            "Place the AR content before opening a topic.",
                            true);
                    }
                    break;
            }
        }

        private void StartARSession()
        {
            if (!sessionStartRequested)
                return;

            if (ARSession.state == ARSessionState.Unsupported)
            {
                sessionStartRequested = false;
                SetState(LociSessionState.Idle);
                SendError(LociErrorCode.E102, "This device does not support AR.", false);
                return;
            }

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                StartCoroutine(InstallARSoftwareThenStart(sessionRequestVersion));
                return;
            }

            sessionStartRequested = false;
            EnableARSession();
        }

        private IEnumerator InstallARSoftwareThenStart(int requestVersion)
        {
            SetState(LociSessionState.Starting);
            yield return ARSession.Install();

            if (requestVersion != sessionRequestVersion || !sessionStartRequested)
                yield break;

            if (ARSession.state == ARSessionState.Ready)
            {
                sessionStartRequested = false;
                EnableARSession();
                yield break;
            }

            sessionStartRequested = false;
            SetState(LociSessionState.Idle);
            SendError(LociErrorCode.E103, "AR software could not be installed or updated.", true);
        }

        private void EnableARSession()
        {
            if (arSession == null || planeManager == null || raycastManager == null ||
                cameraManager == null || cameraBackground == null || arCamera == null)
            {
                SetState(LociSessionState.Idle);
                SendError(LociErrorCode.E104, "The AR scene is missing a required session component.", true);
                return;
            }

            sessionRunning = true;
            hasTracked = false;
            placed = false;
            candidateKnown = false;
            candidateValid = false;
            followVelocity = Vector3.zero;

            raycastManager.enabled = true;
            planeManager.enabled = true;
            cameraManager.enabled = true;
            cameraBackground.enabled = true;
            arSession.enabled = true;
            SetState(LociSessionState.Starting);
        }

        private void StopARSession()
        {
            sessionRequestVersion++;
            sessionStartRequested = false;
            sessionRunning = false;
            hasTracked = false;
            placed = false;

            SetCandidate(false);
            if (contentRoot != null)
                contentRoot.SetActive(false);
            if (raycastManager != null)
                raycastManager.enabled = false;
            if (planeManager != null)
                planeManager.enabled = false;
            if (cameraBackground != null)
                cameraBackground.enabled = false;
            if (cameraManager != null)
                cameraManager.enabled = false;
            if (arSession != null)
                arSession.enabled = false;

            SetState(LociSessionState.Idle);
        }

        private void UpdatePlacementCandidate()
        {
            if (raycastManager == null || arCamera == null)
                return;

            Vector2 screenPoint = new Vector2(
                Screen.width * reticleViewportPoint.x,
                Screen.height * reticleViewportPoint.y);

            bool found = raycastManager.Raycast(
                screenPoint,
                RaycastHits,
                TrackableType.PlaneWithinPolygon);

            if (found && RaycastHits.Count > 0)
            {
                candidatePose = RaycastHits[0].pose;
                SetCandidate(true);
                SetState(LociSessionState.SurfaceFound);
            }
            else
            {
                SetCandidate(false);
                SetState(LociSessionState.Scanning);
            }
        }

        private void ConfirmPlacement()
        {
            if (!sessionRunning || !candidateKnown || !candidateValid || placed)
                return;

            placed = true;
            if (contentRoot != null)
            {
                contentRoot.transform.SetPositionAndRotation(candidatePose.position, candidatePose.rotation);
                contentRoot.SetActive(true);
            }

            SetState(LociSessionState.Placed);
            SpikeBridge.SendMessageToShell(
                LociMessageTypes.PlacementConfirmed,
                new PlacementConfirmedPayload());
        }

        private void ResetPlacement()
        {
            if (!sessionRunning)
                return;

            placed = false;
            if (contentRoot != null)
                contentRoot.SetActive(false);
            followVelocity = Vector3.zero;
            candidateKnown = false;
            candidateValid = false;
            if (ARSession.state == ARSessionState.SessionTracking)
                SetState(LociSessionState.Scanning);
            else if (hasTracked)
                SetState(LociSessionState.TrackingLost, MapTrackingReason(ARSession.notTrackingReason));
            else
                SetState(LociSessionState.Starting);
        }

        private void FollowCameraSmoothly()
        {
            if (arCamera == null || contentRoot == null)
                return;

            Vector3 rawForward = arCamera.transform.forward;
            Vector3 flatForward = rawForward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = arCamera.transform.up;
                flatForward.y = 0f;
            }
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;
            flatForward.Normalize();

            float pitchTerm = rawForward.y * placementDistance * pitchHeightInfluence;
            Vector3 targetPosition = arCamera.transform.position + flatForward * placementDistance;
            targetPosition.y = arCamera.transform.position.y + placementHeightOffset + pitchTerm;

            Transform root = contentRoot.transform;
            root.position = Vector3.SmoothDamp(
                root.position,
                targetPosition,
                ref followVelocity,
                positionSmoothTime);

            float rotationLerp = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            root.rotation = Quaternion.Slerp(
                root.rotation,
                Quaternion.LookRotation(flatForward, Vector3.up),
                rotationLerp);
        }

        private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            if (!sessionRunning)
                return;

            if (args.state == ARSessionState.SessionTracking)
            {
                hasTracked = true;
                SetState(placed ? LociSessionState.Placed : LociSessionState.Scanning);
            }
            else if (args.state == ARSessionState.SessionInitializing)
            {
                if (hasTracked)
                {
                    SetCandidate(false);
                    SetState(LociSessionState.TrackingLost, MapTrackingReason(ARSession.notTrackingReason));
                }
                else
                {
                    SetState(LociSessionState.Starting);
                }
            }
        }

        private void SetCandidate(bool valid)
        {
            if (candidateKnown && candidateValid == valid)
                return;

            candidateKnown = true;
            candidateValid = valid;
            SpikeBridge.SendMessageToShell(
                LociMessageTypes.PlacementCandidate,
                new PlacementCandidatePayload { Valid = valid });
        }

        private void SetState(LociSessionState state, LociTrackingReason? reason = null)
        {
            if (currentState == state && hasReason == reason.HasValue &&
                (!reason.HasValue || currentReason == reason.Value))
                return;

            currentState = state;
            hasReason = reason.HasValue;
            if (reason.HasValue)
                currentReason = reason.Value;

            SpikeBridge.SendMessageToShell(
                LociMessageTypes.SessionState,
                new SessionStatePayload
                {
                    State = state,
                    Reason = reason
                });
        }

        private void SendUnityReady(LociArAvailability availability)
        {
            if (unityReadySent)
                return;

            unityReadySent = true;
            SpikeBridge.SendMessageToShell(
                LociMessageTypes.UnityReady,
                new UnityReadyPayload
                {
                    ContractVersion = 1,
                    UnityVersion = Application.unityVersion,
                    ArAvailability = availability
                });
        }

        private void SendError(LociErrorCode code, string message, bool recoverable)
        {
            SpikeBridge.SendMessageToShell(
                LociMessageTypes.Error,
                new ErrorPayload
                {
                    Code = code,
                    Message = message,
                    Recoverable = recoverable
                });
        }

        private static LociArAvailability MapAvailability(ARSessionState state)
        {
            switch (state)
            {
                case ARSessionState.Ready:
                case ARSessionState.SessionInitializing:
                case ARSessionState.SessionTracking:
                    return LociArAvailability.Supported;
                case ARSessionState.NeedsInstall:
                case ARSessionState.Installing:
                    return LociArAvailability.NeedsInstall;
                default:
                    return LociArAvailability.Unsupported;
            }
        }

        private static LociTrackingReason MapTrackingReason(NotTrackingReason reason)
        {
            switch (reason)
            {
                case NotTrackingReason.Initializing: return LociTrackingReason.Initializing;
                case NotTrackingReason.Relocalizing: return LociTrackingReason.Relocalizing;
                case NotTrackingReason.InsufficientLight: return LociTrackingReason.InsufficientLight;
                case NotTrackingReason.InsufficientFeatures: return LociTrackingReason.InsufficientFeatures;
                case NotTrackingReason.ExcessiveMotion: return LociTrackingReason.ExcessiveMotion;
                case NotTrackingReason.Unsupported: return LociTrackingReason.Unsupported;
                default: return LociTrackingReason.Unknown;
            }
        }
    }
}
