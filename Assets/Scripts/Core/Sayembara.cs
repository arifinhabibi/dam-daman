using System;
using System.Collections.Generic;
using System.Linq;

namespace Pamor.Core
{
    /// <summary>One of the eight players in a Sayembara lobby.</summary>
    public sealed class SayembaraPlayer
    {
        public int Id;
        public string Name;
        public bool IsHuman;
        public Commander Commander;
        public int Hp = Sayembara.StartHp;
        public int Coins;
        public int WinStreak;
        public int HomeGames;
        public int LastOpponent = -1;
        public int Pamor = 1000;
        /// <summary>Upgrades bought in Fase Belanja: Prajurit turned into these roles. They carry over between rounds.</summary>
        public readonly Dictionary<Role, int> Upgrades = new Dictionary<Role, int>();
        /// <summary>Place 1..8 once the player is out (or the winner); 0 while still in.</summary>
        public int Placement;
        public bool Alive => Placement == 0;

        /// <summary>The 16-piece army after upgrades.</summary>
        public Dictionary<Role, int> Army()
        {
            var army = new Dictionary<Role, int>();
            foreach (var kv in RoleRules.ArmyCounts) army[kv.Key] = kv.Value;
            foreach (var kv in Upgrades) { army[kv.Key] += kv.Value; army[Role.Prajurit] -= kv.Value; }
            return army;
        }
    }

    /// <summary>One 1v1 of a round. Home plays on their own board and moves second; away moves first.</summary>
    public sealed class Pairing
    {
        public int Home, Away;
        /// <summary>The away "player" is a ghost: the army of an eliminated player, played by the AI.</summary>
        public bool AwayIsGhost;
        public int GhostOf = -1;
        /// <summary>Winner's id, -1 for a draw; null until played.</summary>
        public int? WinnerId;
        public int Damage;
        public int LoserId = -1;
    }

    /// <summary>
    /// The 8-player Sayembara (GDD "Format Sayembara"): 100 HP each; every round a Fase Belanja, random
    /// pairings without immediate rematches, quick 1v1s, and HP damage to the loser (enemy pieces left +
    /// round number). Players at 0 HP are out; the order they go out decides places 8..1.
    /// Pure C#: the game drives the human's match, and bot matches are simulated here.
    /// </summary>
    public sealed class Sayembara
    {
        public const int StartHp = 100;
        public const int Players = 8;
        public const int IncomePerRound = 5;
        public const int TurnsPerPlayer = 10;
        public const int ShopSeconds = 30;
        public const int TurnSeconds = 20;

        public readonly List<SayembaraPlayer> All = new List<SayembaraPlayer>();
        public int Round { get; private set; }
        public List<Pairing> Pairings { get; private set; } = new List<Pairing>();
        readonly Random rng;
        int nextPlacement = Players;

        static readonly string[] BotNames = { "Bagus Lelana", "Sekar Arum", "Wira Kencana", "Ayu Laras", "Damar Seta", "Ratri Wulan", "Bayu Jati" };

        public Sayembara(string humanName, Commander humanCommander, int humanPamor, int seed)
        {
            rng = new Random(seed);
            All.Add(new SayembaraPlayer { Id = 0, Name = humanName, IsHuman = true, Commander = humanCommander, Pamor = humanPamor });
            for (int i = 0; i < Players - 1; i++)
                All.Add(new SayembaraPlayer
                {
                    Id = i + 1,
                    Name = BotNames[i],
                    Commander = CommanderBook.All[rng.Next(CommanderBook.All.Length)],
                    Pamor = Math.Max(600, humanPamor + rng.Next(-150, 151)),
                });
        }

        public SayembaraPlayer Human => All[0];
        public IEnumerable<SayembaraPlayer> Alive => All.Where(p => p.Alive);
        public bool Finished => All.Count(p => p.Alive) <= 1;

        /// <summary>Streak bonus on top of the base income: +1 at 2 wins in a row, +2 at 3, +3 at 4 or more.</summary>
        public static int StreakBonus(int streak) => streak >= 4 ? 3 : streak >= 2 ? streak - 1 : 0;

        /// <summary>Starts a round: income for everyone still in, then bots shop.</summary>
        public void StartRound()
        {
            Round++;
            foreach (var p in Alive) p.Coins += IncomePerRound + StreakBonus(p.WinStreak);
            foreach (var p in Alive) if (!p.IsHuman) BotShop(p);
        }

        // ---------- Fase Belanja

        public static int UpgradeCost(Role role)
        {
            switch (role)
            {
                case Role.TelikSandi: return 2;
                case Role.Pendekar: return 3;
                case Role.Dukun: return 3;
                case Role.Senopati: return 5;
                default: return 0;
            }
        }

        /// <summary>Most of a role an army may have; at least 4 Prajurit always remain.</summary>
        public static int MaxOf(Role role)
        {
            switch (role)
            {
                case Role.Pendekar: return 4;
                case Role.Senopati: case Role.Dukun: case Role.TelikSandi: return 2;
                default: return 0;
            }
        }

        public static readonly Role[] ShopRoles = { Role.Pendekar, Role.Senopati, Role.Dukun, Role.TelikSandi };
        const int MinPrajurit = 4;

        public static string CantBuy(SayembaraPlayer p, Role role)
        {
            var army = p.Army();
            if (army[role] >= MaxOf(role)) return "Maksimal";
            if (army[Role.Prajurit] <= MinPrajurit) return "Prajurit habis";
            if (p.Coins < UpgradeCost(role)) return "Koin kurang";
            return null;
        }

        public static void Buy(SayembaraPlayer p, Role role)
        {
            var why = CantBuy(p, role);
            if (why != null) throw new InvalidOperationException(why);
            p.Coins -= UpgradeCost(role);
            p.Upgrades[role] = (p.Upgrades.TryGetValue(role, out var n) ? n : 0) + 1;
        }

        void BotShop(SayembaraPlayer p)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                var options = ShopRoles.Where(r => CantBuy(p, r) == null).ToList();
                if (options.Count == 0 || rng.NextDouble() < 0.25) break; // bots sometimes save coins
                Buy(p, options[rng.Next(options.Count)]);
            }
        }

        // ---------- pairings

        /// <summary>Random pairs without immediate rematches; whoever has had fewer home games is home. Odd count: one faces a ghost.</summary>
        public List<Pairing> MakePairings()
        {
            var pool = Alive.OrderBy(_ => rng.Next()).ToList();
            var pairs = new List<Pairing>();
            // Try a few shuffles to avoid last round's opponent.
            for (int attempt = 0; attempt < 30; attempt++)
            {
                pairs.Clear();
                var bag = new List<SayembaraPlayer>(pool);
                bool ok = true;
                while (bag.Count >= 2)
                {
                    var a = bag[0]; bag.RemoveAt(0);
                    int j = bag.FindIndex(b => b.Id != a.LastOpponent);
                    if (j < 0) { ok = false; j = 0; }
                    var b2 = bag[j]; bag.RemoveAt(j);
                    var (home, away) = a.HomeGames <= b2.HomeGames ? (a, b2) : (b2, a);
                    pairs.Add(new Pairing { Home = home.Id, Away = away.Id });
                }
                if (bag.Count == 1)
                {
                    var ghost = All.Where(p => !p.Alive).OrderBy(_ => rng.Next()).FirstOrDefault() ?? All.First(p => p.Id != bag[0].Id);
                    pairs.Add(new Pairing { Home = bag[0].Id, Away = ghost.Id, AwayIsGhost = true, GhostOf = ghost.Id });
                }
                if (ok) break;
                pool = pool.OrderBy(_ => rng.Next()).ToList();
            }
            foreach (var pr in pairs) All[pr.Home].HomeGames++;
            Pairings = pairs;
            return pairs;
        }

        public Pairing HumanPairing => Pairings.FirstOrDefault(p => p.Home == 0 || (p.Away == 0 && !p.AwayIsGhost));

        // ---------- matches

        /// <summary>The rules for a quick round: 10 turns each.</summary>
        public static Rules RoundRules() => new Rules { TurnCap = TurnsPerPlayer * 2, AutoPancasona = true };

        /// <summary>Sets up a pairing's board. The caller seats the players (the human is always Hitam, shown at the bottom) and names who moves first (the away player).</summary>
        public Board CreateBoard(SayembaraPlayer hitam, Formation hitamFormation, SayembaraPlayer merah, Formation merahFormation, Side firstMover)
        {
            var board = Board.CreateStandard(hitamFormation, merahFormation);
            board.SetCommander(Side.Hitam, hitam.Commander);
            board.SetCommander(Side.Merah, merah.Commander);
            board.SetFirstMover(firstMover);
            return board;
        }

        public Formation BotFormation(SayembaraPlayer p, Side side) => Formation.Random(side, p.Army(), rng);

        /// <summary>Plays a bot-vs-bot (or bot-vs-ghost) pairing to the end with a fast AI and records it.</summary>
        public void Simulate(Pairing pr)
        {
            var home = All[pr.Home];
            var away = All[pr.Away];
            // Away moves first; seat away as Hitam for the simulation.
            var board = CreateBoard(away, BotFormation(away, Side.Hitam), home, BotFormation(home, Side.Merah), Side.Hitam);
            var rules = RoundRules();
            // Bot-vs-bot rounds are resolved off-screen, so they use a fast shallow search without ajian.
            var ai = new Ai(1, 0.15, rules, rng.Next());
            for (int i = 0; i < 200 && rules.Winner(board) == null && !rules.IsDraw(board); i++)
            {
                var move = ai.ChooseMove(board) ?? Autopilot.ChooseMove(board, rules, rng);
                if (move == null) break;
                rules.Apply(board, move);
            }
            var w = rules.Winner(board);
            Record(pr, w == Side.Hitam ? away.Id : w == Side.Merah ? home.Id : -1, board, w == Side.Hitam ? Side.Hitam : Side.Merah);
        }

        /// <summary>
        /// Records a pairing's result. Damage to the loser = the winner's pieces still standing + the round number.
        /// A draw costs both players the round number. Ghosts take no damage.
        /// </summary>
        public void Record(Pairing pr, int winnerId, Board finalBoard, Side winnerSide)
        {
            pr.WinnerId = winnerId;
            All[pr.Home].LastOpponent = pr.AwayIsGhost ? -1 : pr.Away;
            if (!pr.AwayIsGhost) All[pr.Away].LastOpponent = pr.Home;
            if (winnerId < 0)
            {
                foreach (var id in new[] { pr.Home, pr.Away })
                {
                    if (id == pr.Away && pr.AwayIsGhost) continue;
                    All[id].Hp -= Round; All[id].WinStreak = 0;
                }
                pr.Damage = Round;
                return;
            }
            int loserId = winnerId == pr.Home ? pr.Away : pr.Home;
            pr.LoserId = loserId;
            pr.Damage = finalBoard.CountPieces(winnerSide) + Round;
            All[winnerId].WinStreak++;
            bool loserIsGhost = pr.AwayIsGhost && loserId == pr.Away;
            if (!loserIsGhost) { All[loserId].Hp -= pr.Damage; All[loserId].WinStreak = 0; }
        }

        /// <summary>Ends the round: players at 0 HP go out, lowest HP first (so they place worse).</summary>
        public List<SayembaraPlayer> FinishRound()
        {
            var outNow = Alive.Where(p => p.Hp <= 0).OrderBy(p => p.Hp).ToList();
            foreach (var p in outNow) p.Placement = nextPlacement--;
            if (Finished) foreach (var p in Alive.ToList()) p.Placement = 1;
            return outNow;
        }

        // ---------- Pamor

        /// <summary>GDD "Sistem peringkat Pamor": base points by place, scaled by lobby strength (clamped 0.5..1.5).</summary>
        public static int PamorChange(int placement, int playerPamor, double lobbyAverage)
        {
            int[] table = { 0, 40, 30, 20, 10, -10, -20, -30, -40 };
            int basePoints = table[Math.Max(1, Math.Min(8, placement))];
            double ratio = lobbyAverage / Math.Max(1, playerPamor);
            double mult = basePoints >= 0 ? ratio : 1.0 / ratio;
            mult = Math.Max(0.5, Math.Min(1.5, mult));
            return (int)Math.Round(basePoints * mult);
        }

        public static string Rank(int pamor)
        {
            if (pamor < 1000) return "Prajurit";
            if (pamor < 1300) return "Pendekar";
            if (pamor < 1600) return "Senopati";
            if (pamor < 1900) return "Patih";
            return "Maharaja";
        }

        public double LobbyAverage => All.Average(p => p.Pamor);
    }
}
