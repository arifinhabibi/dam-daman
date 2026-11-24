using System.Collections;
using System.Linq;
using System.Text;
using Pamor.Core;
using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// The offline Sayembara: you plus 7 AI players. Commander once per lobby, then per round:
    /// Fase Belanja (30 s) → pairings → Fase Susun → your 1v1 (20 s turns, 10 turns each) → results.
    /// Bot-vs-bot pairings are simulated by the core. Pamor is saved on the device.
    /// </summary>
    public partial class GameController
    {
        const string PamorPref = "pamor.rating";

        Sayembara lobby;
        Pairing humanPairing;
        SayembaraPlayer opponent;
        Coroutine shopTimer;

        static int SavedPamor => PlayerPrefs.GetInt(PamorPref, 1000);

        void RefreshTitleRank() => hud.SetTitleRank($"PAMOR {SavedPamor} · {Sayembara.Rank(SavedPamor).ToUpperInvariant()}");

        /// <summary>From the title: pick a Commander for the whole lobby, then the lobby opens.</summary>
        void BeginSayembara()
        {
            lobby = null;
            BeginCommanderSelect(Side.Hitam);
        }

        void StartLobby(Commander c)
        {
            lobby = new Sayembara("KAMU", c, SavedPamor, System.Environment.TickCount);
            ShowStandings("SAYEMBARA", "Delapan pendekar, 100 HP. Yang kalah kehilangan HP sebanyak bidak lawan yang tersisa + nomor ronde. Bertahan sampai akhir!\n\n",
                ("MULAI RONDE 1", NextRound, true), ("KELUAR", ExitSayembara, true));
        }

        void ExitSayembara()
        {
            lobby = null;
            BeginTransition(GameState.Title, MatchMode.VsAi);
        }

        string Standings()
        {
            var sb = new StringBuilder();
            foreach (var p in lobby.All.OrderBy(p => p.Alive ? 0 : 1).ThenByDescending(p => p.Hp).ThenBy(p => p.Placement))
            {
                string hp = p.Alive ? $"{Mathf.Max(0, p.Hp),3} HP" : $"#{p.Placement}";
                string you = p.IsHuman ? "<color=#D9A441>" : "";
                string end = p.IsHuman ? "</color>" : "";
                string streak = p.WinStreak >= 2 ? $"  menang {p.WinStreak}x" : "";
                sb.AppendLine($"{you}{p.Name,-14}{end}<pos=36%>{CommanderBook.Name(p.Commander)}<pos=64%>{hp}{streak}");
            }
            return sb.ToString();
        }

        void ShowStandings(string title, string intro, params (string, System.Action, bool)[] buttons)
        {
            EnterState(GameState.Sayembara);
            hud.ShowSayembara(title, intro + Standings(), buttons);
        }

        // ---------- round

        void NextRound()
        {
            lobby.StartRound();
            shopSecondsLeft = Sayembara.ShopSeconds;
            ShowShop();
            if (shopTimer != null) StopCoroutine(shopTimer);
            shopTimer = StartCoroutine(ShopCountdown());
        }

        int shopSecondsLeft;

        IEnumerator ShopCountdown()
        {
            while (shopSecondsLeft > 0)
            {
                yield return new WaitForSecondsRealtime(1f);
                shopSecondsLeft--;
                if (State == GameState.Sayembara && lobby != null) ShowShop();
            }
            FinishShop();
        }

        void ShowShop()
        {
            var me = lobby.Human;
            var army = me.Army();
            var sb = new StringBuilder();
            sb.AppendLine($"Koin: <color=#D9A441>{me.Coins}</color>   (pendapatan 5 + bonus menang beruntun)   Sisa waktu: {shopSecondsLeft} detik\n");
            sb.AppendLine("Ubah Prajurit jadi peran lain. Peningkatan terbawa ke ronde berikutnya.\n");
            sb.AppendLine("Pasukanmu: " + string.Join(", ", army.Where(kv => kv.Value > 0).Select(kv => $"{RoleName(kv.Key)} {kv.Value}")));
            var buttons = Sayembara.ShopRoles.Select(r =>
            {
                var why = Sayembara.CantBuy(me, r);
                return ($"{RoleName(r).ToUpperInvariant()} ({Sayembara.UpgradeCost(r)})", (System.Action)(() => { Sayembara.Buy(me, r); ShowShop(); }), why == null);
            }).ToList();
            buttons.Add(("SELESAI", FinishShop, true));
            hud.ShowSayembara($"FASE BELANJA · RONDE {lobby.Round}", sb.ToString(), buttons.ToArray());
        }

        static string RoleName(Role r) => r == Role.TelikSandi ? "Telik Sandi" : r.ToString();

        void FinishShop()
        {
            if (shopTimer != null) { StopCoroutine(shopTimer); shopTimer = null; }
            if (lobby == null || State != GameState.Sayembara) return;
            lobby.MakePairings();
            humanPairing = lobby.HumanPairing;
            int oppId = humanPairing.Home == 0 ? humanPairing.Away : humanPairing.Home;
            opponent = lobby.All[oppId];
            bool home = humanPairing.Home == 0;

            var sb = new StringBuilder();
            sb.AppendLine($"Lawanmu: <color=#D9A441>{opponent.Name}</color> ({CommanderBook.Name(opponent.Commander)}){(humanPairing.AwayIsGhost ? " · BAYANGAN" : "")}");
            sb.AppendLine(home ? "Kamu TUAN RUMAH: main di papanmu, jalan kedua." : "Kamu TAMU: jalan duluan.");
            sb.AppendLine($"Maksimal {Sayembara.TurnsPerPlayer} giliran tiap pemain, {Sayembara.TurnSeconds} detik per giliran.\n");
            sb.AppendLine("Pertandingan lain ronde ini:");
            foreach (var pr in lobby.Pairings.Where(p => p != humanPairing))
                sb.AppendLine($"  {lobby.All[pr.Home].Name} vs {lobby.All[pr.Away].Name}{(pr.AwayIsGhost ? " (bayangan)" : "")}");
            hud.ShowSayembara($"RONDE {lobby.Round}", sb.ToString(), ("MAIN", StartRoundMatch, true));
        }

        void StartRoundMatch()
        {
            commanders[0] = lobby.Human.Commander;
            commanders[1] = opponent.Commander;
            blockers[0].Clear(); blockers[1].Clear();
            formations[0] = FormationOf(Side.Hitam).FitTo(Side.Hitam, lobby.Human.Army());
            formations[1] = lobby.BotFormation(opponent, Side.Merah);
            BeginSetup(Side.Hitam);
        }

        /// <summary>The human's round is over: record it, resolve the other pairings, show the results.</summary>
        IEnumerator AfterRoundMatch()
        {
            yield return new WaitForSeconds(1.8f);
            int winnerId = Winner == Side.Hitam ? 0 : Winner == Side.Merah ? opponent.Id : -1;
            var winnerSide = Winner ?? Side.Hitam;
            lobby.Record(humanPairing, winnerId, Board, winnerSide);
            foreach (var pr in lobby.Pairings.Where(p => p != humanPairing)) lobby.Simulate(pr);
            var outNow = lobby.FinishRound();

            var sb = new StringBuilder();
            foreach (var pr in lobby.Pairings)
            {
                string h = lobby.All[pr.Home].Name, a = lobby.All[pr.Away].Name + (pr.AwayIsGhost ? " (bayangan)" : "");
                string result = pr.WinnerId < 0 ? $"SERI (-{pr.Damage} HP keduanya)" : $"{lobby.All[pr.WinnerId.Value].Name} menang · {lobby.All[pr.LoserId].Name} -{pr.Damage} HP";
                sb.AppendLine($"{h} vs {a}: {result}");
            }
            if (outNow.Count > 0) sb.AppendLine("\nTersingkir: " + string.Join(", ", outNow.Select(p => $"{p.Name} (#{p.Placement})")));
            sb.AppendLine();

            bool humanOut = !lobby.Human.Alive || lobby.Finished;
            ShowStandings($"HASIL RONDE {lobby.Round}", sb.ToString(),
                humanOut ? ("LIHAT HASIL AKHIR", (System.Action)ShowFinal, true) : ($"RONDE {lobby.Round + 1}", (System.Action)NextRound, true));
        }

        void ShowFinal()
        {
            int place = lobby.Human.Placement;
            int before = SavedPamor;
            int delta = Sayembara.PamorChange(place, before, lobby.LobbyAverage);
            int after = Mathf.Max(0, before + delta);
            PlayerPrefs.SetInt(PamorPref, after);
            PlayerPrefs.Save();
            RefreshTitleRank();
            string title = place == 1 ? "JUARA SAYEMBARA!" : $"PERINGKAT #{place}";
            string body = $"Pamor: {before} → <color=#D9A441>{after}</color> ({(delta >= 0 ? "+" : "")}{delta})\nPangkat: {Sayembara.Rank(after)}\n\n";
            lobby = null;
            EnterState(GameState.Sayembara);
            hud.ShowSayembara(title, body, ("SAYEMBARA LAGI", () => BeginTransition(GameState.Playing, MatchMode.Sayembara), true), ("JUDUL", () => BeginTransition(GameState.Title, MatchMode.VsAi), true));
        }

        // ---------- turn timer

        float turnTimeLeft;
        int timedTurnKey = -1;

        /// <summary>Sayembara: 20 seconds per turn for the human; when time runs out the autopilot moves.</summary>
        void TickTurnTimer()
        {
            if (Mode != MatchMode.Sayembara || State != GameState.Playing || IsAiTurn || Busy) { if (Mode != MatchMode.Sayembara) hud.SetTimer(-1); return; }
            int key = Board.Ply * 4 + (Board.InBonusMove ? 1 : 0) + (Board.ForcedPiece >= 0 ? 2 : 0);
            if (key != timedTurnKey) { timedTurnKey = key; turnTimeLeft = Sayembara.TurnSeconds; }
            turnTimeLeft -= Time.deltaTime;
            hud.SetTimer(Mathf.CeilToInt(Mathf.Max(0f, turnTimeLeft)));
            if (turnTimeLeft > 0f) return;
            var move = Autopilot.ChooseMove(Board, rules, new System.Random());
            if (move == null) return;
            hud.NudgeHint("WAKTU HABIS: AUTOPILOT");
            Deselect();
            turnRoutine = StartCoroutine(PlayTurn(move));
        }
    }
}
