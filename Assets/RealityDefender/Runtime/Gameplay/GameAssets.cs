using System.Collections.Generic;
using UnityEngine;

namespace RealityDefender
{
    /// <summary>
    /// Shipped presentation assets that live under Resources (so they need no scene wiring):
    ///  - Fonts/RussoOne-Regular (display) and Fonts/Rajdhani-Bold (UI body): Google Fonts, SIL Open Font License 1.1.
    ///  - Audio/sfx_*: recorded sci-fi effects from Kenney's "Sci-Fi Sounds" pack, CC0 1.0.
    /// Every accessor returns null when an asset is missing, and callers fall back to built-in or synthesised content.
    /// </summary>
    public static class GameAssets
    {
        static Font display, body;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        /// <summary>Bold geometric display face for titles, the score and FIRE.</summary>
        public static Font Display => display ? display : display = Resources.Load<Font>("Fonts/RussoOne-Regular");

        /// <summary>Condensed technical face for HUD body text.</summary>
        public static Font Body => body ? body : body = Resources.Load<Font>("Fonts/Rajdhani-Bold");

        public static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out AudioClip clip) && clip) return clip;
            clip = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = clip;
            return clip;
        }
    }
}
