using System.Collections;
using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// One 3D piece: a lacquered disc with an inlaid gold ring on top, plus a gold crown when it is a Dam.
    /// The root sits on the board point; animation moves the body, so the root stays the logical position.
    /// </summary>
    public class PieceView : MonoBehaviour
    {
        const float DiscHeight = 0.26f;
        /// <summary>Figure height in board units (wayang outlines are about 1 unit tall).</summary>
        const float FigureScale = 1.55f;
        /// <summary>How far (0..1) a figure leans back toward the overhead camera, so its silhouette isn't foreshortened.</summary>
        const float LeanToCamera = 0.6f;

        Transform body;
        Transform crown;
        Transform figure;
        Color bodyColor;
        Renderer[] renderers;
        Vector3 bodyHome;
        Coroutine liftRoutine;
        FeelTunables feel;

        public bool IsDam { get; private set; }

        public static PieceView Create(Transform parent, Color bodyColor, FeelTunables feel)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PieceView>();
            view.feel = feel;

            var lacquer = Mats.Lit(bodyColor, 0.6f);
            var gold = Mats.Lit(Palette.AccentPrada, 0.75f, 0.85f);
            view.body = new GameObject("Body").transform;
            view.body.SetParent(go.transform, false);
            view.bodyHome = new Vector3(0f, DiscHeight * 0.5f, 0f);
            view.body.localPosition = view.bodyHome;
            // Cylinder primitives are 2 units tall: y scale = half the height.
            Mats.Prim(PrimitiveType.Cylinder, view.body, "Disc", lacquer, Vector3.zero, new Vector3(0.9f, DiscHeight * 0.5f, 0.9f));
            Mats.Prim(PrimitiveType.Cylinder, view.body, "Gold Ring", gold, new Vector3(0, 0.015f, 0), new Vector3(0.64f, DiscHeight * 0.5f, 0.64f));
            Mats.Prim(PrimitiveType.Cylinder, view.body, "Core", lacquer, new Vector3(0, 0.03f, 0), new Vector3(0.5f, DiscHeight * 0.5f, 0.5f));

            view.crown = new GameObject("Crown").transform;
            view.crown.SetParent(view.body, false);
            view.crown.localPosition = new Vector3(0, DiscHeight * 0.5f, 0);
            Mats.Prim(PrimitiveType.Cylinder, view.crown, "Crown Band", gold, new Vector3(0, 0.08f, 0), new Vector3(0.42f, 0.08f, 0.42f));
            Mats.Prim(PrimitiveType.Sphere, view.crown, "Crown Jewel", Mats.Lit(bodyColor, 0.9f), new Vector3(0, 0.2f, 0), Vector3.one * 0.2f);
            view.crown.localScale = Vector3.zero;

            view.bodyColor = bodyColor;
            view.renderers = go.GetComponentsInChildren<Renderer>(true);
            return view;
        }

        /// <summary>Which role the viewer sees on this piece (an unrevealed enemy shows as Prajurit).</summary>
        public Core.Role ShownRole { get; private set; } = (Core.Role)(-1);

        /// <summary>
        /// Placeholder character figure standing on the disc: a dark wayang-like silhouette with a gold
        /// prop per role. Real character art replaces these later.
        /// </summary>
        public void ShowRole(Core.Role role)
        {
            if (role == ShownRole && figure != null) return;
            ShownRole = role;
            if (figure != null) Destroy(figure.gameObject);
            figure = new GameObject("Figure " + role).transform;
            figure.SetParent(body, false);
            figure.localPosition = new Vector3(0, DiscHeight * 0.5f + 0.03f, 0);
            figure.localScale = Vector3.one * FigureScale;

            // The cutout turns to face the camera (see LateUpdate); Merah figures face the other way.
            cutout = new GameObject("Wayang").transform;
            cutout.SetParent(figure, false);
            float s = WayangFigures.Scale(role);
            facing = side == Core.Side.Merah ? -1f : 1f;
            cutout.localScale = new Vector3(s, s, s);
            var mats = new System.Collections.Generic.Dictionary<WayangFigures.Mat, Material>
            {
                { WayangFigures.Mat.Body, Mats.Lit(bodyColor, 0.55f) },
                { WayangFigures.Mat.Gold, Mats.Lit(Palette.AccentPrada, 0.75f, 0.85f) },
                { WayangFigures.Mat.Cloth, Mats.Lit(Palette.Sogan, 0.3f) },
                { WayangFigures.Mat.Horn, Mats.Lit(Palette.Kelir, 0.5f) },
            };
            foreach (var part in WayangFigures.For(role))
            {
                var go = new GameObject("Part");
                go.transform.SetParent(cutout, false);
                go.transform.localPosition = new Vector3(0f, 0f, part.Z);
                go.AddComponent<MeshFilter>().sharedMesh = WayangMesh.Get(part.Outline);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mats[part.Mat];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            // Dam crown floats above the head (or the Patih's makutha).
            crown.localPosition = new Vector3(0, DiscHeight * 0.5f + (1.08f * s) * FigureScale, 0);
            renderers = GetComponentsInChildren<Renderer>(true);
            if (!visible) SetVisible(false);
        }

        Transform cutout;
        float facing = 1f;
        float swayPhase = -1f;
        bool visible = true;
        Core.Side side;

        /// <summary>Which side the piece belongs to: decides which way its figure faces.</summary>
        public void SetSide(Core.Side s) => side = s;

        void LateUpdate()
        {
            if (cutout == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            // Billboard around the vertical axis so the flat silhouette always reads, plus a slow puppet sway.
            var full = cutout.position - cam.transform.position;
            var flat = new Vector3(full.x, 0f, full.z);
            if (flat.sqrMagnitude < 1e-4f) return;
            if (swayPhase < 0f) swayPhase = Random.value * 10f;
            float sway = Mathf.Sin((Time.time + swayPhase) * 1.3f) * 2.5f;
            var upright = Quaternion.LookRotation(flat);
            var facingCamera = Quaternion.LookRotation(full);
            cutout.rotation = Quaternion.Slerp(upright, facingCamera, LeanToCamera) * Quaternion.Euler(0f, 0f, sway);
            float s = WayangFigures.Scale(ShownRole);
            cutout.localScale = new Vector3(s * facing, s, s);
        }

        /// <summary>Hides the piece's renderers (it stays active so its animations keep running).</summary>
        public void SetVisible(bool visible)
        {
            this.visible = visible;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        public void SetDam(bool dam, bool animate)
        {
            IsDam = dam;
            if (!animate) { crown.localScale = dam ? Vector3.one : Vector3.zero; return; }
            float from = crown.localScale.x, to = dam ? 1f : 0f;
            StartCoroutine(Ease.Tween(feel.crownDuration, t => crown.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, t), Ease.OutBack, unscaled: false));
        }

        public void SetSelected(bool selected)
        {
            if (liftRoutine != null) StopCoroutine(liftRoutine);
            Vector3 fromPos = body.localPosition;
            Vector3 toPos = bodyHome + (selected ? Vector3.up * 0.3f : Vector3.zero);
            float fromScale = body.localScale.x, toScale = selected ? feel.selectLiftScale : 1f;
            liftRoutine = StartCoroutine(Ease.Tween(feel.selectDuration, t =>
            {
                body.localPosition = Vector3.LerpUnclamped(fromPos, toPos, t);
                body.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, toScale, t);
            }, Ease.OutBack, unscaled: false));
        }

        /// <summary>Jumps to a board position in an arc, then lands with a squash.</summary>
        public IEnumerator Hop(Vector3 to)
        {
            if (liftRoutine != null) StopCoroutine(liftRoutine);
            Vector3 from = transform.localPosition;
            float arc = 0.4f + Vector3.Distance(from, to) * 0.15f;
            yield return Ease.Tween(feel.hopDuration, t =>
            {
                transform.localPosition = Vector3.LerpUnclamped(from, to, t);
                body.localPosition = bodyHome + Vector3.up * (Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * arc);
                body.localScale = Vector3.one;
            }, Ease.OutCubic, unscaled: false);

            yield return Ease.Tween(feel.landSquashDuration, t =>
            {
                float y = Mathf.Lerp(feel.landSquashY, 1f, t);
                float xz = Mathf.Lerp(2f - feel.landSquashY, 1f, t);
                body.localScale = new Vector3(xz, y, xz);
            }, Ease.OutCubic, unscaled: false);
            body.localScale = Vector3.one;
            body.localPosition = bodyHome;
        }

        /// <summary>Pops up, then shrinks into the board; the GameObject is destroyed at the end.</summary>
        public IEnumerator Captured()
        {
            float pop = feel.capturePopScale;
            Vector3 home = body.localPosition;
            yield return Ease.Tween(feel.captureDuration * 0.4f, t =>
            {
                body.localScale = Vector3.one * Mathf.LerpUnclamped(1f, pop, t);
                body.localPosition = home + Vector3.up * 0.35f * t;
            }, Ease.OutBack, unscaled: false);
            yield return Ease.Tween(feel.captureDuration * 0.6f, t =>
            {
                body.localScale = Vector3.one * Mathf.Lerp(pop, 0f, t);
                body.localPosition = home + Vector3.up * Mathf.Lerp(0.35f, 0f, t);
            }, Ease.OutCubic, unscaled: false);
            Destroy(gameObject);
        }

        public void Shake() => StartCoroutine(ShakeRoutine());

        IEnumerator ShakeRoutine()
        {
            yield return Ease.Tween(feel.shakeDuration, t =>
            {
                float decay = 1f - t;
                body.localPosition = bodyHome + new Vector3(Mathf.Sin(t * Mathf.PI * 8f) * feel.shakeAmplitude * decay, 0f, 0f);
            }, Ease.Linear, unscaled: false);
            body.localPosition = bodyHome;
        }
    }
}
