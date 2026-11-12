using UnityEngine;
using UnityEngine.UI;

namespace RealityDefender
{
    /// <summary>Responsive, safe-area HUD. The hit marker and all score labels consume accepted wave events.</summary>
    public sealed class RealityHud : MonoBehaviour
    {
        public WaveController wave;
        public PlacementController placement;
        public Font font;
        public Button FireButton { get; private set; }
        public Button PracticeButton { get; private set; }
        public Button ARButton { get; private set; }

        static readonly Color Cyan = new Color(.18f, .86f, 1f);
        static readonly Color Green = new Color(.3f, 1f, .57f);
        static readonly Color Amber = new Color(1f, .74f, .21f);
        static readonly Color Navy = new Color(.015f, .045f, .085f, .86f);
        RectTransform canvasRect, safe, title, scorePanel, statusPanel, promptPanel, menu, complete, reticle, hitMarker, fireRect, modeBadge;
        RectTransform menuButtonRect, replayRect;
        CanvasScaler scaler;
        Text score, progress, status, prompt, modeText, menuTitle, menuBody, completionCaption, hitText;
        TechGraphic statusFrame;
        ReticleGraphic fireGraphic;
        Text fireText, replayText;
        Button menuButton, replay;
        readonly GameObject[] checks = new GameObject[4];
        int lastWidth, lastHeight;
        Rect lastSafe;
        bool wasPlaced;
        float confirmUntil, hitUntil, kickUntil;
        Vector3 hitPosition;
        bool built;
        AuraCelebration celebration;
        public AuraCelebration Celebration => celebration;
        Font displayFont;
        RectTransform levelPanel;
        Text levelTitle, levelName;
        TechGraphic levelFrame;
        readonly TechGraphic[] pips = new TechGraphic[3];

        void Start()
        {
            Build();
            if (wave)
            {
                wave.StateChanged += Refresh;
                wave.HitAccepted += OnHit;
                wave.ShotFired += OnShot;
                wave.LevelStarted += OnLevelStarted;
                wave.LevelCompleted += OnLevelCompleted;
                wave.RoundReset += OnRoundReset;
            }
            if (placement) placement.Changed += Refresh;
            Refresh();
        }

        void OnShot() => kickUntil = Time.unscaledTime + 0.25f;
        void OnLevelStarted(LevelInfo info) { if (celebration) celebration.PlayIntro(info); }
        void OnRoundReset()
        {
            // Transient feedback belongs to the round that produced it.
            if (celebration) celebration.Stop();
            hitUntil = kickUntil = 0f;
            if (hitMarker) hitMarker.gameObject.SetActive(false);
        }

        void OnLevelCompleted(LevelResult result)
        {
            if (celebration) celebration.PlayLevelComplete(result, result.final ? 5f : wave.celebrationSeconds);
            Refresh();
        }

        void Build()
        {
            if (built) return;
            built = true;
            // Professional faces shipped under Resources (OFL); the built-in font remains the fallback.
            if (GameAssets.Body) font = GameAssets.Body;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            displayFont = GameAssets.Display ? GameAssets.Display : font;
            var go = new GameObject("Reality Defender HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasRect = go.GetComponent<RectTransform>();
            scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            safe = Rect("Safe area", canvasRect);
            Stretch(safe);

            title = Panel("Title", safe, Navy, Cyan);
            var logo = Rect("Shield reticle", title);
            Layout(logo, new Vector2(0, .5f), new Vector2(70, 0), new Vector2(70, 70), new Vector2(.5f, .5f));
            var logoGraphic = logo.gameObject.AddComponent<ReticleGraphic>();
            logoGraphic.color = Cyan; logoGraphic.raycastTarget = false;
            Label("THE REALITY", title, 32, Color.white, TextAnchor.MiddleLeft, new Vector2(122, -17), new Vector2(395, 42), new Vector2(0, 1));
            Label("DEFENDER", title, 42, Color.white, TextAnchor.MiddleLeft, new Vector2(120, -50), new Vector2(395, 52), new Vector2(0, 1));

            scorePanel = Panel("Score", safe, Navy, Cyan);
            Label("SCORE", scorePanel, 22, Cyan, TextAnchor.MiddleCenter, new Vector2(0, -12), new Vector2(200, 30), new Vector2(.5f, 1));
            score = Label("0", scorePanel, 60, Color.white, TextAnchor.MiddleCenter, new Vector2(0, -37), new Vector2(200, 68), new Vector2(.5f, 1));

            modeBadge = Rect("Mode label", safe);
            modeText = Label("", modeBadge, 21, Cyan, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(650, 35), new Vector2(0, 1));
            modeText.fontStyle = FontStyle.Bold;

            statusPanel = Panel("Wave status", safe, Navy, Cyan);
            statusFrame = statusPanel.GetComponent<TechGraphic>();
            progress = Label("WAVE 1   |   TARGETS 3/3", statusPanel, 29, Color.white, TextAnchor.MiddleLeft, new Vector2(30, -9), new Vector2(510, 44), new Vector2(0, 1));
            status = Label("BASE ANCHORED", statusPanel, 20, Green, TextAnchor.MiddleLeft, new Vector2(30, -52), new Vector2(510, 35), new Vector2(0, 1));
            status.fontStyle = FontStyle.Bold;
            for (int i = 0; i < checks.Length; i++)
            {
                var check = Panel("Confirmed hit " + (i + 1), statusPanel, new Color(.02f, .14f, .1f, .8f), Green);
                Layout(check, new Vector2(0, 1), new Vector2(252 + i * 75, -12), new Vector2(54, 45), new Vector2(0, 1));
                check.GetComponent<TechGraphic>().cut = 9;
                check.GetComponent<TechGraphic>().accents = false;
                var mark = Rect("Check", check);
                Stretch(mark);
                var success = mark.gameObject.AddComponent<SuccessGraphic>();
                success.color = Green; success.raycastTarget = false;
                checks[i] = check.gameObject;
            }

            promptPanel = Panel("Guidance", safe, Navy, Cyan);
            prompt = Label("Move your phone slowly over a well-lit table or floor", promptPanel, 29, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(820, 94), new Vector2(.5f, .5f));
            prompt.fontStyle = FontStyle.Normal;

            fireRect = Rect("FIRE", safe);
            fireGraphic = fireRect.gameObject.AddComponent<ReticleGraphic>();
            fireGraphic.fireControl = true;
            fireGraphic.color = new Color(1f, .27f, .13f);
            fireGraphic.thickness = 3f;
            FireButton = fireRect.gameObject.AddComponent<Button>();
            FireButton.targetGraphic = fireGraphic;
            var fireColors = FireButton.colors;
            fireColors.normalColor = Color.white;
            fireColors.highlightedColor = new Color(1f, .95f, .85f);
            fireColors.pressedColor = new Color(1f, .8f, .6f);
            fireColors.disabledColor = new Color(.36f, .4f, .47f, .55f);
            FireButton.colors = fireColors;
            FireButton.onClick.AddListener(() => { if (wave) wave.Fire(); });
            fireText = Label("FIRE", fireRect, 51, Color.white, TextAnchor.MiddleCenter, new Vector2(0, -2), new Vector2(190, 75), new Vector2(.5f, .5f));

            reticle = Rect("Camera-axis reticle", canvasRect);
            Layout(reticle, new Vector2(.5f, .5f), Vector2.zero, new Vector2(57, 57), new Vector2(.5f, .5f));
            var aim = reticle.gameObject.AddComponent<ReticleGraphic>();
            aim.color = new Color(1f, 1f, 1f, .95f); aim.raycastTarget = false;

            hitMarker = Rect("Accepted hit +10", canvasRect);
            Layout(hitMarker, new Vector2(0, 0), Vector2.zero, new Vector2(160, 80), new Vector2(.5f, .5f));
            hitText = Label("+10", hitMarker, 52, Amber, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(160, 80), new Vector2(.5f, .5f));
            hitMarker.gameObject.SetActive(false);

            menuButton = ActionButton("MENU", safe, 23, () => { if (placement) placement.ReturnToMenu(); });
            menuButtonRect = menuButton.GetComponent<RectTransform>();

            complete = Panel("Wave cleared", safe, new Color(.01f, .055f, .085f, .94f), Cyan);
            Label("WAVE CLEARED", complete, 68, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 22), new Vector2(850, 100), new Vector2(.5f, .5f));
            completionCaption = Label("ALL TARGETS NEUTRALIZED", complete, 23, Green, TextAnchor.MiddleCenter, new Vector2(0, -58), new Vector2(780, 50), new Vector2(.5f, .5f));
            completionCaption.fontStyle = FontStyle.Bold;
            replay = ActionButton("PLAY AGAIN", safe, 26, () =>
            {
                if (!placement) return;
                if (placement.State == PlacementState.Checking) placement.EnterPractice();
                else placement.Restart();
            });
            replayRect = replay.GetComponent<RectTransform>();
            replayText = replay.GetComponentInChildren<Text>();

            menu = Panel("Mode selection", safe, new Color(.008f, .025f, .055f, .97f), Cyan);
            menuTitle = Label("YOUR WORLD.\nYOUR ARENA.", menu, 66, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0, 205), new Vector2(800, 190), new Vector2(.5f, .5f));
            menuBody = Label("Find a surface. Place your base.\nAim with your phone and clear three energy targets.", menu, 28,
                new Color(.76f, .88f, .95f), TextAnchor.MiddleCenter, new Vector2(0, 60), new Vector2(760, 95), new Vector2(.5f, .5f));
            menuBody.fontStyle = FontStyle.Normal;
            ARButton = ActionButton("ENTER AR", menu, 32, () => { if (placement) placement.EnterAR(); });
            Layout(ARButton.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(690, 94), new Vector2(.5f, .5f));
            var arPlate = ARButton.GetComponent<TechGraphic>();
            arPlate.color = new Color(.02f, .20f, .28f, .95f);
            PracticeButton = ActionButton("PRACTICE MODE", menu, 30, () => { if (placement) placement.EnterPractice(); });
            Layout(PracticeButton.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(0, -177), new Vector2(690, 88), new Vector2(.5f, .5f));
            Label("01  SCAN     /     02  PLACE     /     03  DEFEND", menu, 21, Cyan, TextAnchor.MiddleCenter,
                new Vector2(0, -282), new Vector2(760, 55), new Vector2(.5f, .5f));
            // Always-visible level tracker: "LEVEL 2 / 3", the level name, and one segment per level.
            levelPanel = Panel("Level tracker", safe, Navy, Cyan);
            levelFrame = levelPanel.GetComponent<TechGraphic>();
            levelTitle = Label("LEVEL 1 / 3", levelPanel, 40, Color.white, TextAnchor.MiddleCenter, new Vector2(0, -6), new Vector2(480, 50), new Vector2(.5f, 1));
            levelName = Label("STILL WATERS", levelPanel, 25, Cyan, TextAnchor.MiddleCenter, new Vector2(0, -52), new Vector2(480, 30), new Vector2(.5f, 1));
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = Panel("Level " + (i + 1) + " segment", levelPanel, new Color(1f, 1f, 1f, .12f), new Color(1f, 1f, 1f, .25f));
                Layout(pip, new Vector2(.5f, 0), new Vector2((i - 1) * 132f, 12), new Vector2(118, 12), new Vector2(.5f, 0));
                pips[i] = pip.GetComponent<TechGraphic>();
                pips[i].cut = 3;
                pips[i].accents = false;
            }

            // Display face for headline text; real font files carry their own weight, so no synthetic bold/italic.
            foreach (var headline in new[] { score, fireText, menuTitle, levelTitle })
                if (headline) headline.font = displayFont;
            foreach (var text in title.GetComponentsInChildren<Text>(true)) text.font = displayFont;
            foreach (var text in complete.GetComponentsInChildren<Text>(true))
                if (text != completionCaption) text.font = displayFont;
            if (GameAssets.Body || GameAssets.Display)
                foreach (var text in canvasRect.GetComponentsInChildren<Text>(true)) text.fontStyle = FontStyle.Normal;

            // Full-screen level popups sit above everything but never take input.
            celebration = gameObject.AddComponent<AuraCelebration>();
            celebration.Build(canvasRect, font, displayFont);
            UpdateLayout();
        }

        void Update()
        {
            if (!built) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafe) UpdateLayout();
            if (wave)
            {
                FireButton.interactable = wave.CanFire;
                fireText.color = wave.IsActive && wave.IsTracking && !wave.IsCelebrating ? Color.white : new Color(.6f, .66f, .72f, .8f);
                // The final WAVE CLEARED panel waits until the aura celebration has played.
                bool showComplete = wave.IsComplete && !(celebration && celebration.IsPlaying);
                if (complete.gameObject.activeSelf != showComplete) complete.gameObject.SetActive(showComplete);
            }
            // The active level segment breathes so the player always sees where they are.
            if (wave && wave.Level > 0 && !wave.IsCelebrating && !wave.IsComplete)
            {
                var active = pips[Mathf.Clamp(wave.Level - 1, 0, pips.Length - 1)];
                Color c = wave.CurrentLevel.primary;
                c.a = .55f + .45f * Mathf.Sin(Time.unscaledTime * 5f);
                active.color = c;
            }
            // Reticle kick: punches out and spins a quarter turn on every shot.
            float kick = Mathf.Clamp01((kickUntil - Time.unscaledTime) / 0.25f);
            kick *= kick;
            reticle.localScale = Vector3.one * (1f + 0.6f * kick);
            reticle.localRotation = Quaternion.Euler(0f, 0f, 90f * kick);
            if (placement && placement.IsResizing)
            {
                promptPanel.gameObject.SetActive(true);
                prompt.text = placement.Status;
            }
            else if (placement && placement.HasPlaced && wave && wave.IsTracking && Time.unscaledTime > confirmUntil && !wave.IsComplete)
                promptPanel.gameObject.SetActive(false);
            UpdateProjectedMarkers();
        }

        void UpdateProjectedMarkers()
        {
            var camera = wave && wave.ActiveCamera ? wave.ActiveCamera : placement ? placement.ActiveCamera : null;
            if (camera && reticle.gameObject.activeSelf)
            {
                Vector3 screen = camera.WorldToScreenPoint(camera.transform.position + camera.transform.forward * 2f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var point);
                reticle.localPosition = point;
            }
            if (Time.unscaledTime >= hitUntil) { hitMarker.gameObject.SetActive(false); return; }
            if (!camera) return;
            Vector3 hit = camera.WorldToScreenPoint(hitPosition);
            bool visible = hit.z > 0f && hit.x >= 0 && hit.x <= Screen.width && hit.y >= 0 && hit.y <= Screen.height;
            hitMarker.gameObject.SetActive(visible);
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, hit, null, out var local);
            hitMarker.localPosition = local + new Vector2(40, 70 + (1f - (hitUntil - Time.unscaledTime)) * 42f);
            var color = Amber;
            color.a = Mathf.Clamp01((hitUntil - Time.unscaledTime) * 2.4f);
            hitText.color = color;
        }

        void OnHit(Vector3 point)
        {
            hitPosition = point;
            hitUntil = Time.unscaledTime + .8f;
        }

        public void Refresh()
        {
            if (!built || !canvasRect || !placement || !wave) return;
            bool inMenu = placement.State == PlacementState.Menu || placement.State == PlacementState.Unavailable;
            bool checking = placement.State == PlacementState.Checking;
            bool hasBase = placement.HasPlaced;
            bool isPractice = placement.Mode == ExperienceMode.Practice;
            bool cleared = wave.IsComplete;
            score.text = wave.Score.ToString();
            scorePanel.gameObject.SetActive(hasBase);
            menu.gameObject.SetActive(inMenu);
            menuButton.gameObject.SetActive(!inMenu);
            FireButton.gameObject.SetActive(hasBase);
            FireButton.interactable = wave.CanFire;
            statusPanel.gameObject.SetActive(hasBase);
            reticle.gameObject.SetActive(!inMenu && !checking && !cleared);
            complete.gameObject.SetActive(cleared && !(celebration && celebration.IsPlaying));
            completionCaption.text = "ALL " + Levels.Count + " LEVELS NEUTRALIZED  /  SCORE " + wave.Score;
            bool recovery = hasBase && !wave.IsTracking && !cleared;
            replay.gameObject.SetActive(cleared || recovery || checking);
            replayText.text = checking ? "PRACTICE MODE" : recovery ? "RESTART" : "PLAY AGAIN";
            modeBadge.gameObject.SetActive(!inMenu);
            modeText.text = isPractice ? "PRACTICE MODE  /  DRAG TO AIM" : "AR TARGET PRACTICE";
            if (!wasPlaced && hasBase) confirmUntil = Time.unscaledTime + 4f;
            wasPlaced = hasBase;
            bool celebrating = wave.IsCelebrating;
            int level = Mathf.Max(1, wave.Level);
            LevelInfo info = Levels.Get(level);
            levelPanel.gameObject.SetActive(hasBase);
            levelTitle.text = cleared ? "ALL LEVELS CLEARED" : "LEVEL " + level + " / " + Levels.Count;
            levelName.text = cleared ? "SCORE " + wave.Score : celebrating ? info.name + "  COMPLETE" : info.name;
            levelName.color = cleared ? Green : info.primary;
            levelFrame.borderColor = cleared ? Green : info.primary;
            levelFrame.SetVerticesDirty();
            for (int i = 0; i < pips.Length; i++)
            {
                bool done = cleared || i < level - 1 || (celebrating && i == level - 1);
                bool current = !done && i == level - 1;
                pips[i].color = done ? Green : current ? info.primary : new Color(1f, 1f, 1f, .12f);
                pips[i].borderColor = done ? Green : current ? Color.white : new Color(1f, 1f, 1f, .25f);
                pips[i].SetVerticesDirty();
            }
            progress.text = cleared ? "ALL LEVELS"
                : celebrating ? "LEVEL " + level
                : "LEVEL " + level + "/" + Levels.Count + "   |   TARGETS " + wave.Remaining + "/" + Mathf.Max(wave.Remaining, wave.LevelTargetCount);
            // One lit check per bubble of the level just cleared.
            int lit = cleared || celebrating ? wave.LevelTargetCount : 0;
            for (int i = 0; i < checks.Length; i++) checks[i].SetActive(i < lit);
            status.text = cleared ? "ALL " + Levels.Count + " LEVELS CLEARED"
                : celebrating ? "LEVEL " + level + " COMPLETE"
                : !wave.IsTracking && hasBase ? "TRACKING PAUSED" : isPractice ? "BASE PLACED" : "BASE ANCHORED";
            status.color = wave.IsTracking || cleared ? Green : Amber;
            statusFrame.borderColor = wave.IsTracking || cleared ? Cyan : Amber;
            statusFrame.SetVerticesDirty();
            string text = placement.Status;
            if (hasBase && wave.IsTracking && Time.unscaledTime <= confirmUntil) text = isPractice ? "BASE PLACED  /  DRAG TO AIM  /  PINCH TO RESIZE" : "BASE ANCHORED  /  PINCH WITH 2 FINGERS TO RESIZE";
            prompt.text = text;
            promptPanel.gameObject.SetActive(!inMenu && !cleared && (!hasBase || !wave.IsTracking || Time.unscaledTime <= confirmUntil));
            if (inMenu)
            {
                bool unavailable = placement.State == PlacementState.Unavailable;
                menuTitle.text = unavailable ? "AR IS UNAVAILABLE" : "YOUR WORLD.\nYOUR ARENA.";
                menuTitle.fontSize = unavailable ? 48 : 66;
                menuBody.text = unavailable ? placement.Status : "Find a surface. Place your base.\nAim with your phone and clear three energy targets.";
                ARButton.GetComponentInChildren<Text>().text = unavailable ? "RETRY AR" : "ENTER AR";
            }
        }

        void UpdateLayout()
        {
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            bool portrait = lastHeight > lastWidth;
            scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            // Match the narrow axis so the short side is always exactly 1080 units: fixed-width panels never overlap or
            // clip on tall (20:9+) phones, which instead gain extra space along the long axis.
            scaler.matchWidthOrHeight = portrait ? 0f : 1f;
            safe.anchorMin = new Vector2(lastSafe.xMin / Mathf.Max(1, lastWidth), lastSafe.yMin / Mathf.Max(1, lastHeight));
            safe.anchorMax = new Vector2(lastSafe.xMax / Mathf.Max(1, lastWidth), lastSafe.yMax / Mathf.Max(1, lastHeight));
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            Layout(title, new Vector2(0, 1), new Vector2(32, -30), new Vector2(portrait ? 610 : 555, 112), new Vector2(0, 1));
            Layout(scorePanel, new Vector2(1, 1), new Vector2(-32, -30), new Vector2(250, 112), new Vector2(1, 1));
            Layout(modeBadge, new Vector2(0, 1), new Vector2(48, -155), new Vector2(650, 40), new Vector2(0, 1));
            Layout(statusPanel, Vector2.zero, new Vector2(35, 38), new Vector2(portrait ? 565 : 570, 104), Vector2.zero);
            Layout(fireRect, new Vector2(1, 0), new Vector2(-35, 38), new Vector2(portrait ? 250 : 225, portrait ? 250 : 225), new Vector2(1, 0));
            // Narrow landscape (4:3 / 3:2 tablets): the safe area is under ~1800 units wide, too tight for the title,
            // level tracker, MENU and score in one row, so the tracker drops to a second row like portrait.
            float safeUnits = lastSafe.width / Mathf.Max(1f, lastHeight) * 1080f;
            bool narrow = !portrait && safeUnits < 1800f;
            Layout(promptPanel, new Vector2(.5f, 0), new Vector2(0, portrait ? 325 : 165), new Vector2(portrait || narrow ? 900 : 1080, 110), new Vector2(.5f, 0));
            // Landscape: MENU sits just left of the score so the level tracker owns the top centre.
            if (portrait) Layout(menuButtonRect, new Vector2(.5f, 1), new Vector2(340, -163), new Vector2(150, 62), new Vector2(.5f, 1));
            else Layout(menuButtonRect, new Vector2(1, 1), new Vector2(-300, -55), new Vector2(150, 62), new Vector2(1, 1));
            Vector2 trackerPosition = portrait ? new Vector2(-50, -240) : narrow ? new Vector2(0, -205) : new Vector2(0, -30);
            Layout(levelPanel, new Vector2(.5f, 1), trackerPosition, new Vector2(portrait ? 500 : 460, 112), new Vector2(.5f, 1));
            Layout(complete, new Vector2(.5f, portrait ? .72f : .69f), Vector2.zero, new Vector2(portrait ? 950 : 1080, 230), new Vector2(.5f, .5f));
            // Portrait: below the guidance panel (y 325-435) and above the status panel (y 38-142), left of FIRE.
            // Narrow landscape: shifted right of the status panel (x <= 605) and left of FIRE.
            Layout(replayRect, new Vector2(.5f, 0), new Vector2(narrow ? 90 : 0, portrait ? 172 : 72), new Vector2(320, 82), new Vector2(.5f, 0));
            Layout(menu, new Vector2(.5f, .5f), new Vector2(0, portrait ? 30 : -20), new Vector2(portrait ? 930 : 1000, 740), new Vector2(.5f, .5f));
            Canvas.ForceUpdateCanvases();
        }

        RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        RectTransform Panel(string name, Transform parent, Color fill, Color border)
        {
            var rect = Rect(name, parent);
            var frame = rect.gameObject.AddComponent<TechGraphic>();
            frame.color = fill; frame.borderColor = border; frame.raycastTarget = false;
            return rect;
        }

        Text Label(string content, Transform parent, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 dimensions, Vector2 anchor)
        {
            var rect = Rect(content, parent);
            Layout(rect, anchor, position, dimensions, anchor);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.text = content; text.fontSize = size; text.color = color;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        Button ActionButton(string label, Transform parent, int size, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(label, parent, Navy, Cyan);
            var plate = rect.GetComponent<TechGraphic>();
            plate.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.7f, 1f, 1f);
            colors.pressedColor = new Color(.45f, .75f, 1f);
            button.colors = colors;
            button.onClick.AddListener(action);
            var text = Label(label, rect, size, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            Stretch(text.rectTransform);
            return button;
        }

        static void Layout(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void OnDestroy()
        {
            built = false;
            if (wave)
            {
                wave.StateChanged -= Refresh;
                wave.HitAccepted -= OnHit;
                wave.ShotFired -= OnShot;
                wave.LevelStarted -= OnLevelStarted;
                wave.LevelCompleted -= OnLevelCompleted;
                wave.RoundReset -= OnRoundReset;
            }
            if (placement) placement.Changed -= Refresh;
        }
    }
}
