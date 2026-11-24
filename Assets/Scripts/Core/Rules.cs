using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>
    /// Prototype dam-daman rules.
    /// - Pieces move diagonally on dark squares. Men step one square forward.
    /// - Capturing is mandatory: if any capture exists, only captures are legal.
    /// - A capture chain must be completed: the piece keeps jumping while it can.
    /// - Men capture forward and backward (MenCaptureBackward).
    /// - A Dam is a "flying" piece (FlyingDam): it moves and captures along a diagonal at any distance.
    /// - Captured pieces are removed when the turn ends and can't be jumped twice in one chain.
    /// - A man promotes to Dam only when its move ends on the far row.
    /// - A side with no legal move (including no pieces) loses.
    /// </summary>
    public sealed class Rules
    {
        public bool MenCaptureBackward = true;
        public bool FlyingDam = true;

        static readonly (int dr, int dc)[] Diagonals = { (1, 1), (1, -1), (-1, 1), (-1, -1) };

        public static readonly Rules Default = new Rules();

        public List<Move> LegalMoves(Board board)
        {
            var captures = new List<Move>();
            foreach (var s in Board.AllSquares())
                if (Board.Owner(board[s]) == board.ToMove)
                    AddCaptures(board, s, captures);
            if (captures.Count > 0) return captures;

            var steps = new List<Move>();
            foreach (var s in Board.AllSquares())
                if (Board.Owner(board[s]) == board.ToMove)
                    AddSteps(board, s, steps);
            return steps;
        }

        public List<Move> LegalMovesFrom(Board board, Square from)
        {
            var result = new List<Move>();
            foreach (var m in LegalMoves(board))
                if (m.From == from) result.Add(m);
            return result;
        }

        public bool HasAnyCapture(Board board)
        {
            var moves = LegalMoves(board);
            return moves.Count > 0 && moves[0].IsCapture;
        }

        /// <summary>The winner, or null while the side to move still has a legal move.</summary>
        public Side? Winner(Board board)
        {
            if (LegalMoves(board).Count > 0) return null;
            return board.ToMove == Side.Hitam ? Side.Merah : Side.Hitam;
        }

        void AddSteps(Board board, Square from, List<Move> into)
        {
            int piece = board[from];
            int forward = (int)Board.Owner(piece).Value;
            bool dam = Board.IsDam(piece);

            foreach (var (dr, dc) in Diagonals)
            {
                if (!dam && dr != forward) continue;
                var to = from.Offset(dr, dc);
                while (to.IsOnBoard && board[to] == 0)
                {
                    into.Add(new Move(new[] { from, to }, new Square[0]));
                    if (!(dam && FlyingDam)) break;
                    to = to.Offset(dr, dc);
                }
            }
        }

        void AddCaptures(Board board, Square from, List<Move> into)
        {
            int piece = board[from];
            var path = new List<Square> { from };
            var captured = new List<Square>();
            // The moving piece leaves its start square, so that square counts as empty during the chain.
            CaptureSearch(board, piece, from, from, path, captured, into);
        }

        void CaptureSearch(Board board, int piece, Square origin, Square at,
                           List<Square> path, List<Square> captured, List<Move> into)
        {
            var side = Board.Owner(piece).Value;
            int forward = (int)side;
            bool dam = Board.IsDam(piece);
            bool extended = false;

            foreach (var (dr, dc) in Diagonals)
            {
                if (!dam && !MenCaptureBackward && dr != forward) continue;

                // Walk to the first occupied square in this direction (only one step for a man / short Dam).
                var probe = at.Offset(dr, dc);
                if (dam && FlyingDam)
                    while (probe.IsOnBoard && IsEmpty(board, probe, origin)) probe = probe.Offset(dr, dc);
                if (!probe.IsOnBoard) continue;

                int target = board[probe];
                if (probe == origin || target == 0) continue;
                if (Board.Owner(target) == side) continue;
                if (captured.Contains(probe)) continue; // already jumped: it blocks

                // Landing squares beyond the captured piece.
                var land = probe.Offset(dr, dc);
                while (land.IsOnBoard && IsEmpty(board, land, origin))
                {
                    extended = true;
                    path.Add(land);
                    captured.Add(probe);
                    CaptureSearch(board, piece, origin, land, path, captured, into);
                    path.RemoveAt(path.Count - 1);
                    captured.RemoveAt(captured.Count - 1);
                    if (!(dam && FlyingDam)) break;
                    land = land.Offset(dr, dc);
                }
            }

            // A chain ends only when no further capture is possible.
            if (!extended && captured.Count > 0)
                into.Add(new Move(path.ToArray(), captured.ToArray()));
        }

        static bool IsEmpty(Board board, Square s, Square origin) => s == origin || board[s] == 0;
    }
}
