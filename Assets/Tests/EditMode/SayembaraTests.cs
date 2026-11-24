using System.Linq;
using NUnit.Framework;
using Pamor.Core;

namespace Pamor.Tests
{
    public class SayembaraTests
    {
        static int P(string name) => BoardGraph.Parse(name);

        [Test]
        public void TurnCap_DecidesByArmyValue()
        {
            var rules = new Rules { TurnCap = 2 };
            var b = Board.CreateEmpty();
            b.Place(P("a1"), Side.Hitam, Role.Patih); b.Place(P("a5"), Side.Hitam);
            b.Place(P("i3"), Side.Merah, Role.Patih);
            rules.Apply(b, rules.LegalMoves(b).First(m => m.From == P("a5")));
            Assert.IsNull(rules.Winner(b));
            rules.Apply(b, rules.LegalMoves(b)[0]);
            Assert.AreEqual(Side.Hitam, rules.Winner(b), "Hitam 6 points vs Merah 5 at the cap");
        }

        [Test]
        public void Shop_UpgradesPrajurit_WithLimits_AndFormationFitsTheArmy()
        {
            var p = new SayembaraPlayer { Coins = 20 };
            Sayembara.Buy(p, Role.Senopati);
            Assert.AreEqual(15, p.Coins);
            Assert.AreEqual(2, p.Army()[Role.Senopati]);
            Assert.AreEqual(9, p.Army()[Role.Prajurit]);
            Assert.AreEqual("Maksimal", Sayembara.CantBuy(p, Role.Senopati));

            var f = Formation.Default(Side.Hitam).FitTo(Side.Hitam, p.Army());
            Assert.IsTrue(f.IsValid(Side.Hitam, p.Army(), out var error), error);
            Assert.IsTrue(Formation.Random(Side.Merah, p.Army(), new System.Random(1)).IsValid(Side.Merah, p.Army(), out error), error);
        }

        [Test]
        public void Income_IncludesTheStreakBonus()
        {
            Assert.AreEqual(0, Sayembara.StreakBonus(1));
            Assert.AreEqual(1, Sayembara.StreakBonus(2));
            Assert.AreEqual(3, Sayembara.StreakBonus(6));
        }

        [Test]
        public void Pairings_CoverEveryoneOnce_AndAvoidRematches()
        {
            var s = new Sayembara("KAMU", Commander.SiPitung, 1000, 3);
            s.StartRound();
            var first = s.MakePairings();
            Assert.AreEqual(4, first.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 8), first.SelectMany(p => new[] { p.Home, p.Away }));
            foreach (var pr in first) s.Simulate(pr);
            s.FinishRound();
            s.StartRound();
            var second = s.MakePairings();
            foreach (var pr in second)
                Assert.AreNotEqual(s.All[pr.Home].LastOpponent, pr.Away, "no immediate rematch");
        }

        [Test]
        public void Damage_IsSurvivingEnemyPiecesPlusRound()
        {
            var s = new Sayembara("KAMU", Commander.SiPitung, 1000, 1);
            s.StartRound();
            var pr = new Pairing { Home = 0, Away = 1 };
            var b = Board.CreateEmpty();
            for (int i = 0; i < 5; i++) b.Place(i, Side.Merah);
            s.Record(pr, 1, b, Side.Merah);
            Assert.AreEqual(5 + 1, pr.Damage);
            Assert.AreEqual(100 - 6, s.Human.Hp);
            Assert.AreEqual(1, s.All[1].WinStreak);
        }

        [Test]
        public void PamorChange_FollowsTheTable_AndLobbyStrength()
        {
            Assert.AreEqual(40, Sayembara.PamorChange(1, 1000, 1000));
            Assert.AreEqual(-40, Sayembara.PamorChange(8, 1000, 1000));
            Assert.AreEqual(60, Sayembara.PamorChange(1, 1000, 2000), "capped at 1.5x");
            Assert.AreEqual(-20, Sayembara.PamorChange(8, 1000, 2000), "stronger lobby softens the loss");
            Assert.AreEqual("Pendekar", Sayembara.Rank(1000));
        }

        [Test]
        public void AFullBotLobby_EndsWithOneWinner_AndEightPlaces()
        {
            var s = new Sayembara("KAMU", Commander.JakaTingkir, 1000, 7);
            int rounds = 0;
            while (!s.Finished && rounds++ < 60)
            {
                s.StartRound();
                foreach (var pr in s.MakePairings()) s.Simulate(pr);
                s.FinishRound();
            }
            Assert.IsTrue(s.Finished, $"still going after {rounds} rounds");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 8), s.All.Select(p => p.Placement));
        }

        [Test]
        public void Autopilot_TakesACaptureWhenThereIsOne()
        {
            var b = Board.CreateEmpty();
            b.Place(P("d3"), Side.Hitam); b.Place(P("e3"), Side.Merah); b.Place(P("a1"), Side.Hitam);
            var move = Autopilot.ChooseMove(b, Rules.Default, new System.Random(1));
            Assert.AreEqual("d3xf3", move.ToString());
        }
    }
}
