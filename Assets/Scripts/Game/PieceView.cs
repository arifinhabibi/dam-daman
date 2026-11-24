using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pamor
{
    /// <summary>One piece on the board: a layered disc (shadow, body, gold rim, core) plus a crown when it is a Dam.</summary>
    public class PieceView : MonoBehaviour
    {
        const int RestOrder = 10, LiftedOrder = 30;

        SortingGroup group;
        Transform body;
        Transform crown;
        SpriteRenderer[] renderers;
        Vector3 baseScale = Vector3.one;
        Coroutine scaleRoutine;
        FeelTunables feel;

        public bool IsDam { get; private set; }

        public static PieceView Create(Transform parent, Sprite disc, Material mat, Color bodyColor, FeelTunables feel)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PieceView>();
            view.feel = feel;
            view.group = go.AddComponent<SortingGroup>();
            view.group.sortingOrder = RestOrder;

            SpriteRenderer Layer(Transform p, string name, Color c, float scale, Vector3 offset, int order)
            {
                var t = new GameObject(name).transform;
                t.SetParent(p, false);
                t.localPosition = offset;
                t.localScale = Vector3.one * scale;
                var sr = t.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = disc; sr.sharedMaterial = mat; sr.color = c; sr.sortingOrder = order;
                return sr;
            }

            Layer(go.transform, "Shadow", Palette.Shadow, 0.82f, new Vector3(0.05f, -0.07f, 0), 0);
            view.body = new GameObject("Body").transform;
            view.body.SetParent(go.transform, false);
            Layer(view.body, "Disc", bodyColor, 0.8f, Vector3.zero, 1);
            var rim = Layer(view.body, "Rim", Palette.AccentPrada, 0.55f, Vector3.zero, 2);
            rim.color = new Color(rim.color.r, rim.color.g, rim.color.b, 0.85f);
            Layer(view.body, "Core", bodyColor, 0.45f, Vector3.zero, 3);

            view.crown = new GameObject("Crown").transform;
            view.crown.SetParent(view.body, false);
            Layer(view.crown, "Crown Gold", Palette.AccentPrada, 0.34f, Vector3.zero, 4);
            Layer(view.crown, "Crown Jewel", bodyColor, 0.14f, Vector3.zero, 5);
            view.crown.localScale = Vector3.zero;

            view.renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            return view;
        }

        public void SetDam(bool dam, bool animate)
        {
            IsDam = dam;
            if (!animate) { crown.localScale = dam ? Vector3.one : Vector3.zero; return; }
            StartCoroutine(Ease.Tween(feel.crownDuration, t => crown.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, t), Ease.OutBack, unscaled: false));
        }

        public void SetSelected(bool selected)
        {
            group.sortingOrder = selected ? LiftedOrder : RestOrder;
            ScaleBodyTo(selected ? feel.selectLiftScale : 1f, feel.selectDuration);
        }

        void ScaleBodyTo(float target, float duration)
        {
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            float from = body.localScale.x;
            scaleRoutine = StartCoroutine(Ease.Tween(duration, t => body.localScale = baseScale * Mathf.LerpUnclamped(from, target, t), Ease.OutBack, unscaled: false));
        }

        /// <summary>Slides to a square with a small lift arc, then a landing squash.</summary>
        public IEnumerator Hop(Vector3 to)
        {
            group.sortingOrder = LiftedOrder;
            Vector3 from = transform.localPosition;
            float lift = feel.hopArcScale - 1f;
            yield return Ease.Tween(feel.hopDuration, t =>
            {
                transform.localPosition = Vector3.LerpUnclamped(from, to, t);
                float arc = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * lift;
                body.localScale = baseScale * (1f + arc);
            }, Ease.OutCubic, unscaled: false);

            yield return Ease.Tween(feel.landSquashDuration, t =>
            {
                float y = Mathf.Lerp(feel.landSquashY, 1f, t);
                float x = Mathf.Lerp(2f - feel.landSquashY, 1f, t);
                body.localScale = new Vector3(baseScale.x * x, baseScale.y * y, 1f);
            }, Ease.OutCubic, unscaled: false);
            body.localScale = baseScale;
            group.sortingOrder = RestOrder;
        }

        /// <summary>Pops, then shrinks and fades away; the GameObject is destroyed at the end.</summary>
        public IEnumerator Captured()
        {
            group.sortingOrder = LiftedOrder - 1;
            float pop = feel.capturePopScale;
            yield return Ease.Tween(feel.captureDuration * 0.4f, t => transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, pop, t), Ease.OutBack, unscaled: false);
            var start = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) start[i] = renderers[i].color;
            yield return Ease.Tween(feel.captureDuration * 0.6f, t =>
            {
                transform.localScale = Vector3.one * Mathf.Lerp(pop, 0f, t);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].color = new Color(start[i].r, start[i].g, start[i].b, start[i].a * (1f - t));
            }, Ease.OutCubic, unscaled: false);
            Destroy(gameObject);
        }

        public void Shake()
        {
            StartCoroutine(ShakeRoutine());
        }

        IEnumerator ShakeRoutine()
        {
            Vector3 home = body.localPosition;
            yield return Ease.Tween(feel.shakeDuration, t =>
            {
                float decay = 1f - t;
                body.localPosition = home + new Vector3(Mathf.Sin(t * Mathf.PI * 8f) * feel.shakeAmplitude * decay, 0f, 0f);
            }, Ease.Linear, unscaled: false);
            body.localPosition = home;
        }
    }
}
