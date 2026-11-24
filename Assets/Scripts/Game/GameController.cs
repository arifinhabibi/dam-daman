using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Pamor.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pamor
{
    public enum GameState { Title, Setup, Handoff, Playing, Paused, Ended }
    public enum MatchMode { LocalPvP, VsAi }

    /// <summary>
    /// Owns one match: rules state, shell state machine, pointer input and the AI opponent.
    /// Local PvP is hotseat on one device; vs AI the player is Hitam (moves first) and the AI is Merah.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        const string DifficultyPref = "pamor.difficulty";
        const float MinAiThinkSeconds = 0.6f;

        [SerializeField] BoardView boardView;
        [SerializeField] HudView hud;
        [SerializeField] CameraRig cameraRig;

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

        readonly Rules rules = Rules.Default;
        int? selected;
        Camera cam;
        Coroutine turnRoutine;
        bool transitioning;
        int matchId;

        bool IsAiTurn => Mode == MatchMode.VsAi && Board.ToMove == AiSide;

        /// <summary>Whose eyes the board is drawn for: the player vs AI; the side to move in hotseat.</summary>
        Side Viewer => Mode == MatchMode.VsAi ? Board.Other(AiSide) : Board.ToMove;

        void Start()
        {
            cam = Camera.main;
            AiDifficulty = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyPref, (int)Difficulty.Sedang), 0, 2);
            hud.Build();
            hud.SetDifficulty(AiDifficulty);
            hud.ResumePressed += () => SetPaused(false);
            hud.PausePressed += () => SetPaused(true);
            hud.RestartPressed += () => BeginTransition(GameState.Playing, Mode);
            hud.TitlePressed += () => BeginTransition(GameState.Title, Mode);
            hud.PlayLocalPressed += () => BeginTransition(GameState.Playing, MatchMode.LocalPvP);
            hud.PlayAiPressed += () => BeginTransition(GameState.Playing, MatchMode.VsAi);
            hud.DifficultyPressed += CycleDifficulty;
            hud.SetupRandomPressed += RandomizeSetup;
            hud.SetupReadyPressed += ConfirmSetup;
            hud.HandoffPressed += ConfirmHandoff;
            ResetMatch();
            EnterState(GameState.Title);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (State == GameState.Playing) SetPaused(true);
                else if (State == GameState.Paused) SetPaused(false);
            }

            HandleGestures();
        }

        // A press that moves less than this (in screen pixels, scaled by DPI) is a tap; more is a camera drag.
        const float DragThresholdInches = 0.08f;
        Vector2 pressStart, lastPointer;
        bool pressing, dragging, pinching;
        float lastPinch;

        /// <summary>One finger or the mouse: tap selects, drag orbits the camera. Two fingers or the wheel: zoom.</summary>
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
                return;
            }
            pinching = false;

            var pointer = Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                // Presses that start on a button or panel belong to the UI, not the board or camera.
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.IsPointerOverGameObject()) { pressing = false; return; }
                pressing = true; dragging = false;
                pressStart = lastPointer = pos;
            }
            else if (pressing && pointer.press.isPressed)
            {
                float threshold = DragThresholdInches * (Screen.dpi > 0 ? Screen.dpi : 160f);
                if (!dragging && (pos - pressStart).magnitude > threshold) dragging = true;
                if (dragging && State != GameState.Paused) cameraRig.Rotate(pos - lastPointer);
                lastPointer = pos;
            }
            else if (pressing && pointer.press.wasReleasedThisFrame)
            {
                pressing = false;
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
                var mine = legal.Where(m => m.From == point).ToList();
                if (mine.Count > 0)
                {
                    Select(point, mine.Select(m => m.To).Distinct());
                    return;
                }

                Deselect();
                boardView.PieceAt(point)?.Shake();
                hud.NudgeHint("BIDAK INI TIDAK BISA JALAN");
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
        }

        void Deselect()
        {
            if (selected.HasValue) boardView.PieceAt(selected.Value)?.SetSelected(false);
            selected = null;
            boardView.ClearDestinations();
        }

        IEnumerator PlayTurn(Move move)
        {
            Busy = true;
            var mover = Board.ToMove;
            boardView.PieceAt(move.From)?.SetSelected(false);
            selected = null;
            var result = rules.Apply(Board, move);
            MovesPlayed++;
            Debug.Log($"[Pamor] {mover}{(Mode == MatchMode.VsAi && mover == AiSide ? " (AI)" : "")} {move}"
                      + (result.Promoted ? " = Dam" : "")
                      + (result.Burned >= 0 ? $" (dibakar {BoardGraph.Name(result.Burned)})" : "")
                      + (result.Demoted.Count > 0 ? $" (turun pangkat {string.Join(",", result.Demoted.Select(BoardGraph.Name))})" : "")
                      + (result.CapturedPieces.Count > 0 ? $" [{string.Join(",", result.CapturedPieces.Select(p => p.IsDam ? "Dam" : p.Role.ToString()))}]" : "")
                      + (result.Shield >= 0 ? $" (perisai {BoardGraph.Name(result.Shield)})" : "")
                      + (result.BonusMove ? " (combo)" : ""));
            yield return boardView.PlayMove(result);
            boardView.RefreshRoles(Board, Viewer);
            hud.SetCounts(Board.CountPieces(Side.Hitam), Board.CountPieces(Side.Merah));
            bool patihFell = result.CapturedPieces.Any(p => p.Role == Role.Patih) || (result.Burned >= 0 && result.BurnedPiece.Role == Role.Patih);
            string note = patihFell ? "PATIH GUGUR!"
                        : result.Burned >= 0 ? "DIBAKAR! TIDAK MAKAN"
                        : result.SenopatiFell.Count > 0 ? "SENOPATI GUGUR: PRAJURIT TIDAK BISA MAKAN"
                        : result.BonusMove ? "COMBO! LANGKAH BONUS"
                        : result.Shield >= 0 ? "DUKUN MELINDUNGI"
                        : result.Demoted.Count > 0 ? "DAM TURUN PANGKAT"
                        : null;

            Winner = rules.Winner(Board);
            IsDraw = !Winner.HasValue && rules.IsDraw(Board);
            Busy = false;
            if (Winner.HasValue || IsDraw)
            {
                Debug.Log($"[Pamor] {(IsDraw ? "draw" : Winner + " wins")} after {MovesPlayed} moves");
                boardView.RefreshRoles(Board, Viewer, revealAll: true);
                EnterState(GameState.Ended);
                yield return hud.PlayEnd(EndBanner(), MovesPlayed);
                yield break;
            }
            if (Mode == MatchMode.LocalPvP && !result.BonusMove)
            {
                // Pass the phone: cover the board so the next player can't see the other army's roles.
                pendingNote = note;
                ShowHandoff(Board.ToMove, BeginTurnView);
                yield break;
            }
            RefreshTurnHints();
            if (note != null) hud.NudgeHint(note);
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
            boardView.Build(Board);
            boardView.RefreshRoles(Board, side);
            boardView.ClearMustCapture();
            cameraRig.FaceSide(side);
            hud.SetSetup(side, "KETUK DUA BIDAKMU UNTUK MENUKAR POSISI");
            hud.SetRoleInfo("Ketuk bidakmu untuk melihat perannya.");
            EnterState(GameState.Setup);
        }

        /// <summary>Tap during Fase Susun: select one of your pieces, then another to swap their roles.</summary>
        public void SetupTap(int point)
        {
            if (State != GameState.Setup) return;
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
            formations[SideIndex(setupSide)] = Formation.Random(setupSide, new System.Random());
            BeginSetup(setupSide);
        }

        public void ConfirmSetup()
        {
            if (State != GameState.Setup) return;
            if (!FormationOf(setupSide).IsValid(setupSide, out var error)) { hud.SetSetup(setupSide, error); return; }
            SetupDeselect();
            if (Mode == MatchMode.LocalPvP && setupSide == Side.Hitam) { ShowHandoff(Side.Merah, () => BeginSetup(Side.Merah)); return; }
            ResetMatch();
            if (Mode == MatchMode.LocalPvP) { ShowHandoff(Side.Hitam, BeginTurnView); return; }
            cameraRig.FaceSide(Viewer);
            EnterState(GameState.Playing);
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
        IEnumerator AiTurn()
        {
            Busy = true;
            AiThinking = true;
            int id = matchId;
            var snapshot = Board.Clone();
            var ai = Ai.For(AiDifficulty);
            var task = Task.Run(() => ai.ChooseMove(snapshot));
            float started = Time.time;
            while (!task.IsCompleted || Time.time - started < MinAiThinkSeconds) yield return null;
            AiThinking = false;
            if (id != matchId) yield break; // the match was reset while the AI was thinking

            if (task.IsFaulted || task.Result == null)
            {
                Debug.LogError("[Pamor] AI failed to choose a move: " + task.Exception);
                Busy = false;
                yield break;
            }
            yield return PlayTurn(task.Result);
        }

        string EndBanner()
        {
            if (IsDraw) return "SERI";
            if (Mode == MatchMode.VsAi) return Winner == AiSide ? "AI MENANG" : "KAMU MENANG";
            return Winner == Side.Hitam ? "HITAM MENANG" : "MERAH MENANG";
        }

        void RefreshTurnHints()
        {
            bool mustCapture = rules.HasAnyCapture(Board);
            string label;
            if (Mode == MatchMode.VsAi) label = IsAiTurn ? "GILIRAN AI" : "GILIRANMU";
            else label = Board.ToMove == Side.Hitam ? "GILIRAN HITAM" : "GILIRAN MERAH";
            string hint = IsAiTurn ? "AI BERPIKIR..." : mustCapture ? "WAJIB MAKAN!" : "";
            hud.SetTurn(Board.ToMove, label, hint);
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
            // Each player's Fase Susun arrangement; the AI arranges itself at random every match.
            if (Mode == MatchMode.VsAi) formations[SideIndex(AiSide)] = Formation.Random(AiSide, new System.Random());
            Board = Board.CreateStandard(FormationOf(Side.Hitam), FormationOf(Side.Merah));
            MovesPlayed = 0;
            Winner = null;
            IsDraw = false;
            selected = null;
            boardView.Build(Board);
            boardView.RefreshRoles(Board, Viewer);
            hud.SetCounts(Board.CountPieces(Side.Hitam), Board.CountPieces(Side.Merah));
            if (Mode == MatchMode.VsAi) hud.SetPlayerNames("KAMU", "AI " + AiDifficulty.ToString().ToUpperInvariant());
            else hud.SetPlayerNames("HITAM", "MERAH");
            RefreshTurnHints();
        }

        void SetPaused(bool paused)
        {
            if (paused && State != GameState.Playing) return;
            if (!paused && State != GameState.Paused) return;
            EnterState(paused ? GameState.Paused : GameState.Playing);
        }

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
            ResetMatch();
            // Starting (or restarting) a match always goes through Fase Susun, keeping last match's arrangement.
            if (next == GameState.Playing) BeginSetup(Side.Hitam);
            else EnterState(next);
            yield return hud.FadeIn();
            transitioning = false;
        }
    }
}
