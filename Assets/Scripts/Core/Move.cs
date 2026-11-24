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
        public bool Promoted;
        /// <summary>The point of the piece burned for skipping a capture ("dibakar"), or -1.</summary>
        public int Burned = -1;
        /// <summary>Points of Dams that turned back into men this turn.</summary>
        public List<int> Demoted = new List<int>();
        /// <summary>The mover takes another move (combo).</summary>
        public bool BonusMove;
    }
}
