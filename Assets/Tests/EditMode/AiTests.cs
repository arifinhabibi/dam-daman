using System.Diagnostics;
using NUnit.Framework;
using Pamor.Core;

namespace Pamor.Tests
{
    public class AiTests
    {
        static Square Sq(string name) => new Square(name[1] - '1', name[0] - 'a');

        [Test]
        public void ReturnsNull_WhenNoLegalMove()
        {
            var b = Board.CreateEmpty(Side.Merah);
            b[Sq("a1")] = -Board.Man;
            Assert.IsNull(new Ai(3, seed: 1).ChooseMove(b));
        }

        [Test]
        public void AlwaysReturnsALegalMove_FromStart()
        {
            var b = Board.CreateStandard();
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                var move = Ai.For(d, seed: 7).ChooseMove(b);
                Assert.IsNotNull(move, d.ToString());
                CollectionAssert.Contains(Rules.Default.LegalMoves(b).ConvertAll(m => m.ToString()), move.ToString(), d.ToString());
            }
        }

        [Test]
        public void TakesTheWinningCapture()
        {
            var b = Board.CreateEmpty(Side.Merah);
            b[Sq("f6")] = -Board.Man;
            b[Sq("e5")] = Board.Man; // Hitam's last piece
            b[Sq("a1")] = -Board.Man;
            var move = new Ai(3, seed: 1).ChooseMove(b);
            Assert.AreEqual("f6xd4", move.ToString());
        }

        [Test]
        public void AvoidsSteppingIntoACapture()
        {
            // Merah man on c5 can go b4 (safe) or d4 (eaten by Hitam's e3 man, then nothing back).
            var b = Board.CreateEmpty(Side.Merah);
            b[Sq("c5")] = -Board.Man;
            b[Sq("e3")] = Board.Man;
            b[Sq("h8")] = -Board.Man;
            b[Sq("a1")] = Board.Man;
            var move = new Ai(4, seed: 3).ChooseMove(b);
            Assert.AreNotEqual("c5-d4", move.ToString());
        }

        [Test]
        public void Sedang_BeatsMudah_MostOfTheTime()
        {
            int strongWins = 0;
            for (int game = 0; game < 4; game++)
            {
                var b = Board.CreateStandard();
                var hitam = game % 2 == 0 ? Ai.For(Difficulty.Sedang, game) : Ai.For(Difficulty.Mudah, game);
                var merah = game % 2 == 0 ? Ai.For(Difficulty.Mudah, game + 100) : Ai.For(Difficulty.Sedang, game + 100);
                var result = Play(b, hitam, merah, 300);
                var strongSide = game % 2 == 0 ? Side.Hitam : Side.Merah;
                if (result == strongSide) strongWins++;
            }
            Assert.GreaterOrEqual(strongWins, 3);
        }

        [Test]
        public void DeeperSearch_BeatsShallower_FromBothSeats()
        {
            // Regression: fail-hard bounds at the root were once taken as ties, so deeper search played worse.
            int deepWins = 0;
            for (int game = 0; game < 4; game++)
            {
                bool deepIsHitam = game % 2 == 0;
                var deep = new Ai(4, seed: game);
                var shallow = new Ai(2, seed: game + 50);
                var result = Play(Board.CreateStandard(), deepIsHitam ? deep : shallow, deepIsHitam ? shallow : deep, 250);
                if (result == (deepIsHitam ? Side.Hitam : Side.Merah)) deepWins++;
            }
            Assert.GreaterOrEqual(deepWins, 3);
        }

        [Test]
        public void SulitMove_IsFastEnoughForMobile()
        {
            var b = Board.CreateStandard();
            var ai = Ai.For(Difficulty.Sulit, 5);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 6; i++)
            {
                var m = ai.ChooseMove(b);
                b.Apply(m);
            }
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds / 6.0, 1500, "average Sulit move time (ms) on the desktop Editor");
        }

        [Test]
        public void DrawRule_TriggersAfterQuietDamMoves()
        {
            var b = Board.CreateEmpty();
            b[Sq("c1")] = Board.Dam;
            b[Sq("f8")] = -Board.Dam;
            var rules = new Rules { DrawQuietMoveLimit = 2 };
            Assert.IsFalse(rules.IsDraw(b));
            b.Apply(rules.LegalMoves(b).Find(m => m.ToString() == "c1-b2"));
            Assert.IsFalse(rules.IsDraw(b));
            b.Apply(rules.LegalMoves(b).Find(m => m.ToString() == "f8-g7"));
            Assert.IsTrue(rules.IsDraw(b));
            Assert.IsNull(rules.Winner(b), "a draw has no winner");
        }

        static Side? Play(Board b, Ai hitam, Ai merah, int maxMoves)
        {
            var rules = Rules.Default;
            for (int i = 0; i < maxMoves; i++)
            {
                if (rules.IsDraw(b)) return null;
                var winner = rules.Winner(b);
                if (winner.HasValue) return winner;
                var ai = b.ToMove == Side.Hitam ? hitam : merah;
                b.Apply(ai.ChooseMove(b));
            }
            return null;
        }
    }
}
