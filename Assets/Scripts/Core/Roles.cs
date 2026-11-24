using System;
using System.Collections.Generic;
using System.Linq;

namespace Pamor.Core
{
    /// <summary>The six roles from the GDD. A Dam is a promoted Prajurit, not a role of its own.</summary>
    public enum Role { Prajurit, Pendekar, TelikSandi, Dukun, Senopati, Patih }

    /// <summary>One piece on the board.</summary>
    public struct Piece
    {
        public bool Exists;
        public Side Side;
        public Role Role;
        public bool IsDam;
        /// <summary>The opponent knows this piece's role (it captured, or did something only its role can do).</summary>
        public bool Revealed;
        /// <summary>Own turns in a row this Dam has gone without capturing.</summary>
        public int DamIdle;
        /// <summary>Tameng Waja: opponent turns left during which this piece can't be captured.</summary>
        public int ImmuneTurns;
        /// <summary>Panglimunan: opponent turns left during which this piece is unseen and can't be captured.</summary>
        public int HiddenTurns;
        /// <summary>Santet: own turns left during which this piece can't move or capture.</summary>
        public int FrozenTurns;
        /// <summary>Saipi Angin: extra moves this piece may still make this turn.</summary>
        public int ExtraMoves;

        /// <summary>Jaka Tingkir's Pendekar: survives the first capture that hits it.</summary>
        public bool GuardOnce;

        public bool Untouchable => ImmuneTurns > 0 || HiddenTurns > 0;

        public static Piece Of(Side side, Role role, bool dam = false) =>
            new Piece { Exists = true, Side = side, Role = role, IsDam = dam };

        public override string ToString() => Exists ? $"{Side} {(IsDam ? "Dam" : Role.ToString())}" : "empty";
    }

    /// <summary>
    /// Role movement and capture patterns on the line board (docs/DECISIONS.md, "Roles on the line board").
    /// "Backward" means toward the piece's own side; "turn" means changing direction between chain jumps.
    /// </summary>
    public static class RoleRules
    {
        public static int StepRange(Piece p) => p.IsDam ? int.MaxValue : p.Role == Role.Senopati ? 2 : 1;
        public static bool MovesBackward(Piece p) => p.IsDam || p.Role == Role.Pendekar || p.Role == Role.Patih;
        public static bool CanCapture(Piece p) => p.IsDam || p.Role != Role.Dukun;
        public static bool CapturesBackward(Piece p) => MovesBackward(p);
        public static bool ChainMayTurn(Piece p) => p.IsDam || p.Role == Role.Pendekar;
        public static bool Promotes(Piece p) => !p.IsDam && p.Role == Role.Prajurit;

        /// <summary>The army every player brings: 16 pieces.</summary>
        public static readonly IReadOnlyDictionary<Role, int> ArmyCounts = new Dictionary<Role, int>
        {
            { Role.Prajurit, 10 }, { Role.Pendekar, 2 }, { Role.TelikSandi, 1 },
            { Role.Dukun, 1 }, { Role.Senopati, 1 }, { Role.Patih, 1 },
        };

        /// <summary>Rank used when a Dukun picks which neighbour to shield, and by the AI.</summary>
        public static int Importance(Piece p)
        {
            if (p.IsDam) return 4;
            switch (p.Role)
            {
                case Role.Patih: return 9;
                case Role.Senopati: return 6;
                case Role.Pendekar: return 5;
                case Role.Dukun: return 3;
                case Role.TelikSandi: return 2;
                default: return 1;
            }
        }
    }

    /// <summary>
    /// A side's arrangement: which role stands on each of its 16 starting points (Fase Susun).
    /// </summary>
    public sealed class Formation
    {
        public readonly Dictionary<int, Role> Roles = new Dictionary<int, Role>();

        /// <summary>Swaps the roles on two of the side's starting points; counts stay valid by construction.</summary>
        public void Swap(int a, int b)
        {
            if (!Roles.ContainsKey(a) || !Roles.ContainsKey(b)) throw new ArgumentException("Both points must be starting points of this formation.");
            var t = Roles[a]; Roles[a] = Roles[b]; Roles[b] = t;
        }

        public Formation Copy()
        {
            var f = new Formation();
            foreach (var kv in Roles) f.Roles[kv.Key] = kv.Value;
            return f;
        }

        /// <summary>The 16 starting points of a side: its triangle plus the two nearest columns.</summary>
        public static int[] StartPoints(Side side) =>
            Board.AllPoints().Where(p => side == Side.Hitam ? BoardGraph.Xs[p] <= 3 : BoardGraph.Xs[p] >= 5).ToArray();

        public bool IsValid(Side side, out string error) => IsValid(side, RoleRules.ArmyCounts, out error);

        /// <summary>Valid against a given army (Sayembara armies can have upgraded roles).</summary>
        public bool IsValid(Side side, IReadOnlyDictionary<Role, int> army, out string error)
        {
            var points = StartPoints(side);
            if (Roles.Count != points.Length || !points.All(Roles.ContainsKey)) { error = "Every starting point needs a role."; return false; }
            foreach (var kv in army)
            {
                int n = Roles.Values.Count(r => r == kv.Key);
                if (n != kv.Value) { error = $"{kv.Key}: {n} placed, {kv.Value} required."; return false; }
            }
            error = null;
            return true;
        }

        /// <summary>
        /// The default arrangement: Patih at the back of the triangle with the Dukun beside it,
        /// Senopati at the triangle's mouth, Pendekar on the front flanks, Telik Sandi in the front line.
        /// </summary>
        public static Formation Default(Side side)
        {
            var f = new Formation();
            foreach (var p in StartPoints(side)) f.Roles[p] = Role.Prajurit;
            int X(int x) => side == Side.Hitam ? x : BoardGraph.Width - 1 - x;
            f.Roles[BoardGraph.At(X(0), 2)] = Role.Patih;
            f.Roles[BoardGraph.At(X(1), 2)] = Role.Dukun;
            f.Roles[BoardGraph.At(X(2), 2)] = Role.Senopati;
            f.Roles[BoardGraph.At(X(3), 0)] = Role.Pendekar;
            f.Roles[BoardGraph.At(X(3), 4)] = Role.Pendekar;
            f.Roles[BoardGraph.At(X(3), 1)] = Role.TelikSandi;
            return f;
        }

        /// <summary>
        /// Rearranges this formation to match an army: upgraded roles replace Prajurit (front column first,
        /// so the special pieces fight), and surplus special roles turn back into Prajurit.
        /// </summary>
        public Formation FitTo(Side side, IReadOnlyDictionary<Role, int> army)
        {
            var f = Copy();
            foreach (var kv in army)
            {
                var role = kv.Key;
                int have = f.Roles.Values.Count(r => r == role);
                int front = side == Side.Hitam ? 3 : 5;
                while (have > kv.Value)
                {
                    var p = f.Roles.First(x => x.Value == role).Key;
                    f.Roles[p] = Role.Prajurit; have--;
                }
                while (have < kv.Value && role != Role.Prajurit)
                {
                    var spare = f.Roles.Where(x => x.Value == Role.Prajurit).OrderByDescending(x => BoardGraph.Xs[x.Key] == front ? 1 : 0).Select(x => x.Key).DefaultIfEmpty(-1).First();
                    if (spare < 0) break;
                    f.Roles[spare] = role; have++;
                }
            }
            return f;
        }

        /// <summary>A random valid arrangement, with the Patih kept off the front column.</summary>
        public static Formation Random(Side side, Random rng) => Random(side, RoleRules.ArmyCounts, rng);

        public static Formation Random(Side side, IReadOnlyDictionary<Role, int> army, Random rng)
        {
            var points = StartPoints(side).ToList();
            var bag = army.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value)).ToList();
            var f = new Formation();
            int front = side == Side.Hitam ? 3 : 5;
            var safe = points.Where(p => BoardGraph.Xs[p] != front).ToList();
            int patihPoint = safe[rng.Next(safe.Count)];
            f.Roles[patihPoint] = Role.Patih;
            bag.Remove(Role.Patih);
            points.Remove(patihPoint);
            foreach (var p in points.OrderBy(_ => rng.Next()))
            {
                f.Roles[p] = bag[bag.Count - 1];
                bag.RemoveAt(bag.Count - 1);
            }
            return f;
        }
    }
}
