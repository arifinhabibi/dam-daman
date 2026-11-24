using System;
using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>The two sides. Hitam starts on the left (x 0..3) and moves toward +x; Merah moves toward -x.</summary>
    public enum Side { Hitam = 1, Merah = -1 }

    /// <summary>Board state on the <see cref="BoardGraph"/> points: pieces, turn and lasting effects. Game logic lives in <see cref="Rules"/>.</summary>
    public sealed class Board
    {
        readonly Piece[] cells = new Piece[BoardGraph.PointCount];
        readonly bool[] patihPlaced = new bool[2];
        readonly int[] shielded = { -1, -1 };
        readonly bool[] noPrajuritCapture = new bool[2];
        readonly int[] tenaga = new int[2];
        readonly int[,] cooldowns = new int[2, 6];
        readonly bool[] pancasonaArmed = new bool[2];
        readonly bool[] pancasonaUsed = new bool[2];
        readonly int[] patihStart = { -1, -1 };

        /// <summary>The side to move has already cast an ajian this turn.</summary>
        public bool CastThisTurn { get; internal set; }
        /// <summary>During a Saipi Angin second move, the only piece that may move; otherwise -1.</summary>
        public int ForcedPiece { get; internal set; } = -1;

        public int Tenaga(Side side) => tenaga[I(side)];
        public void SetTenaga(Side side, int value) => tenaga[I(side)] = System.Math.Max(0, System.Math.Min(AjianBook.MaxTenaga, value));
        public int CooldownLeft(Side side, Ajian a) => cooldowns[I(side), (int)a];
        internal void SetCooldown(Side side, Ajian a, int turns) => cooldowns[I(side), (int)a] = turns;
        internal void TickCooldowns(Side side) { for (int a = 0; a < 6; a++) if (cooldowns[I(side), a] > 0) cooldowns[I(side), a]--; }
        public bool PancasonaArmed(Side side) => pancasonaArmed[I(side)];
        public bool PancasonaUsed(Side side) => pancasonaUsed[I(side)];
        internal void ArmPancasona(Side side) { pancasonaArmed[I(side)] = true; pancasonaUsed[I(side)] = true; }
        internal void DisarmPancasona(Side side) => pancasonaArmed[I(side)] = false;
        /// <summary>Where this side's Patih started: where Pancasona brings it back.</summary>
        public int PatihStart(Side side) => patihStart[I(side)];

        public Side ToMove { get; internal set; } = Side.Hitam;
        /// <summary>Turns in a row with no capture, burn or non-Dam move; drives the draw rule.</summary>
        public int QuietMoves { get; internal set; }
        /// <summary>True while the side to move is taking its combo bonus move.</summary>
        public bool InBonusMove { get; internal set; }

        static int I(Side s) => s == Side.Hitam ? 0 : 1;

        public Piece this[int p] => cells[p];
        public bool IsEmpty(int p) => !cells[p].Exists;
        public Side? Owner(int p) => cells[p].Exists ? cells[p].Side : (Side?)null;

        public void Place(int p, Side side, Role role = Role.Prajurit, bool dam = false)
        {
            cells[p] = Piece.Of(side, role, dam);
            if (role == Role.Patih) { patihPlaced[I(side)] = true; patihStart[I(side)] = p; }
        }

        public void PlaceDam(int p, Side side) => Place(p, side, Role.Prajurit, true);
        public void Clear(int p) => cells[p] = default;
        internal void Set(int p, Piece piece) => cells[p] = piece;

        internal void MovePiece(int from, int to)
        {
            if (from == to) return;
            cells[to] = cells[from];
            cells[from] = default;
        }

        /// <summary>The point the side's Dukun shields during the opponent's turn, or -1.</summary>
        public int Shielded(Side side) => shielded[I(side)];
        internal void SetShielded(Side side, int p) => shielded[I(side)] = p;
        public bool IsShielded(int p) => cells[p].Exists && shielded[I(cells[p].Side)] == p;

        /// <summary>The side's Prajurit can't capture this turn (its Senopati died last turn).</summary>
        public bool PrajuritCaptureBlocked(Side side) => noPrajuritCapture[I(side)];
        internal void SetPrajuritCaptureBlocked(Side side, bool blocked) => noPrajuritCapture[I(side)] = blocked;

        /// <summary>Whether this side plays with a Patih (losing it loses the game).</summary>
        public bool HasPatihRule(Side side) => patihPlaced[I(side)];

        /// <summary>16 pieces each in the given arrangements (the default arrangement when null).</summary>
        public static Board CreateStandard(Formation hitam = null, Formation merah = null)
        {
            var b = new Board();
            foreach (var kv in (hitam ?? Formation.Default(Side.Hitam)).Roles) b.Place(kv.Key, Side.Hitam, kv.Value);
            foreach (var kv in (merah ?? Formation.Default(Side.Merah)).Roles) b.Place(kv.Key, Side.Merah, kv.Value);
            return b;
        }

        /// <summary>An empty board with the given side to move; for tests and puzzles.</summary>
        public static Board CreateEmpty(Side toMove = Side.Hitam) => new Board { ToMove = toMove };

        public Board Clone()
        {
            var b = new Board { ToMove = ToMove, QuietMoves = QuietMoves, InBonusMove = InBonusMove, CastThisTurn = CastThisTurn, ForcedPiece = ForcedPiece };
            Array.Copy(cells, b.cells, cells.Length);
            Array.Copy(patihPlaced, b.patihPlaced, 2);
            Array.Copy(shielded, b.shielded, 2);
            Array.Copy(noPrajuritCapture, b.noPrajuritCapture, 2);
            Array.Copy(tenaga, b.tenaga, 2);
            Array.Copy(cooldowns, b.cooldowns, cooldowns.Length);
            Array.Copy(pancasonaArmed, b.pancasonaArmed, 2);
            Array.Copy(pancasonaUsed, b.pancasonaUsed, 2);
            Array.Copy(patihStart, b.patihStart, 2);
            return b;
        }

        public static Side Other(Side s) => s == Side.Hitam ? Side.Merah : Side.Hitam;

        /// <summary>The column a side's Prajurit promote on: the outer edge of the opponent's triangle.</summary>
        public static int PromotionX(Side side) => side == Side.Hitam ? BoardGraph.Width - 1 : 0;

        public int CountPieces(Side side)
        {
            int n = 0;
            foreach (var c in cells) if (c.Exists && c.Side == side) n++;
            return n;
        }

        public int Find(Side side, Role role)
        {
            for (int p = 0; p < cells.Length; p++)
                if (cells[p].Exists && cells[p].Side == side && cells[p].Role == role) return p;
            return -1;
        }

        public static IEnumerable<int> AllPoints()
        {
            for (int p = 0; p < BoardGraph.PointCount; p++) yield return p;
        }
    }
}
