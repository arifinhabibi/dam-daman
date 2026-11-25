using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pamor.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pamor
{
    // BELAJAR UNITY - STATE MACHINE (mesin status):
    // Game ini selalu berada di SATU "layar/status" pada satu waktu: judul, pilih Commander,
    // menyusun pasukan, bermain, jeda, dst. Itu disimpan di enum GameState. Banyak kode di bawah
    // berbunyi "kalau State bukan Playing, abaikan sentuhan ini". Ini pola yang sangat umum di game.
    public enum GameState { Title, CommanderSelect, Setup, Handoff, Playing, Paused, Ended, Sayembara }
    public enum MatchMode { LocalPvP, VsAi, Sayembara }

    // BELAJAR UNITY - "PUSAT KENDALI":
    // GameController adalah penghubung antara tiga bagian:
    //   Core.Rules/Board  (aturan)  <-->  GameController  <-->  BoardView/HudView (tampilan)
    // Alurnya: sentuhan pemain -> HandleGestures -> Tap(titik) -> minta Rules apa yang sah ->
    // Rules.Apply mengubah Board -> BoardView.PlayMove menganimasikannya -> HudView memperbarui angka.
    //
    // "partial class": kelas ini dipecah ke beberapa file (GameController.cs dan
    // GameController.Sayembara.cs) supaya file tidak terlalu panjang. Kompilator menggabungkannya.

    /// <summary>
    /// Owns one match: rules state, shell state machine, pointer input and the AI opponent.
    /// Local PvP is hotseat on one device; vs AI the player is Hitam (moves first) and the AI is Merah.
    /// </summary>
    public partial class GameController : MonoBehaviour
    {
        // PlayerPrefs = penyimpanan kecil bawaan Unity (nama -> angka/teks) yang bertahan setelah
        // game ditutup. Dipakai untuk tingkat AI dan Pamor. Ini kuncinya.
        const string DifficultyPref = "pamor.difficulty";
        const float MinAiThinkSeconds = 0.6f;

        // BELAJAR UNITY - REFERENSI DI INSPECTOR:
        // [SerializeField] pada field private membuatnya muncul di Inspector. Kita seret objek
        // dari scene ke slot ini (komponen BoardView, HudView, CameraRig di GameObject "Game").
        // Unity mengisinya otomatis saat scene dimuat. Kalau slotnya kosong, nilainya null dan
        // game akan error saat dipakai. Kalau error "NullReferenceException", periksa ini dulu.
        [SerializeField] BoardView boardView;
        [SerializeField] HudView hud;
        [SerializeField] CameraRig cameraRig;

        // Properti { get; private set; }: luar boleh membaca, hanya kelas ini yang boleh menulis.
        public GameState State { get; private set; } = GameState.Title;
        public MatchMode Mode { get; private set; } = MatchMode.LocalPvP;
        public Difficulty AiDifficulty { get; private set; } = Difficulty.Sedang;
        public Side AiSide => Side.Merah;
        public Board Board { get; private set; }
        public int MovesPlayed { get; private set; }
        public Side? Winner { get; private set; }
        public bool IsDraw { get; private set; }
        public bool Busy { get; private set; }
        public bool AiThinking { get; private set; }

        Rules rules = Rules.Default;

        /// <summary>There is an AI opponent (vs AI and Sayembara); it always plays Merah.</summary>
        bool HasAi => Mode != MatchMode.LocalPvP;
        int? selected;               // "int?" = bisa berisi angka ATAU kosong (null): titik yang sedang dipilih, kalau ada
        Camera cam;
        Coroutine turnRoutine;       // coroutine giliran yang sedang jalan; disimpan supaya bisa dihentikan saat reset
        bool transitioning;
        int matchId;                 // naik tiap match baru; dipakai untuk membatalkan hasil AI yang datang terlambat

        bool IsAiTurn => HasAi && Board.ToMove == AiSide;

        /// <summary>Whose eyes the board is drawn for: the player vs AI; the side to move in hotseat.</summary>
        Side Viewer => HasAi ? Board.Other(AiSide) : Board.ToMove;

        // Start() dipanggil Unity SEKALI, tepat sebelum frame pertama. Dipakai untuk pengaturan awal.
        // (Awake lebih awal lagi; Start dipakai di sini karena butuh komponen lain sudah siap.)
        void Start()
        {
            cam = Camera.main;
            AiDifficulty = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyPref, (int)Difficulty.Sedang), 0, 2);
            hud.Build();
            hud.SetDifficulty(AiDifficulty);
            // BELAJAR UNITY - EVENT: HudView tidak tahu apa-apa soal aturan game. Ia hanya
            // "berteriak" (event) saat tombol ditekan, dan di sini kita mendaftar untuk
            // mendengarnya. "+=" artinya "tambahkan pendengar"; "() => ..." adalah lambda,
            // yaitu fungsi kecil yang dijalankan saat event itu terjadi.
            // Pola ini menjaga UI dan logika tetap terpisah.
            hud.ResumePressed += () => SetPaused(false);
            hud.PausePressed += () => SetPaused(true);
            // In Sayembara, leaving the match leaves the lobby.
            hud.RestartPressed += () => { if (Mode == MatchMode.Sayembara) ExitSayembara(); else BeginTransition(GameState.Playing, Mode); };
            hud.TitlePressed += () => { if (Mode == MatchMode.Sayembara) ExitSayembara(); else BeginTransition(GameState.Title, Mode); };
            hud.PlaySayembaraPressed += () => BeginTransition(GameState.Playing, MatchMode.Sayembara);
            RefreshTitleRank();
            hud.PlayLocalPressed += () => BeginTransition(GameState.Playing, MatchMode.LocalPvP);
            hud.PlayAiPressed += () => BeginTransition(GameState.Playing, MatchMode.VsAi);
            hud.DifficultyPressed += CycleDifficulty;
            hud.SetupRandomPressed += RandomizeSetup;
            hud.SetupReadyPressed += ConfirmSetup;
            hud.HandoffPressed += ConfirmHandoff;
            hud.AjianPressed += PressAjian;
            hud.CommanderPressed += PressCommander;
            hud.CommanderChosen += ChooseCommander;
            hud.PancasonaAnswered += yes => pancasonaAnswer = yes;
            ResetMatch();
            EnterState(GameState.Title);
        }

        // Update() dipanggil Unity SETIAP frame. Di sini: baca masukan pemain dan jalankan timer.
        // Hindari pekerjaan berat di Update (mis. mencari AI terbaik) karena akan membuat game patah-patah;
        // untuk itu lihat AiTurn yang memakai thread terpisah.
        void Update()
        {
            // BELAJAR UNITY - INPUT SYSTEM: proyek ini memakai paket "Input System" baru
            // (UnityEngine.InputSystem), bukan Input.GetKey yang lama. "wasPressedThisFrame"
            // bernilai true hanya pada frame tombol mulai ditekan.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (State == GameState.Playing) SetPaused(true);
                else if (State == GameState.Paused) SetPaused(false);
            }

            HandleGestures();
            TickTurnTimer();
        }

        // A press that moves less than this (in screen pixels, scaled by DPI) is a tap; more is a camera drag.
        const float DragThresholdInches = 0.08f;
        Vector2 pressStart, lastPointer;
        bool pressing, dragging, pinching;
        float lastPinch;

        // Piece drag: a press that starts on one of your movable pieces drags the piece, not the camera.
        int pieceCandidate = -1, pieceDragFrom = -1;
        bool pieceDragging;

        /// <summary>The piece a press at this screen position would pick up, or -1 (empty point, enemy piece, nothing movable, not your turn).</summary>
        int FindDraggablePiece(Vector2 pos)
        {
            if (transitioning) return -1;
            if (!boardView.TryGetPoint(cam.ScreenPointToRay(pos), out var p)) return -1;
            if (State == GameState.Setup) return Board.Owner(p) == setupSide ? p : -1;
            if (State != GameState.Playing || Busy || IsAiTurn || targeting || commanderTargeting) return -1;
            if (Board.Owner(p) != Board.ToMove) return -1;
            return rules.LegalMovesFrom(Board, p).Count > 0 ? p : -1;
        }

        void BeginPieceDrag(int p)
        {
            pieceDragFrom = p;
            pieceDragging = true;
            if (State == GameState.Setup) { SetupDeselect(); SetupTap(p); }
            else if (selected != p) Tap(p); // selects it and shows its destinations
        }

        void UpdatePieceDrag(Vector2 pos)
        {
            if (boardView.TryGetBoardPoint(cam.ScreenPointToRay(pos), out var local)) boardView.DragPieceTo(pieceDragFrom, local);
        }

        void CancelPieceDrag()
        {
            if (!pieceDragging) return;
            boardView.SnapPieceBack(pieceDragFrom);
            pieceDragging = false; pieceDragFrom = -1;
        }

        /// <summary>Dropping on a legal destination plays the move; in Fase Susun, dropping on another of your pieces swaps roles. Anything else snaps back.</summary>
        void EndPieceDrag(Vector2 pos)
        {
            int from = pieceDragFrom;
            pieceDragging = false; pieceDragFrom = -1;
            bool hit = boardView.TryGetPoint(cam.ScreenPointToRay(pos), out var to);
            if (State == GameState.Setup)
            {
                if (hit && to != from) SetupTap(to); else SetupDeselect();
                boardView.SnapPieceBack(from);
                return;
            }
            if (State == GameState.Playing && !Busy && hit && to != from && selected == from
                && rules.LegalMovesFrom(Board, from).Any(m => m.To == to))
            {
                Tap(to);
                return;
            }
            boardView.SnapPieceBack(from);
        }

        /// <summary>One finger or the mouse: drag one of your pieces to move it, drag elsewhere to orbit the camera, tap to select. Two fingers or the wheel: zoom.</summary>
        void HandleGestures()
        {
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) cameraRig.Zoom(scroll * 0.001f);
            }

            var touches = Touchscreen.current;
            if (touches != null && touches.touches[1].press.isPressed && touches.touches[0].press.isPressed)
            {
                float dist = Vector2.Distance(touches.touches[0].position.ReadValue(), touches.touches[1].position.ReadValue());
                if (pinching) cameraRig.Zoom((dist - lastPinch) * 0.004f);
                lastPinch = dist;
                pinching = true;
                dragging = true; // a pinch never ends in a tap
                pieceCandidate = -1;
                CancelPieceDrag();
                return;
            }
            pinching = false;

            // The turn moved on (timer, AI) while a piece was held: put it back.
            if (pieceDragging && ((State != GameState.Playing && State != GameState.Setup) || (State == GameState.Playing && Busy))) CancelPieceDrag();

            // Pointer.current = "jari atau mouse yang sedang dipakai" (satu jalur untuk keduanya,
            // jadi kode yang sama jalan di HP maupun di Editor dengan mouse).
            // Gestur dihitung sendiri dari tiga momen: ditekan, ditahan, dilepas.
            var pointer = Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();    // posisi sentuhan di layar, dalam piksel
            if (pointer.press.wasPressedThisFrame)
            {
                // Presses that start on a button or panel belong to the UI, not the board or camera.
                // EventSystem = sistem Unity untuk klik pada UI. IsPointerOverGameObject() true
                // kalau jari sedang di atas tombol/panel UI, jadi sentuhan itu bukan untuk papan.
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.IsPointerOverGameObject()) { pressing = false; return; }
                pressing = true; dragging = false;
                pressStart = lastPointer = pos;
                pieceCandidate = FindDraggablePiece(pos);
            }
            else if (pressing && pointer.press.isPressed)
            {
                float threshold = DragThresholdInches * (Screen.dpi > 0 ? Screen.dpi : 160f);
                if (!dragging && (pos - pressStart).magnitude > threshold)
                {
                    dragging = true;
                    if (pieceCandidate >= 0) BeginPieceDrag(pieceCandidate);
                }
                if (dragging && State != GameState.Paused)
                {
                    if (pieceDragging) UpdatePieceDrag(pos);
                    else cameraRig.Rotate(pos - lastPointer);
                }
                lastPointer = pos;
            }
            else if (pressing && pointer.press.wasReleasedThisFrame)
            {
                pressing = false;
                pieceCandidate = -1;
                if (pieceDragging) { EndPieceDrag(pos); return; }
                if (dragging || transitioning) return;
                bool hit = boardView.TryGetPoint(cam.ScreenPointToRay(pos), out var point);
                if (State == GameState.Setup) { if (hit) SetupTap(point); else SetupDeselect(); return; }
                if (State != GameState.Playing || Busy || IsAiTurn) return;
                if (hit) Tap(point);
                else Deselect();
            }
        }

        public void CycleDifficulty()
        {
            AiDifficulty = (Difficulty)(((int)AiDifficulty + 1) % 3);
            PlayerPrefs.SetInt(DifficultyPref, (int)AiDifficulty);
            hud.SetDifficulty(AiDifficulty);
        }

        /// <summary>Handles a tap on a board point; public so it can be driven from tests and the CLI.</summary>
        public void Tap(int point)
        {
            if (State != GameState.Playing || Busy || IsAiTurn) return;
            if (commanderTargeting)
            {
                var valid = rules.CommanderTargets(Board, commanderChosen);
                if (!valid.Contains(point)) { CancelCommanderTargeting("DIBATALKAN"); return; }
                commanderChosen.Add(point);
                var c = Board.CommanderOf(Board.ToMove).Value;
                if (commanderChosen.Count < CommanderBook.TargetCount(c))
                {
                    boardView.ShowDestinations(rules.CommanderTargets(Board, commanderChosen));
                    hud.NudgeHint(CommanderBook.TargetPrompt(c, commanderChosen.Count));
                    return;
                }
                commanderTargeting = false;
                StartCoroutine(CommanderCast(commanderChosen.ToArray()));
                return;
            }
            if (targeting)
            {
                // Santet: the tap picks the target, or cancels.
                targeting = false;
                if (selected.HasValue && rules.CastTargets(Board, selected.Value).Contains(point)) { DoCast(selected.Value, point); return; }
                hud.NudgeHint("SANTET DIBATALKAN");
                if (selected.HasValue) Select(selected.Value, rules.LegalMoves(Board).Where(m => m.From == selected.Value).Select(m => m.To).Distinct());
                return;
            }
            var legal = rules.LegalMoves(Board);

            if (selected.HasValue)
            {
                var candidates = legal.Where(m => m.From == selected.Value && m.To == point).ToList();
                if (candidates.Count > 0)
                {
                    // Two chains can end on the same point; take the one that captures more.
                    var move = candidates.OrderByDescending(m => m.Captured.Count).First();
                    turnRoutine = StartCoroutine(PlayTurn(move));
                    return;
                }
            }

            if (Board.Owner(point) == Board.ToMove)
            {
                // Your piece: select it (even if it can't move, its ajian may still be usable).
                var mine = legal.Where(m => m.From == point).ToList();
                Select(point, mine.Select(m => m.To).Distinct());
                if (mine.Count == 0)
                {
                    boardView.PieceAt(point)?.Shake();
                    hud.NudgeHint(Board[point].FrozenTurns > 0 ? "TERKENA SANTET" : Board.ForcedPiece >= 0 ? "SAIPI ANGIN: GERAKKAN PENDEKAR" : "BIDAK INI TIDAK BISA JALAN");
                }
                return;
            }

            Deselect();
        }

        void Select(int point, System.Collections.Generic.IEnumerable<int> destinations)
        {
            if (selected.HasValue && selected.Value != point) boardView.PieceAt(selected.Value)?.SetSelected(false);
            selected = point;
            boardView.PieceAt(point)?.SetSelected(true);
            boardView.ShowDestinations(destinations);
            var piece = Board[point];
            if (piece.IsDam) hud.HideAjian();
            else hud.ShowAjian(AjianBook.For(piece.Role), rules.CastBlocker(Board, point));
        }

        void Deselect()
        {
            if (selected.HasValue) boardView.PieceAt(selected.Value)?.SetSelected(false);
            selected = null;
            targeting = false;
            commanderTargeting = false;
            commanderChosen.Clear();
            boardView.ClearDestinations();
            hud.HideAjian();
        }

        // ---------- ajian

        bool targeting;
        bool? pancasonaAnswer;

        void PressAjian()
        {
            if (State != GameState.Playing || Busy || IsAiTurn || !selected.HasValue) return;
            int caster = selected.Value;
            var blocker = rules.CastBlocker(Board, caster);
            if (blocker != null) { hud.NudgeHint(blocker.ToUpperInvariant()); return; }
            var ajian = AjianBook.For(Board[caster].Role);
            if (AjianBook.NeedsTarget(ajian))
            {
                targeting = true;
                boardView.ShowDestinations(rules.CastTargets(Board, caster));
                hud.NudgeHint("PILIH TARGET " + AjianBook.Name(ajian).ToUpperInvariant());
                return;
            }
            DoCast(caster, caster);
        }

        void DoCast(int caster, int target)
        {
            var result = rules.Cast(Board, caster, target);
            Debug.Log($"[Pamor] {result.Caster}{(HasAi && result.Caster == AiSide ? " (AI)" : "")} ajian {AjianBook.Name(result.Ajian)} on {BoardGraph.Name(target)}");
            StartCoroutine(PlayCast(result));
        }

        /// <summary>One short line for the battle log.</summary>
        string LogLine(Side mover, Move move, MoveResult result)
        {
            string who = mover == Side.Hitam ? "<color=#D9A441>H</color>" : "<color=#C0553F>M</color>";
            string line = $"{who} {move}";
            if (result.CapturedPieces.Count > 0) line += " makan " + string.Join(", ", result.CapturedPieces.Select(p => p.IsDam ? "Dam" : RoleWord(p.Role)));
            if (result.Burned >= 0) line += " · dibakar";
            if (result.Promoted) line += " · jadi Dam";
            if (result.PatihRevived >= 0) line += " · Pancasona";
            return line;
        }

        static string RoleWord(Role r) => r == Role.TelikSandi ? "Telik Sandi" : r.ToString();

        IEnumerator PlayCast(CastResult result)
        {
            AvatarOf(result.Caster).Play(CommanderArt.Cast);
            hud.AddLog($"{(result.Caster == Side.Hitam ? "<color=#D9A441>H</color>" : "<color=#C0553F>M</color>")} ajian {AjianBook.Name(result.Ajian)}");
            bool wasBusy = Busy;
            Busy = true;
            hud.NudgeHint("AJIAN " + AjianBook.Name(result.Ajian).ToUpperInvariant() + "!");
            hud.FlashAccent();
            yield return boardView.PlayCast(result);
            boardView.RefreshRoles(Board, Viewer);
            hud.SetTenaga(Board.Tenaga(Side.Hitam), Board.Tenaga(Side.Merah));
            Busy = wasBusy;
            RefreshCommanderPanel();
            // Re-show the caster's moves: an ajian can change them (e.g. Saipi Angin, Supit Urang).
            if (!IsAiTurn && Board.Owner(result.CasterPoint) == Board.ToMove)
                Select(result.CasterPoint, rules.LegalMoves(Board).Where(m => m.From == result.CasterPoint).Select(m => m.To).Distinct());
            else Deselect();
        }

        // ---------- Commander active

        bool commanderTargeting;
        readonly List<int> commanderChosen = new List<int>();

        /// <summary>Shows the Commander panel for the player to move (hidden on the AI's turn).</summary>
        void RefreshCommanderPanel()
        {
            var c = Board.CommanderOf(Board.ToMove);
            if (State != GameState.Playing || IsAiTurn || !c.HasValue) { hud.HideCommander(); return; }
            hud.ShowCommander(c.Value, rules.CommanderBlocker(Board));
        }

        void PressCommander()
        {
            if (State != GameState.Playing || Busy || IsAiTurn) return;
            var blocker = rules.CommanderBlocker(Board);
            var c0 = Board.CommanderOf(Board.ToMove);
            if (blocker != null)
            {
                // The round button has no room for text: explain the active and why it's not ready.
                hud.NudgeHint(c0.HasValue ? $"{CommanderBook.ActiveName(c0.Value).ToUpperInvariant()}: {blocker.ToUpperInvariant()}" : blocker.ToUpperInvariant());
                return;
            }
            var c = Board.CommanderOf(Board.ToMove).Value;
            Deselect();
            commanderChosen.Clear();
            if (CommanderBook.TargetCount(c) == 0) { StartCoroutine(CommanderCast(new int[0])); return; }
            commanderTargeting = true;
            boardView.ShowDestinations(rules.CommanderTargets(Board, commanderChosen));
            hud.NudgeHint(CommanderBook.TargetPrompt(c, 0));
        }

        void CancelCommanderTargeting(string why)
        {
            commanderTargeting = false;
            commanderChosen.Clear();
            boardView.ClearDestinations();
            hud.NudgeHint(why);
        }

        /// <summary>Uses the Commander active (asking about Pancasona if a Kerbau Ngamuk charge would kill a Patih).</summary>
        IEnumerator CommanderCast(int[] targets)
        {
            bool wasBusy = Busy;
            Busy = true;
            boardView.ClearDestinations();
            var caster = Board.ToMove;
            var enemy = Board.Other(caster);
            if (rules.CanUsePancasona(Board, enemy) && rules.CommanderKillsPatih(Board, targets, enemy))
                yield return AskPancasona(enemy);

            var result = rules.CastCommander(Board, targets);
            Debug.Log($"[Pamor] {caster}{(HasAi && caster == AiSide ? " (AI)" : "")} commander {CommanderBook.ActiveName(result.Active.Value)}"
                      + (targets.Length > 0 ? " on " + string.Join(",", targets.Select(BoardGraph.Name)) : "")
                      + (result.CapturedPieces.Count > 0 ? $" [{string.Join(",", result.CapturedPieces.Select(p => p.Role.ToString()))}]" : ""));
            hud.NudgeHint(CommanderBook.ActiveName(result.Active.Value).ToUpperInvariant() + "!");
            AvatarOf(caster).Play(CommanderArt.Cast);
            if (result.CapturedPieces.Count > 0) AvatarOf(enemy).Play(CommanderArt.Hit);
            hud.AddLog($"{(caster == Side.Hitam ? "<color=#D9A441>H</color>" : "<color=#C0553F>M</color>")} {CommanderBook.ActiveName(result.Active.Value)}"
                       + (result.CapturedPieces.Count > 0 ? $" makan {result.CapturedPieces.Count}" : ""));
            hud.FlashAccent();
            yield return boardView.PlayCast(result);
            if (result.PatihRevived >= 0) boardView.Build(Board);
            boardView.RefreshRoles(Board, Viewer);
            hud.SetCounts(Board.CountPieces(Side.Hitam), Board.CountPieces(Side.Merah));
            hud.SetTenaga(Board.Tenaga(Side.Hitam), Board.Tenaga(Side.Merah));
            Busy = wasBusy;
            if (CheckEnd()) yield break;
            RefreshTurnHints();
            RefreshCommanderPanel();
        }

        IEnumerator AskPancasona(Side side)
        {
            bool use;
            if (HasAi && side == AiSide) use = true;
            else
            {
                pancasonaAnswer = null;
                hud.ShowPancasonaPrompt(side, true);
                while (pancasonaAnswer == null) yield return null;
                hud.ShowPancasonaPrompt(side, false);
                use = pancasonaAnswer.Value;
            }
            if (use) { rules.UsePancasona(Board, side); Debug.Log($"[Pamor] {side} uses Pancasona"); }
        }

        /// <summary>Ends the match if someone has won or it's a draw; true if it ended.</summary>
        bool CheckEnd()
        {
            Winner = rules.Winner(Board);
            IsDraw = !Winner.HasValue && rules.IsDraw(Board);
            if (!Winner.HasValue && !IsDraw) return false;
            Debug.Log($"[Pamor] {(IsDraw ? "draw" : Winner + " wins")} after {MovesPlayed} moves");
            boardView.RefreshRoles(Board, Viewer, revealAll: true);
            hud.HideCommander();
            hud.HideAjian();
            hud.SetTimer(-1);
            if (Winner.HasValue)
            {
                AvatarOf(Winner.Value).Play(CommanderArt.Victory);
                AvatarOf(Board.Other(Winner.Value)).Play(CommanderArt.Defeat);
            }
            EnterState(GameState.Ended);
            StartCoroutine(hud.PlayEnd(EndBanner(), MovesPlayed));
            if (Mode == MatchMode.Sayembara && lobby != null) StartCoroutine(AfterRoundMatch());
            return true;
        }

        /// <summary>Before a move resolves: if it would kill a Patih whose side can afford Pancasona, that side decides.</summary>
        IEnumerator OfferPancasona(Move move)
        {
            foreach (var side in new[] { Side.Hitam, Side.Merah })
            {
                if (!rules.CanUsePancasona(Board, side) || !rules.KillsPatih(Board, move, side)) continue;
                bool use;
                if (HasAi && side == AiSide) use = true;
                else
                {
                    pancasonaAnswer = null;
                    hud.ShowPancasonaPrompt(side, true);
                    while (pancasonaAnswer == null) yield return null;
                    hud.ShowPancasonaPrompt(side, false);
                    use = pancasonaAnswer.Value;
                }
                if (use) { rules.UsePancasona(Board, side); Debug.Log($"[Pamor] {side} uses Pancasona"); }
            }
        }

        // PlayTurn: satu giliran penuh, ditulis sebagai coroutine supaya urutannya terbaca seperti cerita:
        //   1) tandai sibuk  2) (tanya Pancasona kalau perlu)  3) minta Rules menerapkan langkah ke Board
        //   4) animasikan di layar  5) perbarui HUD  6) cek menang/seri  7) lanjut ke giliran berikutnya.
        // Bagian penting: Rules.Apply mengubah DATA seketika; animasinya baru berjalan sesudahnya.
        // "Busy = true" mengunci sentuhan selama animasi supaya pemain tidak bisa menekan hal lain.
        IEnumerator PlayTurn(Move move)
        {
            Busy = true;
            var mover = Board.ToMove;
            boardView.PieceAt(move.From)?.SetSelected(false);
            selected = null;
            targeting = false;
            hud.HideAjian();
            hud.HideCommander();
            yield return OfferPancasona(move);
            var result = rules.Apply(Board, move);
            MovesPlayed++;
            Debug.Log($"[Pamor] {mover}{(HasAi && mover == AiSide ? " (AI)" : "")} {move}"
                      + (result.Promoted ? " = Dam" : "")
                      + (result.Burned >= 0 ? $" (dibakar {BoardGraph.Name(result.Burned)})" : "")
                      + (result.Demoted.Count > 0 ? $" (turun pangkat {string.Join(",", result.Demoted.Select(BoardGraph.Name))})" : "")
                      + (result.CapturedPieces.Count > 0 ? $" [{string.Join(",", result.CapturedPieces.Select(p => p.IsDam ? "Dam" : p.Role.ToString()))}]" : "")
                      + (result.Shield >= 0 ? $" (perisai {BoardGraph.Name(result.Shield)})" : "")
                      + (result.BonusMove ? " (combo)" : ""));
            hud.AddLog(LogLine(mover, move, result));
            if (result.CapturedPieces.Count > 0) AvatarOf(Board.Other(mover)).Play(CommanderArt.Hit);
            else if (result.Burned >= 0) AvatarOf(mover).Play(CommanderArt.Hit);
            yield return boardView.PlayMove(result);
            if (result.PatihRevived >= 0) boardView.Build(Board); // Pancasona: the Patih reappears at its start
            boardView.RefreshRoles(Board, Viewer);
            hud.SetCounts(Board.CountPieces(Side.Hitam), Board.CountPieces(Side.Merah));
            hud.SetTenaga(Board.Tenaga(Side.Hitam), Board.Tenaga(Side.Merah));
            bool patihFell = result.CapturedPieces.Any(p => p.Role == Role.Patih) || (result.Burned >= 0 && result.BurnedPiece.Role == Role.Patih);
            string note = result.PatihRevived >= 0 ? "PANCASONA! PATIH HIDUP LAGI"
                        : result.Guarded.Count > 0 ? "PENDEKAR JAKA TINGKIR SELAMAT"
                        : result.ExtraMove ? "SAIPI ANGIN: GERAK SEKALI LAGI"
                        : patihFell ? "PATIH GUGUR!"
                        : result.Burned >= 0 ? "DIBAKAR! TIDAK MAKAN"
                        : result.SenopatiFell.Count > 0 ? "SENOPATI GUGUR: PRAJURIT TIDAK BISA MAKAN"
                        : result.BonusMove ? "COMBO! LANGKAH BONUS"
                        : result.Shield >= 0 ? "DUKUN MELINDUNGI"
                        : result.Demoted.Count > 0 ? "DAM TURUN PANGKAT"
                        : null;

            Busy = false;
            if (CheckEnd()) yield break;
            if (Mode == MatchMode.LocalPvP && Board.ToMove != mover)
            {
                // Pass the phone: cover the board so the next player can't see the other army's roles.
                pendingNote = note;
                ShowHandoff(Board.ToMove, BeginTurnView);
                yield break;
            }
            RefreshTurnHints();
            if (note != null) hud.NudgeHint(note);
            // Saipi Angin's second move: keep the Pendekar selected for the player.
            if (result.ExtraMove && !IsAiTurn && Board.ForcedPiece >= 0)
                Select(Board.ForcedPiece, rules.LegalMoves(Board).Select(m => m.To).Distinct());
            if (IsAiTurn) turnRoutine = StartCoroutine(AiTurn());
        }

        // ---------- Fase Susun and pass-the-phone

        readonly Formation[] formations = new Formation[2];
        Side setupSide;
        int? setupSelected;
        System.Action afterHandoff;
        string pendingNote;

        static int SideIndex(Side s) => s == Side.Hitam ? 0 : 1;
        Formation FormationOf(Side s) => formations[SideIndex(s)] ??= Formation.Default(s);

        /// <summary>Starts a side's secret arrangement on the live board.</summary>
        void BeginSetup(Side side)
        {
            setupSide = side;
            setupSelected = null;
            Board = Board.CreateStandard(FormationOf(Side.Hitam), FormationOf(Side.Merah));
            ApplyCommandersAndBlockers(Board);
            boardView.Build(Board);
            boardView.RefreshRoles(Board, side);
            boardView.ClearMustCapture();
            cameraRig.FaceSide(side);
            hud.SetSetup(side, commanders[SideIndex(side)] == Commander.Sangkuriang
                ? "TUKAR BIDAK · KETUK TITIK TENGAH UNTUK 2 PENGHALANG"
                : "KETUK DUA BIDAKMU UNTUK MENUKAR POSISI");
            hud.SetRoleInfo("Ketuk bidakmu untuk melihat perannya.");
            EnterState(GameState.Setup);
        }

        // ---------- Commander select

        readonly Commander?[] commanders = new Commander?[2];
        readonly List<int>[] blockers = { new List<int>(), new List<int>() };

        void BeginCommanderSelect(Side side)
        {
            setupSide = side;
            hud.SetCommanderSelect(side);
            EnterState(GameState.CommanderSelect);
        }

        /// <summary>The army a side brings: the standard 16, or a Sayembara player's upgraded army.</summary>
        IReadOnlyDictionary<Role, int> ArmyOf(Side side)
        {
            if (Mode != MatchMode.Sayembara || lobby == null) return RoleRules.ArmyCounts;
            return side == Side.Hitam ? lobby.Human.Army() : opponent?.Army() ?? (IReadOnlyDictionary<Role, int>)RoleRules.ArmyCounts;
        }

        public void ChooseCommander(Commander c)
        {
            if (State != GameState.CommanderSelect) return;
            if (Mode == MatchMode.Sayembara && lobby == null) { StartLobby(c); return; }
            commanders[SideIndex(setupSide)] = c;
            if (c != Commander.Sangkuriang) blockers[SideIndex(setupSide)].Clear();
            Debug.Log($"[Pamor] {setupSide} chooses {CommanderBook.Name(c)}");
            BeginSetup(setupSide);
        }

        /// <summary>The AI's Commander: random each match; a Sangkuriang AI drops its 2 blockers on random free centre points.</summary>
        void PickAiCommander()
        {
            var rng = new System.Random();
            var c = CommanderBook.All[rng.Next(CommanderBook.All.Length)];
            commanders[SideIndex(AiSide)] = c;
            var list = blockers[SideIndex(AiSide)];
            list.Clear();
            if (c != Commander.Sangkuriang) return;
            var centre = Board.AllPoints().Where(p => BoardGraph.Xs[p] == 4 && !blockers[SideIndex(Board.Other(AiSide))].Contains(p)).OrderBy(_ => rng.Next()).ToList();
            list.AddRange(centre.Take(CommanderBook.SangkuriangBlockers));
        }

        CommanderAvatar[] avatars;

        CommanderAvatar AvatarOf(Side s)
        {
            if (avatars == null)
            {
                var root = new GameObject("Commanders").transform;
                root.SetParent(transform, false);
                avatars = new CommanderAvatar[2];
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject(i == 0 ? "Hitam Commander" : "Merah Commander");
                    go.transform.SetParent(root, false);
                    avatars[i] = go.AddComponent<CommanderAvatar>();
                }
            }
            return avatars[s == Side.Hitam ? 0 : 1];
        }

        void ApplyCommandersAndBlockers(Board board)
        {
            AvatarOf(Side.Hitam).Show(Side.Hitam, commanders[0]);
            AvatarOf(Side.Merah).Show(Side.Merah, commanders[1]);
            foreach (var side in new[] { Side.Hitam, Side.Merah })
            {
                board.SetCommander(side, commanders[SideIndex(side)]);
                foreach (var p in blockers[SideIndex(side)]) board.SetBlocker(p, true);
            }
            hud.SetCommanders(commanders[0], commanders[1]);
        }

        /// <summary>Tap during Fase Susun: select one of your pieces, then another to swap their roles.</summary>
        public void SetupTap(int point)
        {
            if (State != GameState.Setup) return;
            // Sangkuriang: tap a free centre point to place or lift one of 2 blockers.
            if (commanders[SideIndex(setupSide)] == Commander.Sangkuriang && BoardGraph.Xs[point] == 4 && !Board[point].Exists)
            {
                var mine = blockers[SideIndex(setupSide)];
                bool theirs = blockers[SideIndex(Board.Other(setupSide))].Contains(point);
                if (mine.Contains(point)) mine.Remove(point);
                else if (!theirs && mine.Count < CommanderBook.SangkuriangBlockers) mine.Add(point);
                else { hud.NudgeHint("PENGHALANG MAKS. 2"); return; }
                Board.SetBlocker(point, mine.Contains(point));
                boardView.RefreshRoles(Board, setupSide);
                return;
            }
            if (Board.Owner(point) != setupSide) { SetupDeselect(); return; }
            hud.SetRoleInfo(RoleInfo(Board[point].Role));
            if (!setupSelected.HasValue) { setupSelected = point; boardView.PieceAt(point)?.SetSelected(true); return; }
            int a = setupSelected.Value;
            SetupDeselect();
            if (a == point) return;

            var f = FormationOf(setupSide);
            f.Swap(a, point);
            Board.Place(a, setupSide, f.Roles[a]);
            Board.Place(point, setupSide, f.Roles[point]);
            boardView.RefreshRoles(Board, setupSide);
            boardView.PieceAt(a)?.Shake();
            boardView.PieceAt(point)?.Shake();
            hud.SetRoleInfo(RoleInfo(f.Roles[point]));
        }

        void SetupDeselect()
        {
            if (setupSelected.HasValue) boardView.PieceAt(setupSelected.Value)?.SetSelected(false);
            setupSelected = null;
        }

        public void RandomizeSetup()
        {
            if (State != GameState.Setup) return;
            formations[SideIndex(setupSide)] = Formation.Random(setupSide, ArmyOf(setupSide), new System.Random());
            BeginSetup(setupSide);
        }

        public void ConfirmSetup()
        {
            if (State != GameState.Setup) return;
            if (!FormationOf(setupSide).IsValid(setupSide, ArmyOf(setupSide), out var error)) { hud.SetSetup(setupSide, error); return; }
            SetupDeselect();
            if (Mode == MatchMode.LocalPvP && setupSide == Side.Hitam) { ShowHandoff(Side.Merah, () => BeginCommanderSelect(Side.Merah)); return; }
            ResetMatch();
            if (Mode == MatchMode.LocalPvP) { ShowHandoff(Side.Hitam, BeginTurnView); return; }
            cameraRig.FaceSide(Viewer);
            EnterState(GameState.Playing);
            RefreshTurnHints();
            if (IsAiTurn) turnRoutine = StartCoroutine(AiTurn());
        }

        void ShowHandoff(Side next, System.Action after)
        {
            afterHandoff = after;
            hud.SetHandoff(next);
            EnterState(GameState.Handoff);
        }

        public void ConfirmHandoff()
        {
            if (State != GameState.Handoff) return;
            var after = afterHandoff;
            afterHandoff = null;
            after?.Invoke();
        }

        /// <summary>After a handoff: show the board as the player to move sees it, from their side.</summary>
        void BeginTurnView()
        {
            boardView.RefreshRoles(Board, Viewer);
            cameraRig.FaceSide(Board.ToMove);
            EnterState(GameState.Playing);
            RefreshTurnHints();
            if (pendingNote != null) hud.NudgeHint(pendingNote);
            pendingNote = null;
        }

        static string RoleInfo(Role role)
        {
            switch (role)
            {
                case Role.Patih: return "PATIH\nGerak 1 titik ke segala arah. Kalau Patih gugur, kamu kalah.";
                case Role.Senopati: return "SENOPATI\nGerak sampai 2 titik, tidak mundur. Kalau gugur, Prajurit-mu tidak bisa makan 1 giliran.";
                case Role.Pendekar: return "PENDEKAR\nGerak 1 titik, boleh mundur. Boleh belok saat makan beruntun.";
                case Role.Dukun: return "DUKUN\nTidak bisa makan. Saat digerakkan, melindungi 1 bidak di sebelahnya selama giliran lawan.";
                case Role.TelikSandi: return "TELIK SANDI\nTerlihat seperti Prajurit sampai ia makan atau dimakan.";
                default: return "PRAJURIT\nGerak 1 titik ke depan, serong, atau samping. Jadi Dam di ujung segitiga lawan.";
            }
        }

        /// <summary>Searches on a worker thread so the frame never hitches, with a short minimum "thinking" beat.</summary>
        // BELAJAR UNITY - THREAD vs FRAME:
        // Mencari langkah AI itu hitungan berat (bisa ratusan ribu kemungkinan). Kalau dihitung
        // langsung di Update/coroutine, game MEMBEKU selama beberapa saat. Solusinya: kerjakan di
        // THREAD terpisah (Task.Run), sementara coroutine hanya menunggu tiap frame sampai selesai.
        // ATURAN EMAS: thread terpisah BOLEH menghitung (kode Core murni C#), tapi TIDAK BOLEH
        // menyentuh objek Unity (GameObject, Transform, dst) karena Unity hanya aman diakses dari
        // thread utama. Itu sebabnya AI bekerja pada salinan data (Board.Clone) dan bukan objek Unity.
        IEnumerator AiTurn()
        {
            Busy = true;
            AiThinking = true;
            int id = matchId;
            var snapshot = Board.Clone();    // salinan papan: AI berpikir di salinan, bukan di papan asli
            var ai = Ai.For(AiDifficulty, null, rules.TurnCap > 0 ? new Rules { TurnCap = rules.TurnCap, AutoPancasona = true } : null);
            var task = Task.Run(() => ai.ChooseTurn(snapshot));     // mulai berpikir di thread lain
            float started = Time.time;
            // Tunggu per frame sampai AI selesai DAN jeda minimum lewat (agar terasa seperti berpikir).
            // "yield return null" = lanjutkan di frame berikutnya, jadi game tetap mulus.
            while (!task.IsCompleted || Time.time - started < MinAiThinkSeconds) yield return null;
            AiThinking = false;
            // Pemain bisa menekan "ulang" saat AI berpikir. matchId yang berbeda berarti hasil ini
            // milik match lama dan harus dibuang.
            if (id != matchId) yield break; // the match was reset while the AI was thinking

            if (task.IsFaulted || task.Result == null || task.Result.Move == null)
            {
                Debug.LogError("[Pamor] AI failed to choose a move: " + task.Exception);
                Busy = false;
                yield break;
            }
            var plan = task.Result;
            if (plan.CommanderTargets != null && rules.CommanderBlocker(Board) == null)
            {
                yield return CommanderCast(plan.CommanderTargets);
                if (State != GameState.Playing) yield break; // the active won the game
                yield return new WaitForSeconds(0.5f);
            }
            else if (plan.Caster >= 0 && rules.CastBlocker(Board, plan.Caster) == null)
            {
                DoCast(plan.Caster, plan.Target);
                yield return new WaitForSeconds(0.9f);
            }
            yield return PlayTurn(plan.Move);
        }

        string EndBanner()
        {
            if (IsDraw) return "SERI";
            if (Mode == MatchMode.Sayembara) return Winner == AiSide ? "KALAH RONDE" : "MENANG RONDE";
            if (HasAi) return Winner == AiSide ? "AI MENANG" : "KAMU MENANG";
            return Winner == Side.Hitam ? "HITAM MENANG" : "MERAH MENANG";
        }

        void RefreshTurnHints()
        {
            bool mustCapture = rules.HasAnyCapture(Board);
            string label;
            if (HasAi) label = IsAiTurn ? "GILIRAN AI" : "GILIRANMU";
            else label = Board.ToMove == Side.Hitam ? "GILIRAN HITAM" : "GILIRAN MERAH";
            string hint = IsAiTurn ? "AI BERPIKIR..." : mustCapture ? "WAJIB MAKAN!" : "";
            hud.SetTurn(Board.ToMove, label, hint);
            hud.SetViewer(Viewer);
            int turnNo = Board.Ply / 2 + 1;
            if (Mode == MatchMode.Sayembara && lobby != null) hud.SetRound($"RONDE {lobby.Round}");
            else { hud.SetRound("GILIRAN"); hud.SetTimer(turnNo); }
            RefreshCommanderPanel();
            if (mustCapture && !IsAiTurn) boardView.ShowMustCapture(rules.Captures(Board).Select(m => m.From).Distinct());
            else boardView.ClearMustCapture();
        }

        /// <summary>A real reset path: fresh rules state and a rebuilt board, no leftovers.</summary>
        public void ResetMatch()
        {
            if (turnRoutine != null) StopCoroutine(turnRoutine);
            turnRoutine = null;
            matchId++;
            Busy = false;
            AiThinking = false;
            // Each player's Fase Susun arrangement; the vs-AI opponent arranges itself at random every match.
            // In Sayembara the opponent's army, formation and Commander come from the lobby (StartRoundMatch).
            bool round = Mode == MatchMode.Sayembara && lobby != null && opponent != null;
            if (Mode == MatchMode.VsAi) { formations[SideIndex(AiSide)] = Formation.Random(AiSide, new System.Random()); PickAiCommander(); }
            rules = round ? new Rules { TurnCap = Sayembara.TurnsPerPlayer * 2 } : Rules.Default;
            Board = Board.CreateStandard(FormationOf(Side.Hitam), FormationOf(Side.Merah));
            ApplyCommandersAndBlockers(Board);
            // Sayembara: the away player moves first.
            if (round && humanPairing.Home == 0) Board.SetFirstMover(Side.Merah);
            MovesPlayed = 0;
            Winner = null;
            IsDraw = false;
            selected = null;
            boardView.Build(Board);
            boardView.RefreshRoles(Board, Viewer);
            hud.SetCounts(Board.CountPieces(Side.Hitam), Board.CountPieces(Side.Merah));
            hud.SetTenaga(0, 0);
            hud.HideAjian();
            hud.ShowPancasonaPrompt(Side.Hitam, false);
            hud.ClearLog();
            hud.SetViewer(Viewer);
            RefreshLobbyRoster();
            targeting = false;
            if (round) hud.SetPlayerNames("KAMU", opponent.Name.ToUpperInvariant());
            else if (HasAi) hud.SetPlayerNames("KAMU", "AI " + AiDifficulty.ToString().ToUpperInvariant());
            else hud.SetPlayerNames("HITAM", "MERAH");
            RefreshTurnHints();
        }

        void SetPaused(bool paused)
        {
            if (paused && State != GameState.Playing) return;
            if (!paused && State != GameState.Paused) return;
            EnterState(paused ? GameState.Paused : GameState.Playing);
        }

        // Satu pintu untuk pindah layar. Time.timeScale adalah "kecepatan waktu" global Unity:
        // 1 = normal, 0 = berhenti total (semua animasi berbasis Time.deltaTime ikut berhenti).
        // Itulah cara jeda dibuat. Animasi UI yang harus tetap jalan saat jeda memakai
        // Time.unscaledDeltaTime (lihat parameter "unscaled" di Ease.Tween).
        void EnterState(GameState next)
        {
            State = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            hud.ShowScreen(next);
        }

        public void BeginTransition(GameState next, MatchMode mode)
        {
            if (transitioning) return;
            StartCoroutine(Transition(next, mode));
        }

        IEnumerator Transition(GameState next, MatchMode mode)
        {
            transitioning = true;
            yield return hud.FadeOut();
            Time.timeScale = 1f;
            Mode = mode;
            if (mode != MatchMode.Sayembara) { lobby = null; opponent = null; }
            ResetMatch();
            // Starting a match: Commander, then Fase Susun (last match's arrangement is kept). Sayembara opens its lobby.
            if (next == GameState.Playing && mode == MatchMode.Sayembara) BeginSayembara();
            else if (next == GameState.Playing) BeginCommanderSelect(Side.Hitam);
            else EnterState(next);
            yield return hud.FadeIn();
            transitioning = false;
        }
    }
}
