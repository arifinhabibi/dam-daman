using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>
    /// Pamor base rules on the dam-daman line board (docs/DECISIONS.md, "Board rules"; docs/GDD.md, "Aturan dasar").
    /// - Men step one point along a line: forward, forward-diagonal or sideways, never backward.
    /// - Men capture by jumping an adjacent enemy along a line to the empty point beyond, in the same
    ///   directions. Capture chains are completed: the piece keeps jumping while it can.
    /// - Capturing is not forced, but skipping it is punished: if a capture was available and the move
    ///   wasn't one, the piece that could have captured is burned ("dibakar"). With several, the one
    ///   with the biggest capture burns.
    /// - A man that ends its move on the outer edge of the enemy triangle becomes a Dam, which flies any
    ///   distance along a line and captures that way. A Dam that goes DamIdleLimit of its owner's turns
    ///   without capturing turns back into a man.
    /// - Capturing ComboCaptures or more pieces in one move earns one bonus move.
    /// - A side with no pieces, or no legal move on its turn, loses.
    /// - Draw after DrawQuietMoveLimit turns with only Dams moving and nothing captured or burned.
    /// Not yet: Patih and roles, the passive-limit edge collapse.
    /// </summary>
    public sealed class Rules
    {
        public bool MenCaptureBackward = false;
        public bool FlyingDam = true;
        public int DamIdleLimit = 3;
        public int ComboCaptures = 3;
        public int DrawQuietMoveLimit = 50;

        public static readonly Rules Default = new Rules();

        /// <summary>Every legal move: steps and capture chains (captures aren't forced; see the burn rule).</summary>
        public List<Move> LegalMoves(Board board)
        {
            var moves = Captures(board);
            for (int p = 0; p < BoardGraph.PointCount; p++)
                if (Board.Owner(board[p]) == board.ToMove)
                    AddSteps(board, p, moves);
            return moves;
        }

        public List<Move> Captures(Board board)
        {
            var captures = new List<Move>();
            for (int p = 0; p < BoardGraph.PointCount; p++)
                if (Board.Owner(board[p]) == board.ToMove)
                    AddCaptures(board, p, captures);
            return captures;
        }

        public List<Move> LegalMovesFrom(Board board, int from)
        {
            var result = new List<Move>();
            foreach (var m in LegalMoves(board))
                if (m.From == from) result.Add(m);
            return result;
        }

        public bool HasAnyCapture(Board board) => Captures(board).Count > 0;

        public bool IsDraw(Board board) => board.QuietMoves >= DrawQuietMoveLimit;

        /// <summary>The winner, or null while the game goes on.</summary>
        public Side? Winner(Board board)
        {
            if (board.CountPieces(Side.Hitam) == 0) return Side.Merah;
            if (board.CountPieces(Side.Merah) == 0) return Side.Hitam;
            if (LegalMoves(board).Count == 0) return Board.Other(board.ToMove);
            return null;
        }

        /// <summary>Applies a legal move with every turn-end rule: burn, promotion, demotion, combo.</summary>
        public MoveResult Apply(Board board, Move move)
        {
            var mover = board.ToMove;
            int piece = board[move.From];
            if (Board.Owner(piece) != mover) throw new System.InvalidOperationException($"No {mover} piece on {BoardGraph.Name(move.From)}.");
            var result = new MoveResult { Move = move, Mover = mover };

            // Who could have captured, remembered before the move for the burn rule.
            Move bestSkipped = null;
            if (!move.IsCapture)
                foreach (var c in Captures(board))
                    if (bestSkipped == null || c.Captured.Count > bestSkipped.Captured.Count) bestSkipped = c;

            board.MovePiece(move.From, move.To);
            foreach (var cap in move.Captured) board[cap] = 0;

            if (!Board.IsDam(piece) && BoardGraph.Xs[move.To] == Board.PromotionX(mover))
            {
                board[move.To] = (int)mover * Board.Dam;
                result.Promoted = true;
            }

            if (bestSkipped != null)
            {
                int burned = bestSkipped.From == move.From ? move.To : bestSkipped.From;
                board[burned] = 0;
                result.Burned = burned;
            }

            // Dam idle counting: every Dam of the mover that didn't capture this turn ages by one turn.
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                if (Board.Owner(board[p]) != mover || !Board.IsDam(board[p])) continue;
                bool capturedNow = p == move.To && move.IsCapture;
                bool justPromoted = p == move.To && result.Promoted;
                if (capturedNow || justPromoted) { board.SetDamIdle(p, 0); continue; }
                int idle = board.DamIdle(p) + 1;
                if (idle >= DamIdleLimit)
                {
                    board[p] = (int)mover * Board.Man;
                    result.Demoted.Add(p);
                }
                else board.SetDamIdle(p, idle);
            }

            bool quiet = !move.IsCapture && result.Burned < 0 && Board.IsDam(piece);
            board.QuietMoves = quiet ? board.QuietMoves + 1 : 0;

            if (move.Captured.Count >= ComboCaptures && !board.InBonusMove)
            {
                board.InBonusMove = true;
                result.BonusMove = true;
            }
            else
            {
                board.InBonusMove = false;
                board.ToMove = Board.Other(mover);
            }
            return result;
        }

        bool ManMayGo(Side side, (int dx, int dy) dir) => dir.dx != -(int)side;

        void AddSteps(Board board, int from, List<Move> into)
        {
            int piece = board[from];
            var side = Board.Owner(piece).Value;
            bool dam = Board.IsDam(piece);
            var rays = BoardGraph.Rays[from];
            for (int r = 0; r < rays.Length; r++)
            {
                if (!dam && !ManMayGo(side, BoardGraph.RayDirs[from][r])) continue;
                foreach (var to in rays[r])
                {
                    if (board[to] != 0) break;
                    into.Add(new Move(new[] { from, to }, new int[0]));
                    if (!(dam && FlyingDam)) break;
                }
            }
        }

        void AddCaptures(Board board, int from, List<Move> into)
        {
            var path = new List<int> { from };
            var captured = new List<int>();
            CaptureSearch(board, board[from], from, from, path, captured, into);
        }

        void CaptureSearch(Board board, int piece, int origin, int at, List<int> path, List<int> captured, List<Move> into)
        {
            var side = Board.Owner(piece).Value;
            bool dam = Board.IsDam(piece);
            bool flying = dam && FlyingDam;
            bool extended = false;
            var rays = BoardGraph.Rays[at];

            for (int r = 0; r < rays.Length; r++)
            {
                if (!dam && !MenCaptureBackward && !ManMayGo(side, BoardGraph.RayDirs[at][r])) continue;
                var ray = rays[r];

                // First occupied point along the ray (adjacent only, unless flying).
                int i = 0;
                if (flying) while (i < ray.Length && IsEmpty(board, ray[i], origin)) i++;
                if (i >= ray.Length) continue;
                int target = ray[i];
                if (IsEmpty(board, target, origin)) continue;
                if (Board.Owner(board[target]) == side || captured.Contains(target)) continue;

                for (int j = i + 1; j < ray.Length && IsEmpty(board, ray[j], origin); j++)
                {
                    extended = true;
                    path.Add(ray[j]);
                    captured.Add(target);
                    CaptureSearch(board, piece, origin, ray[j], path, captured, into);
                    path.RemoveAt(path.Count - 1);
                    captured.RemoveAt(captured.Count - 1);
                    if (!flying) break;
                }
            }

            if (!extended && captured.Count > 0)
                into.Add(new Move(path.ToArray(), captured.ToArray()));
        }

        // The moving piece has left its start point, so that point counts as empty during the chain.
        static bool IsEmpty(Board board, int p, int origin) => p == origin || board[p] == 0;
    }
}
