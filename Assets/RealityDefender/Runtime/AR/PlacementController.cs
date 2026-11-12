using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
#if UNITY_ANDROID
using UnityEngine.Android;
using UnityEngine.XR.ARCore;
#endif

namespace RealityDefender
{
    public enum ExperienceMode { Menu, AR, Practice }
    public enum PlacementState { Menu, Checking, Scanning, Placing, Playing, Unavailable }

    /// <summary>Owns mode lifetime, the single accepted placement, and tracked-play gating.</summary>
    public sealed class PlacementController : MonoBehaviour
    {
        // Any tracked floor point at least 25 cm from the phone camera; no upper limit beyond what ARCore detects.
        public const float MinimumARPlacementDistance = 0.25f;
        public const float MaximumARPlacementDistance = float.PositiveInfinity;
        [Header("AR rig (inactive until the explicit AR action succeeds)")]
        public GameObject arRig;
        public ARSession session;
        public XROrigin origin;
        public ARPlaneManager planeManager;
        public ARRaycastManager raycastManager;
        public ARAnchorManager anchorManager;
        public Camera arCamera;
        [Header("Shared game and camera-free practice")]
        public Camera practiceCamera;
        public GameObject practiceEnvironment;
        public GameBase basePrefab;
        public WaveController wave;
        public Material planeMaterial;
        [Tooltip("Soft radial decal under the base so it reads as resting on the real surface.")]
        public Material contactShadowMaterial;
        [Tooltip("Soft ground ring shown while finding the floor (RealityDefender/Placement Ring).")]
        public Material placementRingMaterial;
        [Tooltip("Uniform size of the AR arena. 1 = 0.65 m wide base; 2.5 = about 1.6 m wide.")]
        [Range(1f, 4f)] public float arenaScale = 2.5f;
        [Min(0f)] public float practiceFloorHeight;
        [Range(0.03f, 0.4f)] public float aimDegreesPerPixel = 0.12f;

        public ExperienceMode Mode { get; private set; } = ExperienceMode.Menu;
        public PlacementState State { get; private set; } = PlacementState.Menu;
        public string Status { get; private set; } = "Choose AR or Practice Mode";
        public Camera ActiveCamera => Mode == ExperienceMode.AR ? arCamera : practiceCamera;
        public bool HasPlaced => placedBase != null;
        public GameBase PlacedBase => placedBase;
        public event Action Changed;
        public event Action<GameBase, Camera> Placed;

        readonly List<ARRaycastHit> hits = new List<ARRaycastHit>(8);
        readonly List<ARRaycastHit> previewHits = new List<ARRaycastHit>(8);
        readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);
        readonly Dictionary<TrackableId, ScannedPlaneVisual> planeVisuals = new Dictionary<TrackableId, ScannedPlaneVisual>();
        readonly List<TrackableId> oldPlanes = new List<TrackableId>();
        ARAnchor acceptedAnchor;
        GameBase placedBase;
        GameObject practiceRoot;
        GameObject reticle;
        Material ownedPlaneMaterial;
        int generation;
        bool paused;
        bool focused = true;
        bool trackingAllowed;
        bool pointerHeld;
        bool pointerBlocked;
        Vector2 pointerStart;
        Vector2 pointerLast;
        float pointerTravel;
        float initialYaw;
        float yaw;
        float pitch;
        Vector3 practiceStartPosition;
        Quaternion practiceStartRotation;
        float nextPlaneRefresh;
        float trackingLostAt = -1f;
        const float PlaneStableSeconds = 1.25f;
        const float AnchorStableSeconds = 0.15f;
        const float BaseHalfSize = 0.325f;
        // A large arena may overhang a little; require at most a 1 m square of observed plane beneath it.
        const float MaxRequiredHalfSize = 0.5f;
        // ARCore sets metric scale from IMU + camera parallax. A short sideways move helps it settle, but
        // calibration always completes within WarmupMaxSeconds of tracking starting so play is never blocked.
        const float WarmupSeconds = 1f;
        const float WarmupTravelMeters = 0.1f;
        const float WarmupMaxSeconds = 4f;
        const float RelocalizationJumpMeters = 0.35f;
        float calibrationStartedAt = -1f;
        bool warmLatched;
        float trackingSince = -1f;
        float warmupTravel;
        Vector3 lastTrackedCameraPosition;
        bool loggedCameraConfiguration;
        ARCameraManager cameraManager;
        GameObject contactShadow;
#if UNITY_ANDROID
        ARCoreCameraSubsystem arcoreCamera;
#endif
        ARPlane candidatePlane;
        Pose candidatePlanePose;
        float candidateStableSince = -1f;
        float nextDiagnostic;
        string candidateMessage = "Move your phone slowly to find the ground";
        Coroutine startup;
        Coroutine providerStep;
        bool releaseProviderWhenIdle;
        XRManagerSettings initializedManager;

        void Awake()
        {
            Application.targetFrameRate = 30;
            if (practiceCamera)
            {
                practiceStartPosition = practiceCamera.transform.position;
                practiceStartRotation = practiceCamera.transform.rotation;
            }
            if (session) { session.attemptUpdate = false; session.matchFrameRateRequested = false; session.enabled = false; }
            if (origin)
            {
                origin.CameraYOffset = 0f;
                if (origin.CameraFloorOffsetObject)
                    origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
            }
            // Real-room lighting for the arena and bubbles (ARCore ambient light estimation; AR only).
            if (arCamera && arCamera.TryGetComponent(out ARCameraManager arCameraManager) && !arCamera.GetComponent<ArLightEstimation>())
            {
                Light key = RenderSettings.sun;
                if (!key)
                    foreach (var candidate in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (candidate.type == LightType.Directional) { key = candidate; break; }
                arCamera.gameObject.AddComponent<ArLightEstimation>().Bind(key);
            }
            SetArRig(false);
            SetPracticeRig(true);
        }

        void Start() => Publish();

        public void EnterAR()
        {
            EndRound();
            Mode = ExperienceMode.AR;
            SetPracticeRig(true); // Keep a usable rendered backdrop during provider checks.
            SetState(PlacementState.Checking, "Checking AR support...");
            startup = StartCoroutine(StartAR(generation));
        }

        public void EnterPractice()
        {
            EndRound();
            Mode = ExperienceMode.Practice;
            SetPracticeRig(true);
            if (practiceCamera)
            {
                practiceCamera.transform.SetPositionAndRotation(practiceStartPosition, practiceStartRotation);
                Vector3 angles = practiceCamera.transform.eulerAngles;
                pitch = NormalizeAngle(angles.x);
                yaw = initialYaw = angles.y;
            }
            SetState(PlacementState.Scanning, "Tap the highlighted surface to place your arena");
        }

        public void Restart()
        {
            if (Mode == ExperienceMode.AR) EnterAR();
            else if (Mode == ExperienceMode.Practice) EnterPractice();
            else ReturnToMenu();
        }

        public void ReturnToMenu()
        {
            EndRound();
            Mode = ExperienceMode.Menu;
            SetPracticeRig(true);
            if (practiceCamera) practiceCamera.transform.SetPositionAndRotation(practiceStartPosition, practiceStartRotation);
            SetState(PlacementState.Menu, "Choose AR or Practice Mode");
        }

        IEnumerator StartAR(int ticket)
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            yield return null;
            if (ticket == generation) Unavailable("AR requires a supported Android phone. Practice Mode is ready.");
#else
            // ARSession caches CheckingAvailability/Installing until its original coroutine completes.
            // Let a previous services request finish before creating another provider session.
            float previousRequestDeadline = Time.realtimeSinceStartup + 18f;
            while (providerStep != null && ticket == generation && Time.realtimeSinceStartup < previousRequestDeadline)
                yield return null;
            if (ticket != generation) yield break;
            if (providerStep != null)
            {
                Unavailable("The previous AR services request is still finishing. Retry shortly or play Practice Mode.");
                yield break;
            }
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null)
            {
                Unavailable("AR provider is unavailable. Retry or enter Practice Mode.");
                yield break;
            }
            initializedManager = manager;
            if (!manager.isInitializationComplete)
            {
                bool done = false;
                providerStep = StartCoroutine(ProviderStep(manager.InitializeLoader(), () => done = true));
                float deadline = Time.realtimeSinceStartup + 18f;
                while (!done && ticket == generation && Time.realtimeSinceStartup < deadline) yield return null;
                if (ticket != generation) yield break;
                if (!done) { CancelProviderStep(); Unavailable("AR initialization timed out. Retry or enter Practice Mode."); yield break; }
            }
            if (manager.activeLoader == null)
            {
                Unavailable("This phone has no compatible AR provider. Practice Mode is ready.");
                yield break;
            }
            ConfigureArCoreCamera(manager.activeLoader);
            bool checkedAvailability = false;
            providerStep = StartCoroutine(ProviderStep(ARSession.CheckAvailability(), () => checkedAvailability = true));
            float checkDeadline = Time.realtimeSinceStartup + 18f;
            while (!checkedAvailability && ticket == generation && Time.realtimeSinceStartup < checkDeadline) yield return null;
            if (ticket != generation) yield break;
            if (!checkedAvailability) { CancelProviderStep(); Unavailable("AR availability check timed out. Retry or enter Practice Mode."); yield break; }
            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                SetState(PlacementState.Checking, "Install or update Google Play Services for AR to continue");
                bool installed = false;
                providerStep = StartCoroutine(ProviderStep(ARSession.Install(), () => installed = true));
                float installDeadline = Time.realtimeSinceStartup + 120f;
                while (!installed && ticket == generation && Time.realtimeSinceStartup < installDeadline) yield return null;
                if (ticket != generation) yield break;
                if (!installed) { CancelProviderStep(); Unavailable("AR services installation did not finish. Retry or enter Practice Mode."); yield break; }
            }
            if (ARSession.state != ARSessionState.Ready && ARSession.state != ARSessionState.SessionInitializing && ARSession.state != ARSessionState.SessionTracking)
            {
                Unavailable("AR is unavailable or installation was cancelled. Retry or enter Practice Mode.");
                yield break;
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                SetState(PlacementState.Checking, "Allow camera access to scan your table or floor");
                bool permissionFinished = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => permissionFinished = true;
                callbacks.PermissionDenied += _ => permissionFinished = true;
                callbacks.PermissionDeniedAndDontAskAgain += _ => permissionFinished = true;
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                float permissionDeadline = Time.realtimeSinceStartup + 120f;
                while (!permissionFinished && ticket == generation && Time.realtimeSinceStartup < permissionDeadline) yield return null;
                if (ticket != generation) yield break;
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    Unavailable("Camera access was denied. Enable it in Android Settings and retry, or play Practice Mode.");
                    yield break;
                }
            }
            if (ticket != generation) yield break;
            // Fixed focus keeps the camera intrinsics ARCore calibrated against the IMU.
            if (CameraManager) CameraManager.autoFocusRequested = false;
            ResetWarmup();
            loggedCameraConfiguration = false;
            manager.StartSubsystems();
            SetPracticeRig(false);
            SetArRig(true);
            if (planeManager) planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            if (session) { session.attemptUpdate = false; session.enabled = true; }
            SetState(PlacementState.Scanning, "Move your phone slowly over a well-lit table or floor");
#endif
        }

        IEnumerator ProviderStep(IEnumerator operation, Action onComplete)
        {
            yield return operation;
            onComplete();
            providerStep = null;
            if (releaseProviderWhenIdle) ReleaseProvider();
        }

        void Unavailable(string reason)
        {
            SetArRig(false);
            SetPracticeRig(true);
            SetTracking(false);
            CancelProviderStep();
            Debug.Log("Reality Defender AR: " + reason);
            SetState(PlacementState.Unavailable, reason);
        }

        void LateUpdate()
        {
            if (Mode == ExperienceMode.AR && State != PlacementState.Checking && State != PlacementState.Unavailable && State != PlacementState.Menu)
            {
                if (Time.unscaledTime >= nextDiagnostic)
                {
                    nextDiagnostic = Time.unscaledTime + 1f;
                    LogTrackingDiagnostic();
                }
                bool reliable = !paused && focused && ARSession.state == ARSessionState.SessionTracking;
                UpdateWarmup(reliable);
                if (HasPlaced)
                {
                    reliable &= acceptedAnchor && acceptedAnchor.trackingState == TrackingState.Tracking;
                    SetTracking(reliable);
                    if (!reliable)
                    {
                        if (trackingLostAt < 0f) trackingLostAt = Time.unscaledTime;
                        string message = acceptedAnchor == null || Time.unscaledTime - trackingLostAt > 15f
                            ? "Anchor lost. Tap RESTART to scan a new surface."
                            : "Tracking paused. Move slowly back toward your arena in good light.";
                        SetStatus(message);
                    }
                    else
                    {
                        trackingLostAt = -1f;
                        SetStatus("BASE ANCHORED");
                    }
                }
                if (State == PlacementState.Scanning && Time.unscaledTime >= nextPlaneRefresh)
                {
                    nextPlaneRefresh = Time.unscaledTime + 0.25f;
                    RefreshPlaneVisuals();
                }
            }
            if (Mode == ExperienceMode.Practice && HasPlaced) SetTracking(!paused && focused);
            UpdateSurfaceLock();
            UpdateCandidate();
            UpdateReticle();
            if (!UpdatePinch()) ReadPointer();
        }

        // ---------- Two-finger pinch to resize the placed arena ----------
        const float MinArenaScale = 0.4f;
        const float MaxArenaScale = 6f;
        bool pinching;
        bool pinchRejected;
        public bool IsResizing => pinching;
        float pinchStartDistance;
        float pinchStartScale;

        /// <summary>Returns true while a two-finger gesture owns the touch input.</summary>
        bool UpdatePinch()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null) { pinching = pinchRejected = false; return false; }
            Vector2 a = default, b = default;
            int count = 0;
            foreach (var t in touch.touches)
            {
                if (!t.press.isPressed) continue;
                if (count == 0) a = t.position.ReadValue(); else if (count == 1) b = t.position.ReadValue();
                count++;
            }
            if (count < 2)
            {
                bool wasGesture = pinching || pinchRejected;
                pinching = pinchRejected = false;
                if (wasGesture) { pointerHeld = false; pointerBlocked = true; }
                return wasGesture && count == 1; // ignore the leftover finger of a pinch
            }
            pointerHeld = false;
            pointerBlocked = true;
            if (paused || !focused || !HasPlaced || State != PlacementState.Playing || pinchRejected) return true;
            float distance = Vector2.Distance(a, b);
            if (!pinching)
            {
                // A finger resting on FIRE or another control is not a resize gesture.
                if (IsOverUI(a) || IsOverUI(b) || distance < 20f) { pinchRejected = true; return true; }
                pinching = true;
                pinchStartDistance = distance;
                pinchStartScale = placedBase.transform.localScale.x;
                return true;
            }
            float scale = Mathf.Clamp(pinchStartScale * distance / Mathf.Max(1f, pinchStartDistance), MinArenaScale, MaxArenaScale);
            placedBase.transform.localScale = Vector3.one * scale;
            SetStatus("ARENA SIZE " + Mathf.RoundToInt(scale * 65f) + " cm");
            return true;
        }

        // ---------- Keep the placed arena glued to the real surface ----------
        bool anchorOnPlane;
        float nextAnchorUpgrade;

        /// <summary>
        /// Every frame, find the tracked horizontal surface under the base (within 0.5 m of its height) and ease
        /// the base onto it. ARCore keeps re-estimating surface height as tracking corrects itself, so this keeps
        /// the base resting on the floor instead of hovering above or sinking into it. If the base was placed on a
        /// bare surface point, it is re-anchored onto the plane as soon as ARCore detects one beneath it.
        /// </summary>
        void UpdateSurfaceLock()
        {
            if (Mode != ExperienceMode.AR || !HasPlaced || !acceptedAnchor || !planeManager || !trackingAllowed) return;
            Transform root = placedBase.transform;
            ARPlane best = null;
            float bestOffset = 0f, bestDistance = float.MaxValue;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (!plane || plane.subsumedBy != null || plane.alignment != PlaneAlignment.HorizontalUp || plane.trackingState != TrackingState.Tracking) continue;
                if (!plane.boundary.IsCreated || plane.boundary.Length < 3) continue;
                Vector3 local = plane.transform.InverseTransformPoint(root.position);
                if (Mathf.Abs(local.y) > 0.5f) continue; // a different surface (table vs floor)
                if (!ScannedPlaneVisual.Contains(plane.boundary, new Vector2(local.x, local.z))) continue;
                if (Mathf.Abs(local.y) < bestDistance) { bestDistance = Mathf.Abs(local.y); bestOffset = local.y; best = plane; }
            }
            if (!best) return;
            if (bestDistance > 0.002f)
            {
                float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f);
                root.position -= best.transform.up * (bestOffset * t);
            }
            if (!anchorOnPlane && Time.unscaledTime >= nextAnchorUpgrade && anchorManager)
            {
                nextAnchorUpgrade = Time.unscaledTime + 0.5f;
                Vector3 onPlane = root.position - best.transform.up * bestOffset;
                ARAnchor planeAnchor = anchorManager.AttachAnchor(best, new Pose(onPlane, root.rotation));
                if (planeAnchor)
                {
                    ARAnchor old = acceptedAnchor;
                    root.SetParent(planeAnchor.transform, true);
                    acceptedAnchor = planeAnchor;
                    anchorOnPlane = true;
                    ReleaseAnchor(old);
                    Debug.Log("Reality Defender AR: arena re-anchored onto detected surface " + best.trackableId);
                }
            }
        }

        void ReadPointer()
        {
            if (paused || !focused || (State != PlacementState.Scanning && State != PlacementState.Playing))
            {
                pointerHeld = false;
                return;
            }
            bool down = false, held = false, up = false;
            Vector2 position = default;
            Touchscreen touch = Touchscreen.current;
            if (touch != null && (touch.primaryTouch.press.isPressed || touch.primaryTouch.press.wasReleasedThisFrame))
            {
                int touchCount = 0;
                foreach (var t in touch.touches) if (t.press.isPressed) touchCount++;
                if (touchCount > 1) { pointerBlocked = true; return; }
                position = touch.primaryTouch.position.ReadValue();
                down = touch.primaryTouch.press.wasPressedThisFrame;
                held = touch.primaryTouch.press.isPressed;
                up = touch.primaryTouch.press.wasReleasedThisFrame;
            }
            else if (Mouse.current != null)
            {
                position = Mouse.current.position.ReadValue();
                down = Mouse.current.leftButton.wasPressedThisFrame;
                held = Mouse.current.leftButton.isPressed;
                up = Mouse.current.leftButton.wasReleasedThisFrame;
            }
            if (down)
            {
                pointerHeld = true;
                pointerStart = pointerLast = position;
                pointerTravel = 0f;
                pointerBlocked = IsOverUI(position);
            }
            if (held && pointerHeld)
            {
                Vector2 delta = position - pointerLast;
                pointerLast = position;
                pointerTravel += delta.magnitude;
                if (!pointerBlocked && Mode == ExperienceMode.Practice && HasPlaced && !IsOverUI(position) && practiceCamera)
                {
                    float sensitivity = aimDegreesPerPixel * (720f / Mathf.Max(360f, Mathf.Min(Screen.width, Screen.height)));
                    yaw = Mathf.Clamp(yaw + delta.x * sensitivity, initialYaw - 75f, initialYaw + 75f);
                    pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -55f, 70f);
                    practiceCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                }
            }
            if (up && pointerHeld)
            {
                pointerHeld = false;
                float tapSlop = Mathf.Max(16f, Screen.dpi > 0f ? Screen.dpi * 0.08f : 16f);
                if (!pointerBlocked && pointerTravel < tapSlop && Vector2.Distance(position, pointerStart) < tapSlop && !IsOverUI(position) && State == PlacementState.Scanning)
                {
                    if (Mode == ExperienceMode.Practice) PlacePractice(position);
                    else if (Mode == ExperienceMode.AR && ARSession.state == ARSessionState.SessionTracking)
                    {
                        // Tap anywhere while the ring is showing: the base goes exactly where the ring is.
                        if (ringValid) PlaceAnchor(ringPlane, ringPose);
                        else SetStatus(TrackingWarm ? "Point at the ground until the white circle appears, then tap" : candidateMessage);
                    }
                }
            }
        }

        void UpdateWarmup(bool tracking)
        {
            if (warmLatched) return;
            if (!tracking || !arCamera) { trackingSince = -1f; warmupTravel = 0f; return; }
            float now = Time.unscaledTime;
            if (calibrationStartedAt < 0f) calibrationStartedAt = now;
            Vector3 position = arCamera.transform.position;
            if (trackingSince < 0f)
            {
                trackingSince = now;
                lastTrackedCameraPosition = position;
                warmupTravel = 0f;
            }
            else
            {
                float step = Vector3.Distance(position, lastTrackedCameraPosition);
                lastTrackedCameraPosition = position;
                if (step > RelocalizationJumpMeters)
                {
                    // A one-frame jump this large is an ARCore map correction, not hand motion.
                    Debug.Log("Reality Defender AR: world correction of " + step.ToString("F2") + " m during calibration.");
                    trackingSince = now;
                    warmupTravel = 0f;
                }
                else warmupTravel += step;
            }
            bool settled = now - trackingSince >= WarmupSeconds && warmupTravel >= WarmupTravelMeters;
            if (settled || now - calibrationStartedAt >= WarmupMaxSeconds)
            {
                warmLatched = true;
                Debug.Log("Reality Defender AR: calibrated in " + (now - calibrationStartedAt).ToString("F1") + " s (travel " + warmupTravel.ToString("F2") + " m)");
            }
        }

        void ResetWarmup()
        {
            trackingSince = -1f;
            calibrationStartedAt = -1f;
            warmupTravel = 0f;
            warmLatched = false;
        }

        bool TrackingWarm => warmLatched;

        float CalibrationProgress => calibrationStartedAt < 0f ? 0f : Mathf.Clamp01(Mathf.Max(
            (Time.unscaledTime - calibrationStartedAt) / WarmupMaxSeconds,
            trackingSince < 0f ? 0f : Mathf.Min((Time.unscaledTime - trackingSince) / WarmupSeconds, warmupTravel / WarmupTravelMeters)));

        float RequiredFootprintHalfSize => Mathf.Min(BaseHalfSize * ArenaScale, MaxRequiredHalfSize);
        float ArenaScale => Mathf.Max(1f, arenaScale);

        ARCameraManager CameraManager
        {
            get
            {
                if (!cameraManager && arCamera) cameraManager = arCamera.GetComponent<ARCameraManager>();
                return cameraManager;
            }
        }

        void ConfigureArCoreCamera(XRLoader loader)
        {
#if UNITY_ANDROID
            var camera = loader ? loader.GetLoadedSubsystem<UnityEngine.XR.ARSubsystems.XRCameraSubsystem>() as ARCoreCameraSubsystem : null;
            if (camera == arcoreCamera) return;
            UnhookArCoreCamera();
            if (camera == null) return;
            arcoreCamera = camera;
            arcoreCamera.beforeGetCameraConfiguration += FilterCameraConfiguration;
#endif
        }

        void UnhookArCoreCamera()
        {
#if UNITY_ANDROID
            if (arcoreCamera != null)
            {
                try { arcoreCamera.beforeGetCameraConfiguration -= FilterCameraConfiguration; }
                catch (Exception error) { Debug.LogWarning("Reality Defender camera filter release: " + error.Message); }
            }
            arcoreCamera = null;
#endif
        }

#if UNITY_ANDROID
        // Prefer the 30 fps tracking camera ARCore calibrates for, and keep any depth sensor out of the loop.
        static void FilterCameraConfiguration(ARCoreBeforeGetCameraConfigurationEventArgs args)
        {
            args.filter.SetTargetFps(args.session, ArCameraConfigTargetFps.Fps30);
            args.filter.SetDepthSensorUsage(args.session, ArCameraConfigDepthSensorUsage.DoNotUse);
        }
#endif

        void LogCameraConfigurationOnce()
        {
            if (loggedCameraConfiguration || !CameraManager || CameraManager.subsystem == null) return;
            var current = CameraManager.currentConfiguration;
            if (!current.HasValue) return;
            loggedCameraConfiguration = true;
            Debug.Log("Reality Defender AR camera: config=" + current.Value + " autoFocus=" + CameraManager.autoFocusEnabled);
        }

        bool IsOverUI(Vector2 screenPoint)
        {
            if (EventSystem.current == null) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint };
            uiHits.Clear();
            EventSystem.current.RaycastAll(pointer, uiHits);
            return uiHits.Count != 0;
        }

        bool CandidateStable => candidatePlane && candidateStableSince >= 0f &&
            Time.unscaledTime - candidateStableSince >= PlaneStableSeconds;

        bool TryPlanePose(Vector2 screenPoint, out Pose pose, out ARPlane hitPlane, bool enforcePlacementDistance = true)
        {
            pose = default;
            hitPlane = null;
            if (!raycastManager || !planeManager || !arCamera) return false;
            hits.Clear();
            if (!raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon)) return false;
            foreach (ARRaycastHit hit in hits)
            {
                ARPlane plane = planeManager.GetPlane(hit.trackableId);
                if (!plane || plane.subsumedBy != null || plane.alignment != PlaneAlignment.HorizontalUp || plane.trackingState != TrackingState.Tracking) continue;
                if (hit.pose.position.y >= arCamera.transform.position.y - 0.05f) continue;
                float cameraDistance = Vector3.Distance(arCamera.transform.position, hit.pose.position);
                if (enforcePlacementDistance && (cameraDistance < MinimumARPlacementDistance || cameraDistance > MaximumARPlacementDistance)) continue;
                Vector3 normal = plane.transform.up.normalized;
                Vector3 forward = Vector3.ProjectOnPlane(arCamera.transform.forward, normal);
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(plane.transform.forward, normal);
                pose = new Pose(hit.pose.position, Quaternion.LookRotation(forward.normalized, normal));
                hitPlane = plane;
                return true;
            }
            return false;
        }

        bool FootprintFits(ARPlane plane, Pose pose)
        {
            if (!plane || !plane.boundary.IsCreated || plane.boundary.Length < 3) return false;
            float half = RequiredFootprintHalfSize;
            // Sample all four edges, including corners, against the observed polygon.
            for (int edge = 0; edge < 4; edge++)
                for (int i = 0; i <= 8; i++)
                {
                    float t = Mathf.Lerp(-half, half, i / 8f);
                    float x = edge < 2 ? (edge == 0 ? -half : half) : t;
                    float z = edge < 2 ? t : (edge == 2 ? -half : half);
                    Vector3 local = plane.transform.InverseTransformPoint(pose.position + pose.rotation * new Vector3(x, 0f, z));
                    if (!ScannedPlaneVisual.Contains(plane.boundary, new Vector2(local.x, local.z))) return false;
                }
            return true;
        }

        void UpdateCandidate()
        {
            if (Mode != ExperienceMode.AR || State != PlacementState.Scanning || paused || !focused || ARSession.state != ARSessionState.SessionTracking)
            {
                candidatePlane = null;
                candidateStableSince = -1f;
                return;
            }
            candidatePlane = null;
            candidateStableSince = -1f;
            if (!TrackingWarm)
            {
                int percent = Mathf.RoundToInt(100f * CalibrationProgress);
                candidateMessage = "Calibrating: move your phone slowly side to side " + Mathf.Clamp(percent, 0, 99) + "%";
                return;
            }
            // The ring is the single source of truth: if it is visible, a tap places the base on it.
            candidateMessage = ringValid
                ? "Tap to place your base  (" + Vector3.Distance(arCamera.transform.position, ringPose.position).ToString("F1") + " m)"
                : "Move your phone slowly to find the ground";
        }

        async void PlaceAnchor(ARPlane plane, Pose worldPose)
        {
            if (State != PlacementState.Scanning || HasPlaced || !anchorManager) return;
            int ticket = generation;
            ARAnchor anchor = null;
            SetState(PlacementState.Placing, "Locking the arena to this surface...");
            try
            {
                if (plane)
                {
                    // ARCore keeps the anchor attached as its estimate of this surface evolves.
                    anchor = anchorManager.AttachAnchor(plane, worldPose);
                }
                else
                {
                    // No plane yet (small object / fresh scan): anchor the tracked surface point itself.
                    var result = await anchorManager.TryAddAnchorAsync(worldPose);
                    if (this == null || ticket != generation) { if (result.status.IsSuccess()) ReleaseAnchor(result.value); return; }
                    if (result.status.IsSuccess()) anchor = result.value;
                }
                if (!anchor)
                {
                    SetState(PlacementState.Scanning, "Could not anchor there. Move slowly and tap again.");
                    return;
                }
                float deadline = Time.realtimeSinceStartup + 3f;
                float stableSince = -1f;
                while (anchor && ticket == generation && Time.realtimeSinceStartup < deadline)
                {
                    bool tracked = !paused && focused && ARSession.state == ARSessionState.SessionTracking && anchor.trackingState == TrackingState.Tracking;
                    if (!tracked) stableSince = -1f;
                    else if (stableSince < 0f) stableSince = Time.realtimeSinceStartup;
                    else if (Time.realtimeSinceStartup - stableSince >= AnchorStableSeconds) break;
                    await Awaitable.NextFrameAsync();
                }
                if (this == null || ticket != generation || Mode != ExperienceMode.AR)
                {
                    ReleaseAnchor(anchor);
                    return;
                }
                if (!anchor || stableSince < 0f || Time.realtimeSinceStartup - stableSince < AnchorStableSeconds)
                {
                    ReleaseAnchor(anchor);
                    SetState(PlacementState.Scanning, "Tracking dropped while placing. Hold steady and tap the circle again.");
                    return;
                }
                acceptedAnchor = anchor;
                anchorOnPlane = plane;
                CompletePlacement(anchor.transform, arCamera, false);
            }
            catch (Exception error)
            {
                ReleaseAnchor(anchor);
                if (this != null && ticket == generation)
                {
                    Debug.LogWarning("Reality Defender placement: " + error.Message);
                    SetState(PlacementState.Scanning, "Placement failed. Tap a highlighted floor to retry.");
                }
            }
        }

        void LogTrackingDiagnostic()
        {
            string cameraPose = arCamera ? $"world={arCamera.transform.position:F3} rot={arCamera.transform.eulerAngles:F1} local={arCamera.transform.localPosition:F3} localRot={arCamera.transform.localEulerAngles:F1}" : "missing";
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            string nativePose = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 nativePosition)
                ? nativePosition.ToString("F3") : "unavailable";
            string plane = candidatePlane ? $"{candidatePlane.trackableId} {candidatePlane.trackingState} pos={candidatePlane.transform.position:F3}" : "none";
            string anchor = acceptedAnchor ? $"{acceptedAnchor.trackableId} {acceptedAnchor.trackingState} pos={acceptedAnchor.transform.position:F3}" : "none";
            LogCameraConfigurationOnce();
            Debug.Log($"Reality Defender AR diagnostic: session={ARSession.state} reason={ARSession.notTrackingReason} warm={TrackingWarm} travel={warmupTravel:F2} camera={cameraPose} inputPosition={nativePose} plane={plane} anchor={anchor} reticle={(reticle && reticle.activeSelf ? reticle.transform.position.ToString("F3") : "hidden")}");
        }

        void PlacePractice(Vector2 screenPoint)
        {
            if (!practiceCamera || HasPlaced || !basePrefab) return;
            var floor = new Plane(Vector3.up, new Vector3(0f, practiceFloorHeight, 0f));
            Ray ray = practiceCamera.ScreenPointToRay(screenPoint);
            if (!floor.Raycast(ray, out float distance) || distance < 0.3f || distance > 3.5f)
            {
                SetStatus("Tap the nearby floor to place your arena");
                return;
            }
            SetState(PlacementState.Placing, "Placing arena...");
            Vector3 point = ray.GetPoint(distance);
            Vector3 heading = Vector3.ProjectOnPlane(practiceCamera.transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 0.0001f) heading = Vector3.forward;
            practiceRoot = new GameObject("Practice Placement Root");
            practiceRoot.transform.SetPositionAndRotation(point, Quaternion.LookRotation(heading.normalized, Vector3.up));
            CompletePlacement(practiceRoot.transform, practiceCamera, true);
        }

        void CompletePlacement(Transform parent, Camera camera, bool practice)
        {
            if (!basePrefab || !wave) { SetState(PlacementState.Unavailable, "Arena setup is incomplete"); return; }
            placedBase = Instantiate(basePrefab, parent, false);
            placedBase.transform.localPosition = Vector3.zero;
            placedBase.transform.localRotation = Quaternion.identity;
            // Practice keeps the authored 0.65 m arena; AR uses the larger floor-scale arena.
            placedBase.transform.localScale = Vector3.one * (practice ? 1f : ArenaScale);
            AddContactShadow(placedBase.transform);
            HidePlaneVisuals();
            if (reticle) reticle.SetActive(false);
            wave.BeginWave(placedBase, camera, practice);
            SetTracking(!paused && focused);
            SetState(PlacementState.Playing, practice ? "BASE PLACED" : "BASE ANCHORED");
            Placed?.Invoke(placedBase, camera);
        }

        void AddContactShadow(Transform baseRoot)
        {
            Material material = contactShadowMaterial;
            if (!material)
            {
                Shader shader = Shader.Find("RealityDefender/Contact Shadow");
                if (!shader) return;
                material = contactShadowMaterial = new Material(shader) { name = "Runtime Contact Shadow" };
            }
            // Base-local quad, 1.6x the 0.65 m footprint, a hair above the surface; scales with the base.
            contactShadow = new GameObject("Contact shadow", typeof(MeshFilter), typeof(MeshRenderer));
            contactShadow.transform.SetParent(baseRoot, false);
            contactShadow.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
            contactShadow.GetComponent<MeshFilter>().sharedMesh = ShadowQuad();
            var renderer = contactShadow.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh shadowQuad;
        static Mesh ShadowQuad() => FlatQuad(ref shadowQuad, "Contact shadow quad", BaseHalfSize * 1.6f);

        /// <summary>Horizontal quad of half-size h with 0..1 UVs, cached in the given slot.</summary>
        static Mesh FlatQuad(ref Mesh cache, string name, float h)
        {
            if (cache) return cache;
            cache = new Mesh { name = name };
            cache.SetVertices(new List<Vector3> { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h) });
            cache.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            cache.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            cache.RecalculateNormals();
            cache.RecalculateBounds();
            return cache;
        }

        void SetTracking(bool allowed)
        {
            if (Mode == ExperienceMode.AR && placedBase && placedBase.gameObject.activeSelf != allowed)
                placedBase.gameObject.SetActive(allowed);
            if (trackingAllowed == allowed && (!wave || wave.IsTracking == allowed)) return;
            trackingAllowed = allowed;
            if (wave) wave.SetTracking(allowed);
            Publish();
        }

        void RefreshPlaneVisuals()
        {
            if (!planeManager) return;
            int usable = 0;
            oldPlanes.Clear();
            foreach (var key in planeVisuals.Keys) oldPlanes.Add(key);
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (!plane || plane.subsumedBy != null || plane.alignment != PlaneAlignment.HorizontalUp || plane.trackingState != TrackingState.Tracking) continue;
                usable++;
                if (usable > 8) continue;
                oldPlanes.Remove(plane.trackableId);
                if (!planeVisuals.TryGetValue(plane.trackableId, out ScannedPlaneVisual visual) || !visual)
                {
                    visual = plane.GetComponent<ScannedPlaneVisual>();
                    if (!visual) visual = plane.gameObject.AddComponent<ScannedPlaneVisual>();
                    visual.Initialize(GetPlaneMaterial());
                    planeVisuals[plane.trackableId] = visual;
                }
                visual.Show(plane);
            }
            foreach (TrackableId old in oldPlanes)
            {
                if (planeVisuals.TryGetValue(old, out ScannedPlaneVisual visual) && visual) visual.Hide();
            }
            if (ARSession.state == ARSessionState.SessionTracking)
                SetStatus(candidateMessage);
            else SetStatus("Finding tracking. Move slowly in good light.");
        }

        static readonly Color RingSearching = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color RingReady = new Color(1f, 1f, 1f, 0.92f);
        const float RingRadius = 0.35f;
        MaterialPropertyBlock ringBlock;
        Renderer ringRenderer;
        public bool GroundFound { get; private set; }

        /// <summary>
        /// Ground placeholder: appears as soon as ARCore has any horizontal surface under the screen
        /// centre (white), and turns cyan and pulses once that exact spot is safe to anchor.
        /// </summary>
        void UpdateReticle()
        {
            if (State != PlacementState.Scanning || paused || !focused) { HideReticle(); return; }
            Pose pose = default;
            bool found = false, ready = false;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ringValid = false;
            if (Mode == ExperienceMode.AR && ARSession.state == ARSessionState.SessionTracking && TrackingWarm)
            {
                found = ready = TryGroundPreview(center, out pose, out ARPlane plane);
                if (found) { ringValid = true; ringPose = pose; ringPlane = plane; }
            }
            else if (Mode == ExperienceMode.Practice && practiceCamera)
            {
                Ray ray = practiceCamera.ScreenPointToRay(center);
                var floor = new Plane(Vector3.up, new Vector3(0f, practiceFloorHeight, 0f));
                if (floor.Raycast(ray, out float distance) && distance < 3.5f)
                {
                    pose = new Pose(ray.GetPoint(distance), Quaternion.identity);
                    found = ready = true;
                }
            }
            GroundFound = found;
            if (!found) { HideReticle(); return; }
            if (!reticle) reticle = CreatePlacementRing();
            bool appearing = !reticle.activeSelf;
            reticle.SetActive(true);
            Vector3 target = pose.position + pose.up * 0.004f;
            if (appearing) reticle.transform.SetPositionAndRotation(target, pose.rotation);
            else
            {
                // Smooth the hit point so the ring glides over the floor instead of jittering.
                float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f);
                reticle.transform.SetPositionAndRotation(Vector3.Lerp(reticle.transform.position, target, t),
                    Quaternion.Slerp(reticle.transform.rotation, pose.rotation, t));
            }
            float pulse = ready ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
            reticle.transform.localScale = Vector3.one * pulse;
            if (ringRenderer)
            {
                ringBlock ??= new MaterialPropertyBlock();
                ringBlock.SetColor("_BaseColor", ready ? RingReady : RingSearching);
                ringRenderer.SetPropertyBlock(ringBlock);
            }
        }

        void HideReticle()
        {
            GroundFound = false;
            ringValid = false;
            if (reticle) reticle.SetActive(false);
        }

        GameObject CreatePlacementRing()
        {
            if (!placementRingMaterial) return ScannedPlaneVisual.CreateReticle(GetPlaneMaterial());
            var ring = new GameObject("Ground placement ring", typeof(MeshFilter), typeof(MeshRenderer));
            ring.GetComponent<MeshFilter>().sharedMesh = FlatQuad(ref ringQuad, "Placement ring quad", RingRadius / 0.86f);
            ringRenderer = ring.GetComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = placementRingMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            return ring;
        }

        static Mesh ringQuad;
        bool ringValid;
        Pose ringPose;
        ARPlane ringPlane;

        /// <summary>
        /// Surface under the screen centre for the ring and for placement. Prefers a detected horizontal
        /// surface (floor, table, box top); before any plane exists it uses ARCore's tracked surface points
        /// so the ring appears quickly on any physical object. The base is kept upright either way.
        /// </summary>
        bool TryGroundPreview(Vector2 screenPoint, out Pose pose, out ARPlane surfacePlane)
        {
            pose = default;
            surfacePlane = null;
            if (!raycastManager || !planeManager || !arCamera) return false;
            previewHits.Clear();
            if (!raycastManager.Raycast(screenPoint, previewHits,
                TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated | TrackableType.FeaturePoint)) return false;
            Vector3 cameraPosition = arCamera.transform.position;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (ARRaycastHit hit in previewHits)
                {
                    bool isPlane = (hit.hitType & (TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated)) != 0;
                    if (isPlane != (pass == 0)) continue;
                    if (hit.pose.position.y >= cameraPosition.y - 0.05f) continue;
                    if (Vector3.Distance(cameraPosition, hit.pose.position) < MinimumARPlacementDistance) continue;
                    Vector3 normal = Vector3.up;
                    ARPlane plane = null;
                    if (isPlane)
                    {
                        plane = planeManager.GetPlane(hit.trackableId);
                        if (!plane || plane.alignment != PlaneAlignment.HorizontalUp) continue;
                        normal = plane.transform.up.normalized;
                    }
                    Vector3 forward = Vector3.ProjectOnPlane(arCamera.transform.forward, normal);
                    if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(arCamera.transform.up, normal);
                    pose = new Pose(hit.pose.position, Quaternion.LookRotation(forward.normalized, normal));
                    surfacePlane = plane;
                    return true;
                }
            }
            return false;
        }

        Material GetPlaneMaterial()
        {
            if (planeMaterial) return planeMaterial;
            if (ownedPlaneMaterial) return ownedPlaneMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            ownedPlaneMaterial = new Material(shader) { name = "Runtime Cyan Scan" };
            ownedPlaneMaterial.SetColor("_BaseColor", new Color(0.08f, 0.95f, 1f, 0.66f));
            ownedPlaneMaterial.SetFloat("_Surface", 1f);
            ownedPlaneMaterial.SetFloat("_ZWrite", 0f);
            ownedPlaneMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ownedPlaneMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ownedPlaneMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ownedPlaneMaterial.renderQueue = 3000;
            return ownedPlaneMaterial;
        }

        void HidePlaneVisuals()
        {
            foreach (var entry in planeVisuals) if (entry.Value) entry.Value.Hide();
        }

        void EndRound()
        {
            generation++;
            if (startup != null) { StopCoroutine(startup); startup = null; }
            pointerHeld = false;
            SetTracking(false);
            if (wave) wave.ResetWave();
            if (placedBase) Destroy(placedBase.gameObject);
            placedBase = null;
            ReleaseAnchor(acceptedAnchor);
            acceptedAnchor = null;
            anchorOnPlane = false;
            pinching = pinchRejected = false;
            if (practiceRoot) Destroy(practiceRoot);
            practiceRoot = null;
            if (reticle) reticle.SetActive(false);
            HidePlaneVisuals();
            planeVisuals.Clear();
            trackingLostAt = -1f;
            candidatePlane = null;
            candidateStableSince = -1f;
            contactShadow = null; // Child of the destroyed base.
            ResetWarmup();
            if (session) session.enabled = false;
            SetArRig(false);
            CancelProviderStep();
        }

        void CancelProviderStep()
        {
            // Aborting CheckAvailability/Install strands ARSession's static state. Keep the
            // camera inactive, retire this generation, and release the provider when it settles.
            if (providerStep != null) { releaseProviderWhenIdle = true; return; }
            ReleaseProvider();
        }

        void ReleaseProvider()
        {
            releaseProviderWhenIdle = false;
            UnhookArCoreCamera();
            if (initializedManager != null && initializedManager.isInitializationComplete)
                initializedManager.DeinitializeLoader();
            initializedManager = null;
        }

        void ReleaseAnchor(ARAnchor anchor)
        {
            if (!anchor) return;
            if (anchorManager && anchorManager.subsystem != null) anchorManager.TryRemoveAnchor(anchor);
            else Destroy(anchor.gameObject);
        }

        void SetArRig(bool active)
        {
            if (arRig) arRig.SetActive(active);
            else if (origin) origin.gameObject.SetActive(active);
            if (arCamera) arCamera.gameObject.SetActive(active);
        }

        void SetPracticeRig(bool active)
        {
            if (practiceCamera) practiceCamera.gameObject.SetActive(active);
            if (practiceEnvironment) practiceEnvironment.SetActive(active);
        }

        void SetState(PlacementState state, string text)
        {
            State = state;
            Status = text;
            Debug.Log("Reality Defender state: " + Mode + " / " + state + " / " + text);
            Publish();
        }

        void SetStatus(string text)
        {
            if (Status == text) return;
            Status = text;
            Publish();
        }

        void Publish() => Changed?.Invoke();
        static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;

        void OnApplicationPause(bool value)
        {
            paused = value;
            pointerHeld = false;
            if (value) SetTracking(false);
#if UNITY_ANDROID && !UNITY_EDITOR
            else if (Mode == ExperienceMode.AR && (State == PlacementState.Scanning || State == PlacementState.Placing || State == PlacementState.Playing) && !Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                SetTracking(false);
                if (session) session.enabled = false;
                SetArRig(false);
                SetPracticeRig(true);
                SetState(PlacementState.Unavailable, "Camera access was revoked. Enable it in Settings and retry, or play Practice Mode.");
            }
#endif
        }

        void OnApplicationFocus(bool value)
        {
            focused = value;
            pointerHeld = false;
            if (!value) SetTracking(false);
        }

        void OnDestroy()
        {
            EndRound();
            if (reticle) Destroy(reticle);
            if (ownedPlaneMaterial) Destroy(ownedPlaneMaterial);
        }
    }
}
