using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>
    /// Pamor rules on the dam-daman line board (docs/DECISIONS.md; docs/GDD.md "Aturan dasar" and "Role bidak").
    /// Movement by role (along lines; "backward" = toward the piece's own side):
    /// - Prajurit, Telik Sandi, Dukun: 1 point forward, forward-diagonal or sideways.
    /// - Senopati: up to 2 points in those directions (the path must be clear).
    /// - Pendekar, Patih: 1 point in any direction.  Dam (promoted Prajurit): any distance, any direction.
    /// Capture: jump an adjacent enemy along a line to the empty point beyond, in the piece's directions
    /// (a Dam may jump from a distance). Chains are completed and go straight; only Pendekar and Dam may
    /// turn between jumps. Dukun never captures. A piece the enemy Dukun shields can't be captured.
    /// Skipping an available capture burns the piece that could have captured ("dibakar").
    /// Prajurit promote to Dam on the enemy triangle tip; a Dam idle for DamIdleLimit own turns turns back.
    /// When a Senopati falls, its side's Prajurit can't capture on their next turn.
    /// A Dukun that moves shields its most important neighbour during the opponent's next turn.
    /// Capturing ComboCaptures or more earns one bonus move.
    /// Losing the Patih loses the game; so does having no pieces or no legal move. Draw after
    /// DrawQuietMoveLimit turns in which only backward-capable pieces (Dam, Pendekar, Patih) moved and
    /// nothing was captured or burned.
    /// </summary>
    public sealed class Rules
    {
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
                if (board.Owner(p) == board.ToMove)
                    AddSteps(board, p, moves);
            return moves;
        }

        public List<Move> Captures(Board board)
        {
            var captures = new List<Move>();
            for (int p = 0; p < BoardGraph.PointCount; p++)
                if (board.Owner(p) == board.ToMove && CanCaptureNow(board, board[p]))
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
            foreach (var side in new[] { Side.Hitam, Side.Merah })
            {
                if (board.CountPieces(side) == 0) return Board.Other(side);
                if (board.HasPatihRule(side) && board.Find(side, Role.Patih) < 0) return Board.Other(side);
            }
            if (LegalMoves(board).Count == 0) return Board.Other(board.ToMove);
            return null;
        }

        static bool CanCaptureNow(Board board, Piece p) =>
            RoleRules.CanCapture(p) && !(p.Role == Role.Prajurit && !p.IsDam && board.PrajuritCaptureBlocked(p.Side));

        static bool IsBackward(Side side, (int dx, int dy) dir) => dir.dx == -(int)side;

        /// <summary>Applies a legal move with every turn-end rule: reveal, promotion, burn, demotion, shield, combo.</summary>
        public MoveResult Apply(Board board, Move move)
        {
            var mover = board.ToMove;
            var piece = board[move.From];
            if (!piece.Exists || piece.Side != mover) throw new System.InvalidOperationException($"No {mover} piece on {BoardGraph.Name(move.From)}.");
            var result = new MoveResult { Move = move, Mover = mover };

            // Who could have captured, remembered before the move for the burn rule.
            Move bestSkipped = null;
            if (!move.IsCapture)
                foreach (var c in Captures(board))
                    if (bestSkipped == null || c.Captured.Count > bestSkipped.Captured.Count) bestSkipped = c;

            board.MovePiece(move.From, move.To);
            foreach (var cap in move.Captured)
            {
                var victim = board[cap];
                result.CapturedPieces.Add(victim);
                if (victim.Role == Role.Senopati && !victim.IsDam) result.SenopatiFell.Add(victim.Side);
                board.Clear(cap);
            }

            // Reveal: any capture, or a move only this role could make.
            var moved = board[move.To];
            if (!moved.Revealed && (move.IsCapture || RevealsRole(moved, move)))
            {
                moved.Revealed = true;
                result.RevealedMover = true;
            }
            if (RoleRules.Promotes(moved) && BoardGraph.Xs[move.To] == Board.PromotionX(mover))
            {
                moved.IsDam = true;
                moved.DamIdle = 0;
                moved.Revealed = true;
                result.Promoted = true;
            }
            board.Set(move.To, moved);

            if (bestSkipped != null)
            {
                int burned = bestSkipped.From == move.From ? move.To : bestSkipped.From;
                result.BurnedPiece = board[burned];
                if (result.BurnedPiece.Role == Role.Senopati && !result.BurnedPiece.IsDam) result.SenopatiFell.Add(mover);
                board.Clear(burned);
                result.Burned = burned;
            }

            // Dam idle counting: every Dam of the mover that didn't capture this turn ages by one turn.
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                var d = board[p];
                if (!d.Exists || d.Side != mover || !d.IsDam) continue;
                if (p == move.To && (move.IsCapture || result.Promoted)) { d.DamIdle = 0; board.Set(p, d); continue; }
                d.DamIdle++;
                if (d.DamIdle >= DamIdleLimit) { d.IsDam = false; d.DamIdle = 0; result.Demoted.Add(p); }
                board.Set(p, d);
            }

            // Only pieces that can go backward (Dam, Pendekar, Patih) can shuffle forever; a forward-only
            // move is progress and resets the draw count, as does any capture or burn.
            bool quiet = !move.IsCapture && result.Burned < 0 && RoleRules.MovesBackward(piece);
            board.QuietMoves = quiet ? board.QuietMoves + 1 : 0;

            // Dukun shield: a Dukun that just moved shields its most important neighbour for the opponent's turn.
            if (board[move.To].Exists && board[move.To].Role == Role.Dukun && !board[move.To].IsDam)
            {
                int best = -1, bestRank = -1;
                foreach (var ray in BoardGraph.Rays[move.To])
                {
                    int n = ray[0];
                    if (board.Owner(n) != mover) continue;
                    int rank = RoleRules.Importance(board[n]);
                    if (rank > bestRank) { bestRank = rank; best = n; }
                }
                if (best >= 0)
                {
                    board.SetShielded(mover, best);
                    result.Shield = best;
                    var dukun = board[move.To];
                    if (!dukun.Revealed) { dukun.Revealed = true; board.Set(move.To, dukun); result.RevealedMover = true; }
                }
            }
            // A shield follows its piece only while it stays put: if the shielded piece moved, it's gone.
            if (board.Shielded(mover) == move.From) board.SetShielded(mover, -1);

            bool bonus = move.Captured.Count >= ComboCaptures && !board.InBonusMove;
            if (!bonus)
            {
                // The turn ends: this side's capture block was for this turn, and the opponent's shield
                // (which covered this turn) expires.
                board.SetPrajuritCaptureBlocked(mover, false);
                board.SetShielded(Board.Other(mover), -1);
            }
            foreach (var side in result.SenopatiFell) board.SetPrajuritCaptureBlocked(side, true);

            if (bonus)
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

        static bool RevealsRole(Piece p, Move move)
        {
            if (p.IsDam) return false;
            var dir = DirOf(move.From, move.Path[1]);
            if ((p.Role == Role.Pendekar || p.Role == Role.Patih) && IsBackward(p.Side, dir)) return true;
            if (p.Role == Role.Senopati && !BoardGraph.AreAdjacent(move.From, move.To)) return true;
            return false;
        }

        static (int, int) DirOf(int a, int b) =>
            (System.Math.Sign(BoardGraph.Xs[b] - BoardGraph.Xs[a]), System.Math.Sign(BoardGraph.Ys[b] - BoardGraph.Ys[a]));

        void AddSteps(Board board, int from, List<Move> into)
        {
            var piece = board[from];
            int range = FlyingDam || !piece.IsDam ? RoleRules.StepRange(piece) : 1;
            var rays = BoardGraph.Rays[from];
            for (int r = 0; r < rays.Length; r++)
            {
                if (!RoleRules.MovesBackward(piece) && IsBackward(piece.Side, BoardGraph.RayDirs[from][r])) continue;
                var ray = rays[r];
                for (int k = 0; k < ray.Length && k < range; k++)
                {
                    if (!board.IsEmpty(ray[k])) break;
                    into.Add(new Move(new[] { from, ray[k] }, new int[0]));
                }
            }
        }

        void AddCaptures(Board board, int from, List<Move> into)
        {
            var path = new List<int> { from };
            var captured = new List<int>();
            CaptureSearch(board, board[from], from, from, (0, 0), path, captured, into);
        }

        void CaptureSearch(Board board, Piece piece, int origin, int at, (int, int) lastDir,
                           List<int> path, List<int> captured, List<Move> into)
        {
            bool flying = piece.IsDam && FlyingDam;
            bool extended = false;
            var rays = BoardGraph.Rays[at];

            for (int r = 0; r < rays.Length; r++)
            {
                var dir = BoardGraph.RayDirs[at][r];
                if (!RoleRules.CapturesBackward(piece) && IsBackward(piece.Side, dir)) continue;
                if (captured.Count > 0 && !RoleRules.ChainMayTurn(piece) && !dir.Equals(lastDir)) continue;
                var ray = rays[r];

                // First occupied point along the ray (adjacent only, unless flying).
                int i = 0;
                if (flying) while (i < ray.Length && IsEmpty(board, ray[i], origin)) i++;
                if (i >= ray.Length) continue;
                int target = ray[i];
                if (IsEmpty(board, target, origin)) continue;
                if (board.Owner(target) == piece.Side || captured.Contains(target) || board.IsShielded(target)) continue;

                for (int j = i + 1; j < ray.Length && IsEmpty(board, ray[j], origin); j++)
                {
                    extended = true;
                    path.Add(ray[j]);
                    captured.Add(target);
                    CaptureSearch(board, piece, origin, ray[j], dir, path, captured, into);
                    path.RemoveAt(path.Count - 1);
                    captured.RemoveAt(captured.Count - 1);
                    if (!flying) break;
                }
            }

            if (!extended && captured.Count > 0)
                into.Add(new Move(path.ToArray(), captured.ToArray()));
        }

        // The moving piece has left its start point, so that point counts as empty during the chain.
        static bool IsEmpty(Board board, int p, int origin) => p == origin || board.IsEmpty(p);
    }
}
