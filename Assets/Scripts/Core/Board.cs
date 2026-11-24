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
        // Two shield slots per side (Lutung Kasarung's Dukun shields two pieces).
        readonly int[,] shields = { { -1, -1 }, { -1, -1 } };
        readonly int[] commanders = { -1, -1 };
        readonly int[] commanderCooldown = new int[2];
        readonly bool[] blocked = new bool[BoardGraph.PointCount];
        readonly int[] wallTurns = new int[BoardGraph.PointCount];
        readonly int[] wallOwner = new int[BoardGraph.PointCount];

        /// <summary>Turns played so far; seeds the deterministic "random" picks (Teluh Massal).</summary>
        public int Ply { get; internal set; }

        public Commander? CommanderOf(Side side) => commanders[I(side)] < 0 ? (Commander?)null : (Commander)commanders[I(side)];

        /// <summary>Sets a side's Commander; Jaka Tingkir's passive guards that side's Pendekar from here on.</summary>
        public void SetCommander(Side side, Commander? c)
        {
            commanders[I(side)] = c.HasValue ? (int)c.Value : -1;
            for (int p = 0; p < cells.Length; p++)
            {
                var piece = cells[p];
                if (!piece.Exists || piece.Side != side || piece.Role != Role.Pendekar) continue;
                piece.GuardOnce = c == Commander.JakaTingkir;
                cells[p] = piece;
            }
        }

        public int CommanderCooldownLeft(Side side) => commanderCooldown[I(side)];
        internal void SetCommanderCooldown(Side side, int turns) => commanderCooldown[I(side)] = turns;

        /// <summary>A Sangkuriang blocker for the whole match, or a Perahu Semalam wall still standing.</summary>
        public bool IsBlocked(int p) => blocked[p] || wallTurns[p] > 0;
        public bool IsPermanentBlocker(int p) => blocked[p];
        public bool IsWall(int p) => wallTurns[p] > 0;
        public void SetBlocker(int p, bool on) => blocked[p] = on;
        internal void SetWall(int p, Side owner, int turns) { wallTurns[p] = turns; wallOwner[p] = I(owner); }

        /// <summary>Walls last a number of the opponent's turns: tick the walls the given side's opponent raised.</summary>
        internal void TickWalls(Side endedTurn)
        {
            for (int p = 0; p < wallTurns.Length; p++)
                if (wallTurns[p] > 0 && wallOwner[p] != I(endedTurn)) wallTurns[p]--;
        }

        /// <summary>Nothing stands here: no piece, no blocker, no wall.</summary>
        public bool IsFree(int p) => !cells[p].Exists && !IsBlocked(p);
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

        /// <summary>The (first) point the side's Dukun shields during the opponent's turn, or -1.</summary>
        public int Shielded(Side side) => shields[I(side), 0];
        public int[] Shields(Side side) => new[] { shields[I(side), 0], shields[I(side), 1] };
        internal void SetShielded(Side side, int p) { shields[I(side), 0] = p; shields[I(side), 1] = -1; }
        internal void SetShields(Side side, int a, int b) { shields[I(side), 0] = a; shields[I(side), 1] = b; }
        internal void DropShield(Side side, int p)
        {
            for (int k = 0; k < 2; k++) if (shields[I(side), k] == p) shields[I(side), k] = -1;
        }
        public bool IsShielded(int p)
        {
            if (!cells[p].Exists) return false;
            int s = I(cells[p].Side);
            return shields[s, 0] == p || shields[s, 1] == p;
        }

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
            var b = new Board { ToMove = ToMove, QuietMoves = QuietMoves, InBonusMove = InBonusMove, CastThisTurn = CastThisTurn, ForcedPiece = ForcedPiece, Ply = Ply };
            Array.Copy(cells, b.cells, cells.Length);
            Array.Copy(patihPlaced, b.patihPlaced, 2);
            Array.Copy(shields, b.shields, shields.Length);
            Array.Copy(commanders, b.commanders, 2);
            Array.Copy(commanderCooldown, b.commanderCooldown, 2);
            Array.Copy(blocked, b.blocked, blocked.Length);
            Array.Copy(wallTurns, b.wallTurns, wallTurns.Length);
            Array.Copy(wallOwner, b.wallOwner, wallOwner.Length);
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
