GDD — Pamor: Taktik Dam Nusantara
Nov 24, 2026 · @Muhammad Arifin Habibi · Draft 1 (working title)
Ringkasan
Pamor adalah game taktik giliran yang berakar dari dam-daman Jawa, ditambah role bidak, ajian, dan pusaka bertema kerajaan Nusantara. Game ini bukan dam-daman digital: aturan dam (gerak diagonal, lompat untuk makan, promosi jadi Dam) jadi fondasi, sisanya bebas dikembangkan.
• Pitch: dam-daman versi modern, dengan role, ajian, dan ranked lobby 8 orang.
• Target pemain: pemain mobile Indonesia yang suka strategi singkat (pemain Magic Chess, catur online, dan game kartu).
• Platform: Android dulu, lalu PC. Engine Unity (C#).
• Durasi: match 1v1 sekitar 8–12 menit, lobby 8 orang sekitar 25–30 menit.
• Pembeda: mekanik dam yang familiar, wajib makan yang memaksa bentrok, dan identitas budaya Nusantara.
Istilah dalam game ini sengaja dipilih dari Nusantara: Sayembara untuk lobby 8 orang, Liga Nusantara untuk mode musiman, Pamor untuk peringkat, dan Unggah-ungguh untuk skor perilaku pemain.
Aturan dasar
Permainan berjalan di papan 8x8 dengan gerak diagonal, dan makan bidak lawan bersifat wajib.
1.  Papan: 8x8, hanya petak gelap yang dipakai. Tiap pemain punya 12 bidak di 3 baris terdekat.
2.  Gerak: diagonal 1 petak maju, kecuali role tertentu.
3.  Makan: lompati bidak lawan yang bersebelahan ke petak kosong di belakangnya. Makan beruntun boleh dalam satu giliran.
4.  Wajib makan: kalau ada kesempatan makan tapi tidak diambil, bidak itu "dibakar" (dihapus) oleh lawan.
5.  Batas pasif: 5 giliran berturut-turut tanpa makan membuat baris paling pinggir papan runtuh.
6.  Promosi: Prajurit yang sampai baris ujung lawan naik jadi Dam (gerak jauh diagonal). Dam turun lagi jadi Prajurit kalau 3 giliran tidak makan.
7.  Combo: makan 3 bidak dalam satu giliran memberi 1 langkah bonus.
8.  Menang: bunuh Patih lawan, atau habisi semua bidak lawan.
Pertanyaan terbuka: ukuran papan 8x8 atau 10x10 untuk mode lobby?
Role bidak
Dua belas bidak tiap pemain terbagi ke enam role, dan posisinya ditata rahasia di Fase Susun sebelum match.
Role
Jumlah
Gerak
Kemampuan pasif
Prajurit
6
Diagonal 1 maju
Promosi jadi Dam di baris ujung
Pendekar
2
Diagonal 1, boleh mundur
Makan beruntun tanpa harus lompat lurus
Telik Sandi
1
Diagonal 1 maju
Terlihat sebagai Prajurit sampai makan atau dimakan
Dukun
1
Diagonal 1 maju
Tidak bisa makan; melindungi 1 bidak di sebelahnya 1 giliran
Senopati
1
Diagonal sampai 2 petak
Kalau mati, Prajurit sekutu tidak bisa makan 1 giliran
Patih
1
Diagonal 1 segala arah
Target utama: Patih mati berarti kalah
Dam
promosi
Diagonal jauh
Turun pangkat kalau 3 giliran tidak makan
Fase Susun: tiap pemain menata 12 bidak di 3 baris awalnya tanpa dilihat lawan. Letak Telik Sandi dan Patih jadi tebak-tebakan pertama.
Commander
Selain role bidak, tiap pemain memilih satu Commander di awal match. Commander punya 1 kemampuan pasif dan 1 kemampuan aktif yang dibayar dengan Tenaga Dalam, dan hanya didapat lewat bermain, bukan dibeli, supaya ranked tetap adil.
Commander
Asal
Pasif
Aktif
Sangkuriang
Sunda
Di Fase Susun boleh menaruh 2 petak penghalang
Perahu Semalam (6): tembok 3 petak selama 2 giliran
Si Pitung
Betawi
Tiap makan mencuri 1 Tenaga Dalam lawan
Bayangan Pitung (4): Telik Sandi sembunyi lagi walau sudah ketahuan
Calon Arang
Bali
Santet lebih murah 1
Teluh Massal (7): 3 bidak lawan acak tidak bisa gerak 1 giliran
Jaka Tingkir
Jawa
Pendekar kebal 1 serangan pertama
Kerbau Ngamuk (5): 1 bidak menerjang lurus 3 petak, makan semua di jalurnya
Sawerigading
Bugis
Bidak baris pinggir gerak 1 petak lebih jauh
Layar Pinisi (5): memindahkan 2 bidak ke sisi papan lain
Lutung Kasarung
Sunda
Dukun bisa melindungi 2 bidak
Wujud Asli (6): 1 Prajurit langsung naik jadi Dam
Commander dipilih sebelum Fase Susun dan tetap sama sepanjang match. Daftar ini akan bertambah tiap update, dan jadi sumber konten jangka panjang utama.
Prinsip pay-to-win: kemampuan Commander tidak pernah dijual. Yang bisa dibeli hanya tampilan (lihat Kosmetik).
Latar belakang dan sinergi Commander
• Sangkuriang (Sunda). Pemuda yang nyaris menikahi ibunya sendiri, Dayang Sumbi, lalu diberi syarat mustahil: membangun bahtera dalam semalam. Gagal, ia menendang perahunya hingga jadi Gunung Tangkuban Parahu. Temanya penghalang dan waktu yang berkejaran, cocok untuk gaya main bertahan/stalling; sinergi kuat dengan Dukun untuk mengunci area tengah papan.
• Si Pitung (Betawi). Jawara Betawi yang merampok tuan tanah dan Kompeni untuk dibagikan ke rakyat kecil, lincah dan sulit ditangkap. Cocok untuk gaya agresif-ekonomi: mencuri Tenaga Dalam lawan lewat makan beruntun; sinergi kuat dengan Telik Sandi untuk gaya menyusup berulang.
• Calon Arang (Bali/Jawa). Janda sakti yang ditakuti karena ilmu hitamnya, memicu wabah lewat kutukan saat dihina. Fokus kontrol tim lewat ajian yang melumpuhkan banyak bidak sekaligus; sinergi kuat dengan Dukun untuk tumpukan kutukan (Santet + Teluh Massal).
• Jaka Tingkir (Jawa). Pemuda yang menaklukkan kerbau liar dan buaya saat menyeberangi sungai, lalu naik takhta jadi raja Pajang. Gaya frontline agresif: kebal 1 serangan lalu menerjang lurus memakan semua yang menghalangi; sinergi kuat dengan Pendekar dan Senopati untuk dorongan lini depan.
• Sawerigading (Bugis). Pangeran pelaut dari epos La Galigo, mengarungi lautan luas dengan kapalnya mencari pinangan. Fokus mobilitas dan kontrol peta: bidak pinggir bergerak lebih jauh, bisa memindahkan bidak lintas papan; sinergi kuat untuk gaya split-push yang meregangkan pertahanan lawan.
• Lutung Kasarung (Sunda). Pangeran terkutuk jadi lutung hitam yang diam-diam membantu Purbasari sebelum wujud aslinya terungkap. Fokus scaling akhir permainan: memperkuat perlindungan Dukun lalu membuka wujud asli dengan promosi instan; sinergi kuat dengan Prajurit yang dijaga sampai momentum siap.
Tenaga Dalam dan Ajian
Ajian dibayar dengan Tenaga Dalam, dan pemakaiannya tidak menghabiskan jatah gerak di giliran itu.
• Mulai dengan 0, dapat +1 tiap giliran, +2 tiap makan bidak, maksimal 10.
• Satu ajian per giliran. Cooldown minimal 2 giliran untuk ajian yang sama.
Role
Ajian
Biaya
Efek
Prajurit
Tameng Waja
2
Kebal dimakan 1 giliran
Pendekar
Saipi Angin
3
Gerak 2 kali dalam 1 giliran
Telik Sandi
Panglimunan
3
Tak terlihat 2 giliran, bisa lewat di antara bidak lawan
Dukun
Santet
4
1 bidak lawan tidak bisa gerak 2 giliran
Dukun
Rajah Penolak
2
Membatalkan ajian lawan yang baru dipakai
Senopati
Gelar Supit Urang
5
Prajurit di sekitarnya maju 1 langkah bersamaan
Patih
Pancasona
8
Sekali per match: hidup lagi di petak awal saat mati
Ajian Pamungkas
Dipilih satu di Fase Susun, dilepas Patih saat Tenaga Dalam penuh (biaya 10).
• Gelap Ngampar: semua bidak lawan di satu baris terkena Santet 1 giliran.
• Rawa Rontek: bidak sekutu yang mati di giliran ini hidup lagi.
• Lembu Sekilan: semua bidak sekutu kebal 1 giliran.
Versi pertama cukup 1 ajian per role, tanpa Ajian Pamungkas, supaya balance bisa diuji dulu.
Detail cooldown dan jangkauan Ajian
Ajian | Cooldown | Jangkauan | Catatan taktis
Tameng Waja | 2 giliran | Diri sendiri | Pakai sebelum melangkah ke petak yang rawan dimakan.
Saipi Angin | 3 giliran | Diri sendiri | Dikombinasikan dengan bonus combo (makan 3 bidak) untuk mobilitas berlapis.
Panglimunan | 4 giliran | Diri sendiri | Dipakai saat posisi Telik Sandi sudah ketahuan, untuk reset tebak-tebakan lawan.
Santet | 3 giliran | 1 bidak lawan, radius 3 petak | Sasar Patih atau Senopati lawan untuk membuka lini.
Rajah Penolak | 2 giliran | Ajian lawan yang baru dipakai | Reaktif: harus dipakai di giliran yang sama atau giliran berikutnya untuk membatalkan.
Gelar Supit Urang | 5 giliran | Prajurit dalam radius 2 petak dari Senopati | Dipakai untuk breakthrough saat barisan lawan padat.
Pancasona | Sekali per match | Diri sendiri (Patih) | Idealnya dipicu manual sesaat sebelum Patih benar-benar mati, bukan otomatis, supaya pemain tetap punya kendali taktis.
Ajian Pamungkas (Gelap Ngampar, Rawa Rontek, Lembu Sekilan) tidak punya cooldown karena hanya bisa dipakai sekali per match saat Tenaga Dalam penuh (biaya 10).
Pusaka di papan
Satu pusaka muncul acak di petak kosong area tengah tiap 4 giliran, dan bidak yang menginjaknya mendapat bonus permanen. Tujuannya memaksa pemain rebutan wilayah tengah.
Pusaka
Efek
Keris
Bisa makan dari jarak 2 petak
Tombak
Bisa makan lurus, bukan cuma diagonal
Batu Akik
+1 Tenaga Dalam tambahan tiap giliran
Selendang
Sekali teleport ke petak kosong mana pun
Satu bidak hanya boleh membawa 1 pusaka. Kalau bidak pembawa mati, pusakanya jatuh di petak tempat ia mati.
Kosmetik
Semua yang bisa dibeli di luar progres bermain bersifat kosmetik murni dan tidak mengubah aturan atau kekuatan bidak.
Skin papan
• Candi: batu andesit dengan motif relief
• Sawah Terasering: papan bertingkat, ada suara air dan katak
• Pendopo Keraton: lantai kayu jati berukir
• Pasar Terapung: papan di atas air dengan perahu jukung
• Mega Mendung: motif batik Cirebon yang bergerak pelan
Skin bidak: wayang kulit, ukiran kayu Jepara, batu candi, wayang golek.
Skin Commander: kostum alternatif tiap Commander, tanpa mengubah pasif atau aktifnya.
Nuansa Nusantara di semua bagian
• Audio: gamelan di menu, kendang makin cepat saat ronde menegangkan, angklung untuk combo makan, gong saat Patih mati
• UI: bingkai batik, ornamen aksara Sunda/Jawa di judul, teks utama tetap Latin
• Gaya art: siluet ala wayang kulit, flat dan tegas
• Cerita: tiap Commander punya kisah singkat yang bisa dibaca di menu
Catatan sensitivitas: hindari tokoh yang disakralkan atau dipercaya secara spiritual, dan tokoh agama. Tokoh sejarah nyata butuh kehati-hatian ekstra karena risiko digambarkan kalah atau lucu; tokoh legenda lebih aman.
Mode game
Enam mode dibangun bertahap, dari yang offline sampai liga online.
Mode
Pemain
Online
Isi
Tutorial
1
Tidak
5 level: gerak, wajib makan, role, ajian, pusaka
vs AI
1
Tidak
3 tingkat kesulitan (Prajurit, Senopati, Patih)
Local PvP
2
Tidak
Hotseat di satu device
Draft
2
Opsional
Susun komposisi role sendiri dengan budget 100 poin
Sayembara
8
Ya
Format bertahan hidup 8 orang, home/away, mempengaruhi Pamor
Liga Nusantara
8 per grup
Ya
Liga mingguan/musiman, promosi dan degradasi antar divisi
Mode tambahan nanti: Puzzle ("menang dalam 3 langkah") dan Campaign per kerajaan.
Format Sayembara (Lobby 8 orang)
Delapan pemain mulai dengan 100 HP, bertarung 1v1 bergilir tiap ronde, dan urutan tersingkir menentukan peringkat 8 sampai 1.
1.  Fase Belanja (30 detik): dapat koin tiap ronde (5 + bonus menang beruntun). Beli role, pusaka, atau upgrade ajian untuk pasukan yang dibawa ke ronde berikutnya.
2.  Pairing: dipasangkan acak, tidak boleh lawan yang sama 2 ronde berturut-turut. Jumlah pemain ganjil berarti satu pemain melawan "bayangan" pasukan pemain yang sudah tersingkir.
3.  Ronde cepat: maksimal 10 giliran per pemain, 20 detik per giliran. Waktu habis berarti pemenang ditentukan dari total nilai bidak yang tersisa.
4.  Damage: yang kalah kehilangan HP = jumlah bidak lawan yang hidup + nomor ronde.
5.  Tersingkir: HP 0 berarti keluar lobby. Pemain terakhir yang bertahan juara 1.
Home dan away. Tiap pertandingan 1v1 dalam satu ronde Sayembara punya tuan rumah (home) dan tamu (away), dimainkan di papan milik home.

Home
Away
Papan & musik
Skin milik home dipakai
Main di papan home
Langkah pertama
Jalan kedua
Jalan duluan (kompensasi)
Pandangan
Bidak sendiri selalu di bawah layar
Sama, tampilan di-flip di client
Sistem mencatat jumlah giliran home tiap pemain, dan yang paling sedikit dapat giliran home berikutnya supaya merata sepanjang Sayembara. Aturan, Commander, dan ajian tidak pernah berbeda antara home dan away; yang beda hanya tampilan dan urutan langkah.
Pertanyaan terbuka: pasukan dibangun dari nol tiap lobby (gaya auto-battler) atau dibawa dari koleksi pemain?
Penanganan AFK dan Autopilot
Tiap giliran harus ada yang gerak, jadi satu pemain AFK bisa membekukan seluruh Sayembara. Deteksi dan hukuman berjalan di server, tidak bisa dimanipulasi dari client.
1.  Timer giliran: 20 detik. Lewat waktu, autopilot mengambil makan wajib kalau ada, atau langkah aman acak.
2.  Strike: 2 kali timeout berturut-turut menandai pemain AFK; autopilot mengambil alih penuh, termasuk belanja dasar di Fase Belanja. Autopilot sengaja dibuat lemah supaya tidak dimanfaatkan.
3.  Reconnect: kontrol kembali ke pemain mulai giliran berikutnya. Kurang dari 2 strike, tidak ada hukuman.
4.  Hukuman Pamor: AFK 3 ronde atau lebih membuat Pamor tidak naik walau hasil akhir top 4, ditambah potongan tetap (−15).
5.  Mode 1v1 ranked lebih tegas: 2 kali timeout berturut-turut berarti kalah otomatis.
Unggah-ungguh (skor perilaku, mulai 100): AFK atau keluar paksa −10, match selesai bersih +2. Di bawah 80 tidak bisa masuk ranked; di bawah 60 hanya dipertemukan sesama pemain skor rendah.
Liga Nusantara
Liga adalah versi panjang dari Sayembara: tiap pemain bertemu 7 lawan dalam satu grup selama 1 minggu atau 1 season, dengan home dan away yang diatur merata.
• Menang 3 poin, seri 1 poin, kalah 0 poin.
• Grup berisi 8 pemain.
• Match bisa dimainkan kapan saja dalam rentang hari yang ditentukan, cocok untuk pemain yang tidak selalu online bersamaan.
Liga ditaruh di fase 6 roadmap, setelah Sayembara online berjalan stabil: penjadwalan dan notifikasi butuh fondasi server yang sama.
Sistem peringkat Pamor
Pamor adalah skor peringkat pengganti Elo. Pemain mulai di 1000 Pamor; top 4 lobby selalu naik, bottom 4 selalu turun.
Peringkat lobby
Pamor dasar
1
+40
2
+30
3
+20
4
+10
5
−10
6
−20
7
−30
8
−40
Penyesuaian kekuatan lobby: poin naik dikali (rata-rata Pamor lobby ÷ Pamor pemain), poin turun dikali kebalikannya. Pengali dibatasi 0.5 sampai 1.5.
\text{Pamor}_{baru} = \text{Pamor}_{lama} + \text{dasar} \times \text{clamp}\left(\frac{\overline{\text{Pamor}}_{lobby}}{\text{Pamor}_{lama}},\ 0.5,\ 1.5\right)
Mode 1v1 ranked memakai Elo standar di belakang layar (K = 40 untuk 20 match pertama, lalu K = 20), tapi tetap ditampilkan sebagai Pamor.
Pamor
Pangkat
di bawah 1000
Prajurit
1000–1299
Pendekar
1300–1599
Senopati
1600–1899
Patih
1900 ke atas
Maharaja
Season: 2–3 bulan. Di akhir season, Pamor ditarik setengah jalan ke 1000, dan hadiah skin bidak diberikan sesuai pangkat tertinggi.
Arsitektur teknis
Logika permainan dipisah dari tampilan Unity, supaya aturan yang sama bisa dipakai client, AI, dan server.
Core (C# murni, tanpa UnityEngine)
 ├─ BoardState        // posisi bidak, giliran, Tenaga Dalam
 ├─ MoveGenerator     // daftar langkah legal + wajib makan
 ├─ Command           // Move, Capture, CastAjian (bisa di-undo dan di-replay)
 └─ RulesEngine       // menang/kalah, promosi, runtuhnya papan

Data (ScriptableObject)
 ├─ RoleData          // nama, pola gerak, ajian default
 ├─ AjianData         // biaya, cooldown, target, efek
 └─ PusakaData

Unity (tampilan)
 ├─ BoardView / PieceView
 ├─ InputController   // klik bidak, klik petak
 └─ UI (HUD Tenaga Dalam, daftar ajian)

AI
 └─ Minimax + alpha-beta di atas Core
• Status efek (kebal, terkutuk, tak terlihat) disimpan sebagai daftar StatusEffect di tiap bidak, dan event OnTurnStart / OnCaptured menjalankannya.
• Backend: Next.js/Node + PostgreSQL untuk akun, riwayat match, dan Pamor; WebSocket untuk realtime. Alternatif: Nakama (open source, self-host) atau Unity Gaming Services.
• Anti-curang: server menjalankan Core yang sama dan memvalidasi tiap Command. Client tidak pernah menghitung Pamor.
Roadmap dan risiko
Pengembangan dimulai dari fase Fondasi, dan fase berikutnya baru dikerjakan setelah gerbangnya lolos.
Fitur online sengaja ditaruh paling akhir: mekanik lobby bisa diuji penuh dengan AI sebelum ada server.
Risiko utama
• Balance: ajian dan pusaka bisa membuat satu role dominan. Catat win rate per role sejak playtest pertama.
• Durasi: match dam bisa lama. Uji ronde cepat 10 giliran sejak fase 4.
• Scope: lobby 8 orang online butuh matchmaking dan reconnect. Jangan dimulai sebelum fase 1–4 selesai.
• Kerumitan untuk pemain baru: banyak role dan ajian bisa membingungkan. Tutorial harus memperkenalkannya satu per satu.
