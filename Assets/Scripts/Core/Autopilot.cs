using System;
using System.Collections.Generic;
using System.Linq;

namespace Pamor.Core
{
    /// <summary>
    /// The deliberately weak stand-in for a player who ran out of time (GDD "Penanganan AFK dan Autopilot"):
    /// take a capture if there is one (the biggest), otherwise a random move that doesn't hand the
    /// opponent a capture on the moved piece, otherwise any random move.
    /// </summary>
    public static class Autopilot
    {
        public static Move ChooseMove(Board board, Rules rules, Random rng)
        {
            var captures = rules.Captures(board);
            if (captures.Count > 0) return captures.OrderByDescending(m => m.Captured.Count).First();

            var moves = rules.LegalMoves(board);
            if (moves.Count == 0) return null;
            var safe = new List<Move>();
            foreach (var m in moves)
            {
                var b = board.Clone();
                rules.Apply(b, m);
                if (b.ToMove == board.ToMove || !rules.Captures(b).Any(c => c.Captured.Contains(m.To))) safe.Add(m);
            }
            var pool = safe.Count > 0 ? safe : moves;
            return pool[rng.Next(pool.Count)];
        }
    }
}
