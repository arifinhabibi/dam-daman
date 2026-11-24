using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Pamor.Core;

namespace Pamor.Tests
{
    public class AiTests
    {
        static int P(string name) => BoardGraph.Parse(name);
        static void H(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Hitam, role);
        static void M(Board b, string at, Role role = Role.Prajurit) => b.Place(P(at), Side.Merah, role);

        [Test]
        public void ReturnsNull_WhenNoLegalMove()
        {
            var b = Board.CreateEmpty(Side.Merah);
            M(b, "a1"); H(b, "a3"); H(b, "a5");
            Assert.IsNull(new Ai(3, seed: 1).ChooseMove(b));
        }

        [Test]
        public void AlwaysReturnsALegalMove_FromStart()
        {
            var b = Board.CreateStandard();
            var legal = Rules.Default.LegalMoves(b).Select(m => m.ToString()).ToList();
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                var move = Ai.For(d, seed: 7).ChooseMove(b);
                Assert.IsNotNull(move, d.ToString());
                CollectionAssert.Contains(legal, move.ToString(), d.ToString());
            }
        }

        [Test]
        public void TakesTheWinningCapture()
        {
            var b = Board.CreateEmpty(Side.Merah);
            M(b, "e3"); H(b, "d3"); M(b, "i1");
            Assert.AreEqual("e3xc3", new Ai(3, seed: 1).ChooseMove(b).ToString());
        }

        [Test]
        public void KillsThePatih_OverOtherCaptures()
        {
            var b = Board.CreateEmpty(Side.Merah);
            M(b, "e3"); H(b, "d3", Role.Patih);
            M(b, "g4"); H(b, "f4");
            H(b, "a1"); H(b, "b2"); M(b, "i5");
            Assert.AreEqual("e3xc3", new Ai(2, seed: 1).ChooseMove(b).ToString());
        }

        [Test]
        public void PrefersCapturing_OverBeingBurned()
        {
            var b = Board.CreateEmpty();
            H(b, "d3"); M(b, "e3"); H(b, "a1"); M(b, "i5");
            Assert.AreEqual("d3xf3", new Ai(2, seed: 1).ChooseMove(b).ToString());
        }

        [Test]
        public void ChooseTurn_WithFullTenaga_AlwaysPlansALegalCastAndMove()
        {
            var rules = new Rules { AutoPancasona = true };
            var b = Board.CreateStandard();
            var ai = new Ai(2, seed: 4);
            int casts = 0;
            for (int turn = 0; turn < 24 && rules.Winner(b) == null; turn++)
            {
                b.SetTenaga(b.ToMove, AjianBook.MaxTenaga);
                var plan = ai.ChooseTurn(b);
                if (plan.Caster >= 0)
                {
                    Assert.IsNull(rules.CastBlocker(b, plan.Caster), $"turn {turn}");
                    CollectionAssert.Contains(rules.CastTargets(b, plan.Caster), plan.Target);
                    rules.Cast(b, plan.Caster, plan.Target);
                    casts++;
                }
                CollectionAssert.Contains(rules.LegalMoves(b).Select(m => m.ToString()).ToList(), plan.Move.ToString(), $"turn {turn}");
                rules.Apply(b, plan.Move);
            }
            Assert.Greater(casts, 0, "with full Tenaga Dalam the AI should find some ajian worth casting");
        }

        [Test]
        public void UsesKerbauNgamuk_ToKillThePatih()
        {
            var b = Board.CreateEmpty();
            H(b, "c3"); M(b, "d3"); M(b, "e3", Role.Patih); H(b, "a1", Role.Patih); M(b, "i5"); M(b, "h4");
            b.SetCommander(Side.Hitam, Commander.JakaTingkir);
            b.SetTenaga(Side.Hitam, 5);
            var plan = new Ai(2, seed: 1).ChooseTurn(b);
            Assert.IsNotNull(plan.CommanderTargets, "should charge");
            CollectionAssert.AreEqual(new[] { P("c3"), P("f3") }, plan.CommanderTargets);
        }

        [Test]
        public void ChooseTurn_WithNoTenaga_JustMoves()
        {
            var b = Board.CreateStandard();
            var plan = new Ai(2, seed: 1).ChooseTurn(b);
            Assert.AreEqual(-1, plan.Caster);
            Assert.IsNotNull(plan.Move);
        }

        [Test]
        public void DeeperSearch_BeatsShallower_FromBothSeats()
        {
            // Regression guard: deeper search must play better. A misleading evaluation term or a
            // root bound taken for a tie once made it play worse.
            int deepWins = 0;
            for (int game = 0; game < 4; game++)
            {
                bool deepIsHitam = game % 2 == 0;
                var deep = new Ai(3, seed: game);
                var shallow = new Ai(1, seed: game + 50);
                var result = Play(deepIsHitam ? deep : shallow, deepIsHitam ? shallow : deep, 300);
                if (result == (deepIsHitam ? Side.Hitam : Side.Merah)) deepWins++;
            }
            Assert.GreaterOrEqual(deepWins, 3);
        }

        [Test]
        public void Sedang_BeatsMudah_MostOfTheTime()
        {
            int wins = 0;
            for (int game = 0; game < 4; game++)
            {
                bool strongIsHitam = game % 2 == 0;
                var strong = Ai.For(Difficulty.Sedang, game);
                var weak = Ai.For(Difficulty.Mudah, game + 100);
                var result = Play(strongIsHitam ? strong : weak, strongIsHitam ? weak : strong, 300);
                if (result == (strongIsHitam ? Side.Hitam : Side.Merah)) wins++;
            }
            Assert.GreaterOrEqual(wins, 3);
        }

        [Test]
        public void SulitMove_IsFastEnoughForMobile()
        {
            var b = Board.CreateStandard();
            var ai = Ai.For(Difficulty.Sulit, 5);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 6; i++) Rules.Default.Apply(b, ai.ChooseMove(b));
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds / 6.0, 1500, "average Sulit move time (ms) on the desktop Editor");
        }

        static Side? Play(Ai hitam, Ai merah, int maxMoves)
        {
            // Same Pancasona rule the AI's search assumes (and the game applies for an AI's own Patih).
            var rules = new Rules { AutoPancasona = true };
            var b = Board.CreateStandard();
            for (int i = 0; i < maxMoves; i++)
            {
                var winner = rules.Winner(b);
                if (winner.HasValue) return winner;
                if (rules.IsDraw(b)) return null;
                rules.Apply(b, (b.ToMove == Side.Hitam ? hitam : merah).ChooseMove(b));
            }
            return null;
        }
    }
}
