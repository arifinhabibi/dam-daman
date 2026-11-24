using System.Linq;
using NUnit.Framework;
using Pamor.Core;

namespace Pamor.Tests
{
    public class RulesTests
    {
        static int P(string name) => BoardGraph.Parse(name);
        static Rules R => Rules.Default;
        static string[] Names(System.Collections.Generic.IEnumerable<int> pts) => pts.Select(BoardGraph.Name).OrderBy(s => s).ToArray();
        static Move Find(Board b, string notation) => R.LegalMoves(b).First(m => m.ToString() == notation);

        [Test]
        public void Graph_Has37Points_ApexHas8Neighbours_TriangleTipHas2()
        {
            Assert.AreEqual(37, BoardGraph.PointCount);
            CollectionAssert.AreEqual(new[] { "b2", "b3", "b4", "c2", "c4", "d2", "d3", "d4" }, Names(BoardGraph.Rays[P("c3")].Select(r => r[0])));
            CollectionAssert.AreEqual(new[] { "a3", "b2" }, Names(BoardGraph.Rays[P("a1")].Select(r => r[0])));
        }

        [Test]
        public void StandardBoard_16Each_CentreColumnEmpty_HitamHas9OpeningMoves()
        {
            var b = Board.CreateStandard();
            Assert.AreEqual(16, b.CountPieces(Side.Hitam));
            Assert.AreEqual(16, b.CountPieces(Side.Merah));
            Assert.IsTrue(Board.AllPoints().Where(p => BoardGraph.Xs[p] == 4).All(p => b[p] == 0));
            var moves = R.LegalMoves(b);
            Assert.AreEqual(9, moves.Count, string.Join(" ", moves));
            Assert.IsTrue(moves.All(m => BoardGraph.Xs[m.To] == 4));
        }

        [Test]
        public void Man_MovesForwardDiagonalAndSideways_NeverBackward()
        {
            var b = Board.CreateEmpty();
            b[P("e3")] = Board.Man;
            CollectionAssert.AreEqual(new[] { "e2", "e4", "f2", "f3", "f4" }, Names(R.LegalMoves(b).Select(m => m.To)));
        }

        [Test]
        public void Man_CapturesForward()
        {
            var b = Board.CreateEmpty();
            b[P("d3")] = Board.Man;
            b[P("e3")] = -Board.Man;
            CollectionAssert.AreEqual(new[] { "d3xf3" }, R.Captures(b).Select(m => m.ToString()).ToArray());
        }

        [Test]
        public void Man_CapturesSideways()
        {
            var b = Board.CreateEmpty();
            b[P("e2")] = Board.Man;
            b[P("e3")] = -Board.Man;
            CollectionAssert.AreEqual(new[] { "e2xe4" }, R.Captures(b).Select(m => m.ToString()).ToArray());
        }

        [Test]
        public void Man_DoesNotCaptureBackward()
        {
            var b = Board.CreateEmpty();
            b[P("f3")] = Board.Man;
            b[P("e3")] = -Board.Man;
            Assert.IsFalse(R.HasAnyCapture(b));
        }

        [Test]
        public void CaptureChain_IsCompleted()
        {
            var b = Board.CreateEmpty();
            b[P("c3")] = Board.Man;
            b[P("d3")] = -Board.Man;
            b[P("f3")] = -Board.Man;
            b[P("e1")] = -Board.Man;
            CollectionAssert.AreEqual(new[] { "c3xe3xg3" }, R.Captures(b).Select(m => m.ToString()).ToArray());
        }

        [Test]
        public void SkippingACapture_BurnsThePieceThatCouldCapture()
        {
            var b = Board.CreateEmpty();
            b[P("d3")] = Board.Man;
            b[P("e3")] = -Board.Man;
            b[P("a1")] = Board.Man;
            var result = R.Apply(b, Find(b, "a1-b2"));
            Assert.AreEqual("d3", BoardGraph.Name(result.Burned));
            Assert.AreEqual(0, b[P("d3")]);
            Assert.AreEqual(Side.Merah, b.ToMove);
        }

        [Test]
        public void MovingTheCapturerWithoutCapturing_BurnsItWhereItLands()
        {
            var b = Board.CreateEmpty();
            b[P("d3")] = Board.Man;
            b[P("e3")] = -Board.Man;
            var result = R.Apply(b, Find(b, "d3-d2"));
            Assert.AreEqual("d2", BoardGraph.Name(result.Burned));
            Assert.AreEqual(0, b.CountPieces(Side.Hitam));
        }

        [Test]
        public void Capturing_BurnsNothing()
        {
            var b = Board.CreateEmpty();
            b[P("d3")] = Board.Man;
            b[P("e3")] = -Board.Man;
            b[P("a1")] = -Board.Man;
            var result = R.Apply(b, Find(b, "d3xf3"));
            Assert.AreEqual(-1, result.Burned);
            Assert.AreEqual(1, b.CountPieces(Side.Merah));
        }

        [Test]
        public void Man_PromotesOnTheEnemyTriangleTip()
        {
            var b = Board.CreateEmpty();
            b[P("h2")] = Board.Man;
            b[P("a1")] = -Board.Man;
            var result = R.Apply(b, Find(b, "h2-i1"));
            Assert.IsTrue(result.Promoted);
            Assert.AreEqual(Board.Dam, b[P("i1")]);
        }

        [Test]
        public void Dam_FliesAlongEveryLine()
        {
            var b = Board.CreateEmpty();
            b[P("c3")] = Board.Dam;
            Assert.AreEqual(20, R.LegalMoves(b).Count);
        }

        [Test]
        public void Dam_CapturesAtDistance_AndMayLandAnywhereBeyond()
        {
            var b = Board.CreateEmpty();
            b[P("c3")] = Board.Dam;
            b[P("f3")] = -Board.Man;
            CollectionAssert.AreEqual(new[] { "g3", "h3", "i3" }, Names(R.Captures(b).Select(m => m.To)));
        }

        [Test]
        public void Dam_TurnsBackIntoAMan_After3TurnsWithoutCapturing()
        {
            var b = Board.CreateEmpty();
            b[P("c1")] = Board.Dam;
            b[P("h3")] = -Board.Man;
            R.Apply(b, Find(b, "c1-c2"));
            R.Apply(b, Find(b, "h3-g3"));
            R.Apply(b, Find(b, "c2-c1"));
            R.Apply(b, Find(b, "g3-f3"));
            Assert.AreEqual(Board.Dam, b[P("c1")]);
            var result = R.Apply(b, Find(b, "c1-c2"));
            CollectionAssert.AreEqual(new[] { "c2" }, Names(result.Demoted));
            Assert.AreEqual(Board.Man, b[P("c2")]);
        }

        [Test]
        public void ThreeCapturesInOneMove_GiveABonusMove()
        {
            var b = Board.CreateEmpty();
            b[P("c3")] = Board.Man;
            b[P("d3")] = -Board.Man;
            b[P("f3")] = -Board.Man;
            b[P("h3")] = -Board.Man;
            b[P("e1")] = -Board.Man;
            var move = Find(b, "c3xe3xg3xi3");
            var result = R.Apply(b, move);
            Assert.IsTrue(result.BonusMove);
            Assert.IsTrue(result.Promoted);
            Assert.AreEqual(Side.Hitam, b.ToMove);

            // The bonus move doesn't chain into another bonus.
            var next = R.Apply(b, R.LegalMoves(b)[0]);
            Assert.IsFalse(next.BonusMove);
            Assert.AreEqual(Side.Merah, b.ToMove);
        }

        [Test]
        public void SideWithNoPieces_Loses()
        {
            var b = Board.CreateEmpty(Side.Merah);
            b[P("a1")] = Board.Man;
            Assert.AreEqual(Side.Hitam, R.Winner(b));
        }

        [Test]
        public void SideWithNoLegalMove_Loses()
        {
            // Merah man on a1 may only go sideways to a3 (occupied) or jump it (a5 occupied).
            var b = Board.CreateEmpty(Side.Merah);
            b[P("a1")] = -Board.Man;
            b[P("a3")] = Board.Man;
            b[P("a5")] = Board.Man;
            Assert.AreEqual(0, R.LegalMoves(b).Count);
            Assert.AreEqual(Side.Hitam, R.Winner(b));
        }

        [Test]
        public void StandardBoard_HasNoWinner()
        {
            Assert.IsNull(R.Winner(Board.CreateStandard()));
        }

        [Test]
        public void DrawRule_TriggersAfterQuietDamMoves()
        {
            var rules = new Rules { DrawQuietMoveLimit = 2 };
            var b = Board.CreateEmpty();
            b[P("c1")] = Board.Dam;
            b[P("g5")] = -Board.Dam;
            rules.Apply(b, rules.LegalMoves(b).First(m => m.ToString() == "c1-d1"));
            Assert.IsFalse(rules.IsDraw(b));
            rules.Apply(b, rules.LegalMoves(b).First(m => m.ToString() == "g5-g4"));
            Assert.IsTrue(rules.IsDraw(b));
        }
    }
}
