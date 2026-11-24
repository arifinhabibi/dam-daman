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
        /// <summary>Figures are scaled up so their silhouettes read from the overhead camera.</summary>
        const float FigureScale = 1.8f;

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

            var dark = Mats.Lit(bodyColor, 0.55f);
            var gold = Mats.Lit(Palette.AccentPrada, 0.75f, 0.85f);
            bool tall = role == Core.Role.Patih || role == Core.Role.Senopati;
            float h = tall ? 0.42f : 0.32f;
            Mats.Prim(PrimitiveType.Cylinder, figure, "Torso", dark, new Vector3(0, h * 0.5f, 0), new Vector3(0.26f, h * 0.5f, 0.2f));
            Mats.Prim(PrimitiveType.Sphere, figure, "Head", dark, new Vector3(0, h + 0.1f, 0), Vector3.one * 0.2f);

            switch (role)
            {
                case Core.Role.Patih: // tall gold crown
                    Mats.Prim(PrimitiveType.Cylinder, figure, "Crown", gold, new Vector3(0, h + 0.24f, 0), new Vector3(0.16f, 0.09f, 0.16f));
                    Mats.Prim(PrimitiveType.Sphere, figure, "Crown Top", gold, new Vector3(0, h + 0.36f, 0), Vector3.one * 0.08f);
                    break;
                case Core.Role.Senopati: // spear
                    Mats.Prim(PrimitiveType.Cylinder, figure, "Spear", gold, new Vector3(0.2f, h * 0.65f, 0), new Vector3(0.035f, h * 0.65f, 0.035f));
                    Mats.Prim(PrimitiveType.Cube, figure, "Spear Tip", gold, new Vector3(0.2f, h * 1.33f, 0), new Vector3(0.06f, 0.12f, 0.02f));
                    break;
                case Core.Role.Pendekar: // two crossed blades
                    var a = Mats.Prim(PrimitiveType.Cube, figure, "Blade L", gold, new Vector3(0, h * 0.7f, 0.13f), new Vector3(0.04f, 0.34f, 0.02f));
                    a.localRotation = Quaternion.Euler(0, 0, 35);
                    var b = Mats.Prim(PrimitiveType.Cube, figure, "Blade R", gold, new Vector3(0, h * 0.7f, 0.13f), new Vector3(0.04f, 0.34f, 0.02f));
                    b.localRotation = Quaternion.Euler(0, 0, -35);
                    break;
                case Core.Role.Dukun: // staff with a gold head
                    Mats.Prim(PrimitiveType.Cylinder, figure, "Staff", Mats.Lit(Palette.Sogan, 0.4f), new Vector3(-0.19f, h * 0.6f, 0), new Vector3(0.03f, h * 0.6f, 0.03f));
                    Mats.Prim(PrimitiveType.Sphere, figure, "Staff Head", gold, new Vector3(-0.19f, h * 1.22f, 0), Vector3.one * 0.08f);
                    break;
                case Core.Role.TelikSandi: // hooded: a hood over the head and a gold mask band
                    Mats.Prim(PrimitiveType.Sphere, figure, "Hood", Mats.Lit(Palette.Sogan, 0.3f), new Vector3(0, h + 0.13f, -0.02f), new Vector3(0.25f, 0.22f, 0.25f));
                    Mats.Prim(PrimitiveType.Cube, figure, "Mask", gold, new Vector3(0, h + 0.1f, 0.09f), new Vector3(0.16f, 0.04f, 0.04f));
                    break;
                default: // Prajurit: small gold shield
                    Mats.Prim(PrimitiveType.Cylinder, figure, "Shield", gold, new Vector3(0.15f, h * 0.55f, 0), new Vector3(0.14f, 0.015f, 0.14f)).localRotation = Quaternion.Euler(0, 0, 90);
                    break;
            }
            crown.localPosition = new Vector3(0, DiscHeight * 0.5f + (h + 0.25f) * FigureScale, 0);
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>Hides the piece's renderers (it stays active so its animations keep running).</summary>
        public void SetVisible(bool visible)
        {
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
