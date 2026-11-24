using System;
using System.Collections.Generic;

namespace Pamor.Core
{
    public enum Difficulty { Mudah, Sedang, Sulit }

    /// <summary>
    /// Computer opponent: negamax search with alpha-beta pruning over the prototype rules.
    /// Pure C# with no Unity calls, so it can run on a worker thread and in EditMode tests.
    /// </summary>
    public sealed class Ai
    {
        const int Win = 100000;
        const int ManValue = 100, DamValue = 320;

        public int Depth { get; }
        /// <summary>Chance (0..1) of playing a random legal move instead of the searched one.</summary>
        public double BlunderChance { get; }
        readonly Rules rules;
        readonly Random rng;

        /// <summary>Search score of the move last chosen (side to move's view); for tests and debugging.</summary>
        public int LastScore { get; private set; }

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
                case Difficulty.Sedang: return new Ai(4, 0.05, seed: seed);
                default: return new Ai(7, 0, seed: seed);
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
                child.Apply(m);
                // Window opened by one below alpha: a fail-hard search returns a bound, and a worse move
                // would otherwise come back exactly equal to alpha and be mistaken for a tie with the best.
                int score = -Search(child, Depth - 1, -beta, -(alpha - 1), 1);
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
            if (rules.IsDraw(board)) return 0;
            var moves = rules.LegalMoves(board);
            if (moves.Count == 0) return -Win + ply; // side to move lost; prefer faster wins / slower losses
            // Keep searching through forced captures so the horizon doesn't hide a recapture.
            if (depth <= 0 && !moves[0].IsCapture) return Evaluate(board);
            if (depth <= -6) return Evaluate(board);

            Order(board, moves);
            foreach (var m in moves)
            {
                var child = board.Clone();
                child.Apply(m);
                int score = -Search(child, depth - 1, -beta, -alpha, ply + 1);
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
            if (!Board.IsDam(board[m.From]) && m.To.Row == Board.PromotionRow(board.ToMove)) p += 5;
            return p;
        }

        // Positional weights on top of material (man 100, Dam 320). Tuned with AI-vs-AI matches:
        // a term that misleads the search makes deeper search play worse, so every term is checked
        // by making sure depth 4 still beats depth 2 from both seats.
        public static int AdvanceWeight = 5, HomeGuard = 8, CentreBonus = 6, EdgePenalty = 4;

        /// <summary>Static score from the point of view of the side to move.</summary>
        public static int Evaluate(Board board)
        {
            int score = 0;
            foreach (var s in Board.AllSquares())
            {
                int piece = board[s];
                if (piece == 0) continue;
                var owner = Board.Owner(piece).Value;
                int v;
                if (Board.IsDam(piece)) v = DamValue;
                else
                {
                    int advanced = owner == Side.Hitam ? s.Row : Board.Size - 1 - s.Row;
                    v = ManValue + advanced * AdvanceWeight;
                    if (advanced == 0) v += HomeGuard; // home-row guard against enemy promotion
                }
                if (s.Col >= 2 && s.Col <= 5 && s.Row >= 2 && s.Row <= 5) v += CentreBonus;
                if (s.Col == 0 || s.Col == Board.Size - 1) v -= EdgePenalty;
                score += owner == board.ToMove ? v : -v;
            }
            return score;
        }
    }
}
