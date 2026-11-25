using System;
using System.Collections;
using UnityEngine;

namespace Pamor
{
    // BELAJAR UNITY: "static class" artinya tidak perlu dipasang di GameObject. Kamu cukup
    // memanggilnya dari mana saja, misalnya Ease.OutCubic(0.5f).
    //
    // "Easing" = cara sebuah nilai bergerak dari 0 ke 1. Linear bergerak rata; OutCubic cepat di
    // awal lalu melambat (terasa halus dan alami). Gerakan UI dan bidak jadi terasa "hidup"
    // karena memakai kurva seperti ini, bukan gerak lurus.

    /// <summary>Three easing curves plus a coroutine tween. Tweens run on unscaled time unless told otherwise.</summary>
    public static class Ease
    {
        // Setiap kurva menerima t (0..1) = seberapa jauh animasi berjalan, dan mengembalikan
        // nilai 0..1 yang sudah "dibengkokkan".
        public static float Linear(float t) => t;

        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        // OutBack: melewati target sedikit (overshoot) lalu kembali. Dipakai untuk efek "pop".
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

        // BELAJAR UNITY - COROUTINE:
        // Fungsi yang mengembalikan IEnumerator dan memakai "yield return" disebut Coroutine.
        // Coroutine bisa BERHENTI SEMENTARA di "yield return null" dan dilanjutkan di FRAME
        // BERIKUTNYA. Jadi satu fungsi ini bisa menjalankan animasi selama sepersekian detik
        // tanpa membekukan game. Cara menjalankannya: StartCoroutine(Ease.Tween(...)) dari
        // sebuah MonoBehaviour, atau "yield return Ease.Tween(...)" di dalam coroutine lain
        // untuk menunggunya selesai.
        //
        // Parameter:
        //   duration -> berapa detik animasinya
        //   apply    -> fungsi yang dipanggil tiap frame dengan nilai 0..1 (isinya yang menggerakkan sesuatu)
        //   ease     -> kurva di atas
        //   unscaled -> true = jalan walau game di-pause (Time.timeScale = 0), cocok untuk UI
        public static IEnumerator Tween(float duration, Action<float> apply, Func<float, float> ease, bool unscaled = true)
        {
            // Time.deltaTime = detik yang berlalu sejak frame sebelumnya (ikut melambat saat pause).
            // Time.unscaledDeltaTime = sama, tapi tidak terpengaruh pause.
            for (float t = 0f; t < 1f; t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime) / duration)
            {
                apply(ease(t));
                yield return null;   // tunggu satu frame, lalu lanjut dari baris ini
            }
            apply(1f);               // pastikan animasi berakhir tepat di nilai akhir
        }
    }
}
