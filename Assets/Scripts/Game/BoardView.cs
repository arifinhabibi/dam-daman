using System.Collections;
using System.Collections.Generic;
using Pamor.Core;
using UnityEngine;

namespace Pamor
{
    /// <summary>Draws the dam-daman line board and its pieces from a <see cref="Board"/>, shows hints, and animates moves.</summary>
    public class BoardView : MonoBehaviour
    {
        public const float Spacing = 1.5f;

        [SerializeField] Sprite squareSprite;
        [SerializeField] Sprite discSprite;
        [SerializeField] Material spriteMaterial;
        public FeelTunables feel = new FeelTunables();

        readonly Dictionary<int, PieceView> pieces = new Dictionary<int, PieceView>();
        readonly List<SpriteRenderer> destinationMarkers = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> mustCaptureRings = new List<SpriteRenderer>();
        Transform piecesRoot, hintsRoot;
        SpriteRenderer lastFrom, lastTo;

        public static Vector3 ToLocal(int p) =>
            new Vector3((BoardGraph.Xs[p] - 4) * Spacing, (BoardGraph.Ys[p] - 2) * Spacing, 0f);

        /// <summary>The board point nearest a world position, if it is within reach.</summary>
        public bool TryGetPoint(Vector3 worldPoint, out int point)
        {
            var local = transform.InverseTransformPoint(worldPoint);
            local.z = 0f;
            point = -1;
            float best = Spacing * 0.5f;
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                float d = Vector3.Distance(local, ToLocal(p));
                if (d < best) { best = d; point = p; }
            }
            return point >= 0;
        }

        public void Build(Board board)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            pieces.Clear(); destinationMarkers.Clear(); mustCaptureRings.Clear();

            float w = 8 * Spacing, h = 4 * Spacing;
            Quad(transform, "Frame", squareSprite, Palette.FrameWood, Vector3.zero, new Vector3(w + 1.6f, h + 1.6f, 1f), 0);
            Quad(transform, "Frame Inlay", squareSprite, Palette.AccentPrada, Vector3.zero, new Vector3(w + 1.2f, h + 1.2f, 1f), 1);
            Quad(transform, "Kelir", squareSprite, Palette.Kelir, Vector3.zero, new Vector3(w + 1.0f, h + 1.0f, 1f), 2);

            var lines = new GameObject("Lines").transform; lines.SetParent(transform, false);
            foreach (var line in BoardGraph.Lines)
                for (int i = 0; i + 1 < line.Length; i++)
                    Segment(lines, ToLocal(line[i]), ToLocal(line[i + 1]));
            var dots = new GameObject("Points").transform; dots.SetParent(transform, false);
            for (int p = 0; p < BoardGraph.PointCount; p++)
                Quad(dots, "Pt " + BoardGraph.Name(p), discSprite, Palette.Sogan, ToLocal(p), Vector3.one * 0.22f, 4);

            hintsRoot = new GameObject("Hints").transform; hintsRoot.SetParent(transform, false);
            lastFrom = Quad(hintsRoot, "Last From", discSprite, WithAlpha(Palette.AccentPrada, 0.35f), Vector3.zero, Vector3.one * 1.05f, 5);
            lastTo = Quad(hintsRoot, "Last To", discSprite, WithAlpha(Palette.AccentPrada, 0.55f), Vector3.zero, Vector3.one * 1.05f, 5);
            lastFrom.enabled = lastTo.enabled = false;

            piecesRoot = new GameObject("Pieces").transform; piecesRoot.SetParent(transform, false);
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                int piece = board[p];
                if (piece == 0) continue;
                var side = Board.Owner(piece).Value;
                var view = PieceView.Create(piecesRoot, discSprite, spriteMaterial, side == Side.Hitam ? Palette.PieceA : Palette.PieceB, feel);
                view.name = SideName(side) + " " + BoardGraph.Name(p);
                view.transform.localPosition = ToLocal(p);
                view.transform.localScale = Vector3.one * 1.1f;
                view.SetDam(Board.IsDam(piece), animate: false);
                pieces[p] = view;
            }
        }

        static string SideName(Side s) => s == Side.Hitam ? "Hitam" : "Merah";

        public PieceView PieceAt(int p) => pieces.TryGetValue(p, out var v) ? v : null;

        public void ShowDestinations(IEnumerable<int> points)
        {
            ClearDestinations();
            foreach (var p in points)
                destinationMarkers.Add(Quad(hintsRoot, "Dest " + BoardGraph.Name(p), discSprite, WithAlpha(Palette.AccentPrada, 0.9f), ToLocal(p), Vector3.one * 0.38f, 6));
        }

        public void ClearDestinations()
        {
            foreach (var m in destinationMarkers) if (m) Destroy(m.gameObject);
            destinationMarkers.Clear();
        }

        public void ShowMustCapture(IEnumerable<int> points)
        {
            ClearMustCapture();
            foreach (var p in points)
                mustCaptureRings.Add(Quad(hintsRoot, "Must " + BoardGraph.Name(p), discSprite, WithAlpha(Palette.AccentPrada, 0.6f), ToLocal(p), Vector3.one * 1.25f, 5));
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
                if (m) m.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.44f, k);
            foreach (var r in mustCaptureRings)
                if (r) r.color = WithAlpha(Palette.AccentPrada, Mathf.Lerp(0.3f, 0.75f, k));
        }

        /// <summary>Plays a move and its turn-end effects: hops, captures, promotion, burn, demotion.</summary>
        public IEnumerator PlayMove(MoveResult result)
        {
            var move = result.Move;
            ClearDestinations();
            ClearMustCapture();
            var view = pieces[move.From];
            pieces.Remove(move.From);

            for (int i = 1; i < move.Path.Count; i++)
            {
                yield return view.Hop(ToLocal(move.Path[i]));
                if (move.IsCapture) RemovePiece(move.Captured[i - 1], null);
            }

            pieces[move.To] = view;
            view.name = SideName(result.Mover) + " " + BoardGraph.Name(move.To);
            if (result.Promoted)
            {
                view.SetDam(true, animate: true);
                Burst(ToLocal(move.To), Palette.AccentPrada, 16);
                yield return new WaitForSeconds(feel.crownDuration);
            }

            lastFrom.transform.localPosition = ToLocal(move.From);
            lastTo.transform.localPosition = ToLocal(move.To);
            lastFrom.enabled = lastTo.enabled = true;

            if (result.Burned >= 0)
            {
                yield return new WaitForSeconds(0.15f);
                RemovePiece(result.Burned, Palette.AccentPrada);
                yield return new WaitForSeconds(feel.captureDuration);
            }
            foreach (var p in result.Demoted) PieceAt(p)?.SetDam(false, animate: false);
        }

        void RemovePiece(int p, Color? sparkColor)
        {
            if (!pieces.TryGetValue(p, out var victim)) return;
            pieces.Remove(p);
            Burst(ToLocal(p), sparkColor ?? (victim.name.StartsWith("Hitam") ? Palette.PieceA : Palette.PieceB), sparkColor.HasValue ? 18 : -1);
            StartCoroutine(victim.Captured());
        }

        void Burst(Vector3 at, Color color, int count = -1)
        {
            if (count < 0) count = feel.captureParticles;
            for (int i = 0; i < count; i++)
            {
                float ang = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                var sr = Quad(hintsRoot, "Spark", discSprite, i % 3 == 0 ? Palette.AccentPrada : color, at, Vector3.one * Random.Range(0.08f, 0.14f), 40);
                StartCoroutine(Spark(sr, at, at + dir * Random.Range(0.5f, 0.9f)));
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

        void Segment(Transform parent, Vector3 a, Vector3 b)
        {
            var mid = (a + b) * 0.5f;
            var d = b - a;
            var sr = Quad(parent, "Line", squareSprite, Palette.Sogan, mid, new Vector3(d.magnitude, 0.07f, 1f), 3);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        SpriteRenderer Quad(Transform parent, string name, Sprite sprite, Color color, Vector3 pos, Vector3 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sharedMaterial = spriteMaterial; sr.color = color; sr.sortingOrder = order;
            return sr;
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
