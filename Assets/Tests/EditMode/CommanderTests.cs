using System.Linq;
using NUnit.Framework;
using Pamor.Core;

namespace Pamor.Tests
{
    public class CommanderTests
    {
        static int P(string name) => BoardGraph.Parse(name);
        static Rules R => Rules.Default;
        static Move Find(Board b, string notation) => R.LegalMoves(b).First(m => m.ToString() == notation);
        static void H(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Hitam, role);
        static void M(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Merah, role);

        // ---------- Sangkuriang

        [Test]
        public void Blockers_CantBeEntered_Jumped_OrFlownThrough()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); b.PlaceDam(P("c1"), Side.Hitam); M(b, "i5");
            b.SetBlocker(P("e3"), true);
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.To == P("e3")));
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.ToString() == "c1-g5"), "the Dam's diagonal passes e3");

            var c = Board.CreateEmpty(Side.Merah);
            M(c, "f3"); H(c, "i5");
            c.SetBlocker(P("e3"), true);
            Assert.IsFalse(R.HasAnyCapture(c));
        }

        [Test]
        public void PerahuSemalam_RaisesA3PointWall_ForTwoOpponentTurns()
        {
            var b = Board.CreateEmpty();
            H(b, "a1"); M(b, "f3"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.Sangkuriang);
            b.SetTenaga(Side.Hitam, 6);
            var result = R.CastCommander(b, new[] { P("e3") });
            CollectionAssert.AreEquivalent(new[] { P("e2"), P("e3"), P("e4") }, result.Walls);
            R.Apply(b, Find(b, "a1-b2"));
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.To == P("e3")), "wall on Merah's 1st turn");
            R.Apply(b, Find(b, "i5-h4"));
            R.Apply(b, Find(b, "b2-c3"));
            Assert.IsTrue(b.IsWall(P("e3")), "wall on Merah's 2nd turn");
            R.Apply(b, Find(b, "h4-g3"));
            Assert.IsFalse(b.IsWall(P("e3")), "gone after two Merah turns");
        }

        // ---------- Si Pitung

        [Test]
        public void SiPitung_StealsTenagaOnCaptures()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); M(b, "e3"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.SiPitung);
            b.SetTenaga(Side.Merah, 5);
            R.Apply(b, Find(b, "d3xf3"));
            Assert.AreEqual(4, b.Tenaga(Side.Merah));
            Assert.AreEqual(4, b.Tenaga(Side.Hitam), "+2 capture, +1 stolen, +1 turn");
        }

        [Test]
        public void BayanganPitung_HidesARevealedTelikSandiAgain()
        {
            var b = Board.CreateEmpty();
            H(b, "d3", Role.TelikSandi); M(b, "e3"); M(b, "i5"); M(b, "h3");
            b.SetCommander(Side.Hitam, Commander.SiPitung);
            R.Apply(b, Find(b, "d3xf3"));
            Assert.IsTrue(b[P("f3")].Revealed);
            R.Apply(b, Find(b, "i5-h4"));
            b.SetTenaga(Side.Hitam, 4);
            R.CastCommander(b, new[] { P("f3") });
            Assert.IsFalse(b[P("f3")].Revealed);
        }

        // ---------- Calon Arang

        [Test]
        public void CalonArang_SantetCosts3_AndTeluhMassalFreezes3Enemies()
        {
            var b = Board.CreateEmpty();
            H(b, "c3", Role.Dukun); M(b, "e3"); M(b, "f2"); M(b, "f4"); M(b, "g3"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.CalonArang);
            b.SetTenaga(Side.Hitam, 3);
            Assert.IsNull(R.CastBlocker(b, P("c3")), "Santet affordable at 3");

            b.SetTenaga(Side.Hitam, 7);
            var result = R.CastCommander(b, new int[0]);
            Assert.AreEqual(3, result.Frozen.Count);
            Assert.IsTrue(result.Frozen.All(p => b[p].FrozenTurns == 1 && b[p].Side == Side.Merah));
        }

        // ---------- Jaka Tingkir

        [Test]
        public void JakaTingkir_PendekarSurvivesTheFirstHit()
        {
            var b = Board.CreateEmpty(Side.Merah);
            H(b, "e3", Role.Pendekar); H(b, "a1"); M(b, "f3"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.JakaTingkir);
            var result = R.Apply(b, Find(b, "f3xd3"));
            CollectionAssert.AreEqual(new[] { P("e3") }, result.Guarded);
            Assert.IsTrue(b[P("e3")].Exists, "the Pendekar is still standing");
            Assert.IsFalse(b[P("e3")].GuardOnce, "but only once");
        }

        [Test]
        public void KerbauNgamuk_ChargesStraight_TakingEveryEnemyOnTheWay()
        {
            var b = Board.CreateEmpty();
            H(b, "c3"); M(b, "d3"); M(b, "e3"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.JakaTingkir);
            b.SetTenaga(Side.Hitam, 5);
            CollectionAssert.Contains(R.ChargeEnds(b, P("c3")), P("f3"));
            var result = R.CastCommander(b, new[] { P("c3"), P("f3") });
            Assert.AreEqual(2, result.CapturedPieces.Count);
            Assert.IsTrue(b[P("f3")].Exists && b[P("f3")].Side == Side.Hitam);
            Assert.AreEqual(4, b.Tenaga(Side.Hitam), "5 - 5 + 2 per captured piece");
            Assert.AreEqual(Side.Hitam, b.ToMove, "the active doesn't use up the move");
        }

        // ---------- Sawerigading

        [Test]
        public void Sawerigading_EdgeRowPiecesMoveOneFarther()
        {
            var b = Board.CreateEmpty();
            H(b, "c1"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.Sawerigading);
            Assert.IsTrue(R.LegalMoves(b).Any(m => m.ToString() == "c1-e1"));
            b.SetCommander(Side.Hitam, null);
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.ToString() == "c1-e1"));
        }

        [Test]
        public void LayarPinisi_SendsTwoPiecesToTheOppositeEdge()
        {
            var b = Board.CreateEmpty();
            H(b, "d1"); H(b, "d2"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.Sawerigading);
            b.SetTenaga(Side.Hitam, 5);
            R.CastCommander(b, new[] { P("d1"), P("d2") });
            Assert.IsTrue(b[P("d5")].Exists && b[P("d4")].Exists);
            Assert.IsFalse(b[P("d1")].Exists || b[P("d2")].Exists);
        }

        // ---------- Lutung Kasarung

        [Test]
        public void LutungKasarung_DukunShieldsTwo_AndWujudAsliPromotes()
        {
            var b = Board.CreateEmpty();
            H(b, "d1", Role.Dukun); H(b, "e2"); H(b, "d2", Role.Senopati); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.LutungKasarung);
            R.Apply(b, Find(b, "d1-e1"));
            Assert.IsTrue(b.IsShielded(P("d2")) && b.IsShielded(P("e2")));

            var c = Board.CreateEmpty();
            H(c, "d3"); M(c, "i5");
            c.SetCommander(Side.Hitam, Commander.LutungKasarung);
            c.SetTenaga(Side.Hitam, 6);
            R.CastCommander(c, new[] { P("d3") });
            Assert.IsTrue(c[P("d3")].IsDam);
        }

        [Test]
        public void CommanderActive_SharesTheOneAbilityPerTurnLimit()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); M(b, "i5");
            b.SetCommander(Side.Hitam, Commander.LutungKasarung);
            b.SetTenaga(Side.Hitam, 10);
            R.Cast(b, P("d3"), P("d3"));
            StringAssert.Contains("Sudah", R.CommanderBlocker(b));
        }
    }
}
