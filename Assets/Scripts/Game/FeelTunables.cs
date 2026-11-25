using System;
using UnityEngine;

namespace Pamor
{
    // BELAJAR UNITY: ini BUKAN MonoBehaviour, cuma kelas C# biasa yang menyimpan angka-angka.
    // Karena diberi [Serializable] dan field-nya public, Unity bisa MENAMPILKAN dan MENYIMPAN
    // kelas ini di Inspector (panel kanan Editor) kalau dipakai sebagai field di sebuah
    // komponen (lihat "public FeelTunables feel" di BoardView). Hasilnya: kamu bisa mengubah
    // angka-angka ini saat game jalan, tanpa menulis ulang kode.

    /// <summary>Every feel number in one place, so it can be tuned live from the Inspector.</summary>
    [Serializable]
    public class FeelTunables
    {
        // [Header("...")] hanya memberi judul pengelompokan di Inspector, tidak mengubah perilaku.
        [Header("Selection")]
        public float selectLiftScale = 1.12f;  // seberapa membesar bidak yang dipilih (1 = ukuran asli)
        public float selectDuration = 0.12f;   // detik: lama animasi terangkat

        [Header("Movement")]
        public float hopDuration = 0.2f;       // detik: lama bidak melompat ke titik tujuan
        public float hopArcScale = 1.15f;
        public float landSquashY = 0.85f;      // saat mendarat bidak sedikit "gepeng": tinggi jadi 85%
        public float landSquashDuration = 0.1f;

        [Header("Capture")]
        public float capturePopScale = 1.25f;
        public float captureDuration = 0.22f;
        public int captureParticles = 10;      // jumlah percikan saat bidak dimakan

        [Header("Promotion")]
        public float crownDuration = 0.35f;    // lama mahkota Dam membesar

        [Header("Hints")]
        public float pulsePeriod = 1.1f;       // detik per satu denyut tanda tujuan
        public float shakeAmplitude = 0.08f;   // seberapa jauh bidak bergetar kalau tidak bisa jalan
        public float shakeDuration = 0.25f;
    }
}
