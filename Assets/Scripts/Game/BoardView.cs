using System.Collections;
using System.Collections.Generic;
using Pamor.Core;
using UnityEngine;

namespace Pamor
{
    // BELAJAR UNITY - ARSITEKTUR PROYEK INI (penting dipahami dulu):
    //   Core/  = aturan permainan, C# murni, TIDAK memakai Unity. Tahu apa yang boleh dan tidak,
    //            tapi tidak punya konsep "layar" atau "animasi". Bisa dites tanpa membuka game.
    //   Game/  = bagian Unity: menggambar papan, bidak, UI, menerima sentuhan.
    // BoardView adalah "juru gambar" papan. Ia diberi data Board dari Core lalu membangun
    // GameObject-nya (garis, titik, bidak) dan memainkan animasinya. Dia TIDAK memutuskan
    // langkah mana yang sah, itu tugas Core.Rules.
    //
    // Sumbu dunia 3D Unity: X = kanan, Y = ATAS, Z = depan. Papan terletak mendatar di bidang
    // X-Z, jadi titik papan (kolom, baris) dipetakan ke (x, z) di fungsi ToLocal di bawah.

    /// <summary>
    /// The 3D dam-daman board: a wooden tray with a gold inlay and a kelir-coloured playing surface,
    /// carved lines and points, and the pieces. Board points lie on the XZ plane at y = 0.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public const float Spacing = 1.5f;     // jarak antar titik papan, dalam satuan Unity (1 satuan ~ 1 meter)
        const float SurfaceY = 0.02f;          // tinggi permukaan papan (sedikit di atas 0 agar tidak berimpit dengan alas)

        // Field public bertipe [Serializable] -> bisa diubah di Inspector saat game jalan.
        public FeelTunables feel = new FeelTunables();

        // Dictionary: nomor titik papan -> PieceView bidak yang berdiri di titik itu.
        readonly Dictionary<int, PieceView> pieces = new Dictionary<int, PieceView>();
        readonly List<Transform> destinationMarkers = new List<Transform>();
        readonly List<Transform> mustCaptureRings = new List<Transform>();
        Transform piecesRoot, hintsRoot;
        Transform lastFrom, lastTo;

        public static Vector3 ToLocal(int p) =>
            new Vector3((BoardGraph.Xs[p] - 4) * Spacing, SurfaceY, (BoardGraph.Ys[p] - 2) * Spacing);

        /// <summary>The board point nearest where a screen ray meets the board, if it is within reach.</summary>
        public bool TryGetPoint(Ray worldRay, out int point)
        {
            // BELAJAR UNITY - SENTUHAN KE POSISI 3D:
            // Layar itu 2D, tapi papannya di dunia 3D. Camera.ScreenPointToRay mengubah titik layar
            // yang disentuh menjadi sebuah RAY (sinar) yang menembus dunia dari kamera.
            // Lalu kita tanya: di mana sinar itu memotong bidang papan? (Plane.Raycast)
            // Hasilnya titik 3D di papan, dan kita cari titik papan terdekat darinya.
            // Cara ini tidak butuh Collider dan jauh lebih ringan daripada Physics.Raycast.
            point = -1;
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, SurfaceY, 0)));
            if (!plane.Raycast(worldRay, out float enter)) return false;     // sinar sejajar bidang: tidak memotong
            // InverseTransformPoint: ubah posisi DUNIA menjadi posisi LOKAL (relatif terhadap papan).
            var local = transform.InverseTransformPoint(worldRay.GetPoint(enter));
            float best = Spacing * 0.5f;
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                var d = local - ToLocal(p);
                d.y = 0f;
                if (d.magnitude < best) { best = d.magnitude; point = p; }
            }
            return point >= 0;
        }

        /// <summary>Where a screen ray meets the board surface, in board-local space.</summary>
        public bool TryGetBoardPoint(Ray worldRay, out Vector3 local)
        {
            local = default;
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, SurfaceY, 0)));
            if (!plane.Raycast(worldRay, out float enter)) return false;
            local = transform.InverseTransformPoint(worldRay.GetPoint(enter));
            local.y = SurfaceY;
            return true;
        }

        /// <summary>Carries a held piece under the finger (the piece itself stays lifted by its selection).</summary>
        public void DragPieceTo(int p, Vector3 local)
        {
            var v = PieceAt(p);
            if (v != null) v.transform.localPosition = local;
        }

        /// <summary>Slides a piece that was dragged but not dropped on a legal point back to its own point.</summary>
        public void SnapPieceBack(int p)
        {
            var v = PieceAt(p);
            if (v != null) StartCoroutine(SnapBackRoutine(v, ToLocal(p)));
        }

        IEnumerator SnapBackRoutine(PieceView v, Vector3 home)
        {
            Vector3 from = v.transform.localPosition;
            yield return Ease.Tween(0.14f, t => { if (v != null) v.transform.localPosition = Vector3.LerpUnclamped(from, home, t); }, Ease.OutCubic, unscaled: false);
            if (v != null) v.transform.localPosition = home;
        }

        public void Build(Board board)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            pieces.Clear(); destinationMarkers.Clear(); mustCaptureRings.Clear();

            float w = 8 * Spacing, d = 4 * Spacing;
            var wood = Mats.Lit(Palette.FrameWood, 0.45f);
            var gold = Mats.Lit(Palette.AccentPrada, 0.75f, 0.85f);
            var kelir = Mats.Lit(Palette.Kelir, 0.2f);
            var carve = Mats.Lit(Palette.Sogan, 0.3f);

            var tray = new GameObject("Tray").transform; tray.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Cube, tray, "Frame", wood, new Vector3(0, -0.25f, 0), new Vector3(w + 2.0f, 0.5f, d + 2.0f));
            Mats.Prim(PrimitiveType.Cube, tray, "Gold Inlay", gold, new Vector3(0, -0.02f, 0), new Vector3(w + 1.4f, 0.06f, d + 1.4f));
            Mats.Prim(PrimitiveType.Cube, tray, "Kelir", kelir, new Vector3(0, -0.01f, 0), new Vector3(w + 1.1f, 0.06f, d + 1.1f));

            var lines = new GameObject("Lines").transform; lines.SetParent(transform, false);
            foreach (var line in BoardGraph.Lines)
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector3 a = ToLocal(line[i]), b = ToLocal(line[i + 1]);
                    var seg = Mats.Prim(PrimitiveType.Cube, lines, "Line", carve, (a + b) * 0.5f, new Vector3((b - a).magnitude, 0.012f, 0.08f));
                    seg.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(b.z - a.z, b.x - a.x) * Mathf.Rad2Deg, 0f);
                }
            var dots = new GameObject("Points").transform; dots.SetParent(transform, false);
            for (int p = 0; p < BoardGraph.PointCount; p++)
                Mats.Prim(PrimitiveType.Cylinder, dots, "Pt " + BoardGraph.Name(p), carve, ToLocal(p), new Vector3(0.24f, 0.01f, 0.24f));

            hintsRoot = new GameObject("Hints").transform; hintsRoot.SetParent(transform, false);
            lastFrom = Disc(hintsRoot, "Last From", Mats.Lit(Palette.AccentPrada, 0.3f), Vector3.zero, 1.05f, 0.012f);
            lastTo = Disc(hintsRoot, "Last To", Mats.Lit(Palette.AccentPrada, 0.3f, 0f, 0.4f), Vector3.zero, 1.05f, 0.014f);
            lastFrom.gameObject.SetActive(false); lastTo.gameObject.SetActive(false);

            piecesRoot = new GameObject("Pieces").transform; piecesRoot.SetParent(transform, false);
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                var piece = board[p];
                if (!piece.Exists) continue;
                var view = PieceView.Create(piecesRoot, piece.Side == Side.Hitam ? Palette.PieceA : Palette.PieceB, feel);
                view.SetSide(piece.Side);
                view.name = SideName(piece.Side) + " " + BoardGraph.Name(p);
                view.transform.localPosition = ToLocal(p);
                view.SetDam(piece.IsDam, animate: false);
                pieces[p] = view;
            }
            shieldRoot = null;
        }

        /// <summary>
        /// Shows each piece's role as the viewer may see it: own pieces and revealed enemies show their
        /// real role; unrevealed enemies show as Prajurit. Also places the Dukun shield markers.
        /// </summary>
        public void RefreshRoles(Board board, Side viewer, bool revealAll = false)
        {
            foreach (var kv in pieces)
            {
                var piece = board[kv.Key];
                if (!piece.Exists) continue;
                bool known = revealAll || piece.Side == viewer || piece.Revealed;
                kv.Value.ShowRole(known ? piece.Role : Role.Prajurit);
                // Panglimunan: an unseen Telik Sandi isn't drawn for the opponent.
                kv.Value.SetVisible(revealAll || piece.HiddenTurns == 0 || piece.Side == viewer);
            }
            ShowShields(board, viewer);
        }

        Transform shieldRoot;

        void ShowShields(Board board, Side viewer)
        {
            if (shieldRoot != null) Destroy(shieldRoot.gameObject);
            shieldRoot = new GameObject("Shields").transform;
            shieldRoot.SetParent(hintsRoot, false);
            var gold = Mats.Lit(Palette.AccentPrada, 0.6f, 0.5f, 0.6f);
            var hex = Mats.Lit(Palette.Shadow, 0.2f);
            var mist = Mats.Lit(Palette.SkyBounce, 0.6f, 0f, 0.5f);
            var stone = Mats.Lit(Palette.Sogan, 0.15f);
            var timber = Mats.Lit(Palette.FrameWood, 0.35f);
            for (int p = 0; p < BoardGraph.PointCount; p++)
            {
                // Sangkuriang's blockers: squat stone pillars. Perahu Semalam's wall: a timber palisade.
                if (board.IsPermanentBlocker(p))
                    Mats.Prim(PrimitiveType.Cylinder, shieldRoot, "Penghalang " + BoardGraph.Name(p), stone, ToLocal(p) + Vector3.up * 0.3f, new Vector3(0.8f, 0.3f, 0.8f));
                if (board.IsWall(p))
                    for (int i = -1; i <= 1; i++)
                        Mats.Prim(PrimitiveType.Cube, shieldRoot, "Tembok " + BoardGraph.Name(p), timber, ToLocal(p) + new Vector3(i * 0.28f, 0.4f, 0f), new Vector3(0.22f, 0.8f, 0.9f));
                var piece = board[p];
                if (!piece.Exists) continue;
                // Jaka Tingkir's guard, as its owner sees it: a gold band at the base.
                if (piece.GuardOnce && piece.Side == viewer)
                    Mats.Prim(PrimitiveType.Cylinder, shieldRoot, "Guard " + BoardGraph.Name(p), gold, ToLocal(p) + Vector3.up * 0.04f, new Vector3(1.0f, 0.015f, 1.0f));
                // Tameng Waja: a gold band hovering around the piece.
                if (piece.ImmuneTurns > 0)
                    Mats.Prim(PrimitiveType.Cylinder, shieldRoot, "Tameng " + BoardGraph.Name(p), gold, ToLocal(p) + Vector3.up * 0.45f, new Vector3(1.05f, 0.02f, 1.05f));
                // Santet: dark stakes pinning the piece.
                if (piece.FrozenTurns > 0)
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.PI * 2f / 3f;
                        var stake = Mats.Prim(PrimitiveType.Cube, shieldRoot, "Santet " + BoardGraph.Name(p), hex,
                            ToLocal(p) + new Vector3(Mathf.Cos(a) * 0.55f, 0.18f, Mathf.Sin(a) * 0.55f), new Vector3(0.07f, 0.36f, 0.07f));
                        stake.localRotation = Quaternion.Euler(Mathf.Sin(a) * 18f, 0f, -Mathf.Cos(a) * 18f);
                    }
                // Panglimunan, as its owner sees it: a cool mist ring.
                if (piece.HiddenTurns > 0 && piece.Side == viewer)
                    Mats.Prim(PrimitiveType.Cylinder, shieldRoot, "Panglimunan " + BoardGraph.Name(p), mist, ToLocal(p) + Vector3.up * 0.02f, new Vector3(1.3f, 0.01f, 1.3f));
            }
            foreach (var side in new[] { Side.Hitam, Side.Merah })
            foreach (var p in board.Shields(side))
            {
                if (p < 0) continue;
                // A ring of four gold posts around the shielded piece.
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 0.5f;
                    var post = Mats.Prim(PrimitiveType.Cylinder, shieldRoot, "Shield " + BoardGraph.Name(p), gold,
                        ToLocal(p) + new Vector3(Mathf.Cos(a) * 0.62f, 0.22f, Mathf.Sin(a) * 0.62f), new Vector3(0.08f, 0.22f, 0.08f));
                    post.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        static string SideName(Side s) => s == Side.Hitam ? "Hitam" : "Merah";

        Transform Disc(Transform parent, string name, Material mat, Vector3 pos, float diameter, float height)
        {
            var t = Mats.Prim(PrimitiveType.Cylinder, parent, name, mat, pos + Vector3.up * height, new Vector3(diameter, height, diameter));
            t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        public PieceView PieceAt(int p) => pieces.TryGetValue(p, out var v) ? v : null;

        public void ShowDestinations(IEnumerable<int> points)
        {
            ClearDestinations();
            var glow = Mats.Lit(Palette.AccentPrada, 0.5f, 0f, 1.3f);
            foreach (var p in points)
                destinationMarkers.Add(Disc(hintsRoot, "Dest " + BoardGraph.Name(p), glow, ToLocal(p), 0.42f, 0.03f));
        }

        public void ClearDestinations()
        {
            foreach (var m in destinationMarkers) if (m) Destroy(m.gameObject);
            destinationMarkers.Clear();
        }

        public void ShowMustCapture(IEnumerable<int> points)
        {
            ClearMustCapture();
            var glow = Mats.Lit(Palette.AccentPrada, 0.5f, 0f, 0.9f);
            foreach (var p in points)
                mustCaptureRings.Add(Disc(hintsRoot, "Must " + BoardGraph.Name(p), glow, ToLocal(p), 1.2f, 0.016f));
        }

        public void ClearMustCapture()
        {
            foreach (var r in mustCaptureRings) if (r) Destroy(r.gameObject);
            mustCaptureRings.Clear();
        }

        // Update() dipanggil Unity SETIAP frame. Di sini dipakai untuk animasi yang terus-menerus:
        // tanda tujuan dan cincin "wajib makan" berdenyut membesar-mengecil.
        // Time.time = detik sejak game mulai (berhenti saat pause, jadi denyutnya ikut berhenti).
        void Update()
        {
            // Hints pulse so they read as interactive; scaled time, so they freeze under pause.
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI / feel.pulsePeriod);
            foreach (var m in destinationMarkers)
                if (m) { float s = Mathf.Lerp(0.34f, 0.5f, k); m.localScale = new Vector3(s, m.localScale.y, s); }
            foreach (var r in mustCaptureRings)
                if (r) { float s = Mathf.Lerp(1.1f, 1.28f, k); r.localScale = new Vector3(s, r.localScale.y, s); }
        }

        /// <summary>Plays a move and its turn-end effects: hops, captures, promotion, burn, demotion.</summary>
        // Inilah contoh nyata kenapa Coroutine berguna: satu langkah bisa berupa beberapa lompatan
        // berantai (makan beruntun). "yield return view.Hop(...)" MENUNGGU lompatan itu selesai
        // sebelum lanjut ke baris berikutnya, tanpa membekukan game. Tanpa coroutine kita harus
        // menulis mesin status rumit untuk melacak "sekarang lompatan ke berapa".
        // GameController menunggu fungsi ini dengan "yield return boardView.PlayMove(result)".
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
                if (!move.IsCapture) continue;
                int cap = move.Captured[i - 1];
                if (result.Guarded.Contains(cap))
                {
                    // Jaka Tingkir's guard: the piece shrugs off the hit and stays.
                    Burst(ToLocal(cap), Palette.AccentPrada, 12);
                    PieceAt(cap)?.Shake();
                }
                else RemovePiece(cap, null);
            }

            pieces[move.To] = view;
            view.name = SideName(result.Mover) + " " + BoardGraph.Name(move.To);
            if (result.Promoted)
            {
                view.SetDam(true, animate: true);
                Burst(ToLocal(move.To), Palette.AccentPrada, 18);
                yield return new WaitForSeconds(feel.crownDuration);
            }

            lastFrom.localPosition = ToLocal(move.From) + Vector3.up * 0.012f;
            lastTo.localPosition = ToLocal(move.To) + Vector3.up * 0.014f;
            lastFrom.gameObject.SetActive(true); lastTo.gameObject.SetActive(true);

            if (result.Burned >= 0)
            {
                yield return new WaitForSeconds(0.15f);
                RemovePiece(result.Burned, Palette.AccentPrada);
                yield return new WaitForSeconds(feel.captureDuration);
            }
            foreach (var p in result.Demoted) PieceAt(p)?.SetDam(false, animate: true);
        }

        /// <summary>Plays an ajian: a burst on the caster (and target), and hops for pieces it moves.</summary>
        public IEnumerator PlayCast(CastResult result)
        {
            if (result.CasterPoint >= 0) Burst(ToLocal(result.CasterPoint), Palette.AccentPrada, 14);
            if (result.Target >= 0 && result.Target != result.CasterPoint) Burst(ToLocal(result.Target), Palette.Shadow, 14);
            foreach (var p in result.Frozen) Burst(ToLocal(p), Palette.Shadow, 10);
            foreach (var p in result.Walls) Burst(ToLocal(p), Palette.FrameWood, 10);
            foreach (var p in result.Captured) RemovePiece(p, null);
            foreach (var p in result.Guarded) { Burst(ToLocal(p), Palette.AccentPrada, 12); PieceAt(p)?.Shake(); }
            var hops = new List<Coroutine>();
            foreach (var (from, to) in result.Shifts)
            {
                if (!pieces.TryGetValue(from, out var view)) continue;
                pieces.Remove(from);
                pieces[to] = view;
                hops.Add(StartCoroutine(view.Hop(ToLocal(to))));
            }
            foreach (var h in hops) yield return h;
            foreach (var p in result.Promoted) PieceAt(p)?.SetDam(true, animate: true);
            yield return new WaitForSeconds(0.2f);
        }

        void RemovePiece(int p, Color? sparkColor)
        {
            if (!pieces.TryGetValue(p, out var victim)) return;
            pieces.Remove(p);
            Burst(ToLocal(p), sparkColor ?? (victim.name.StartsWith("Hitam") ? Palette.PieceA : Palette.PieceB), sparkColor.HasValue ? 20 : -1);
            StartCoroutine(victim.Captured());
        }

        // Percikan dibuat dari kubus-kubus kecil yang dihancurkan sendiri setelah animasinya selesai.
        // Untuk efek yang lebih banyak dan rumit, Unity punya Particle System (komponen khusus
        // efek partikel); di sini cukup objek sederhana supaya mudah dipahami.
        void Burst(Vector3 at, Color color, int count = -1)
        {
            if (count < 0) count = feel.captureParticles;
            var mat = Mats.Lit(color, 0.6f, 0f, 1.5f);
            var goldMat = Mats.Lit(Palette.AccentPrada, 0.6f, 0f, 2f);
            for (int i = 0; i < count; i++)
            {
                float ang = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(ang), Random.Range(0.6f, 1.4f), Mathf.Sin(ang));
                var spark = Mats.Prim(PrimitiveType.Cube, hintsRoot, "Spark", i % 3 == 0 ? goldMat : mat, at + Vector3.up * 0.3f, Vector3.one * Random.Range(0.07f, 0.12f));
                spark.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                StartCoroutine(Spark(spark, at + Vector3.up * 0.3f, dir * Random.Range(0.5f, 0.9f)));
            }
        }

        IEnumerator Spark(Transform t, Vector3 from, Vector3 velocity)
        {
            float s = t.localScale.x;
            var spin = Random.rotation;
            yield return Ease.Tween(0.45f, k =>
            {
                // A short ballistic arc that shrinks away.
                t.localPosition = from + velocity * k * 1.6f + Vector3.down * (k * k * 1.2f);
                t.localRotation = Quaternion.Slerp(Quaternion.identity, spin, k);
                t.localScale = Vector3.one * s * (1f - k);
            }, Ease.Linear, unscaled: false);
            Destroy(t.gameObject);
        }
    }
}
