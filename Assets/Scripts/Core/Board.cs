using System;
using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>The two sides. Hitam (dark) starts at the bottom (rows 0-2) and moves up; Merah moves down.</summary>
    public enum Side { Hitam = 1, Merah = -1 }

    /// <summary>A square on the 8x8 board. Row 0 is Hitam's home row, column 0 is the left edge.</summary>
    public readonly struct Square : IEquatable<Square>
    {
        public readonly int Row;
        public readonly int Col;
        public Square(int row, int col) { Row = row; Col = col; }
        public bool IsOnBoard => Row >= 0 && Row < Board.Size && Col >= 0 && Col < Board.Size;
        public bool IsDark => (Row + Col) % 2 == 0;
        public Square Offset(int dr, int dc) => new Square(Row + dr, Col + dc);
        public bool Equals(Square other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is Square s && Equals(s);
        public override int GetHashCode() => Row * 31 + Col;
        public static bool operator ==(Square a, Square b) => a.Equals(b);
        public static bool operator !=(Square a, Square b) => !a.Equals(b);
        public override string ToString() => $"{(char)('a' + Col)}{Row + 1}";
    }

    /// <summary>
    /// Board state. Each cell holds 0 (empty), +1 Hitam man, +2 Hitam Dam, -1 Merah man, -2 Merah Dam.
    /// </summary>
    public sealed class Board
    {
        public const int Size = 8;
        public const int Man = 1;
        public const int Dam = 2;

        readonly int[,] cells = new int[Size, Size];

        public Side ToMove { get; private set; } = Side.Hitam;

        public int this[Square s]
        {
            get => cells[s.Row, s.Col];
            set => cells[s.Row, s.Col] = value;
        }

        public static Board CreateStandard()
        {
            var b = new Board();
            for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                var s = new Square(r, c);
                if (!s.IsDark) continue;
                if (r < 3) b[s] = Man;
                else if (r > 4) b[s] = -Man;
            }
            return b;
        }

        /// <summary>An empty board with the given side to move; for tests and puzzles.</summary>
        public static Board CreateEmpty(Side toMove = Side.Hitam) => new Board { ToMove = toMove };

        public Board Clone()
        {
            var b = new Board { ToMove = ToMove };
            Array.Copy(cells, b.cells, cells.Length);
            return b;
        }

        public static Side? Owner(int piece) => piece > 0 ? Side.Hitam : piece < 0 ? Side.Merah : (Side?)null;
        public static bool IsDam(int piece) => Math.Abs(piece) == Dam;

        public int CountPieces(Side side)
        {
            int n = 0;
            foreach (var s in AllSquares())
                if (Owner(this[s]) == side) n++;
            return n;
        }

        public static IEnumerable<Square> AllSquares()
        {
            for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                yield return new Square(r, c);
        }

        /// <summary>The row a side's men promote on.</summary>
        public static int PromotionRow(Side side) => side == Side.Hitam ? Size - 1 : 0;

        /// <summary>Applies a legal move: moves the piece, removes captured pieces, promotes, passes the turn.</summary>
        public MoveResult Apply(Move move)
        {
            int piece = this[move.From];
            if (Owner(piece) != ToMove) throw new InvalidOperationException($"No {ToMove} piece on {move.From}.");

            this[move.From] = 0;
            foreach (var cap in move.Captured) this[cap] = 0;

            bool promoted = false;
            if (!IsDam(piece) && move.To.Row == PromotionRow(ToMove))
            {
                piece = Math.Sign(piece) * Dam;
                promoted = true;
            }
            this[move.To] = piece;

            ToMove = ToMove == Side.Hitam ? Side.Merah : Side.Hitam;
            return new MoveResult(move, promoted);
        }
    }

    public readonly struct MoveResult
    {
        public readonly Move Move;
        public readonly bool Promoted;
        public MoveResult(Move move, bool promoted) { Move = move; Promoted = promoted; }
    }
}
