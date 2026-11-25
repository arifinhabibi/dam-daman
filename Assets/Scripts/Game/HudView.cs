using System;
using System.Collections;
using Pamor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pamor
{
    // BELAJAR UNITY - UI (uGUI):
    // UI Unity dibuat dari GameObject di bawah sebuah CANVAS (kanvas). Setiap elemen punya
    // RectTransform (pengganti Transform untuk UI) dan komponen gambar: Image (kotak/gambar),
    // TextMeshProUGUI (teks tajam), Button (bisa ditekan). Biasanya semua ini disusun dengan
    // menyeret di Editor. Di proyek ini sengaja DIBANGUN LEWAT KODE (lihat fungsi Panel, Img,
    // Text, Button di bagian bawah file) supaya seluruh tampilan terkendali dari satu tempat dan
    // warna selalu diambil dari Palette. Untuk belajar, cobalah juga membuat satu tombol lewat
    // menu GameObject > UI > Button di Editor dan bandingkan hasilnya.
    //
    // HudView hanya MENAMPILKAN dan MELAPORKAN tombol yang ditekan lewat event (di bawah).
    // Ia tidak tahu aturan permainan; GameController yang memutuskan apa yang terjadi.
    //
    // "partial class": kelas ini dipecah di HudView.cs (layar-layar) dan HudView.Match.cs (HUD saat match).

    /// <summary>
    /// uGUI + TextMeshPro screens, built at runtime from the palette: the in-match HUD plus the
    /// Title, Pause and Ended overlays, and one fade overlay for transitions.
    /// </summary>
    public partial class HudView : MonoBehaviour
    {
        // "event Action" = pengumuman yang bisa didengar pihak lain (publish/subscribe).
        // Saat tombol ditekan, kita panggil "ResumePressed?.Invoke()"; tanda "?" berarti
        // "hanya kalau ada yang mendengarkan". GameController mendaftar di Start().
        public event Action ResumePressed, RestartPressed, TitlePressed, PausePressed;
        public event Action PlayLocalPressed, PlayAiPressed, DifficultyPressed, PlaySayembaraPressed;
        public event Action SetupRandomPressed, SetupReadyPressed, HandoffPressed;
        public event Action AjianPressed, CommanderPressed;
        public event Action<Commander> CommanderChosen;
        public event Action<bool> PancasonaAnswered;

        RectTransform root;
        GameObject hud, title, pause, ended, setup, handoff;
        TextMeshProUGUI setupTitle, setupHint, handoffTitle, handoffButtonLabel, roleInfo;
        TextMeshProUGUI ajianTitle, ajianDesc, ajianStatus, pancasonaText;
        GameObject ajianPanel, ajianButton, pancasonaPrompt;
        GameObject commanderSelect, commanderPanel, commanderButton;
        GameObject sayembara;
        RectTransform sayembaraButtons;
        TextMeshProUGUI sayembaraTitle, sayembaraBody, timerText, titleRank;
        TextMeshProUGUI commanderSelectTitle, commanderTitle, commanderDesc, commanderStatus;
        TextMeshProUGUI turnText, hintText, endedBanner, endedStats, difficultyLabel;
        Image turnDisc, fade, flash;
        RectTransform endedBannerRect, hintPanel;
        Coroutine hintRoutine;

        public bool IsBuilt => root != null;

        public void Build()
        {
            // EventSystem wajib ada agar tombol UI bisa menerima klik/sentuhan. Kalau tidak ada,
            // tombol terlihat tapi tidak merespons. InputSystemUIInputModule menghubungkannya
            // dengan paket Input System yang dipakai proyek ini.
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            // Canvas = "kertas" tempat semua UI digambar. new GameObject(nama, komponen...) langsung
            // memasang komponen yang disebut: Canvas, CanvasScaler (menyesuaikan ukuran UI dengan
            // layar), GraphicRaycaster (mendeteksi sentuhan pada UI).
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            // ScreenSpaceOverlay = UI digambar di atas segalanya, menempel ke layar (bukan di dunia 3D).
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            // PENTING UNTUK MOBILE: HP punya banyak ukuran layar. Dengan "ScaleWithScreenSize" kita
            // mendesain di satu resolusi acuan (1920x1080) dan Unity membesarkan/mengecilkan UI
            // mengikuti layar sebenarnya. Semua angka ukuran/posisi UI di file ini dalam satuan acuan itu.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // 0.5 = campuran lebar dan tinggi: layar yang lebih lebar atau lebih tinggi dari acuan
            // tetap tampak seimbang.
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGo.transform;

            // --- In-match HUD
            hud = Panel(root, "HUD", Color.clear, Vector2.zero, Vector2.one).gameObject;
            BuildMatchHud(hud.transform);
            hintPanel = FramedPanel(hud.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0, 62), new Vector2(620, 76));
            hintText = Text(hintPanel, "Label", "", 34, Palette.AccentPrada, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 70));
            hintText.characterSpacing = 4;
            hintText.enableAutoSizing = true;
            hintText.fontSizeMin = 18; hintText.fontSizeMax = 34;
            hintPanel.parent.gameObject.SetActive(false);

            // Ajian panel: appears above the Commander button when one of your pieces is selected.
            var ajian = FramedPanel(hud.transform, "Ajian", new Vector2(1f, 0f), new Vector2(-470, 150), new Vector2(400, 230));
            ajianPanel = ajian.parent.gameObject;
            ajianTitle = Text(ajian, "Title", "AJIAN", 30, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(380, 40));
            ajianDesc = Text(ajian, "Desc", "", 22, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(370, 80));
            ajianDesc.textWrappingMode = TextWrappingModes.Normal;
            ajianStatus = Text(ajian, "Status", "", 20, WithAlpha(Palette.Kelir, 0.7f), new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(370, 30));
            ajianButton = Button(ajian, "Cast", "PAKAI AJIAN", new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(300, 64), () => AjianPressed?.Invoke());
            ajianButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 30;
            ajianPanel.SetActive(false);

            // Pancasona prompt: a reaction asked when the player's Patih is about to fall.
            pancasonaPrompt = Panel(hud.transform, "Pancasona Prompt", WithAlpha(Palette.Shadow, 0.82f), Vector2.zero, Vector2.one).gameObject;
            pancasonaText = Text(pancasonaPrompt.transform, "Text", "PATIH AKAN GUGUR!\nPAKAI PANCASONA (8 TENAGA)?", 60, Palette.AccentPrada, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1500, 200));
            Button(pancasonaPrompt.transform, "Yes", "PAKAI", new Vector2(0.5f, 0.35f), new Vector2(-170, 0), new Vector2(300, 96), () => PancasonaAnswered?.Invoke(true));
            Button(pancasonaPrompt.transform, "No", "TIDAK", new Vector2(0.5f, 0.35f), new Vector2(170, 0), new Vector2(300, 96), () => PancasonaAnswered?.Invoke(false));
            pancasonaPrompt.SetActive(false);

            // --- Title: the live board is the backdrop
            title = Panel(root, "Title", WithAlpha(Palette.Shadow, 0.7f), Vector2.zero, Vector2.one).gameObject;
            var name = Text(title.transform, "Game Name", "PAMOR", 220, Palette.AccentPrada, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1400, 260));
            name.characterSpacing = 30;
            name.fontStyle = FontStyles.Bold;
            var shadow = name.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Palette.Shadow; shadow.effectDistance = new Vector2(6, -6);
            Text(title.transform, "Subtitle", "DAM-DAMAN NUSANTARA", 40, WithAlpha(Palette.Kelir, 0.85f), new Vector2(0.5f, 0.6f), new Vector2(0, -150), new Vector2(1200, 60)).characterSpacing = 18;
            Button(title.transform, "Play Local", "LAWAN TEMAN", new Vector2(0.5f, 0.3f), new Vector2(-480, 0), new Vector2(440, 96), () => PlayLocalPressed?.Invoke());
            Button(title.transform, "Play AI", "LAWAN AI", new Vector2(0.5f, 0.3f), new Vector2(0, 0), new Vector2(440, 96), () => PlayAiPressed?.Invoke());
            Button(title.transform, "Play Sayembara", "SAYEMBARA", new Vector2(0.5f, 0.3f), new Vector2(480, 0), new Vector2(440, 96), () => PlaySayembaraPressed?.Invoke());
            titleRank = Text(title.transform, "Rank", "", 30, WithAlpha(Palette.Kelir, 0.85f), new Vector2(0.5f, 0.3f), new Vector2(480, -100), new Vector2(440, 50));
            var diff = Button(title.transform, "Difficulty", "TINGKAT AI: SEDANG", new Vector2(0.5f, 0.3f), new Vector2(0, -112), new Vector2(440, 76), () => DifficultyPressed?.Invoke());
            difficultyLabel = diff.GetComponentInChildren<TextMeshProUGUI>();
            difficultyLabel.fontSize = 28;

            // --- Pause
            pause = Panel(root, "Pause", WithAlpha(Palette.Shadow, 0.75f), Vector2.zero, Vector2.one).gameObject;
            Text(pause.transform, "Label", "JEDA", 110, Palette.AccentPrada, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(800, 140)).characterSpacing = 24;
            Button(pause.transform, "Resume", "LANJUT", new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(420, 96), () => ResumePressed?.Invoke());
            Button(pause.transform, "Restart", "ULANG", new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(420, 96), () => RestartPressed?.Invoke());
            Button(pause.transform, "Title", "JUDUL", new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(420, 96), () => TitlePressed?.Invoke());

            // --- Ended
            ended = Panel(root, "Ended", WithAlpha(Palette.Shadow, 0.7f), Vector2.zero, Vector2.one).gameObject;
            endedBanner = Text(ended.transform, "Banner", "HITAM MENANG", 130, Palette.AccentPrada, new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(1600, 180));
            endedBanner.characterSpacing = 16; endedBanner.fontStyle = FontStyles.Bold;
            endedBannerRect = endedBanner.rectTransform;
            endedStats = Text(ended.transform, "Stats", "", 40, Palette.Kelir, new Vector2(0.5f, 0.64f), new Vector2(0, -130), new Vector2(1200, 60));
            Button(ended.transform, "Again", "MAIN LAGI", new Vector2(0.5f, 0.35f), new Vector2(0, 40), new Vector2(460, 96), () => RestartPressed?.Invoke());
            Button(ended.transform, "Title", "JUDUL", new Vector2(0.5f, 0.35f), new Vector2(0, -80), new Vector2(460, 96), () => TitlePressed?.Invoke());

            // --- Fase Susun: the live board stays visible; a banner on top, controls at the bottom
            setup = Panel(root, "Setup", Color.clear, Vector2.zero, Vector2.one).gameObject;
            var setupBanner = FramedPanel(setup.transform, "Setup Banner", new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(720, 96));
            setupTitle = Text(setupBanner, "Label", "FASE SUSUN · HITAM", 40, Palette.AccentPrada, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 80));
            setupTitle.characterSpacing = 6;
            var setupBar = FramedPanel(setup.transform, "Setup Bar", new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(1240, 90));
            setupHint = Text(setupBar, "Hint", "KETUK DUA BIDAKMU UNTUK MENUKAR POSISI", 24, Palette.Kelir, new Vector2(0f, 0.5f), new Vector2(370, 0), new Vector2(690, 80));
            setupHint.characterSpacing = 2;
            setupHint.enableAutoSizing = true;
            setupHint.fontSizeMin = 16; setupHint.fontSizeMax = 24;
            Button(setupBar, "Random", "ACAK", new Vector2(1f, 0.5f), new Vector2(-380, 0), new Vector2(220, 76), () => SetupRandomPressed?.Invoke());
            Button(setupBar, "Ready", "SIAP", new Vector2(1f, 0.5f), new Vector2(-140, 0), new Vector2(220, 76), () => SetupReadyPressed?.Invoke());
            var infoPanel = FramedPanel(setup.transform, "Role Info", new Vector2(0f, 0.5f), new Vector2(200, 0), new Vector2(330, 300));
            roleInfo = Text(infoPanel, "Text", "", 26, Palette.Kelir, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 280));
            roleInfo.textWrappingMode = TextWrappingModes.Normal;
            roleInfo.alignment = TextAlignmentOptions.TopLeft;

            // --- Commander select: six cards over a dim board
            commanderSelect = Panel(root, "Commander Select", WithAlpha(Palette.Shadow, 0.88f), Vector2.zero, Vector2.one).gameObject;
            commanderSelectTitle = Text(commanderSelect.transform, "Title", "PILIH COMMANDER · HITAM", 56, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1400, 80));
            commanderSelectTitle.characterSpacing = 10;
            for (int i = 0; i < CommanderBook.All.Length; i++)
            {
                var c = CommanderBook.All[i];
                var pos = new Vector2((i % 3 - 1) * 600, i < 3 ? 140 : -200);
                var card = Button(commanderSelect.transform, "Card " + c, "", new Vector2(0.5f, 0.5f), pos, new Vector2(570, 320), () => CommanderChosen?.Invoke(c));
                var label = card.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 26; label.characterSpacing = 0;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.alignment = TextAlignmentOptions.TopLeft;
                label.rectTransform.sizeDelta = new Vector2(530, 290);
                label.text = $"<size=34><color=#{ColorUtility.ToHtmlStringRGB(Palette.AccentPrada)}>{CommanderBook.Name(c).ToUpperInvariant()}</color></size>  <size=24>({CommanderBook.Origin(c)})</size>\n\n"
                           + $"<b>Pasif:</b> {CommanderBook.Passive(c)}\n\n"
                           + $"<b>Aktif · {CommanderBook.ActiveName(c)} ({CommanderBook.ActiveCost(c)}):</b> {CommanderBook.ActiveDescription(c)}";
            }

            // --- Sayembara: one reusable panel (standings, shop, pairings, results, final) with a row of buttons
            sayembara = Panel(root, "Sayembara", WithAlpha(Palette.Shadow, 0.9f), Vector2.zero, Vector2.one).gameObject;
            var frame = FramedPanel(sayembara.transform, "Frame", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1300, 820));
            sayembaraTitle = Text(frame, "Title", "SAYEMBARA", 56, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1240, 80));
            sayembaraTitle.characterSpacing = 10;
            sayembaraBody = Text(frame, "Body", "", 30, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(0, -400), new Vector2(1200, 560));
            sayembaraBody.alignment = TextAlignmentOptions.TopLeft;
            sayembaraBody.textWrappingMode = TextWrappingModes.Normal;
            sayembaraBody.enableAutoSizing = true; sayembaraBody.fontSizeMin = 18; sayembaraBody.fontSizeMax = 30;
            var row = new GameObject("Buttons", typeof(RectTransform));
            sayembaraButtons = (RectTransform)row.transform;
            sayembaraButtons.SetParent(frame, false);
            sayembaraButtons.anchorMin = sayembaraButtons.anchorMax = new Vector2(0.5f, 0f);
            sayembaraButtons.anchoredPosition = new Vector2(0, 70);
            sayembara.SetActive(false);

            // --- Handoff: an opaque cover so the next player can't see the other's army
            handoff = Panel(root, "Handoff", Palette.Shadow, Vector2.zero, Vector2.one).gameObject;
            handoffTitle = Text(handoff.transform, "Label", "SERAHKAN KE MERAH", 100, Palette.AccentPrada, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1600, 140));
            handoffTitle.characterSpacing = 16;
            Text(handoff.transform, "Sub", "JANGAN INTIP PASUKAN LAWAN", 36, WithAlpha(Palette.Kelir, 0.8f), new Vector2(0.5f, 0.62f), new Vector2(0, -110), new Vector2(1200, 60)).characterSpacing = 12;
            var go = Button(handoff.transform, "Continue", "AKU MERAH, LANJUT", new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(560, 100), () => HandoffPressed?.Invoke());
            handoffButtonLabel = go.GetComponentInChildren<TextMeshProUGUI>();

            flash = Panel(root, "Flash", WithAlpha(Palette.AccentPrada, 0f), Vector2.zero, Vector2.one).GetComponent<Image>();
            flash.raycastTarget = false;
            fade = Panel(root, "Fade", WithAlpha(Palette.Shadow, 0f), Vector2.zero, Vector2.one).GetComponent<Image>();
            fade.raycastTarget = false;
        }

        public void SetDifficulty(Difficulty d)
        {
            difficultyLabel.text = "TINGKAT AI: " + d.ToString().ToUpperInvariant();
            var t = difficultyLabel.transform;
            StartCoroutine(Ease.Tween(0.25f, k => t.localScale = Vector3.one * Mathf.LerpUnclamped(1.2f, 1f, k), Ease.OutBack));
        }

        public void ShowScreen(GameState state)
        {
            title.SetActive(state == GameState.Title);
            hud.SetActive(state == GameState.Playing || state == GameState.Paused);
            pause.SetActive(state == GameState.Paused);
            ended.SetActive(state == GameState.Ended);
            setup.SetActive(state == GameState.Setup);
            handoff.SetActive(state == GameState.Handoff);
            commanderSelect.SetActive(state == GameState.CommanderSelect);
            sayembara.SetActive(state == GameState.Sayembara);
            if (state != GameState.Playing) commanderPanel.SetActive(false);
        }

        /// <summary>Fills the Sayembara panel. Each button is (label, action, enabled); the row is rebuilt every call.</summary>
        public void ShowSayembara(string titleText, string body, params (string label, Action action, bool enabled)[] buttons)
        {
            sayembaraTitle.text = titleText;
            sayembaraBody.text = body;
            for (int i = sayembaraButtons.childCount - 1; i >= 0; i--) Destroy(sayembaraButtons.GetChild(i).gameObject);
            float width = buttons.Length > 3 ? 230 : 320, gap = 20;
            float total = buttons.Length * width + (buttons.Length - 1) * gap;
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, action, enabled) = buttons[i];
                var b = Button(sayembaraButtons, "Btn " + label, label, new Vector2(0.5f, 0.5f), new Vector2(-total / 2 + width / 2 + i * (width + gap), 0), new Vector2(width, 84), () => action());
                b.GetComponent<Button>().interactable = enabled;
                var t = b.GetComponentInChildren<TextMeshProUGUI>();
                t.characterSpacing = 2;
                t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 30;
            }
        }

        public void SetTitleRank(string text) => titleRank.text = text;

        public void SetCommanderSelect(Side side) => commanderSelectTitle.text = "PILIH COMMANDER · " + SideWord(side);

        static string SideWord(Side s) => s == Side.Hitam ? "HITAM" : "MERAH";

        public void SetSetup(Side side, string hint)
        {
            setupTitle.text = "FASE SUSUN · " + SideWord(side);
            setupHint.text = hint;
        }

        /// <summary>Shows what a role does, in the panel beside the board during Fase Susun.</summary>
        public void SetRoleInfo(string text) => roleInfo.text = text;

        public void SetHandoff(Side nextPlayer)
        {
            handoffTitle.text = "SERAHKAN KE " + SideWord(nextPlayer);
            handoffButtonLabel.text = "AKU " + SideWord(nextPlayer) + ", LANJUT";
        }

        public IEnumerator FadeOut() => Ease.Tween(0.15f, t => fade.color = WithAlpha(Palette.Shadow, t), Ease.OutCubic);
        public IEnumerator FadeIn() => Ease.Tween(0.15f, t => fade.color = WithAlpha(Palette.Shadow, 1f - t), Ease.OutCubic);

        void SetHint(string text)
        {
            hintText.text = text;
            hintPanel.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Shows the selected piece's ajian; blocker is why it can't be cast now (null = castable).</summary>
        public void ShowAjian(Ajian a, string blocker)
        {
            ajianPanel.SetActive(true);
            ajianTitle.text = $"{AjianBook.Name(a).ToUpperInvariant()} · {AjianBook.Cost(a)}";
            ajianDesc.text = AjianBook.Description(a);
            ajianStatus.text = blocker ?? "Siap dipakai. Tidak menghabiskan langkah.";
            ajianButton.GetComponent<Button>().interactable = blocker == null;
        }

        public void HideAjian() => ajianPanel.SetActive(false);

        /// <summary>A brief gold flash over the screen, for ajian casts.</summary>
        public void FlashAccent() =>
            StartCoroutine(Ease.Tween(0.25f, t => flash.color = WithAlpha(Palette.AccentPrada, Mathf.Lerp(0.35f, 0f, t)), Ease.OutCubic));

        public void ShowPancasonaPrompt(Side owner, bool show)
        {
            pancasonaPrompt.SetActive(show);
            if (show) pancasonaText.text = $"PATIH {SideWord(owner)} AKAN GUGUR!\nPAKAI PANCASONA (8 TENAGA)?";
        }

        /// <summary>Briefly pulses the hint line, e.g. when a player taps a piece that can't move.</summary>
        public void NudgeHint(string text)
        {
            SetHint(text);
            var pill = hintPanel.parent;
            if (hintRoutine != null) StopCoroutine(hintRoutine);
            hintRoutine = StartCoroutine(Ease.Tween(0.35f, t => pill.localScale = Vector3.one * Mathf.LerpUnclamped(1.2f, 1f, t), Ease.OutBack));
        }

        /// <summary>The win moment: flash, then the banner scales in with an overshoot.</summary>
        public IEnumerator PlayEnd(string banner, int moves)
        {
            endedBanner.text = banner;
            endedBanner.color = Palette.AccentPrada;
            endedStats.text = $"{moves} LANGKAH";
            endedBannerRect.localScale = Vector3.zero;
            StartCoroutine(Ease.Tween(0.1f, t => flash.color = WithAlpha(Palette.AccentPrada, Mathf.Lerp(0.6f, 0f, t)), Ease.OutCubic));
            yield return new WaitForSecondsRealtime(0.05f);
            yield return Ease.Tween(0.35f, t => endedBannerRect.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, t), Ease.OutBack);
        }

        // ---------- builders

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // BELAJAR UNITY - ANCHOR (jangkar) pada RectTransform, konsep UI terpenting:
        // Posisi elemen UI diukur DARI TITIK JANGKARNYA pada induknya, bukan dari pojok kiri atas layar.
        // Jangkar (0,0) = pojok kiri bawah induk, (1,1) = pojok kanan atas, (0.5,0.5) = tengah.
        //  - anchorMin dan anchorMax BERBEDA -> elemen MELAR mengikuti induknya (fungsi Panel di bawah:
        //    (0,0)-(1,1) artinya memenuhi seluruh induk, dipakai untuk latar layar penuh).
        //  - anchorMin = anchorMax -> elemen berukuran TETAP dan menempel ke satu titik (fungsi Img, Text).
        // Itulah sebabnya tombol "pause" tetap di pojok kiri atas di HP kecil maupun tablet besar.
        // anchoredPosition = geseran dari jangkar; sizeDelta = lebar dan tinggi (dalam satuan acuan).
        static RectTransform Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            // typeof(RectTransform), typeof(Image): GameObject baru langsung dipasangi dua komponen ini.
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;    // tanpa tepi: pas dengan area jangkar
            var img = go.GetComponent<Image>();
            img.color = color;
            // raycastTarget = apakah elemen ini MENANGKAP sentuhan. Latar transparan tidak boleh
            // menangkapnya, kalau tidak, ia menghalangi sentuhan ke papan di belakangnya.
            img.raycastTarget = color.a > 0f;
            return rt;
        }

        /// <summary>A wood panel with a gold inlay border: the placeholder for the batik frame.</summary>
        static RectTransform FramedPanel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var outer = Img(parent, name, Palette.AccentPrada, anchor, pos, size + new Vector2(10, 10));
            var inner = Img(outer.transform, "Inner", Palette.FrameWood, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            outer.raycastTarget = inner.raycastTarget = false;
            return inner.rectTransform;
        }

        static Image Img(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            // TextMeshPro (TMP) = sistem teks modern Unity: lebih tajam dan fleksibel daripada
            // komponen Text lama. Butuh "TMP Essential Resources" (font bawaan) yang sudah
            // diimpor ke proyek; kalau belum, teks pertama akan error.
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static GameObject Button(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            var frame = Img(parent, name, Palette.AccentPrada, anchor, pos, size);
            var inner = Img(frame.transform, "Inner", Palette.FrameWood, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(10, 10));
            inner.raycastTarget = false;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.75f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
            button.colors = colors;
            // onClick = event bawaan Button. AddListener mendaftarkan fungsi yang dipanggil saat
            // tombol ditekan. Di sini diteruskan ke "onClick" yang dikirim pemanggil (lihat event
            // di bagian atas file, mis. PausePressed).
            button.onClick.AddListener(() => onClick());
            var t = Text(frame.transform, "Label", label, 38, Palette.Kelir, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            t.characterSpacing = 10;
            return frame.gameObject;
        }

        static Sprite circleSprite;
        static Sprite MakeCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - 1f - Mathf.Sqrt(dx * dx + dy * dy))));
            }
            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return circleSprite;
        }
    }
}

