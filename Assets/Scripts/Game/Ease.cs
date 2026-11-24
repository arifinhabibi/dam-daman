using System;
using System.Collections;
using UnityEngine;

namespace Pamor
{
    /// <summary>Three easing curves plus a coroutine tween. Tweens run on unscaled time unless told otherwise.</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;

        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float OutElastic(float t)
        {
            const float c4 = 2f * Mathf.PI / 3f;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static IEnumerator Tween(float duration, Action<float> apply, Func<float, float> ease, bool unscaled = true)
        {
            for (float t = 0f; t < 1f; t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime) / duration)
            {
                apply(ease(t));
                yield return null;
            }
            apply(1f);
        }
    }
}
