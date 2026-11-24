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
        static string[] Notations(System.Collections.Generic.IEnumerable<Move> ms) => ms.Select(m => m.ToString()).OrderBy(s => s).ToArray();
        static Move Find(Board b, string notation) => R.LegalMoves(b).First(m => m.ToString() == notation);
        static void H(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Hitam, role);
        static void M(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Merah, role);

        // ---------- board and setup

        [Test]
        public void Graph_Has37Points_ApexHas8Neighbours_TriangleTipHas2()
        {
            Assert.AreEqual(37, BoardGraph.PointCount);
            CollectionAssert.AreEqual(new[] { "b2", "b3", "b4", "c2", "c4", "d2", "d3", "d4" }, Names(BoardGraph.Rays[P("c3")].Select(r => r[0])));
            CollectionAssert.AreEqual(new[] { "a3", "b2" }, Names(BoardGraph.Rays[P("a1")].Select(r => r[0])));
        }

        [Test]
        public void StandardBoard_16Each_DefaultFormation_HitamHas9OpeningMoves()
        {
            var b = Board.CreateStandard();
            Assert.AreEqual(16, b.CountPieces(Side.Hitam));
            Assert.AreEqual(16, b.CountPieces(Side.Merah));
            Assert.AreEqual("a3", BoardGraph.Name(b.Find(Side.Hitam, Role.Patih)));
            Assert.AreEqual("i3", BoardGraph.Name(b.Find(Side.Merah, Role.Patih)));
            Assert.IsTrue(Board.AllPoints().Where(p => BoardGraph.Xs[p] == 4).All(b.IsEmpty));
            var moves = R.LegalMoves(b);
            Assert.AreEqual(9, moves.Count, string.Join(" ", moves));
        }

        [Test]
        public void Formations_DefaultAndRandom_AreValid()
        {
            var rng = new System.Random(3);
            foreach (var side in new[] { Side.Hitam, Side.Merah })
            {
                Assert.IsTrue(Formation.Default(side).IsValid(side, out var e1), e1);
                for (int i = 0; i < 20; i++) Assert.IsTrue(Formation.Random(side, rng).IsValid(side, out var e2), e2);
            }
        }

        [Test]
        public void FormationSwap_MovesRoles_AndStaysValid()
        {
            var f = Formation.Default(Side.Hitam);
            int patih = P("a3"), front = P("d3");
            f.Swap(patih, front);
            Assert.AreEqual(Role.Patih, f.Roles[front]);
            Assert.AreEqual(Role.Prajurit, f.Roles[patih]);
            Assert.IsTrue(f.IsValid(Side.Hitam, out var error), error);
            var b = Board.CreateStandard(f);
            Assert.AreEqual("d3", BoardGraph.Name(b.Find(Side.Hitam, Role.Patih)));
        }

        // ---------- movement by role

        [Test]
        public void Prajurit_MovesForwardDiagonalAndSideways_NeverBackward()
        {
            var b = Board.CreateEmpty();
            H(b, "e3");
            CollectionAssert.AreEqual(new[] { "e2", "e4", "f2", "f3", "f4" }, Names(R.LegalMoves(b).Select(m => m.To)));
        }

        [Test]
        public void Pendekar_MayMoveBackward_AndThatRevealsIt()
        {
            var b = Board.CreateEmpty();
            H(b, "e3", Role.Pendekar);
            Assert.AreEqual(8, R.LegalMoves(b).Count);
            var result = R.Apply(b, Find(b, "e3-d3"));
            Assert.IsTrue(result.RevealedMover);
            Assert.IsTrue(b[P("d3")].Revealed);
        }

        [Test]
        public void Senopati_MovesUpTo2Points_Not_Backward_AndA2PointMoveRevealsIt()
        {
            var b = Board.CreateEmpty();
            H(b, "c3", Role.Senopati);
            Assert.AreEqual(10, R.LegalMoves(b).Count);
            Assert.IsFalse(R.Apply(b.Clone(), Find(b, "c3-d3")).RevealedMover);
            Assert.IsTrue(R.Apply(b.Clone(), Find(b, "c3-e3")).RevealedMover);
        }

        [Test]
        public void Senopati_CannotPassThroughAPiece()
        {
            var b = Board.CreateEmpty();
            H(b, "c3", Role.Senopati);
            H(b, "d3");
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.ToString() == "c3-e3"));
        }

        [Test]
        public void Patih_MovesOnePointAnyDirection()
        {
            var b = Board.CreateEmpty();
            H(b, "e3", Role.Patih);
            Assert.AreEqual(8, R.LegalMoves(b).Count);
        }

        // ---------- captures

        [Test]
        public void Prajurit_CapturesForwardAndSideways_NotBackward()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); M(b, "e3");
            CollectionAssert.AreEqual(new[] { "d3xf3" }, Notations(R.Captures(b)));

            b = Board.CreateEmpty();
            H(b, "e2"); M(b, "e3");
            CollectionAssert.AreEqual(new[] { "e2xe4" }, Notations(R.Captures(b)));

            b = Board.CreateEmpty();
            H(b, "f3"); M(b, "e3");
            Assert.IsFalse(R.HasAnyCapture(b));
        }

        [Test]
        public void Chains_GoStraight_ForOrdinaryPieces()
        {
            var b = Board.CreateEmpty();
            H(b, "c3"); M(b, "d3"); M(b, "e4");
            CollectionAssert.AreEqual(new[] { "c3xe3" }, Notations(R.Captures(b)));
        }

        [Test]
        public void Pendekar_MayTurnBetweenJumps()
        {
            var b = Board.CreateEmpty();
            H(b, "c3", Role.Pendekar); M(b, "d3"); M(b, "e4");
            CollectionAssert.AreEqual(new[] { "c3xe3xe5" }, Notations(R.Captures(b)));
        }

        [Test]
        public void StraightChain_IsCompleted()
        {
            var b = Board.CreateEmpty();
            H(b, "c3"); M(b, "d3"); M(b, "f3"); M(b, "e1");
            CollectionAssert.AreEqual(new[] { "c3xe3xg3" }, Notations(R.Captures(b)));
        }

        [Test]
        public void Dukun_NeverCaptures_SoNothingBurns()
        {
            var b = Board.CreateEmpty();
            H(b, "d3", Role.Dukun); M(b, "e3"); H(b, "a1");
            Assert.IsFalse(R.HasAnyCapture(b));
            Assert.AreEqual(-1, R.Apply(b, Find(b, "a1-b2")).Burned);
        }

        // ---------- burn

        [Test]
        public void SkippingACapture_BurnsThePieceThatCouldCapture()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); M(b, "e3"); H(b, "a1");
            var result = R.Apply(b, Find(b, "a1-b2"));
            Assert.AreEqual("d3", BoardGraph.Name(result.Burned));
            Assert.IsTrue(b.IsEmpty(P("d3")));
        }

        [Test]
        public void MovingTheCapturerWithoutCapturing_BurnsItWhereItLands()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); M(b, "e3");
            Assert.AreEqual("d2", BoardGraph.Name(R.Apply(b, Find(b, "d3-d2")).Burned));
        }

        [Test]
        public void BurningYourOwnPatih_LosesTheGame()
        {
            var b = Board.CreateEmpty();
            H(b, "d3", Role.Patih); M(b, "e3"); M(b, "i5"); H(b, "a1");
            R.Apply(b, Find(b, "a1-b2"));
            Assert.AreEqual(Side.Merah, R.Winner(b));
        }

        // ---------- passives

        [Test]
        public void MovedDukun_ShieldsANeighbour_ForTheOpponentsTurn()
        {
            var b = Board.CreateEmpty();
            // g2 blocks e2 from jumping f2, so Hitam has no capture to skip (and nothing burns).
            H(b, "d1", Role.Dukun); H(b, "e2"); M(b, "f2"); M(b, "g2"); M(b, "i5");
            var result = R.Apply(b, Find(b, "d1-e1"));
            Assert.AreEqual("e2", BoardGraph.Name(result.Shield));
            Assert.IsTrue(b.IsShielded(P("e2")));
            Assert.IsTrue(b[P("e1")].Revealed, "a shield gives the Dukun away");
            Assert.IsFalse(R.HasAnyCapture(b), "f2 can't jump the shielded e2");

            R.Apply(b, Find(b, "i5-h4"));
            Assert.AreEqual(-1, b.Shielded(Side.Hitam), "the shield lasts one opponent turn");
        }

        [Test]
        public void WhenASenopatiFalls_ItsPrajuritCantCaptureNextTurn()
        {
            var b = Board.CreateEmpty();
            // h3 stops the chain from continuing over g3.
            H(b, "d3"); M(b, "e3", Role.Senopati); M(b, "g3"); M(b, "h3"); M(b, "i5");
            var result = R.Apply(b, Find(b, "d3xf3"));
            CollectionAssert.AreEqual(new[] { Side.Merah }, result.SenopatiFell);
            Assert.IsTrue(b.PrajuritCaptureBlocked(Side.Merah));
            Assert.IsFalse(R.HasAnyCapture(b), "g3 would capture f3, but Merah's Prajurit are blocked");

            R.Apply(b, Find(b, "i5-h4"));
            Assert.IsFalse(b.PrajuritCaptureBlocked(Side.Merah));
        }

        [Test]
        public void TelikSandi_IsRevealedWhenItCaptures_NotWhenItSteps()
        {
            var b = Board.CreateEmpty();
            H(b, "d3", Role.TelikSandi); M(b, "e3"); M(b, "i5");
            Assert.IsTrue(R.Apply(b.Clone(), Find(b, "d3xf3")).RevealedMover);

            b = Board.CreateEmpty();
            H(b, "d3", Role.TelikSandi); M(b, "i5");
            Assert.IsFalse(R.Apply(b, Find(b, "d3-e3")).RevealedMover);
        }

        // ---------- promotion and Dam

        [Test]
        public void OnlyPrajuritPromote()
        {
            var b = Board.CreateEmpty();
            H(b, "h2"); M(b, "a1");
            Assert.IsTrue(R.Apply(b, Find(b, "h2-i1")).Promoted);
            Assert.IsTrue(b[P("i1")].IsDam);

            b = Board.CreateEmpty();
            H(b, "h2", Role.TelikSandi); M(b, "a1");
            Assert.IsFalse(R.Apply(b, Find(b, "h2-i1")).Promoted);
        }

        [Test]
        public void Dam_FliesAlongEveryLine()
        {
            var b = Board.CreateEmpty();
            b.PlaceDam(P("c3"), Side.Hitam);
            Assert.AreEqual(20, R.LegalMoves(b).Count);
        }

        [Test]
        public void Dam_CapturesAtDistance_AndMayLandAnywhereBeyond()
        {
            var b = Board.CreateEmpty();
            b.PlaceDam(P("c3"), Side.Hitam); M(b, "f3");
            CollectionAssert.AreEqual(new[] { "g3", "h3", "i3" }, Names(R.Captures(b).Select(m => m.To)));
        }

        [Test]
        public void Dam_TurnsBackIntoAPrajurit_After3TurnsWithoutCapturing()
        {
            var b = Board.CreateEmpty();
            b.PlaceDam(P("c1"), Side.Hitam); M(b, "h3");
            R.Apply(b, Find(b, "c1-c2"));
            R.Apply(b, Find(b, "h3-g3"));
            R.Apply(b, Find(b, "c2-c1"));
            R.Apply(b, Find(b, "g3-f3"));
            Assert.IsTrue(b[P("c1")].IsDam);
            var result = R.Apply(b, Find(b, "c1-c2"));
            CollectionAssert.AreEqual(new[] { "c2" }, Names(result.Demoted));
            Assert.IsFalse(b[P("c2")].IsDam);
        }

        [Test]
        public void ThreeCapturesInOneMove_GiveABonusMove()
        {
            var b = Board.CreateEmpty();
            H(b, "c3"); M(b, "d3"); M(b, "f3"); M(b, "h3"); M(b, "e1");
            var result = R.Apply(b, Find(b, "c3xe3xg3xi3"));
            Assert.IsTrue(result.BonusMove);
            Assert.AreEqual(Side.Hitam, b.ToMove);
            var next = R.Apply(b, R.LegalMoves(b)[0]);
            Assert.IsFalse(next.BonusMove);
            Assert.AreEqual(Side.Merah, b.ToMove);
        }

        // ---------- end of game

        [Test]
        public void CapturingThePatih_Wins()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); H(b, "a1", Role.Patih); M(b, "e3", Role.Patih); M(b, "i5"); M(b, "h4");
            R.Apply(b, Find(b, "d3xf3"));
            Assert.AreEqual(Side.Hitam, R.Winner(b));
        }

        [Test]
        public void SideWithNoPieces_Loses()
        {
            var b = Board.CreateEmpty(Side.Merah);
            H(b, "a1");
            Assert.AreEqual(Side.Hitam, R.Winner(b));
        }

        [Test]
        public void SideWithNoLegalMove_Loses()
        {
            var b = Board.CreateEmpty(Side.Merah);
            M(b, "a1"); H(b, "a3"); H(b, "a5");
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
            b.PlaceDam(P("c1"), Side.Hitam);
            b.PlaceDam(P("g5"), Side.Merah);
            rules.Apply(b, rules.LegalMoves(b).First(m => m.ToString() == "c1-d1"));
            Assert.IsFalse(rules.IsDraw(b));
            rules.Apply(b, rules.LegalMoves(b).First(m => m.ToString() == "g5-g4"));
            Assert.IsTrue(rules.IsDraw(b));
        }
    }
}
