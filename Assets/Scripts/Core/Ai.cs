using System;
using System.Collections.Generic;

namespace Pamor.Core
{
    public enum Difficulty { Mudah, Sedang, Sulit }

    /// <summary>
    /// Computer opponent: negamax with alpha-beta pruning over <see cref="Rules"/>, with a capture
    /// extension at the horizon. Combo bonus moves keep the same side to move, so those children are
    /// searched without flipping the score. Pure C#: runs on a worker thread and in EditMode tests.
    /// Known gap: the AI sees every role, including unrevealed enemy ones.
    /// </summary>
    public sealed class Ai
    {
        const int Win = 100000;
        const int ManValue = 100, DamValue = 300;

        // Positional weights on top of material. Every term must keep deeper search beating shallower
        // search from both seats (see AiTests); a misleading term makes deeper search play worse.
        public static int AdvanceWeight = 4, CentreBonus = 5, DamIdlePenalty = 15;

        public int Depth { get; }
        /// <summary>Chance (0..1) of playing a random legal move instead of the searched one.</summary>
        public double BlunderChance { get; }
        /// <summary>Search score of the move last chosen (side to move's view); for tests and debugging.</summary>
        public int LastScore { get; private set; }

        readonly Rules rules;
        readonly Random rng;

        public Ai(int depth, double blunderChance = 0, Rules rules = null, int? seed = null)
        {
            Depth = Math.Max(1, depth);
            BlunderChance = blunderChance;
            this.rules = rules ?? Rules.Default;
            rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public static Ai For(Difficulty difficulty, int? seed = null)
        {
            switch (difficulty)
            {
                case Difficulty.Mudah: return new Ai(1, 0.35, seed: seed);
                case Difficulty.Sedang: return new Ai(3, 0.05, seed: seed);
                default: return new Ai(5, 0, seed: seed);
            }
        }

        /// <summary>Picks a move for the side to move, or null when it has none.</summary>
        public Move ChooseMove(Board board)
        {
            var moves = rules.LegalMoves(board);
            if (moves.Count == 0) return null;
            if (moves.Count == 1) return moves[0];
            if (BlunderChance > 0 && rng.NextDouble() < BlunderChance) return moves[rng.Next(moves.Count)];

            Order(board, moves);
            int alpha = -Win * 2, beta = Win * 2;
            int best = int.MinValue;
            var bestMoves = new List<Move>();
            foreach (var m in moves)
            {
                var child = board.Clone();
                rules.Apply(child, m);
                // Window opened one below alpha: a fail-hard bound must never pass for a tie with the best.
                int score = child.ToMove == board.ToMove
                    ? Search(child, Depth - 1, alpha - 1, beta, 1)
                    : -Search(child, Depth - 1, -beta, -(alpha - 1), 1);
                if (score > best) { best = score; bestMoves.Clear(); bestMoves.Add(m); }
                else if (score == best) bestMoves.Add(m);
                if (score > alpha) alpha = score;
            }
            LastScore = best;
            // Equal moves are picked at random so the AI doesn't play the same game every time.
            return bestMoves[rng.Next(bestMoves.Count)];
        }

        int Search(Board board, int depth, int alpha, int beta, int ply)
        {
            var winner = rules.Winner(board);
            if (winner.HasValue) return winner == board.ToMove ? Win - ply : -Win + ply;
            if (rules.IsDraw(board)) return 0;

            List<Move> moves;
            if (depth <= 0)
            {
                // Horizon: keep resolving captures so a pending recapture isn't hidden; otherwise stand pat.
                moves = rules.Captures(board);
                if (moves.Count == 0 || depth <= -6) return Evaluate(board);
            }
            else moves = rules.LegalMoves(board);

            Order(board, moves);
            foreach (var m in moves)
            {
                var child = board.Clone();
                rules.Apply(child, m);
                int score = child.ToMove == board.ToMove
                    ? Search(child, depth - 1, alpha, beta, ply + 1)
                    : -Search(child, depth - 1, -beta, -alpha, ply + 1);
                if (score >= beta) return score;
                if (score > alpha) alpha = score;
            }
            return alpha;
        }

        static void Order(Board board, List<Move> moves)
        {
            // Bigger captures first, then promotions: better pruning.
            moves.Sort((a, b) => Priority(board, b).CompareTo(Priority(board, a)));
        }

        static int Priority(Board board, Move m)
        {
            int p = m.Captured.Count * 10;
            foreach (var c in m.Captured) if (board[c].Role == Role.Patih) p += 100;
            if (RoleRules.Promotes(board[m.From]) && BoardGraph.Xs[m.To] == Board.PromotionX(board.ToMove)) p += 5;
            return p;
        }

        public static int ValueOf(Piece piece)
        {
            if (piece.IsDam) return DamValue;
            switch (piece.Role)
            {
                case Role.Patih: return PatihValue;
                case Role.Senopati: return 180;
                case Role.Pendekar: return 160;
                case Role.Dukun: return 120;
                case Role.TelikSandi: return 110;
                default: return ManValue;
            }
        }

        const int PatihValue = 2000;

        /// <summary>Static score from the point of view of the side to move.</summary>
        public static int Evaluate(Board board)
        {
            int score = 0;
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                var piece = board[p];
                if (!piece.Exists) continue;
                int x = BoardGraph.Xs[p], y = BoardGraph.Ys[p];
                int v = ValueOf(piece);
                if (piece.IsDam) v -= piece.DamIdle * DamIdlePenalty;
                else if (piece.Role != Role.Patih)
                {
                    int advanced = piece.Side == Side.Hitam ? x : BoardGraph.Width - 1 - x;
                    v += advanced * AdvanceWeight;
                }
                if (piece.Role != Role.Patih && x >= 3 && x <= 5 && y >= 1 && y <= 3) v += CentreBonus;
                score += piece.Side == board.ToMove ? v : -v;
            }
            return score;
        }
    }
}
