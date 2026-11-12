using UnityEngine;
using UnityEngine.UI;

namespace RealityDefender
{
    /// <summary>
    /// Full-screen level popups on the HUD canvas.
    ///  - Level complete: aura burst (flash, edge flames, god rays, shock rings, embers, sparkles), confetti cannons and
    ///    rain, an elastic "LEVEL i COMPLETED" title with a pulsing glow, "WAVE CLEARED", a points count-up and a rank line.
    ///    The final level gets a rainbow title, more rays/rings/embers and a longer show.
    ///  - Level intro: a light band with streaks while the level number and name slam in and streak out.
    /// Everything runs on unscaled time (unaffected by hit-stop) and never blocks input.
    /// </summary>
    public sealed class AuraCelebration : MonoBehaviour
    {
        RectTransform root;
        AuraBurstGraphic aura;
        ConfettiGraphic confetti;
        Text title, banner, points, detail;
        Outline titleGlow, bannerGlow;
        float started = -1f;
        float duration;
        bool intro, final;
        Color primary, secondary;
        int pointsTarget;
        Vector2 titleHome, bannerHome, pointsHome, detailHome;

        /// <summary>A level-complete celebration is on screen.</summary>
        public bool IsPlaying => started >= 0f && !intro;

        public void Build(RectTransform canvas, Font font) => Build(canvas, font, font);

        public void Build(RectTransform canvas, Font font, Font display)
        {
            if (!display) display = font;
            // Real font files carry their own weight; only the built-in fallback needs synthetic styling.
            bool realFonts = GameAssets.Body || GameAssets.Display;
            root = new GameObject("Aura celebration", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas, false);
            Stretch(root);

            aura = new GameObject("Aura burst", typeof(RectTransform)).AddComponent<AuraBurstGraphic>();
            aura.rectTransform.SetParent(root, false);
            Stretch(aura.rectTransform);
            aura.raycastTarget = false;
            Material additive = FxMaterials.UIAdditive;
            if (additive) aura.material = additive;

            confetti = new GameObject("Confetti", typeof(RectTransform)).AddComponent<ConfettiGraphic>();
            confetti.rectTransform.SetParent(root, false);
            Stretch(confetti.rectTransform);
            confetti.raycastTarget = false;

            title = MakeText("Celebration title", display, realFonts ? FontStyle.Normal : FontStyle.BoldAndItalic);
            titleGlow = title.gameObject.AddComponent<Outline>();
            titleGlow.effectDistance = new Vector2(4f, -4f);
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            titleShadow.effectDistance = new Vector2(0f, -9f);
            banner = MakeText("Celebration banner", display, realFonts ? FontStyle.Normal : FontStyle.Bold);
            bannerGlow = banner.gameObject.AddComponent<Outline>();
            bannerGlow.effectDistance = new Vector2(2f, -2f);
            points = MakeText("Celebration points", display, realFonts ? FontStyle.Normal : FontStyle.BoldAndItalic);
            points.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -5f);
            detail = MakeText("Celebration detail", font, realFonts ? FontStyle.Normal : FontStyle.Bold);
            detail.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
            root.gameObject.SetActive(false);
        }

        public void PlayLevelComplete(LevelResult result, float seconds)
        {
            LevelInfo info = result.level;
            Begin(false, result.final, info.primary, info.secondary, seconds);
            title.text = "LEVEL " + info.number + " COMPLETED";
            banner.text = "WAVE CLEARED";
            pointsTarget = info.Points;
            points.text = "+0";
            string rank = result.Accuracy >= 100 ? "FLAWLESS" : result.Accuracy >= 75 ? "SHARPSHOOTER" : result.Accuracy >= 50 ? "STEADY AIM" : "SURVIVOR";
            string next = result.final
                ? "ALL " + Levels.Count + " LEVELS CLEARED  /  SCORE " + result.totalScore
                : "NEXT  /  LEVEL " + (info.number + 1) + "  " + Levels.Get(info.number + 1).name;
            detail.text = next + "\n" + rank + "  /  ACCURACY " + result.Accuracy + "%  /  " + result.seconds.ToString("F1") + " S";
            Animate(0f);
        }

        public void PlayIntro(LevelInfo info)
        {
            Begin(true, false, info.primary, info.secondary, 1.7f);
            title.text = "LEVEL " + info.number;
            banner.text = info.name;
            points.text = "";
            detail.text = info.targets + " BUBBLES  /  " + MotionLabel(info.motion);
            Animate(0f);
        }

        public void Stop()
        {
            started = -1f;
            if (root) root.gameObject.SetActive(false);
        }

        static string MotionLabel(TargetMotion motion) =>
            motion == TargetMotion.Static ? "HOLDING STILL" : motion == TargetMotion.Orbit ? "ORBITING THE BASE" : "FREE FLIGHT";

        void Begin(bool isIntro, bool isFinal, Color a, Color b, float seconds)
        {
            if (!root) return;
            intro = isIntro;
            final = isFinal;
            primary = a;
            secondary = b;
            duration = Mathf.Max(0.5f, seconds);
            started = Time.unscaledTime;
            float seed = Random.Range(0f, 100f);
            aura.mode = isIntro ? AuraBurstGraphic.Mode.Intro : AuraBurstGraphic.Mode.Celebrate;
            aura.primary = a;
            aura.secondary = b;
            aura.final = isFinal;
            aura.duration = duration;
            aura.seed = seed;
            confetti.gameObject.SetActive(!isIntro);
            confetti.palette = new[] { a, b, new Color(1f, 0.84f, 0.25f), Color.white, new Color(1f, 0.42f, 0.75f), Color.Lerp(a, Color.white, 0.5f) };
            confetti.final = isFinal;
            confetti.duration = duration;
            confetti.seed = seed;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            Layout();
        }

        void Update()
        {
            if (started < 0f) return;
            float t = Time.unscaledTime - started;
            if (t >= duration) { Stop(); return; }
            Layout();
            Animate(t);
        }

        void Animate(float t)
        {
            aura.time = t;
            aura.SetVerticesDirty();
            if (confetti.gameObject.activeSelf)
            {
                confetti.time = t;
                confetti.SetVerticesDirty();
            }
            float fadeOut = Mathf.Clamp01((duration - t) / 0.45f);
            if (intro) AnimateIntro(t, fadeOut);
            else AnimateComplete(t, fadeOut);
        }

        void AnimateComplete(float t, float fadeOut)
        {
            // Elastic slam: oversized, overshoots, settles with a little twist.
            float punch = 1f + 1.9f * Mathf.Exp(-6.5f * t) * Mathf.Cos(13f * t);
            title.rectTransform.localScale = Vector3.one * Mathf.Max(0.6f, punch);
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 7f * Mathf.Exp(-5f * t) * Mathf.Sin(17f * t));
            title.rectTransform.anchoredPosition = titleHome;
            Color tc = final ? Color.HSVToRGB(Mathf.Repeat(t * 0.45f, 1f), 0.35f, 1f)
                             : Color.Lerp(Color.white, primary, 0.25f + 0.25f * Mathf.Sin(t * 6f));
            tc.a = Mathf.Clamp01(t / 0.1f) * fadeOut;
            title.color = tc;
            titleGlow.effectColor = AuraBurstGraphic.Alpha(primary, (0.55f + 0.35f * Mathf.Sin(t * 8f)) * fadeOut);

            float b = Ease((t - 0.2f) / 0.35f);
            banner.rectTransform.anchoredPosition = bannerHome + Vector2.down * (1f - b) * 40f;
            banner.color = AuraBurstGraphic.Alpha(Color.white, b * fadeOut);
            bannerGlow.effectColor = AuraBurstGraphic.Alpha(secondary, 0.7f * b * fadeOut);

            float p = Mathf.Clamp01((t - 0.38f) / 0.5f);
            points.text = "+" + Mathf.RoundToInt(pointsTarget * Ease(p));
            float pop = t > 0.88f ? 1f + 0.35f * Mathf.Exp(-10f * (t - 0.88f)) : 1f;
            points.rectTransform.localScale = Vector3.one * pop;
            points.rectTransform.anchoredPosition = pointsHome;
            points.color = AuraBurstGraphic.Alpha(new Color(1f, 0.82f, 0.3f), Mathf.Clamp01(p * 3f) * fadeOut);

            float d = Ease((t - 0.65f) / 0.4f);
            detail.rectTransform.anchoredPosition = detailHome + Vector2.down * (1f - d) * 20f;
            detail.color = AuraBurstGraphic.Alpha(Color.Lerp(Color.white, primary, 0.4f), d * fadeOut);
        }

        void AnimateIntro(float t, float fadeOut)
        {
            float width = root.rect.width;
            float enter = Ease(t / 0.28f);
            float exit = Ease((t - (duration - 0.35f)) / 0.35f);
            float x = Mathf.Lerp(-width * 0.6f, 0f, enter) + exit * width * 0.6f;
            title.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Exp(-8f * t));
            title.rectTransform.localRotation = Quaternion.identity;
            title.rectTransform.anchoredPosition = titleHome + new Vector2(x, 0f);
            title.color = AuraBurstGraphic.Alpha(Color.white, enter * fadeOut);
            titleGlow.effectColor = AuraBurstGraphic.Alpha(primary, 0.8f * fadeOut);
            banner.rectTransform.anchoredPosition = bannerHome + new Vector2(-x * 0.7f, 0f);
            banner.color = AuraBurstGraphic.Alpha(Color.Lerp(Color.white, primary, 0.5f), enter * fadeOut);
            bannerGlow.effectColor = AuraBurstGraphic.Alpha(secondary, 0.6f * fadeOut);
            points.color = Color.clear;
            detail.rectTransform.anchoredPosition = detailHome + new Vector2(x * 0.4f, 40f);
            detail.color = AuraBurstGraphic.Alpha(Color.white, Ease((t - 0.2f) / 0.3f) * fadeOut);
        }

        void Layout()
        {
            Rect r = root.rect;
            if (r.width <= 1f) return;
            bool portrait = r.height > r.width;
            Place(title, new Vector2(r.width * 0.94f, portrait ? 130f : 150f), portrait ? 84 : 108);
            Place(banner, new Vector2(r.width * 0.9f, 70f), portrait ? 42 : 48);
            Place(points, new Vector2(r.width * 0.6f, 90f), portrait ? 60 : 66);
            Place(detail, new Vector2(r.width * 0.92f, 100f), portrait ? 26 : 28);
            titleHome = new Vector2(0f, r.height * (portrait ? 0.09f : 0.1f));
            bannerHome = new Vector2(0f, titleHome.y - (portrait ? 105f : 120f));
            pointsHome = new Vector2(0f, bannerHome.y - 85f);
            detailHome = new Vector2(0f, pointsHome.y - 95f);
        }

        static void Place(Text text, Vector2 size, int fontSize)
        {
            text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize;
            text.resizeTextMaxSize = fontSize;
        }

        Text MakeText(string name, Font font, FontStyle style)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static float Ease(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);
    }
}
