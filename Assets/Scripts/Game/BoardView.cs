using System.Collections;
using System.Collections.Generic;
using Pamor.Core;
using UnityEngine;

namespace Pamor
{
    /// <summary>Draws the board and pieces from a <see cref="Board"/>, shows hints, and animates moves.</summary>
    public class BoardView : MonoBehaviour
    {
        [SerializeField] Sprite squareSprite;
        [SerializeField] Sprite discSprite;
        [SerializeField] Material spriteMaterial;
        public FeelTunables feel = new FeelTunables();

        readonly Dictionary<Square, PieceView> pieces = new Dictionary<Square, PieceView>();
        readonly List<SpriteRenderer> destinationMarkers = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> mustCaptureRings = new List<SpriteRenderer>();
        Transform piecesRoot, hintsRoot;
        SpriteRenderer lastFrom, lastTo;

        public static Vector3 ToLocal(Square s) => new Vector3(s.Col - 3.5f, s.Row - 3.5f, 0f);

        public bool TryGetSquare(Vector3 worldPoint, out Square square)
        {
            var local = transform.InverseTransformPoint(worldPoint);
            square = new Square(Mathf.FloorToInt(local.y + 4f), Mathf.FloorToInt(local.x + 4f));
            return square.IsOnBoard;
        }

        public void Build(Board board)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            pieces.Clear(); destinationMarkers.Clear(); mustCaptureRings.Clear();

            Quad(transform, "Frame", squareSprite, Palette.FrameWood, Vector3.zero, 8.8f, 0);
            Quad(transform, "Frame Inlay", squareSprite, Palette.AccentPrada, Vector3.zero, 8.2f, 1);
            var squares = new GameObject("Squares").transform; squares.SetParent(transform, false);
            foreach (var s in Board.AllSquares())
                Quad(squares, "Sq " + s, squareSprite, s.IsDark ? Palette.Sogan : Palette.Kelir, ToLocal(s), 1f, 2);

            hintsRoot = new GameObject("Hints").transform; hintsRoot.SetParent(transform, false);
            lastFrom = Quad(hintsRoot, "Last From", squareSprite, WithAlpha(Palette.AccentPrada, 0.28f), Vector3.zero, 1f, 3);
            lastTo = Quad(hintsRoot, "Last To", squareSprite, WithAlpha(Palette.AccentPrada, 0.4f), Vector3.zero, 1f, 3);
            lastFrom.enabled = lastTo.enabled = false;

            piecesRoot = new GameObject("Pieces").transform; piecesRoot.SetParent(transform, false);
            foreach (var s in Board.AllSquares())
            {
                int p = board[s];
                if (p == 0) continue;
                var view = PieceView.Create(piecesRoot, discSprite, spriteMaterial, Board.Owner(p) == Side.Hitam ? Palette.PieceA : Palette.PieceB, feel);
                view.name = (Board.Owner(p) == Side.Hitam ? "Hitam " : "Merah ") + s;
                view.transform.localPosition = ToLocal(s);
                view.SetDam(Board.IsDam(p), animate: false);
                pieces[s] = view;
            }
        }

        public PieceView PieceAt(Square s) => pieces.TryGetValue(s, out var v) ? v : null;

        public void ShowDestinations(IEnumerable<Square> squares)
        {
            ClearDestinations();
            foreach (var s in squares)
            {
                var dot = Quad(hintsRoot, "Dest " + s, discSprite, WithAlpha(Palette.AccentPrada, 0.85f), ToLocal(s), 0.32f, 4);
                destinationMarkers.Add(dot);
            }
        }

        public void ClearDestinations()
        {
            foreach (var m in destinationMarkers) if (m) Destroy(m.gameObject);
            destinationMarkers.Clear();
        }

        public void ShowMustCapture(IEnumerable<Square> squares)
        {
            ClearMustCapture();
            foreach (var s in squares)
            {
                var ring = Quad(hintsRoot, "Must " + s, discSprite, WithAlpha(Palette.AccentPrada, 0.6f), ToLocal(s), 0.98f, 3);
                mustCaptureRings.Add(ring);
            }
        }

        public void ClearMustCapture()
        {
            foreach (var r in mustCaptureRings) if (r) Destroy(r.gameObject);
            mustCaptureRings.Clear();
        }

        void Update()
        {
            // Hints pulse so they read as interactive; scaled time, so they freeze under pause.
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI / feel.pulsePeriod);
            foreach (var m in destinationMarkers)
                if (m) m.transform.localScale = Vector3.one * Mathf.Lerp(0.26f, 0.36f, k);
            foreach (var r in mustCaptureRings)
                if (r) r.color = WithAlpha(Palette.AccentPrada, Mathf.Lerp(0.3f, 0.75f, k));
        }

        /// <summary>Plays a whole move: one hop per path step, each capture popping as it is jumped.</summary>
        public IEnumerator PlayMove(Move move, bool promoted)
        {
            ClearDestinations();
            ClearMustCapture();
            var view = pieces[move.From];
            pieces.Remove(move.From);

            for (int i = 1; i < move.Path.Count; i++)
            {
                yield return view.Hop(ToLocal(move.Path[i]));
                if (move.IsCapture)
                {
                    var cap = move.Captured[i - 1];
                    if (pieces.TryGetValue(cap, out var victim))
                    {
                        pieces.Remove(cap);
                        Burst(ToLocal(cap), victim.name.StartsWith("Hitam") ? Palette.PieceA : Palette.PieceB);
                        StartCoroutine(victim.Captured());
                    }
                }
            }

            pieces[move.To] = view;
            view.name = (view.name.StartsWith("Hitam") ? "Hitam " : "Merah ") + move.To;
            if (promoted)
            {
                view.SetDam(true, animate: true);
                Burst(ToLocal(move.To), Palette.AccentPrada, 16);
                yield return new WaitForSeconds(feel.crownDuration);
            }

            lastFrom.transform.localPosition = ToLocal(move.From);
            lastTo.transform.localPosition = ToLocal(move.To);
            lastFrom.enabled = lastTo.enabled = true;
        }

        void Burst(Vector3 at, Color color, int count = -1)
        {
            if (count < 0) count = feel.captureParticles;
            for (int i = 0; i < count; i++)
            {
                float ang = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                var sr = Quad(hintsRoot, "Spark", discSprite, i % 3 == 0 ? Palette.AccentPrada : color, at, Random.Range(0.08f, 0.14f), 40);
                StartCoroutine(Spark(sr, at, at + dir * Random.Range(0.45f, 0.8f)));
            }
        }

        IEnumerator Spark(SpriteRenderer sr, Vector3 from, Vector3 to)
        {
            var c = sr.color;
            float s = sr.transform.localScale.x;
            yield return Ease.Tween(0.35f, t =>
            {
                sr.transform.localPosition = Vector3.LerpUnclamped(from, to, t);
                sr.transform.localScale = Vector3.one * s * (1f - t);
                sr.color = WithAlpha(c, 1f - t);
            }, Ease.OutCubic, unscaled: false);
            Destroy(sr.gameObject);
        }

        SpriteRenderer Quad(Transform parent, string name, Sprite sprite, Color color, Vector3 pos, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sharedMaterial = spriteMaterial; sr.color = color; sr.sortingOrder = order;
            return sr;
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
