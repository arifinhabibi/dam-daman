using Unity.Cinemachine;
using UnityEngine;

namespace Pamor
{
    // BELAJAR UNITY - MonoBehaviour:
    // Kelas yang mewarisi MonoBehaviour bisa DIPASANG sebagai komponen pada sebuah GameObject
    // di scene. Unity otomatis memanggil beberapa fungsi khusus di dalamnya, antara lain:
    //   Awake()   -> sekali, saat objek dibuat (sebelum Start)
    //   Start()   -> sekali, sebelum frame pertama
    //   Update()  -> SETIAP frame (30-60 kali per detik)
    //   LateUpdate() -> setiap frame, setelah semua Update selesai
    // Komponen ini dipasang pada GameObject "Game" di scene Board.
    //
    // BELAJAR UNITY - CINEMACHINE:
    // Cinemachine adalah paket resmi Unity untuk kamera. Kamera sungguhan (Main Camera) cuma
    // "mengikuti" sebuah virtual camera. Di sini virtual camera-nya memakai "Orbital Follow":
    // kamera berputar mengelilingi satu titik (tengah papan) di permukaan sebuah bola.
    // Kelas ini hanya menggeser tiga angka di Orbital Follow: sudut mendatar (yaw), sudut
    // naik/turun (tilt), dan jarak (zoom). Selebihnya diurus Cinemachine.

    /// <summary>
    /// Player-controlled orbit around the board, driving a Cinemachine Orbital Follow.
    /// Yaw spins freely all the way round; the tilt is clamped so the camera always stays above the board:
    /// at most <see cref="maxTiltFromTop"/> degrees away from looking straight down.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        // [SerializeField] membuat field PRIVATE tetap muncul di Inspector. Referensi ini diisi
        // dengan menyeret komponen Orbital Follow dari scene ke slotnya di Inspector (atau lewat
        // script setup). Kalau kosong, game error karena orbit bernilai null.
        [SerializeField] CinemachineOrbitalFollow orbit;

        // Field public ikut muncul di Inspector otomatis. [Tooltip] menampilkan teks bantuan saat
        // kursor diam di atas nama field itu di Inspector.
        [Tooltip("Furthest the camera may tilt away from straight down, in degrees.")]
        public float maxTiltFromTop = 40f;
        [Tooltip("Starting tilt from straight down, in degrees.")]
        public float startTiltFromTop = 28f;
        [Tooltip("Starting yaw: 90 puts the camera behind Hitam, so the player's own pieces are at the bottom of the screen.")]
        public float startYaw = 90f;
        [Tooltip("Orbit distance from the board centre at zoom 1. Frames the whole board on a ~2:1 phone screen.")]
        public float radius = 21.5f;
        public float degreesPerPixel = 0.25f;                       // sensitivitas putar: derajat per piksel geser
        public Vector2 zoomRange = new Vector2(0.75f, 1.35f);       // batas zoom (kali jarak dasar)

        // Properti "=>" hanya-baca: menghitung nilainya setiap kali dibaca.
        public float Yaw => orbit.HorizontalAxis.Value;
        public float TiltFromTop => 90f - orbit.VerticalAxis.Value;

        // Awake dipanggil Unity saat komponen ini aktif pertama kali. Cocok untuk pengaturan awal.
        void Awake()
        {
            // Sumbu vertikal = tinggi kamera di atas bidang papan. 90 = tepat di atas (menunduk lurus);
            // 50 = miring 40 derajat. Batas bawah 90 - maxTilt itulah yang membuat kamera TIDAK
            // bisa turun lebih rendah dari 40 derajat, jadi selalu di atas papan.
            orbit.VerticalAxis.Range = new Vector2(90f - maxTiltFromTop, 89.5f);
            orbit.VerticalAxis.Wrap = false;                    // tidak berputar balik di ujung batas
            orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbit.HorizontalAxis.Wrap = true;                   // putaran mendatar boleh 360 derajat tanpa batas
            orbit.RadialAxis.Range = zoomRange;
            orbit.Radius = radius;
            ResetView();
        }

        public void ResetView()
        {
            orbit.HorizontalAxis.Value = startYaw;
            orbit.VerticalAxis.Value = 90f - startTiltFromTop;
            orbit.RadialAxis.Value = 1f;
        }

        /// <summary>Puts the camera behind a side, so that player's own pieces are at the bottom of the screen.</summary>
        public void FaceSide(Core.Side side)
        {
            ResetView();
            orbit.HorizontalAxis.Value = side == Core.Side.Hitam ? startYaw : startYaw - 180f;
        }

        /// <summary>Drag in screen pixels: horizontal spins around the board, vertical tilts.</summary>
        public void Rotate(Vector2 pixels)
        {
            // Pola "ambil, ubah, kembalikan": HorizontalAxis adalah struct (tipe nilai), jadi
            // mengubah salinan lokal "h" tidak otomatis mengubah aslinya. Harus ditulis balik
            // dengan "orbit.HorizontalAxis = h".
            var h = orbit.HorizontalAxis;
            h.Value = h.ClampValue(h.Value + pixels.x * degreesPerPixel);
            orbit.HorizontalAxis = h;
            var v = orbit.VerticalAxis;
            v.Value = v.ClampValue(v.Value - pixels.y * degreesPerPixel);   // ClampValue menjaga nilai dalam batas Range
            orbit.VerticalAxis = v;
        }

        /// <summary>Positive zooms in.</summary>
        public void Zoom(float amount)
        {
            var r = orbit.RadialAxis;
            r.Value = r.ClampValue(r.Value - amount);
            orbit.RadialAxis = r;
        }
    }
}
