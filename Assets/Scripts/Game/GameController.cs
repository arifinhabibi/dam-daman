using System.Collections;
using System.Linq;
using Pamor.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pamor
{
    public enum GameState { Title, Playing, Paused, Ended }

    /// <summary>
    /// Local PvP (hotseat) match: owns the rules state, the shell state machine and pointer input.
    /// Works with mouse and touch through the Input System's Pointer.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [SerializeField] BoardView boardView;
        [SerializeField] HudView hud;

        public GameState State { get; private set; } = GameState.Title;
        public Board Board { get; private set; }
        public int MovesPlayed { get; private set; }
        public Side? Winner { get; private set; }
        public bool Busy { get; private set; }

        readonly Rules rules = Rules.Default;
        Square? selected;
        Camera cam;
        Coroutine turnRoutine;
        bool transitioning;

        void Start()
        {
            cam = Camera.main;
            hud.Build();
            hud.ResumePressed += () => SetPaused(false);
            hud.PausePressed += () => SetPaused(true);
            hud.RestartPressed += () => BeginTransition(GameState.Playing);
            hud.TitlePressed += () => BeginTransition(GameState.Title);
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

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            if (transitioning) return;
            if (State == GameState.Title) { BeginTransition(GameState.Playing); return; }
            if (State != GameState.Playing || Busy) return;

            var screen = pointer.position.ReadValue();
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            if (boardView.TryGetSquare(world, out var square)) Tap(square);
            else Deselect();
        }

        /// <summary>Handles a tap on a board square; public so it can be driven from tests and the CLI.</summary>
        public void Tap(Square square)
        {
            if (State != GameState.Playing || Busy) return;
            var legal = rules.LegalMoves(Board);

            if (selected.HasValue)
            {
                var candidates = legal.Where(m => m.From == selected.Value && m.To == square).ToList();
                if (candidates.Count > 0)
                {
                    // Two chains can end on the same square; take the one that captures more.
                    var move = candidates.OrderByDescending(m => m.Captured.Count).First();
                    turnRoutine = StartCoroutine(PlayTurn(move));
                    return;
                }
            }

            if (Board.Owner(Board[square]) == Board.ToMove)
            {
                var mine = legal.Where(m => m.From == square).ToList();
                if (mine.Count > 0)
                {
                    Select(square, mine.Select(m => m.To).Distinct());
                    return;
                }

                Deselect();
                boardView.PieceAt(square)?.Shake();
                bool mustCapture = legal.Count > 0 && legal[0].IsCapture;
                hud.NudgeHint(mustCapture ? "WAJIB MAKAN!" : "BIDAK INI TIDAK BISA JALAN");
                if (mustCapture) boardView.ShowMustCapture(legal.Select(m => m.From).Distinct());
                return;
            }

            Deselect();
        }

        void Select(Square square, System.Collections.Generic.IEnumerable<Square> destinations)
        {
            if (selected.HasValue && selected.Value != square) boardView.PieceAt(selected.Value)?.SetSelected(false);
            selected = square;
            boardView.PieceAt(square)?.SetSelected(true);
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
            boardView.PieceAt(move.From)?.SetSelected(false);
            selected = null;
            var result = Board.Apply(move);
            MovesPlayed++;
            Debug.Log($"[Pamor] {(Board.ToMove == Side.Hitam ? "Merah" : "Hitam")} {move}{(result.Promoted ? " = Dam" : "")}");
            yield return boardView.PlayMove(move, result.Promoted);
            hud.SetCounts(Board.CountPieces(Side.Hitam), Board.CountPieces(Side.Merah));

            Winner = rules.Winner(Board);
            Busy = false;
            if (Winner.HasValue)
            {
                Debug.Log($"[Pamor] {Winner} wins after {MovesPlayed} moves");
                EnterState(GameState.Ended);
                yield return hud.PlayWin(Winner.Value, MovesPlayed);
                yield break;
            }
            RefreshTurnHints();
        }

        void RefreshTurnHints()
        {
            bool mustCapture = rules.HasAnyCapture(Board);
            hud.SetTurn(Board.ToMove, mustCapture);
            if (mustCapture) boardView.ShowMustCapture(rules.LegalMoves(Board).Select(m => m.From).Distinct());
            else boardView.ClearMustCapture();
        }

        /// <summary>A real reset path: fresh rules state and a rebuilt board, no leftovers.</summary>
        public void ResetMatch()
        {
            if (turnRoutine != null) StopCoroutine(turnRoutine);
            turnRoutine = null;
            Busy = false;
            Board = Board.CreateStandard();
            MovesPlayed = 0;
            Winner = null;
            selected = null;
            boardView.Build(Board);
            hud.SetCounts(12, 12);
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

        void BeginTransition(GameState next)
        {
            if (transitioning) return;
            StartCoroutine(Transition(next));
        }

        IEnumerator Transition(GameState next)
        {
            transitioning = true;
            yield return hud.FadeOut();
            Time.timeScale = 1f;
            ResetMatch();
            EnterState(next);
            yield return hud.FadeIn();
            transitioning = false;
        }
    }
}
