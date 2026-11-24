using UnityEngine;

namespace Pamor
{
    // Art bible — the single source for every color in Pamor.
    // Style: flat wayang-kulit shadow play: bold dark silhouettes on a warm lamplit kelir (screen),
    //        batik sogan browns, gold prada accents.
    // Mood:  one blencong oil lamp behind the screen; warm glow at the centre, edges fall to deep brown.
    public static class Palette
    {
        public static readonly Color Kelir       = Hex("#EAD7AE"); // light board squares / screen glow
        public static readonly Color Sogan       = Hex("#8A5A34"); // dark board squares (batik sogan brown)
        public static readonly Color FrameWood   = Hex("#3E2A1E"); // board frame, UI batik frame base
        public static readonly Color PieceA      = Hex("#241812"); // player A pieces (wayang silhouette black-brown)
        public static readonly Color PieceB      = Hex("#9C2F22"); // player B pieces (merah bata / kain red)
        public static readonly Color Shadow      = Hex("#1A120D"); // background, deep shadow
        public static readonly Color AccentPrada = Hex("#D9A441"); // gold accent: Dam crown, highlights, selection
        public static readonly Color KeyLight    = Hex("#FFE8C8"); // blencong lamp: warm key light
        public static readonly Color SkyBounce   = Hex("#5C6472"); // cool shadow bounce (ambient sky)

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
