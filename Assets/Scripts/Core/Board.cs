using System;
using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>The two sides. Hitam starts on the left (x 0..3) and moves toward +x; Merah moves toward -x.</summary>
    public enum Side { Hitam = 1, Merah = -1 }

    /// <summary>
    /// Board state on the <see cref="BoardGraph"/> points. Each cell holds 0 (empty), +1 Hitam man,
    /// +2 Hitam Dam, -1 Merah man, -2 Merah Dam. Game logic lives in <see cref="Rules"/>.
    /// </summary>
    public sealed class Board
    {
        public const int Man = 1;
        public const int Dam = 2;

        readonly int[] cells = new int[BoardGraph.PointCount];
        readonly int[] damIdle = new int[BoardGraph.PointCount];

        public Side ToMove { get; internal set; } = Side.Hitam;
        /// <summary>Turns in a row with no capture, burn or man move (only Dams shuffling); drives the draw rule.</summary>
        public int QuietMoves { get; internal set; }
        /// <summary>True while the side to move is taking its combo bonus move.</summary>
        public bool InBonusMove { get; internal set; }

        public int this[int p]
        {
            get => cells[p];
            set { cells[p] = value; damIdle[p] = 0; }
        }

        /// <summary>Own turns in a row this Dam has gone without capturing.</summary>
        public int DamIdle(int p) => damIdle[p];
        internal void SetDamIdle(int p, int turns) => damIdle[p] = turns;

        internal void MovePiece(int from, int to)
        {
            if (from == to) return;
            cells[to] = cells[from]; damIdle[to] = damIdle[from];
            cells[from] = 0; damIdle[from] = 0;
        }

        /// <summary>16 pieces each: own triangle plus the two nearest columns of the square; centre column empty.</summary>
        public static Board CreateStandard()
        {
            var b = new Board();
            foreach (var p in AllPoints())
            {
                int x = BoardGraph.Xs[p];
                if (x <= 3) b[p] = Man;
                else if (x >= 5) b[p] = -Man;
            }
            return b;
        }

        /// <summary>An empty board with the given side to move; for tests and puzzles.</summary>
        public static Board CreateEmpty(Side toMove = Side.Hitam) => new Board { ToMove = toMove };

        public Board Clone()
        {
            var b = new Board { ToMove = ToMove, QuietMoves = QuietMoves, InBonusMove = InBonusMove };
            Array.Copy(cells, b.cells, cells.Length);
            Array.Copy(damIdle, b.damIdle, damIdle.Length);
            return b;
        }

        public static Side? Owner(int piece) => piece > 0 ? Side.Hitam : piece < 0 ? Side.Merah : (Side?)null;
        public static bool IsDam(int piece) => Math.Abs(piece) == Dam;
        public static Side Other(Side s) => s == Side.Hitam ? Side.Merah : Side.Hitam;

        /// <summary>The column a side's men promote on: the outer edge of the opponent's triangle.</summary>
        public static int PromotionX(Side side) => side == Side.Hitam ? BoardGraph.Width - 1 : 0;

        public int CountPieces(Side side)
        {
            int n = 0;
            for (int p = 0; p < cells.Length; p++)
                if (Owner(cells[p]) == side) n++;
            return n;
        }

        public static IEnumerable<int> AllPoints()
        {
            for (int p = 0; p < BoardGraph.PointCount; p++) yield return p;
        }
    }
}
