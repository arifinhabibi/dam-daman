using System.Linq;
using NUnit.Framework;
using Pamor.Core;

namespace Pamor.Tests
{
    public class RulesTests
    {
        static Square Sq(string name) => new Square(name[1] - '1', name[0] - 'a');
        static readonly Rules R = Rules.Default;

        [Test]
        public void StandardBoard_Has12PiecesEach_HitamMovesFirst_With7Steps()
        {
            var b = Board.CreateStandard();
            Assert.AreEqual(12, b.CountPieces(Side.Hitam));
            Assert.AreEqual(12, b.CountPieces(Side.Merah));
            Assert.AreEqual(Side.Hitam, b.ToMove);
            var moves = R.LegalMoves(b);
            Assert.AreEqual(7, moves.Count);
            Assert.IsTrue(moves.All(m => !m.IsCapture && m.To.Row == 3));
        }

        [Test]
        public void Men_OnlyStepForward()
        {
            var b = Board.CreateEmpty(Side.Merah);
            b[Sq("d4")] = -Board.Man;
            var tos = R.LegalMoves(b).Select(m => m.To.ToString()).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(new[] { "c3", "e3" }, tos);
        }

        [Test]
        public void Capture_IsMandatory()
        {
            var b = Board.CreateEmpty();
            b[Sq("c3")] = Board.Man;
            b[Sq("d4")] = -Board.Man;
            b[Sq("g1")] = Board.Man; // could step, but must not
            var moves = R.LegalMoves(b);
            Assert.AreEqual(1, moves.Count);
            Assert.AreEqual("c3xe5", moves[0].ToString());
            Assert.IsTrue(R.HasAnyCapture(b));
        }

        [Test]
        public void Men_CaptureBackward()
        {
            var b = Board.CreateEmpty();
            b[Sq("e5")] = Board.Man;
            b[Sq("d4")] = -Board.Man;
            var moves = R.LegalMoves(b);
            Assert.AreEqual(1, moves.Count);
            Assert.AreEqual("e5xc3", moves[0].ToString());
        }

        [Test]
        public void CaptureChain_MustBeCompleted()
        {
            var b = Board.CreateEmpty();
            b[Sq("a1")] = Board.Man;
            b[Sq("b2")] = -Board.Man;
            b[Sq("d4")] = -Board.Man;
            var moves = R.LegalMoves(b);
            Assert.AreEqual(1, moves.Count);
            Assert.AreEqual("a1xc3xe5", moves[0].ToString());
            Assert.AreEqual(2, moves[0].Captured.Count);

            b.Apply(moves[0]);
            Assert.AreEqual(0, b.CountPieces(Side.Merah));
            Assert.AreEqual(Board.Man, b[Sq("e5")]);
        }

        [Test]
        public void Man_PromotesToDam_WhenMoveEndsOnFarRow()
        {
            var b = Board.CreateEmpty();
            b[Sq("b7")] = Board.Man;
            var move = R.LegalMoves(b).First(m => m.To == Sq("a8"));
            var result = b.Apply(move);
            Assert.IsTrue(result.Promoted);
            Assert.AreEqual(Board.Dam, b[Sq("a8")]);
            Assert.AreEqual(Side.Merah, b.ToMove);
        }

        [Test]
        public void Man_DoesNotPromote_WhenChainOnlyPassesFarRow()
        {
            // Hitam man on d6 jumps e7 to land on f8, then must continue jumping g7 back down to h6.
            var b = Board.CreateEmpty();
            b[Sq("d6")] = Board.Man;
            b[Sq("e7")] = -Board.Man;
            b[Sq("g7")] = -Board.Man;
            var moves = R.LegalMoves(b);
            Assert.AreEqual(1, moves.Count);
            Assert.AreEqual("d6xf8xh6", moves[0].ToString());
            var result = b.Apply(moves[0]);
            Assert.IsFalse(result.Promoted);
            Assert.AreEqual(Board.Man, b[Sq("h6")]);
        }

        [Test]
        public void Dam_FliesAlongDiagonals()
        {
            var b = Board.CreateEmpty();
            b[Sq("a1")] = Board.Dam;
            var moves = R.LegalMoves(b);
            Assert.AreEqual(7, moves.Count); // b2..h8
        }

        [Test]
        public void Dam_CapturesAtDistance_AndMayLandAnywhereBeyond()
        {
            var b = Board.CreateEmpty();
            b[Sq("a1")] = Board.Dam;
            b[Sq("d4")] = -Board.Man;
            var tos = R.LegalMoves(b).Select(m => m.To.ToString()).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(new[] { "e5", "f6", "g7", "h8" }, tos);
        }

        [Test]
        public void Dam_CannotJumpTwoAdjacentPieces()
        {
            var b = Board.CreateEmpty();
            b[Sq("a1")] = Board.Dam;
            b[Sq("c3")] = -Board.Man;
            b[Sq("d4")] = -Board.Man;
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.IsCapture));
        }

        [Test]
        public void SideWithNoPieces_Loses()
        {
            var b = Board.CreateEmpty(Side.Merah);
            b[Sq("a1")] = Board.Man;
            Assert.AreEqual(Side.Hitam, R.Winner(b));
        }

        [Test]
        public void SideWithNoLegalMove_Loses()
        {
            // Merah man on a1 can only step "down" off the board: blocked.
            var b = Board.CreateEmpty(Side.Merah);
            b[Sq("a1")] = -Board.Man;
            b[Sq("h8")] = Board.Man;
            Assert.AreEqual(Side.Hitam, R.Winner(b));
        }

        [Test]
        public void StandardBoard_HasNoWinner()
        {
            Assert.IsNull(R.Winner(Board.CreateStandard()));
        }
    }
}
