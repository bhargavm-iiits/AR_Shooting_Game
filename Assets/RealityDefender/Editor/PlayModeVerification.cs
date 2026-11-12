using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace RealityDefender.Editor
{
    /// <summary>Runs the authored scene through player updates and real PhysX contacts. No AR claims are made.</summary>
    [InitializeOnLoad]
    public static class PlayModeVerification
    {
        const string Active = "RealityDefender.Verification.Active";
        const string Stage = "RealityDefender.Verification.Stage";
        const string ExitCode = "RealityDefender.Verification.ExitCode";
        const string Deadline = "RealityDefender.Verification.Deadline";
        const string Restarts = "RealityDefender.Verification.Restarts";
        const string Scene = "Assets/RealityDefender/Scenes/RealityDefender.unity";
        const string ReportPath = "Submission/EditorPhysicsVerification.json";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static IEnumerator routine;
        static Wait waiting;
        static VerificationReport report;
        static Mouse mouse;
        static bool addedMouse;
        static bool inputConfigured;
        static bool previousRunInBackground;
        static InputSettings.BackgroundBehavior previousBackgroundBehavior;
        static InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        static int lastFrame = -1;
        static EditorWindow gameView;

        [Serializable]
        class VerificationReport
        {
            public string category = "Unity Editor Play Mode; actual scene and Rigidbody physics; no physical AR device";
            public string unityVersion;
            public string utc;
            public bool passed;
            public string placementInput = "Synthetic Input System MouseState press/release through PlacementController.ReadPointer";
            public string buttonInput = "Actual runtime HUD Button.onClick actions";
            public string duplicateFixture = "Temporarily set the public fireCooldown to zero for two same-frame physical shots; restore immediately";
            public List<CheckResult> checks = new List<CheckResult>();
            public List<string> runtimeErrors = new List<string>();
            public List<string> screenshots = new List<string>();
        }

        [Serializable]
        class CheckResult
        {
            public string check;
            public bool passed;
            public string detail;
        }

        sealed class Wait
        {
            public Func<bool> predicate;
            public double deadline;
            public string reason;
            public Wait(Func<bool> predicate, float timeout, string reason)
            {
                this.predicate = predicate;
                deadline = EditorApplication.timeSinceStartup + timeout;
                this.reason = reason;
            }
        }

        static PlayModeVerification()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Reality Defender/Verify Scene in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run verification from Edit Mode.");
            if (!File.Exists(Scene)) throw new FileNotFoundException("Build the game scene first.", Scene);
            Directory.CreateDirectory("Submission");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Stage, "entering");
            SessionState.SetInt(ExitCode, 1);
            SessionState.SetInt(Restarts, 0);
            SessionState.SetString(Deadline, (EditorApplication.timeSinceStartup + 300).ToString(System.Globalization.CultureInfo.InvariantCulture));
            try { SetGameViewSize(1280, 720); }
            catch (Exception e) { Debug.LogWarning("Verification Game View sizing: " + e.Message); }
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (SessionState.GetString(Stage, "") == "exiting")
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Active, false);
                int result = SessionState.GetInt(ExitCode, 1);
                Debug.Log("REALITY_DEFENDER_PLAYMODE_VERIFICATION_EXIT " + result);
                if (Application.isBatchMode) EditorApplication.Exit(result);
                return;
            }
            if (SessionState.GetString(Stage, "") == "restarting")
            {
                // A domain reload interrupted the previous attempt; start over once the editor has settled.
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                SessionState.SetString(Stage, "entering");
                SessionState.SetString(Deadline, (EditorApplication.timeSinceStartup + 300).ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorApplication.EnterPlaymode();
                return;
            }
            try
            {
                if (double.TryParse(SessionState.GetString(Deadline, "0"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double stop) && EditorApplication.timeSinceStartup > stop)
                    throw new TimeoutException("Verification exceeded the 300 second run limit.");
                if (!EditorApplication.isPlaying) return;
                if (routine == null && SessionState.GetString(Stage, "") == "playing")
                {
                    // The run was in progress but its static state is gone: an editor domain reload (for example a
                    // background recompile after the editor changed define symbols) happened mid-play. That is an
                    // editor event, not a game result, so restart the whole run from Edit Mode instead of resuming.
                    int restarts = SessionState.GetInt(Restarts, 0);
                    if (restarts < 2)
                    {
                        SessionState.SetInt(Restarts, restarts + 1);
                        SessionState.SetString(Stage, "restarting");
                        Debug.LogWarning("Reality Defender verification: editor domain reload interrupted the run; restarting (" + (restarts + 1) + "/2).");
                        EditorApplication.ExitPlaymode();
                        return;
                    }
                    throw new InvalidOperationException("Editor domain reloads interrupted the run three times.");
                }
                if (routine == null)
                {
                    report = new VerificationReport { unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O") };
                    Application.logMessageReceived += OnLog;
                    routine = Execute();
                    SessionState.SetString(Stage, "playing");
                }
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (waiting != null)
                {
                    if (!waiting.predicate())
                    {
                        if (EditorApplication.timeSinceStartup > waiting.deadline) throw new TimeoutException(waiting.reason);
                        return;
                    }
                    waiting = null;
                }
                if (!routine.MoveNext()) { Finish(); return; }
                waiting = routine.Current as Wait;
            }
            catch (Exception e)
            {
                if (report == null) report = new VerificationReport { unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O") };
                Record("Verification execution", false, e.ToString());
                Finish();
            }
        }

        static IEnumerator Execute()
        {
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType<RealityHud>()?.PracticeButton != null, 10, "HUD did not initialize.");
            var hud = UnityEngine.Object.FindFirstObjectByType<RealityHud>();
            var placement = hud.placement;
            var wave = hud.wave;
            Require(placement && wave, "Scene wiring", "HUD has placement and wave controllers.");
            previousRunInBackground = Application.runInBackground;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            inputConfigured = true;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = Mouse.current;
            if (mouse == null) { mouse = InputSystem.AddDevice<Mouse>(); addedMouse = true; }
            InputSystem.EnableDevice(mouse);
            if (gameView) gameView.Focus();
            Require(placement.State == PlacementState.Menu && !placement.HasPlaced, "Startup menu", "No placed base before a deliberate mode and surface action.");
            hud.PracticeButton.onClick.Invoke();
            yield return Until(() => placement.State == PlacementState.Scanning, 3, "Practice mode did not enter scanning.");
            Require(placement.Mode == ExperienceMode.Practice, "Camera-free Practice Mode", "Practice entered through its runtime HUD button.");
            var camera = placement.practiceCamera;
            Quaternion normalView = camera.transform.rotation;
            yield return null;
            Vector2 tap = camera.WorldToScreenPoint(Vector3.zero);
            Require(tap.x > 0 && tap.x < Screen.width && tap.y > 0 && tap.y < Screen.height,
                "Placement tap is visible", "Ground origin projects to " + tap + " in " + Screen.width + "x" + Screen.height + ".");
            QueueMouse(tap, false); yield return null;
            QueueMouse(tap, true); yield return null; yield return null;
            QueueMouse(tap, false); yield return null;
            yield return Until(() => placement.HasPlaced && wave.IsActive, 4, "Actual pointer press/release did not place the practice arena. Check Game View focus and UI coverage.");
            Require(wave.Score == 0 && wave.Level == 1 && wave.Remaining == 2 && wave.LivingTargets.All(x => x.Motion == TargetMotion.Static),
                "Initial wave", "Level 1 starts with exactly two static bubbles and score zero.");
            var staticStart = wave.LivingTargets.Select(x => x.transform.position).ToArray();
            for (int i = 0; i < 10; i++) yield return null;
            var staticEnd = wave.LivingTargets.Select(x => x.transform.position).ToArray();
            Require(staticStart.Zip(staticEnd, (a, b) => Vector3.Distance(a, b)).All(d => d < 1e-4f),
                "Level 1 static bubbles", "Both level 1 bubbles hold their exact positions across frames.");
            Require(CountBases() == 1 && placement.PlacedBase.HasValidSlots, "One grounded base", "One base with three unique target slots.");
            Require(placement.PlacedBase.transform.localPosition == Vector3.zero && placement.PlacedBase.transform.localRotation == Quaternion.identity,
                "Placement transform", "Base has local identity beneath its placement root.");
            Require(CountActiveCameras() == 1, "Single active camera", "Practice camera active; AR branch inactive.");
            QueueMouse(tap, true); yield return null;
            QueueMouse(tap, false); yield return null;
            Require(CountBases() == 1 && wave.Remaining == 2 && wave.Score == 0,
                "Repeated surface tap", "Repeated input preserves the single base and wave.");
            Vector2 freeArea = new Vector2(Screen.width * .23f, Screen.height * .55f);
            QueueMouse(freeArea, false); yield return null;
            QueueMouse(freeArea, true); yield return null;
            QueueMouse(freeArea + new Vector2(90, 40), true); yield return null;
            QueueMouse(freeArea + new Vector2(90, 40), false); yield return null;
            Require(Quaternion.Angle(normalView, camera.transform.rotation) > 5f && CountBases() == 1,
                "Practice drag aim", "Dragging the free play area rotates the fixed-position camera and preserves the arena.");
            camera.transform.rotation = normalView;
            // Send actual pointer events to FIRE, in addition to the button action tests below.
            Vector2 firePoint = RectTransformUtility.WorldToScreenPoint(null, hud.FireButton.transform.position);
            QueueMouse(firePoint, false); yield return null;
            QueueMouse(firePoint, true); yield return null;
            QueueMouse(firePoint, false); yield return null;
            yield return Until(() => wave.LiveProjectileCount == 1, 1, "Pointer click did not reach FIRE through the EventSystem.");
            Require(Quaternion.Angle(normalView, camera.transform.rotation) < .01f && CountBases() == 1 && wave.Score == 0,
                "UI pointer isolation", "A real pointer click fires through EventSystem without placing another base or rotating the camera.");
            yield return Until(() => wave.LiveProjectileCount == 0, 1, "UI test shot did not expire.");
            for (int i = 0; i < 6; i++) yield return null;
            foreach (var item in Capture("Submission/Practice-Landscape.png", 1280, 720)) yield return item;

            camera.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            yield return Until(() => wave.CanFire, 2, "FIRE was not enabled for the live practice wave.");
            double missStarted = EditorApplication.timeSinceStartup;
            hud.FireButton.onClick.Invoke();
            Require(wave.LiveProjectileCount == 1, "FIRE creates a physical shot", "One projectile was launched through the runtime FIRE button.");
            Require(UnityEngine.Object.FindObjectsByType<PlasmaLance>(FindObjectsSortMode.None).Length >= 1,
                "Plasma lance presentation", "The shot is drawn as a muzzle-born double-helix plasma lance with ion rings.");
            var shot = UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).First(x => !x.IsSpent);
            Require(shot.Body && !shot.Body.isKinematic && !shot.Body.useGravity && !shot.GetComponent<SphereCollider>().isTrigger,
                "Projectile physics", "Dynamic Rigidbody, no gravity, and a non-trigger sphere collider.");
            Require(Vector3.Distance(shot.LaunchPosition, camera.transform.position) < .001f &&
                Vector3.Dot(shot.Body.linearVelocity.normalized, camera.transform.forward) > .9999f,
                "Live camera shot pose", "Shot launch point equals the active camera position; velocity follows camera forward.");
            yield return Until(() => wave.LiveProjectileCount == 0, .95f, "Miss did not expire at its five metre range.");
            Require(wave.Score == 0 && wave.Remaining == 2, "Miss expiry", "Miss retired without scoring after " + (EditorApplication.timeSinceStartup - missStarted).ToString("F3") + " seconds at 11 m/s.");

            camera.transform.rotation = normalView;
            yield return Until(() => wave.CanFire, 2, "FIRE did not recover after a miss.");
            hud.FireButton.onClick.Invoke();
            wave.SetTracking(false);
            Require(wave.LiveProjectileCount == 0 && !wave.CanFire && wave.Score == 0 && wave.Remaining == 2,
                "Tracking pause", "Suspending tracking immediately retires shots, blocks firing, and preserves targets and score.");
            yield return Until(() => wave.CanFire, 2, "Practice tracking did not recover on its next player updates.");

            var first = wave.LivingTargets.OrderBy(x => x.transform.position.x).First();
            Aim(camera, first.transform.position);
            float normalCooldown = wave.fireCooldown;
            wave.fireCooldown = 0;
            hud.FireButton.onClick.Invoke(); hud.FireButton.onClick.Invoke();
            wave.fireCooldown = normalCooldown;
            Require(wave.LiveProjectileCount == 2, "Duplicate-contact fixture", "Two real shots launched in the same player frame toward one collider.");
            yield return Until(() => wave.Score == 10 && wave.Remaining == 1, 2, "Two physical shots did not produce the first single accepted hit.");
            yield return Until(() => wave.LiveProjectileCount == 0, 1.2f, "Duplicate shots did not retire.");
            Require(wave.Score == 10 && wave.Remaining == 1, "Duplicate hit scores once", "Concurrent shots consumed one target and awarded exactly ten points.");
            camera.transform.rotation = normalView;
            foreach (var item in Capture("Submission/Practice-Portrait.png", 720, 1280)) yield return item;

            // ---- Level 1: finish the static pair, then the aura popup ----
            foreach (var item in ClearLevel(hud, wave, camera)) yield return item;
            Require(wave.Score == 20 && wave.IsCelebrating && !wave.CanFire && !wave.IsComplete && wave.LiveProjectileCount == 0,
                "Level 1 cleared", "Two hits clear level 1 (score 20); firing pauses for the celebration.");
            yield return null;
            Require(PopupVisible(hud, "LEVEL 1 COMPLETED"), "Level 1 popup", "A full-screen LEVEL 1 COMPLETED aura celebration is on screen.");
            for (int i = 0; i < 8; i++) yield return null;
            foreach (var item in Capture("Submission/Level1-Completed.png", 1280, 720)) yield return item;

            // ---- Level 2: three bubbles on the rotating carousel ----
            camera.transform.rotation = normalView;
            yield return Until(() => wave.Level == 2 && wave.Remaining == 3 && wave.CanFire, 8, "Level 2 did not start after the celebration.");
            Require(wave.LivingTargets.All(x => x.Motion == TargetMotion.Orbit), "Level 2 spawn", "Level 2 spawns exactly three orbiting bubbles.");
            var orbitStart = wave.LivingTargets.Select(x => x.transform.position).ToArray();
            for (int i = 0; i < 20; i++) yield return null;
            var orbitEnd = wave.LivingTargets.Select(x => x.transform.position).ToArray();
            Require(orbitStart.Zip(orbitEnd, (a, b) => Vector3.Distance(a, b)).All(d => d > 0.005f),
                "Level 2 rotation", "Every level 2 bubble travels around the base between samples.");
            foreach (var item in ClearLevel(hud, wave, camera)) yield return item;
            Require(wave.Score == 50 && wave.IsCelebrating, "Level 2 cleared", "Three moving hits clear level 2 (score 50).");
            yield return null;
            Require(PopupVisible(hud, "LEVEL 2 COMPLETED"), "Level 2 popup", "LEVEL 2 COMPLETED aura celebration is on screen.");

            // ---- Level 3: four bubbles in free flight, always inside the screen ----
            camera.transform.rotation = normalView;
            yield return Until(() => wave.Level == 3 && wave.Remaining == 4 && wave.CanFire, 8, "Level 3 did not start after the celebration.");
            Require(wave.LivingTargets.All(x => x.Motion == TargetMotion.Wander), "Level 3 spawn", "Level 3 spawns exactly four free-flying bubbles.");
            for (int i = 0; i < 30; i++) yield return null; // let them fly out from their spawn seats
            var flightStart = wave.LivingTargets.Select(x => x.transform.position).ToArray();
            int offscreen = 0, samples = 0;
            for (int i = 0; i < 45; i++)
            {
                foreach (var bubble in wave.LivingTargets)
                {
                    Vector3 v = camera.WorldToViewportPoint(bubble.transform.position);
                    samples++;
                    if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) offscreen++;
                }
                yield return null;
            }
            var flightEnd = wave.LivingTargets.Select(x => x.transform.position).ToArray();
            Require(offscreen == 0 && flightStart.Zip(flightEnd, (a, b) => Vector3.Distance(a, b)).All(d => d > 0.01f),
                "Level 3 random flight on screen", samples + " position samples: every bubble moved and stayed inside the visible screen.");
            foreach (var item in Capture("Submission/Level3-Flight.png", 720, 1280)) yield return item;
            camera.transform.rotation = normalView;
            foreach (var item in ClearLevel(hud, wave, camera)) yield return item;
            Require(wave.Score == Levels.TotalPoints && wave.IsComplete && !wave.IsActive && wave.Remaining == 0 && wave.LiveProjectileCount == 0,
                "Wave cleared", "All three levels cleared: score " + Levels.TotalPoints + ", no living targets or projectiles, and completion set.");
            Require(PopupVisible(hud, "LEVEL 3 COMPLETED"), "Final popup", "LEVEL 3 COMPLETED final aura celebration is on screen.");
            yield return null;
            Require(!hud.FireButton.interactable && !wave.Fire(), "Completion disables FIRE", "The runtime button is dimmed and the game rejects further shots.");
            Require(hud.GetComponentsInChildren<Text>(true).Any(x => x.gameObject.activeInHierarchy && x.text == "WAVE CLEARED"),
                "Completion banner", "Exact WAVE CLEARED text is visible in the real HUD.");
            camera.transform.rotation = normalView;
            foreach (var item in Capture("Submission/Practice-Cleared.png", 720, 1280)) yield return item;

            int oldRound = wave.RoundId;
            var replay = hud.GetComponentsInChildren<Button>(true).First(x => x.name == "PLAY AGAIN");
            replay.onClick.Invoke();
            yield return null;
            Require(placement.State == PlacementState.Scanning && !placement.HasPlaced && wave.Score == 0 && wave.Remaining == 0 && wave.RoundId > oldRound,
                "Replay cleanup", "PLAY AGAIN starts fresh placement and retires the completed round.");
            tap = camera.WorldToScreenPoint(Vector3.zero);
            QueueMouse(tap, false); yield return null;
            QueueMouse(tap, true); yield return null;
            QueueMouse(tap, false); yield return null;
            yield return Until(() => placement.HasPlaced && wave.Remaining == 2, 4, "Replay pointer placement did not start a fresh wave.");
            Require(wave.Score == 0 && wave.Level == 1 && CountBases() == 1, "Replay new wave", "A second actual tap restarts at level 1 with two bubbles, score zero and one base.");

            // Boundary fixtures move existing registered targets; they still use the normal FIRE and collision path.
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            var inside = wave.LivingTargets.First();
            inside.transform.SetParent(null, true);
            inside.transform.position = camera.transform.position + camera.transform.forward * 4.6f;
            Physics.SyncTransforms();
            yield return Until(() => wave.CanFire, 2, "Boundary fixture was unable to fire.");
            hud.FireButton.onClick.Invoke();
            yield return Until(() => wave.Score == 10, 2, "Target inside the five metre limit was not hit.");
            Require(wave.Score == 10 && wave.Remaining == 1, "Inside range boundary", "A registered target at 4.6 metres accepted a real collision.");
            var outside = wave.LivingTargets.First();
            outside.transform.SetParent(null, true);
            outside.transform.position = camera.transform.position + camera.transform.forward * 5.3f;
            Physics.SyncTransforms();
            yield return Until(() => wave.CanFire, 2, "Second boundary fixture was unable to fire.");
            hud.FireButton.onClick.Invoke();
            yield return Until(() => wave.LiveProjectileCount == 0, .95f, "Out-of-range shot did not retire.");
            Require(wave.Score == 10 && wave.Remaining == 1 && !outside.IsConsumed,
                "Outside range boundary", "A registered target at 5.3 metres remained alive and earned zero points.");
            var menu = hud.GetComponentsInChildren<Button>(true).First(x => x.name == "MENU");
            menu.onClick.Invoke(); yield return null;
            Require(placement.State == PlacementState.Menu && !placement.HasPlaced && wave.Score == 0 && wave.Remaining == 0 && wave.LiveProjectileCount == 0,
                "Return to menu cleanup", "Leaving practice clears all round-owned targets, shots, and score.");
            Require(CountBases() == 0 && CountActiveCameras() == 1, "Final scene cleanup", "No placed bases remain and exactly one camera is active.");
            Record("Runtime error log", report.runtimeErrors.Count == 0, report.runtimeErrors.Count + " Error/Exception/Assert log entries during the run.");
        }

        static IEnumerable<object> Capture(string path, int width, int height)
        {
            bool sized = true;
            string sizingError = null;
            try { SetGameViewSize(width, height); }
            catch (Exception e) { sized = false; sizingError = e.Message; }
            if (!sized)
            {
                Record("Game View capture " + Path.GetFileName(path), false, "Could not configure a real Game View: " + sizingError);
                yield break;
            }
            for (int i = 0; i < 12; i++) yield return null;
            if (Screen.width != width || Screen.height != height)
            {
                Record("Game View capture " + Path.GetFileName(path), false,
                    "Requested " + width + "x" + height + " but actual player surface is " + Screen.width + "x" + Screen.height + ". No substitute image created.");
                yield break;
            }
            string absolute = Path.GetFullPath(path);
            DateTime captureStart = DateTime.UtcNow;
            ScreenCapture.CaptureScreenshot(absolute);
            double until = EditorApplication.timeSinceStartup + 8;
            while (EditorApplication.timeSinceStartup < until &&
                (!File.Exists(absolute) || File.GetLastWriteTimeUtc(absolute) < captureStart || new FileInfo(absolute).Length < 1000))
                yield return null;
            bool saved = File.Exists(absolute) && File.GetLastWriteTimeUtc(absolute) >= captureStart && new FileInfo(absolute).Length > 1000;
            Record("Game View capture " + Path.GetFileName(path), saved,
                saved ? "Actual rendered Game View and runtime UI at " + width + "x" + height + "." : "ScreenCapture did not produce a fresh image; batch rendering may be unavailable.");
            if (saved) report.screenshots.Add(path);
        }

        static void SetGameViewSize(int width, int height)
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type viewType = assembly.GetType("UnityEditor.GameView", true);
            gameView = EditorWindow.GetWindow(viewType);
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            Type groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeKind = assembly.GetType("UnityEditor.GameViewSizeType", true);
            PropertyInfo singleton = sizesType.GetProperty("instance", Flags | BindingFlags.FlattenHierarchy);
            if (singleton == null) singleton = sizesType.BaseType.GetProperty("instance", Flags | BindingFlags.FlattenHierarchy);
            object sizes = singleton.GetValue(null);
            string platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "Standalone";
            object group = sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { Enum.Parse(groupType, platform) });
            object size = Activator.CreateInstance(sizeType, Flags, null,
                new[] { Enum.Parse(sizeKind, "FixedResolution"), (object)width, height, "RD Verification " + width + "x" + height }, null);
            group.GetType().GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null) - 1;
            viewType.GetProperty("selectedSizeIndex", Flags).SetValue(gameView, index);
            gameView.Focus(); gameView.Repaint();
        }

        /// <summary>Shoots every living bubble of the current level with lead-aimed real FIRE presses until the level clears.</summary>
        static IEnumerable<object> ClearLevel(RealityHud hud, WaveController wave, Camera camera)
        {
            int level = wave.Level;
            while (wave.Level == level && wave.Remaining > 0 && !wave.IsCelebrating)
            {
                int before = wave.Score;
                var target = wave.LivingTargets.Where(x => x && !x.IsConsumed).OrderBy(x => x.transform.position.x).First();
                double deadline = EditorApplication.timeSinceStartup + 8;
                while (wave.Score == before)
                {
                    if (EditorApplication.timeSinceStartup > deadline)
                        throw new TimeoutException("Level " + level + ": lead-aimed physical shots did not hit a live bubble.");
                    if (!target || target.IsConsumed)
                        target = wave.LivingTargets.Where(x => x && !x.IsConsumed).OrderBy(x => x.transform.position.x).FirstOrDefault();
                    if (target && wave.CanFire)
                    {
                        AimWithLead(camera, target, wave.projectileSpeed);
                        hud.FireButton.onClick.Invoke();
                    }
                    yield return null;
                }
                Require(wave.Score >= before + 10 && wave.Score % 10 == 0, "Level " + level + " hit, score " + wave.Score,
                    "A real OnCollisionEnter on a " + wave.CurrentLevel.motion + " bubble removed it and added ten points.");
            }
        }

        static void AimWithLead(Camera camera, EnergyTarget target, float speed)
        {
            Vector3 point = target.transform.position;
            for (int i = 0; i < 3; i++)
            {
                float flight = Vector3.Distance(camera.transform.position, point) / Mathf.Max(1f, speed);
                point = target.transform.position + target.Velocity * flight;
            }
            Aim(camera, point);
        }

        static bool PopupVisible(RealityHud hud, string title) =>
            hud.Celebration && hud.Celebration.IsPlaying &&
            hud.GetComponentsInChildren<Text>(true).Any(x => x.gameObject.activeInHierarchy && x.text == title);

        static Wait Until(Func<bool> predicate, float seconds, string reason) => new Wait(predicate, seconds, reason);
        static void Aim(Camera camera, Vector3 point) => camera.transform.rotation = Quaternion.LookRotation(point - camera.transform.position, Vector3.up);
        static void QueueMouse(Vector2 position, bool pressed) => InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, pressed));
        static int CountBases() => UnityEngine.Object.FindObjectsByType<GameBase>(FindObjectsSortMode.None).Length;
        static int CountActiveCameras() => UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled);
        static void Require(bool condition, string check, string detail)
        {
            Record(check, condition, detail);
            if (!condition) throw new InvalidOperationException(check + ": " + detail);
        }

        static void Record(string check, bool passed, string detail)
        {
            report.checks.Add(new CheckResult { check = check, passed = passed, detail = detail });
            Debug.Log("RD VERIFY " + (passed ? "PASS " : "FAIL ") + check + ": " + detail);
        }

        static void OnLog(string message, string stack, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
                report.runtimeErrors.Add(message + "\n" + stack);
        }

        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            if (inputConfigured)
            {
                Application.runInBackground = previousRunInBackground;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            }
            if (addedMouse && mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            report.passed = report.checks.Count > 0 && report.checks.All(x => x.passed) && report.runtimeErrors.Count == 0;
            Directory.CreateDirectory("Submission");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            SessionState.SetInt(ExitCode, report.passed ? 0 : 1);
            SessionState.SetString(Stage, "exiting");
            routine = null; waiting = null;
            Debug.Log("REALITY_DEFENDER_PLAYMODE_VERIFICATION_" + (report.passed ? "SUCCEEDED" : "FAILED") + " " + ReportPath);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }
    }
}
