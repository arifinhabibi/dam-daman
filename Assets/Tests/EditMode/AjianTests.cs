using System.Linq;
using NUnit.Framework;
using Pamor.Core;

namespace Pamor.Tests
{
    public class AjianTests
    {
        static int P(string name) => BoardGraph.Parse(name);
        static Rules R => Rules.Default;
        static Move Find(Board b, string notation) => R.LegalMoves(b).First(m => m.ToString() == notation);
        static void H(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Hitam, role);
        static void M(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Merah, role);

        [Test]
        public void TenagaDalam_StartsAt0_Plus1PerTurn_Plus2PerCapture_Max10()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); M(b, "e3"); M(b, "i5");
            Assert.AreEqual(0, b.Tenaga(Side.Hitam));
            R.Apply(b, Find(b, "d3xf3"));
            Assert.AreEqual(3, b.Tenaga(Side.Hitam), "+2 for the capture, +1 for the turn");
            b.SetTenaga(Side.Merah, 50);
            Assert.AreEqual(AjianBook.MaxTenaga, b.Tenaga(Side.Merah));
        }

        [Test]
        public void TamengWaja_MakesAPrajuritUncapturable_ForOneOpponentTurn()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); H(b, "a1"); M(b, "e3"); M(b, "f3"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 2);
            R.Cast(b, P("d3"), P("d3"));
            Assert.AreEqual(0, b.Tenaga(Side.Hitam));
            R.Apply(b, Find(b, "a1-b2"));
            Assert.IsFalse(R.Captures(b).Any(m => m.Captured.Contains(P("d3"))), "e3 can't jump the immune d3");
            R.Apply(b, Find(b, "i5-h4"));
            Assert.AreEqual(0, b[P("d3")].ImmuneTurns, "immunity lasts one opponent turn");
        }

        [Test]
        public void Casting_OncePerTurn_NeedsTenaga_AndRespectsCooldown()
        {
            var b = Board.CreateEmpty();
            H(b, "c1"); H(b, "c5"); M(b, "i1"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 10);
            b.SetTenaga(Side.Merah, 10);
            R.Cast(b, P("c1"), P("c1"));
            Assert.IsNotNull(R.CastBlocker(b, P("c5")), "one ajian per turn");

            // Tameng Waja has a 2-turn cooldown: the next two Hitam turns can't cast it, the third can.
            string[] hitam = { "c1-d1", "d1-e1", "e1-f1" }, merah = { "i1-h2", "h2-g3", "i5-h4" };
            for (int t = 0; t < 3; t++)
            {
                R.Apply(b, Find(b, hitam[t]));
                R.Apply(b, Find(b, merah[t]));
                bool blocked = R.CastBlocker(b, P("c5")) != null;
                Assert.AreEqual(t < 2, blocked, $"after {t + 1} round(s)");
            }

            var poor = Board.CreateEmpty();
            H(poor, "c1"); M(poor, "i1");
            StringAssert.Contains("Tenaga", R.CastBlocker(poor, P("c1")));
        }

        [Test]
        public void SaipiAngin_LetsThePendekarMoveTwice()
        {
            var b = Board.CreateEmpty();
            H(b, "e3", Role.Pendekar); H(b, "a1"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 3);
            R.Cast(b, P("e3"), P("e3"));
            var first = R.Apply(b, Find(b, "e3-f3"));
            Assert.IsTrue(first.ExtraMove);
            Assert.AreEqual(Side.Hitam, b.ToMove);
            Assert.IsTrue(R.LegalMoves(b).All(m => m.From == P("f3")), "only the Pendekar moves again");
            var second = R.Apply(b, Find(b, "f3-g3"));
            Assert.IsFalse(second.ExtraMove);
            Assert.AreEqual(Side.Merah, b.ToMove);
        }

        [Test]
        public void Panglimunan_HidesTheTelikSandi_ForTwoOpponentTurns()
        {
            var b = Board.CreateEmpty();
            H(b, "d3", Role.TelikSandi); H(b, "a1"); M(b, "e3"); M(b, "f3"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 3);
            R.Cast(b, P("d3"), P("d3"));
            Assert.IsFalse(b[P("d3")].Revealed, "casting Panglimunan doesn't give the role away");
            R.Apply(b, Find(b, "a1-b2"));
            Assert.IsFalse(R.Captures(b).Any(m => m.Captured.Contains(P("d3"))));
            R.Apply(b, Find(b, "i5-h4"));
            Assert.AreEqual(1, b[P("d3")].HiddenTurns);
            R.Apply(b, Find(b, "b2-c3"));
            R.Apply(b, Find(b, "h4-g3"));
            Assert.AreEqual(0, b[P("d3")].HiddenTurns);
        }

        [Test]
        public void Santet_FreezesAnEnemyWithin3Steps_ForTwoOfItsTurns()
        {
            var b = Board.CreateEmpty();
            H(b, "c3", Role.Dukun); H(b, "a1"); M(b, "f3"); M(b, "i3"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 4);
            var targets = R.CastTargets(b, P("c3")).Select(BoardGraph.Name).ToList();
            CollectionAssert.Contains(targets, "f3");
            CollectionAssert.DoesNotContain(targets, "i3");
            R.Cast(b, P("c3"), P("f3"));
            R.Apply(b, Find(b, "a1-a3"));
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.From == P("f3")), "frozen on Merah's 1st turn");
            R.Apply(b, Find(b, "i5-h4"));
            R.Apply(b, Find(b, "a3-b3"));
            Assert.IsFalse(R.LegalMoves(b).Any(m => m.From == P("f3")), "frozen on Merah's 2nd turn");
            R.Apply(b, Find(b, "h4-g3"));
            R.Apply(b, Find(b, "b3-b4"));
            Assert.IsTrue(R.LegalMoves(b).Any(m => m.From == P("f3")), "free on Merah's 3rd turn");
        }

        [Test]
        public void GelarSupitUrang_AdvancesPrajuritWithin2Steps()
        {
            var b = Board.CreateEmpty();
            H(b, "c3", Role.Senopati); H(b, "d2"); H(b, "e1"); H(b, "b3"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 5);
            var result = R.Cast(b, P("c3"), P("c3"));
            var shifts = result.Shifts.Select(s => BoardGraph.Name(s.from) + ">" + BoardGraph.Name(s.to)).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(new[] { "d2>e2", "e1>f1" }, shifts, "b3 is blocked by the Senopati itself");
            Assert.AreEqual(Side.Hitam, b.ToMove, "an ajian doesn't use up the move");
        }

        [Test]
        public void Pancasona_IsAReaction_ThatRevivesThePatihAtItsStart()
        {
            var b = Board.CreateEmpty(Side.Merah);
            H(b, "d3", Role.Patih); H(b, "a1"); M(b, "e3"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 8);
            var kill = Find(b, "e3xc3");
            Assert.IsTrue(R.KillsPatih(b, kill, Side.Hitam));
            Assert.IsTrue(R.CanUsePancasona(b, Side.Hitam));

            R.UsePancasona(b, Side.Hitam);
            var result = R.Apply(b, kill);
            Assert.AreEqual("d3", BoardGraph.Name(result.PatihRevived));
            Assert.IsNull(R.Winner(b));
            Assert.IsFalse(R.CanUsePancasona(b, Side.Hitam), "once per match");
        }

        [Test]
        public void WithoutPancasona_ThePatihStaysDead()
        {
            var b = Board.CreateEmpty(Side.Merah);
            H(b, "d3", Role.Patih); H(b, "a1"); M(b, "e3"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 8);
            R.Apply(b, Find(b, "e3xc3"));
            Assert.AreEqual(Side.Merah, R.Winner(b));
        }

        [Test]
        public void AutoPancasona_IsUsedWhenAffordable()
        {
            var rules = new Rules { AutoPancasona = true };
            var b = Board.CreateEmpty(Side.Merah);
            H(b, "d3", Role.Patih); H(b, "a1"); M(b, "e3"); M(b, "i5");
            b.SetTenaga(Side.Hitam, 8);
            var result = rules.Apply(b, rules.LegalMoves(b).First(m => m.ToString() == "e3xc3"));
            Assert.AreEqual("d3", BoardGraph.Name(result.PatihRevived));
        }

        [Test]
        public void PancasonaCantBeCastOnYourOwnTurn()
        {
            var b = Board.CreateEmpty();
            H(b, "a3", Role.Patih); M(b, "i5");
            b.SetTenaga(Side.Hitam, 10);
            StringAssert.Contains("ditanya", R.CastBlocker(b, P("a3")));
        }
    }
}
