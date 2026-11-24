using System.Collections;
using Pamor.Core;
using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// A Commander standing at its player's corner of the plinth, facing the board. Shows the 3D model from
    /// <see cref="CommanderArt"/> and drives its Animator triggers (Cast, Hit, Victory, Defeat); without a
    /// model it shows a placeholder figure that plays the same moments procedurally.
    /// </summary>
    public class CommanderAvatar : MonoBehaviour
    {
        Commander? shown;
        Side side;
        Animator animator;
        Transform figure;
        bool placeholder;
        float bobPhase;

        /// <summary>
        /// Where each side's Commander stands: on the plinth's lower step along a long side of the board,
        /// toward its own end, so it stays in frame (yours on the left of your view, the opponent's on the right).
        /// </summary>
        public static Vector3 SpotFor(Side s) => s == Side.Hitam ? new Vector3(-3.6f, -1.1f, 5.45f) : new Vector3(3.6f, -1.1f, -5.45f);

        public void Show(Side s, Commander? c)
        {
            side = s;
            transform.localPosition = SpotFor(s);
            transform.localRotation = Quaternion.LookRotation(-new Vector3(SpotFor(s).x, 0f, SpotFor(s).z));
            if (shown == c && figure != null)
            {
                // Same Commander, new match: stand back up (after a Defeat slump) and return to Idle.
                StopAllCoroutines(); busy = false;
                figure.localPosition = Vector3.zero;
                if (placeholder) figure.localRotation = Quaternion.identity;
                if (animator != null) { animator.Rebind(); animator.Update(0f); }
                return;
            }
            shown = c;
            if (figure != null) Destroy(figure.gameObject);
            figure = null; animator = null;
            if (!c.HasValue) return;

            var entry = CommanderArt.Load()?.For(c.Value);
            if (entry != null && entry.model != null)
            {
                var go = Instantiate(entry.model, transform);
                go.name = CommanderBook.Name(c.Value);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(entry.rotationOffset);
                go.transform.localScale = Vector3.one * entry.scale;
                figure = go.transform;
                animator = go.GetComponentInChildren<Animator>();
                placeholder = false;
            }
            else
            {
                figure = BuildPlaceholder(c.Value).transform;
                placeholder = true;
            }
            bobPhase = Random.value * 10f;
        }

        /// <summary>Plays a moment: Cast, Hit, Victory or Defeat.</summary>
        public void Play(string trigger)
        {
            if (figure == null) return;
            if (animator != null && HasTrigger(animator, trigger)) { animator.SetTrigger(trigger); return; }
            if (placeholder) StartCoroutine(PlaceholderMoment(trigger));
        }

        static bool HasTrigger(Animator a, string name)
        {
            foreach (var p in a.parameters) if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
            return false;
        }

        Transform cutout;

        void LateUpdate()
        {
            // The placeholder is a flat wayang cutout: keep it turned toward the camera.
            if (!placeholder || cutout == null || Camera.main == null) return;
            var d = cutout.position - Camera.main.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) cutout.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, figure.localEulerAngles.z);
        }

        void Update()
        {
            if (!placeholder || figure == null || busy) return;
            // Idle: a slow breath.
            float t = Time.time + bobPhase;
            figure.localPosition = new Vector3(0f, Mathf.Sin(t * 1.6f) * 0.03f, 0f);
            figure.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 1.6f) * 0.015f, 1f);
        }

        bool busy;

        IEnumerator PlaceholderMoment(string trigger)
        {
            busy = true;
            switch (trigger)
            {
                case CommanderArt.Cast: // a hop and a spin
                    yield return Ease.Tween(0.45f, k =>
                    {
                        figure.localPosition = Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
                        figure.localRotation = Quaternion.Euler(0f, k * 360f, 0f);
                    }, Ease.OutCubic, unscaled: false);
                    break;
                case CommanderArt.Hit: // a flinch
                    yield return Ease.Tween(0.3f, k => figure.localRotation = Quaternion.Euler(-Mathf.Sin(k * Mathf.PI) * 18f, 0f, 0f), Ease.Linear, unscaled: false);
                    break;
                case CommanderArt.Victory: // jump for joy, three times
                    for (int i = 0; i < 3; i++)
                        yield return Ease.Tween(0.35f, k => figure.localPosition = Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.5f, Ease.Linear, unscaled: true);
                    break;
                case CommanderArt.Defeat: // slump
                    yield return Ease.Tween(0.6f, k => figure.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 35f, k), 0f, 0f), Ease.OutCubic, unscaled: true);
                    busy = false;
                    yield break; // stay slumped
            }
            figure.localPosition = Vector3.zero;
            figure.localRotation = Quaternion.identity;
            busy = false;
        }

        /// <summary>Until a model exists: a pedestal, the Patih-style wayang figure in the side's colour, and a gold nameplate.</summary>
        GameObject BuildPlaceholder(Commander c)
        {
            var root = new GameObject("Placeholder " + CommanderBook.Name(c));
            root.transform.SetParent(transform, false);
            var gold = Mats.Lit(Palette.AccentPrada, 0.75f, 0.85f);
            var body = Mats.Lit(side == Side.Hitam ? Palette.PieceA : Palette.PieceB, 0.55f);
            Mats.Prim(PrimitiveType.Cylinder, root.transform, "Pedestal", Mats.Lit(Palette.StoneDark, 0.2f), new Vector3(0, 0.08f, 0), new Vector3(0.9f, 0.08f, 0.9f));
            Mats.Prim(PrimitiveType.Cylinder, root.transform, "Pedestal Trim", gold, new Vector3(0, 0.17f, 0), new Vector3(0.95f, 0.012f, 0.95f));
            var cut = new GameObject("Wayang").transform;
            cut.SetParent(root.transform, false);
            cut.localPosition = new Vector3(0, 0.18f, 0);
            cut.localScale = Vector3.one * 1.9f;
            cutout = cut;
            foreach (var part in WayangFigures.For(Role.Patih))
            {
                var go = new GameObject("Part");
                go.transform.SetParent(cut, false);
                go.transform.localPosition = new Vector3(0, 0, part.Z);
                go.AddComponent<MeshFilter>().sharedMesh = WayangMesh.Get(part.Outline);
                go.AddComponent<MeshRenderer>().sharedMaterial = part.Mat == WayangFigures.Mat.Gold ? gold : part.Mat == WayangFigures.Mat.Body ? body : Mats.Lit(Palette.Sogan, 0.3f);
            }
            return root;
        }
    }
}
