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
            var rules = Rules.Default;
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
