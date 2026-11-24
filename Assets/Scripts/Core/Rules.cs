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
                if (MayAct(board, p))
                    AddSteps(board, p, moves);
            return moves;
        }

        public List<Move> Captures(Board board)
        {
            var captures = new List<Move>();
            for (int p = 0; p < BoardGraph.PointCount; p++)
                if (MayAct(board, p) && CanCaptureNow(board, board[p]))
                    AddCaptures(board, p, captures);
            return captures;
        }

        /// <summary>The piece belongs to the side to move, isn't held by Santet, and is the Saipi Angin piece if one is forced.</summary>
        static bool MayAct(Board board, int p) =>
            board.Owner(p) == board.ToMove && board[p].FrozenTurns == 0 && (board.ForcedPiece < 0 || board.ForcedPiece == p);

        // ---------- ajian

        /// <summary>Why the piece on this point can't cast its ajian right now, or null if it can.</summary>
        public string CastBlocker(Board board, int caster)
        {
            var piece = board[caster];
            if (!piece.Exists || piece.Side != board.ToMove) return "Bukan bidakmu.";
            if (piece.IsDam) return "Dam tidak punya ajian.";
            if (board.CastThisTurn) return "Sudah memakai ajian giliran ini.";
            if (board.ForcedPiece >= 0) return "Tidak bisa saat Saipi Angin.";
            if (piece.FrozenTurns > 0) return "Bidak ini terkena Santet.";
            var a = AjianBook.For(piece.Role);
            if (AjianBook.IsReaction(a)) return board.PancasonaUsed(piece.Side) ? "Pancasona sudah dipakai." : "Pancasona dipakai saat Patih akan gugur: kamu akan ditanya.";
            if (board.CooldownLeft(piece.Side, a) > 0) return $"Jeda {board.CooldownLeft(piece.Side, a)} giliran lagi.";
            if (board.Tenaga(piece.Side) < AjianBook.Cost(a)) return $"Tenaga Dalam kurang ({board.Tenaga(piece.Side)}/{AjianBook.Cost(a)}).";
            if (CastTargets(board, caster).Count == 0) return "Tidak ada target.";
            return null;
        }

        /// <summary>Valid targets for the caster's ajian: the caster itself, or enemy pieces in range for Santet.</summary>
        public List<int> CastTargets(Board board, int caster)
        {
            var result = new List<int>();
            var piece = board[caster];
            if (!piece.Exists) return result;
            var a = AjianBook.For(piece.Role);
            if (!AjianBook.NeedsTarget(a)) { result.Add(caster); return result; }
            foreach (var p in WithinSteps(caster, AjianBook.SantetRange))
            {
                var t = board[p];
                if (t.Exists && t.Side != piece.Side && t.HiddenTurns == 0) result.Add(p);
            }
            return result;
        }

        static IEnumerable<int> WithinSteps(int from, int steps)
        {
            var dist = new Dictionary<int, int> { { from, 0 } };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int p = queue.Dequeue();
                if (dist[p] == steps) continue;
                foreach (var ray in BoardGraph.Rays[p])
                    if (!dist.ContainsKey(ray[0])) { dist[ray[0]] = dist[p] + 1; queue.Enqueue(ray[0]); }
            }
            foreach (var kv in dist) if (kv.Key != from) yield return kv.Key;
        }

        /// <summary>Casts the caster's ajian on the target (itself for every ajian but Santet). Doesn't use up the move.</summary>
        public CastResult Cast(Board board, int caster, int target)
        {
            var blocker = CastBlocker(board, caster);
            if (blocker != null) throw new System.InvalidOperationException(blocker);
            if (!CastTargets(board, caster).Contains(target)) throw new System.InvalidOperationException("Target tidak sah.");

            var piece = board[caster];
            var side = piece.Side;
            var a = AjianBook.For(piece.Role);
            var result = new CastResult { Ajian = a, Caster = side, CasterPoint = caster, Target = target };

            switch (a)
            {
                case Ajian.TamengWaja:
                    piece.ImmuneTurns = 1; piece.Revealed = true; board.Set(caster, piece);
                    break;
                case Ajian.SaipiAngin:
                    piece.ExtraMoves = 1; piece.Revealed = true; board.Set(caster, piece);
                    break;
                case Ajian.Panglimunan:
                    piece.HiddenTurns = 2; board.Set(caster, piece);
                    break;
                case Ajian.Santet:
                    var victim = board[target]; victim.FrozenTurns = 2; board.Set(target, victim);
                    piece.Revealed = true; board.Set(caster, piece);
                    break;
                case Ajian.GelarSupitUrang:
                    piece.Revealed = true; board.Set(caster, piece);
                    SupitUrang(board, caster, side, result);
                    break;
            }

            board.SetTenaga(side, board.Tenaga(side) - AjianBook.Cost(a));
            // +1: the casting turn's own end ticks once, so N full turns are skipped afterwards.
            board.SetCooldown(side, a, AjianBook.CooldownTurns(a) + 1);
            board.CastThisTurn = true;
            return result;
        }

        // ---------- Pancasona (reaction)

        /// <summary>When true, Apply uses an affordable Pancasona automatically (for AI search); otherwise the game asks.</summary>
        public bool AutoPancasona;

        /// <summary>This move would kill the given side's Patih (by capture, or by burning it).</summary>
        public bool KillsPatih(Board board, Move move, Side patihSide)
        {
            foreach (var c in move.Captured) if (board[c].Role == Role.Patih && board[c].Side == patihSide && !board[c].IsDam) return true;
            if (move.IsCapture || board.ToMove != patihSide) return false;
            var clone = board.Clone();
            var result = ApplyRaw(clone, move);
            return result.Burned >= 0 && result.BurnedPiece.Role == Role.Patih;
        }

        public bool CanUsePancasona(Board board, Side side) =>
            board.HasPatihRule(side) && !board.PancasonaUsed(side) && board.Tenaga(side) >= AjianBook.Cost(Ajian.Pancasona);

        /// <summary>Spends Tenaga Dalam to arm Pancasona just before a move resolves; the Patih revives if it dies in that move.</summary>
        public void UsePancasona(Board board, Side side)
        {
            if (!CanUsePancasona(board, side)) throw new System.InvalidOperationException("Pancasona tidak bisa dipakai.");
            board.SetTenaga(side, board.Tenaga(side) - AjianBook.Cost(Ajian.Pancasona));
            board.ArmPancasona(side);
        }

        /// <summary>Every allied Prajurit within range of the Senopati steps straight forward, most advanced first.</summary>
        static void SupitUrang(Board board, int senopati, Side side, CastResult result)
        {
            var allies = new List<int>();
            foreach (var p in WithinSteps(senopati, AjianBook.SupitUrangRange))
            {
                var n = board[p];
                if (n.Exists && n.Side == side && n.Role == Role.Prajurit && !n.IsDam && n.FrozenTurns == 0) allies.Add(p);
            }
            allies.Sort((a, b) => ((int)side * BoardGraph.Xs[b]).CompareTo((int)side * BoardGraph.Xs[a]));
            foreach (var p in allies)
            {
                var rays = BoardGraph.Rays[p];
                for (int r = 0; r < rays.Length; r++)
                {
                    if (BoardGraph.RayDirs[p][r] != ((int)side, 0)) continue;
                    int to = rays[r][0];
                    if (!board.IsEmpty(to)) break;
                    board.MovePiece(p, to);
                    result.Shifts.Add((p, to));
                    if (BoardGraph.Xs[to] == Board.PromotionX(side))
                    {
                        var d = board[to]; d.IsDam = true; d.Revealed = true; board.Set(to, d);
                        result.Promoted.Add(to);
                    }
                    break;
                }
            }
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

        /// <summary>
        /// Applies a legal move with every turn-end rule: reveal, promotion, burn, demotion, shield, Tenaga,
        /// Pancasona, Saipi Angin, combo. With AutoPancasona, an affordable Pancasona is used when the move
        /// would kill a Patih; otherwise the caller asks the player first and calls UsePancasona.
        /// </summary>
        public MoveResult Apply(Board board, Move move)
        {
            if (AutoPancasona)
                foreach (var side in new[] { Side.Hitam, Side.Merah })
                    if (CanUsePancasona(board, side) && KillsPatih(board, move, side)) UsePancasona(board, side);
            return ApplyRaw(board, move);
        }

        MoveResult ApplyRaw(Board board, Move move)
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

            // Tenaga Dalam: +2 per captured piece.
            if (move.IsCapture) board.SetTenaga(mover, board.Tenaga(mover) + AjianBook.TenagaPerCapture * move.Captured.Count);

            // Pancasona: a Patih that died in this move while armed comes back at its start point.
            foreach (var side in new[] { Side.Hitam, Side.Merah })
            {
                if (!board.PancasonaArmed(side) || board.Find(side, Role.Patih) >= 0) continue;
                int spot = board.IsEmpty(board.PatihStart(side)) ? board.PatihStart(side) : -1;
                if (spot < 0) foreach (var p in Formation.StartPoints(side)) if (board.IsEmpty(p)) { spot = p; break; }
                board.DisarmPancasona(side);
                if (spot < 0) continue;
                var patih = Piece.Of(side, Role.Patih);
                patih.Revealed = true;
                board.Set(spot, patih);
                result.PatihRevived = spot;
            }
            foreach (var side in new[] { Side.Hitam, Side.Merah }) board.DisarmPancasona(side);

            bool bonus = move.Captured.Count >= ComboCaptures && !board.InBonusMove;

            // Saipi Angin: the Pendekar that cast it moves once more this turn, if it still can.
            var after = board[move.To];
            if (!bonus && after.Exists && after.Side == mover && after.ExtraMoves > 0)
            {
                after.ExtraMoves--;
                board.Set(move.To, after);
                board.ForcedPiece = move.To;
                if (LegalMoves(board).Count > 0) { result.ExtraMove = true; }
                else board.ForcedPiece = -1;
            }
            if (result.ExtraMove) return result;
            board.ForcedPiece = -1;

            if (!bonus) EndTurn(board, mover);
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

        /// <summary>
        /// The mover's turn is over: capture block and the opponent's shield expire, Tenaga Dalam grows,
        /// cooldowns tick, and status timers run (protection counts opponent turns, Santet own turns).
        /// </summary>
        static void EndTurn(Board board, Side mover)
        {
            board.SetPrajuritCaptureBlocked(mover, false);
            board.SetShielded(Board.Other(mover), -1);
            board.SetTenaga(mover, board.Tenaga(mover) + AjianBook.TenagaPerTurn);
            board.TickCooldowns(mover);
            board.CastThisTurn = false;
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                var piece = board[p];
                if (!piece.Exists) continue;
                if (piece.Side == mover)
                {
                    if (piece.FrozenTurns > 0) piece.FrozenTurns--;
                    piece.ExtraMoves = 0;
                }
                else
                {
                    if (piece.ImmuneTurns > 0) piece.ImmuneTurns--;
                    if (piece.HiddenTurns > 0) piece.HiddenTurns--;
                }
                board.Set(p, piece);
            }
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
                if (board.Owner(target) == piece.Side || captured.Contains(target) || board.IsShielded(target) || board[target].Untouchable) continue;

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
