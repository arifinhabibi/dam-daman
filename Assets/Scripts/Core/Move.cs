using System.Collections.Generic;
using System.Linq;

namespace Pamor.Core
{
    /// <summary>
    /// One move: the points the piece visits (Path[0] is where it starts) and the pieces it captures,
    /// in order. A step has two path points and no captures; a capture chain has one hop per capture.
    /// </summary>
    public sealed class Move
    {
        public IReadOnlyList<int> Path { get; }
        public IReadOnlyList<int> Captured { get; }

        public Move(IReadOnlyList<int> path, IReadOnlyList<int> captured)
        {
            Path = path;
            Captured = captured;
        }

        public int From => Path[0];
        public int To => Path[Path.Count - 1];
        public bool IsCapture => Captured.Count > 0;

        public override string ToString() =>
            string.Join(IsCapture ? "x" : "-", Path.Select(BoardGraph.Name));
    }

    /// <summary>What applying a move did, beyond moving the piece: for the view and the log.</summary>
    public sealed class MoveResult
    {
        public Move Move;
        public Side Mover;
        /// <summary>The captured pieces as they were, in capture order (for showing who fell).</summary>
        public List<Piece> CapturedPieces = new List<Piece>();
        public bool Promoted;
        /// <summary>The moving piece's role became known to the opponent this move.</summary>
        public bool RevealedMover;
        /// <summary>The point of the piece burned for skipping a capture ("dibakar"), or -1.</summary>
        public int Burned = -1;
        public Piece BurnedPiece;
        /// <summary>Points of Dams that turned back into Prajurit this turn.</summary>
        public List<int> Demoted = new List<int>();
        /// <summary>The point a Dukun now shields for the opponent's next turn, or -1.</summary>
        public int Shield = -1;
        /// <summary>Sides whose Senopati fell this move (their Prajurit can't capture next turn).</summary>
        public List<Side> SenopatiFell = new List<Side>();
        /// <summary>The mover takes another move (combo).</summary>
        public bool BonusMove;
        /// <summary>Saipi Angin: the same piece moves once more this turn.</summary>
        public bool ExtraMove;
        /// <summary>Pancasona brought the Patih back on this point, or -1.</summary>
        public int PatihRevived = -1;
    }
}
