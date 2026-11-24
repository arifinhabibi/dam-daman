using System.Collections.Generic;
using System.Linq;

namespace Pamor.Core
{
    /// <summary>
    /// One full turn: the squares the piece visits (Path[0] is where it starts) and the pieces it captures,
    /// in order. A plain step has two path squares and no captures; a capture chain has one hop per capture.
    /// </summary>
    public sealed class Move
    {
        public IReadOnlyList<Square> Path { get; }
        public IReadOnlyList<Square> Captured { get; }

        public Move(IReadOnlyList<Square> path, IReadOnlyList<Square> captured)
        {
            Path = path;
            Captured = captured;
        }

        public Square From => Path[0];
        public Square To => Path[Path.Count - 1];
        public bool IsCapture => Captured.Count > 0;

        public override string ToString() =>
            string.Join(IsCapture ? "x" : "-", Path.Select(p => p.ToString()));
    }
}
