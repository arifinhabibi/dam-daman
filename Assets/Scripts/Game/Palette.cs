using UnityEngine;

namespace Pamor
{
    // Art bible — the single source for every color in Pamor.
    // Style: wayang-kulit pieces (bold dark silhouettes with gold prada) on a carved board, set in a
    //        bright Nusantara candi garden: andesite stone, green hills, split gates, tropical trees.
    // Mood:  clear late-morning sun over the courtyard; sky-blue bounce in the shadows, warm highlights.
    //        UI keeps the batik wood-and-gold frames.
    public static class Palette
    {
        // Board and pieces
        public static readonly Color Kelir       = Hex("#EAD7AE"); // board surface (kelir parchment)
        public static readonly Color Sogan       = Hex("#8A5A34"); // carved lines and points (batik sogan brown)
        public static readonly Color FrameWood   = Hex("#3E2A1E"); // board frame, UI batik frame base
        public static readonly Color PieceA      = Hex("#241812"); // Hitam pieces (wayang silhouette black-brown)
        public static readonly Color PieceB      = Hex("#9C2F22"); // Merah pieces (merah bata / kain red)
        public static readonly Color Shadow      = Hex("#1A120D"); // UI scrims, deepest shadow
        public static readonly Color AccentPrada = Hex("#D9A441"); // gold accent: Dam crown, highlights, selection

        // Daylight environment
        public static readonly Color KeyLight    = Hex("#FFF3DE"); // late-morning sun
        public static readonly Color SkyBounce   = Hex("#9CC6E8"); // sky-blue shadow bounce (ambient sky)
        public static readonly Color SkyTop      = Hex("#6FAEDC"); // sky overhead
        public static readonly Color SkyHorizon  = Hex("#D6EAF2"); // hazy horizon, fog
        public static readonly Color GrassLight  = Hex("#86BC4E"); // sunlit meadow
        public static readonly Color GrassDark   = Hex("#4E8A3A"); // distant hills, shaded grass
        public static readonly Color Leaf        = Hex("#3F7A35"); // tree canopies
        public static readonly Color Trunk       = Hex("#6B4A2E"); // tree trunks
        public static readonly Color Stone       = Hex("#9A958A"); // andesite (candi stone)
        public static readonly Color StoneDark   = Hex("#6A655C"); // stone in shade, plinth steps

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
