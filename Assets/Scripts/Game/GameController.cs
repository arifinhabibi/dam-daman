using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Pamor.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pamor
{
    public enum GameState { Title, Playing, Paused, Ended }
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
                if (dragging) return;
                if (transitioning || State != GameState.Playing || Busy || IsAiTurn) return;
                if (boardView.TryGetPoint(cam.ScreenPointToRay(pos), out var point)) Tap(point);
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

            if (Board.Owner(Board[point]) == Board.ToMove)
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
                      + (result.BonusMove ? " (combo)" : ""));
            yield return boardView.PlayMove(result);
            hud.SetCounts(Board.CountPieces(Side.Hitam), Board.CountPieces(Side.Merah));
            string note = result.Burned >= 0 ? "DIBAKAR! TIDAK MAKAN"
                        : result.BonusMove ? "COMBO! LANGKAH BONUS"
                        : result.Demoted.Count > 0 ? "DAM TURUN PANGKAT"
                        : null;

            Winner = rules.Winner(Board);
            IsDraw = !Winner.HasValue && rules.IsDraw(Board);
            Busy = false;
            if (Winner.HasValue || IsDraw)
            {
                Debug.Log($"[Pamor] {(IsDraw ? "draw" : Winner + " wins")} after {MovesPlayed} moves");
                EnterState(GameState.Ended);
                yield return hud.PlayEnd(EndBanner(), MovesPlayed);
                yield break;
            }
            RefreshTurnHints();
            if (note != null) hud.NudgeHint(note);
            if (IsAiTurn) turnRoutine = StartCoroutine(AiTurn());
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
            Board = Board.CreateStandard();
            MovesPlayed = 0;
            Winner = null;
            IsDraw = false;
            selected = null;
            boardView.Build(Board);
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
            if (next == GameState.Playing) cameraRig.ResetView();
            EnterState(next);
            yield return hud.FadeIn();
            transitioning = false;
            if (State == GameState.Playing && IsAiTurn) turnRoutine = StartCoroutine(AiTurn());
        }
    }
}
