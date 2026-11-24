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
    /// <summary>
    /// uGUI + TextMeshPro screens, built at runtime from the palette: the in-match HUD plus the
    /// Title, Pause and Ended overlays, and one fade overlay for transitions.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        public event Action ResumePressed, RestartPressed, TitlePressed, PausePressed;
        public event Action PlayLocalPressed, PlayAiPressed, DifficultyPressed;
        public event Action SetupRandomPressed, SetupReadyPressed, HandoffPressed;
        public event Action AjianPressed, CommanderPressed;
        public event Action<Commander> CommanderChosen;
        public event Action<bool> PancasonaAnswered;

        RectTransform root;
        GameObject hud, title, pause, ended, setup, handoff;
        TextMeshProUGUI setupTitle, setupHint, handoffTitle, handoffButtonLabel, roleInfo;
        TextMeshProUGUI hitamTenaga, merahTenaga, ajianTitle, ajianDesc, ajianStatus, pancasonaText;
        GameObject ajianPanel, ajianButton, pancasonaPrompt;
        GameObject commanderSelect, commanderPanel, commanderButton;
        TextMeshProUGUI commanderSelectTitle, commanderTitle, commanderDesc, commanderStatus, hitamCommander, merahCommander;
        TextMeshProUGUI turnText, hintText, hitamCount, merahCount, hitamName, merahName, endedBanner, endedStats, difficultyLabel;
        Image turnDisc, fade, flash;
        RectTransform endedBannerRect, hintPanel;
        Coroutine hintRoutine;

        public bool IsBuilt => root != null;

        public void Build()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGo.transform;

            // --- In-match HUD
            hud = Panel(root, "HUD", Color.clear, Vector2.zero, Vector2.one).gameObject;
            var turnPanel = FramedPanel(hud.transform, "Turn", new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(520, 96));
            turnDisc = Img(turnPanel, "Disc", Palette.PieceA, new Vector2(0f, 0.5f), new Vector2(64, 0), new Vector2(54, 54));
            turnDisc.sprite = MakeCircleSprite();
            turnText = Text(turnPanel, "Label", "GILIRAN HITAM", 40, Palette.Kelir, new Vector2(0.5f, 0.5f), new Vector2(30, 0), new Vector2(400, 80));
            turnText.characterSpacing = 6;
            hintPanel = FramedPanel(hud.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0, 62), new Vector2(620, 76));
            hintText = Text(hintPanel, "Label", "", 34, Palette.AccentPrada, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 70));
            hintText.characterSpacing = 6;
            hintPanel.parent.gameObject.SetActive(false);

            var left = FramedPanel(hud.transform, "Hitam Info", new Vector2(0f, 0.64f), new Vector2(170, 0), new Vector2(240, 210));
            hitamTenaga = Text(left, "Tenaga", "TENAGA 0/10", 26, Palette.Kelir, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(220, 36));
            hitamName = Text(left, "Name", "HITAM", 34, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(220, 50));
            hitamName.characterSpacing = 6;
            hitamCommander = Text(left, "Commander", "", 20, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(220, 28));
            hitamCount = Text(left, "Count", "12", 64, Palette.AccentPrada, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(220, 80));
            var right = FramedPanel(hud.transform, "Merah Info", new Vector2(1f, 0.64f), new Vector2(-170, 0), new Vector2(240, 210));
            merahTenaga = Text(right, "Tenaga", "TENAGA 0/10", 26, Palette.Kelir, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(220, 36));
            merahName = Text(right, "Name", "MERAH", 34, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(220, 50));
            merahName.characterSpacing = 6;
            merahCommander = Text(right, "Commander", "", 20, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(220, 28));
            merahCount = Text(right, "Count", "12", 64, Palette.AccentPrada, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(220, 80));

            // Ajian panel: appears when one of your pieces is selected.
            var ajian = FramedPanel(hud.transform, "Ajian", new Vector2(1f, 0f), new Vector2(-230, 150), new Vector2(400, 230));
            ajianPanel = ajian.parent.gameObject;
            ajianTitle = Text(ajian, "Title", "AJIAN", 30, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(380, 40));
            ajianDesc = Text(ajian, "Desc", "", 22, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(370, 80));
            ajianDesc.textWrappingMode = TextWrappingModes.Normal;
            ajianStatus = Text(ajian, "Status", "", 20, WithAlpha(Palette.Kelir, 0.7f), new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(370, 30));
            ajianButton = Button(ajian, "Cast", "PAKAI AJIAN", new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(300, 64), () => AjianPressed?.Invoke());
            ajianButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 30;
            ajianPanel.SetActive(false);

            // Commander active panel: bottom-left, during your own turn.
            var cmd = FramedPanel(hud.transform, "Commander", new Vector2(0f, 0f), new Vector2(230, 150), new Vector2(400, 230));
            commanderPanel = cmd.parent.gameObject;
            commanderTitle = Text(cmd, "Title", "", 26, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(380, 40));
            commanderDesc = Text(cmd, "Desc", "", 20, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(0, -86), new Vector2(370, 80));
            commanderDesc.textWrappingMode = TextWrappingModes.Normal;
            commanderStatus = Text(cmd, "Status", "", 20, WithAlpha(Palette.Kelir, 0.7f), new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(370, 30));
            commanderButton = Button(cmd, "Use", "PAKAI", new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(300, 64), () => CommanderPressed?.Invoke());
            commanderButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 30;
            commanderPanel.SetActive(false);

            // Pancasona prompt: a reaction asked when the player's Patih is about to fall.
            pancasonaPrompt = Panel(hud.transform, "Pancasona Prompt", WithAlpha(Palette.Shadow, 0.82f), Vector2.zero, Vector2.one).gameObject;
            pancasonaText = Text(pancasonaPrompt.transform, "Text", "PATIH AKAN GUGUR!\nPAKAI PANCASONA (8 TENAGA)?", 60, Palette.AccentPrada, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1500, 200));
            Button(pancasonaPrompt.transform, "Yes", "PAKAI", new Vector2(0.5f, 0.35f), new Vector2(-170, 0), new Vector2(300, 96), () => PancasonaAnswered?.Invoke(true));
            Button(pancasonaPrompt.transform, "No", "TIDAK", new Vector2(0.5f, 0.35f), new Vector2(170, 0), new Vector2(300, 96), () => PancasonaAnswered?.Invoke(false));
            pancasonaPrompt.SetActive(false);

            var pauseBtn = Button(hud.transform, "Pause Button", "II", new Vector2(1f, 1f), new Vector2(-80, -70), new Vector2(96, 96), () => PausePressed?.Invoke());
            pauseBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 40;

            // --- Title: the live board is the backdrop
            title = Panel(root, "Title", WithAlpha(Palette.Shadow, 0.7f), Vector2.zero, Vector2.one).gameObject;
            var name = Text(title.transform, "Game Name", "PAMOR", 220, Palette.AccentPrada, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1400, 260));
            name.characterSpacing = 30;
            name.fontStyle = FontStyles.Bold;
            var shadow = name.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Palette.Shadow; shadow.effectDistance = new Vector2(6, -6);
            Text(title.transform, "Subtitle", "DAM-DAMAN NUSANTARA", 40, WithAlpha(Palette.Kelir, 0.85f), new Vector2(0.5f, 0.6f), new Vector2(0, -150), new Vector2(1200, 60)).characterSpacing = 18;
            Button(title.transform, "Play Local", "LAWAN TEMAN", new Vector2(0.5f, 0.3f), new Vector2(-250, 0), new Vector2(460, 96), () => PlayLocalPressed?.Invoke());
            Button(title.transform, "Play AI", "LAWAN AI", new Vector2(0.5f, 0.3f), new Vector2(250, 0), new Vector2(460, 96), () => PlayAiPressed?.Invoke());
            var diff = Button(title.transform, "Difficulty", "TINGKAT AI: SEDANG", new Vector2(0.5f, 0.3f), new Vector2(250, -112), new Vector2(460, 76), () => DifficultyPressed?.Invoke());
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

        public void SetPlayerNames(string hitam, string merah)
        {
            hitamName.text = hitam;
            merahName.text = merah;
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
            if (state != GameState.Playing) commanderPanel.SetActive(false);
        }

        public void SetCommanderSelect(Side side) => commanderSelectTitle.text = "PILIH COMMANDER · " + SideWord(side);

        public void SetCommanders(Commander? hitam, Commander? merah)
        {
            hitamCommander.text = hitam.HasValue ? CommanderBook.Name(hitam.Value) : "";
            merahCommander.text = merah.HasValue ? CommanderBook.Name(merah.Value) : "";
        }

        /// <summary>Shows the active player's Commander active; blocker is why it can't be used now (null = ready).</summary>
        public void ShowCommander(Commander c, string blocker)
        {
            commanderPanel.SetActive(true);
            commanderTitle.text = $"{CommanderBook.ActiveName(c).ToUpperInvariant()} · {CommanderBook.ActiveCost(c)}";
            commanderDesc.text = CommanderBook.ActiveDescription(c);
            commanderStatus.text = blocker ?? CommanderBook.Name(c) + " siap.";
            commanderButton.GetComponent<Button>().interactable = blocker == null;
        }

        public void HideCommander() => commanderPanel.SetActive(false);

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

        public void SetTurn(Side side, string label, string hint)
        {
            turnText.text = label;
            turnDisc.color = side == Side.Hitam ? Palette.PieceA : Palette.PieceB;
            SetHint(hint);
        }

        void SetHint(string text)
        {
            hintText.text = text;
            hintPanel.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void SetTenaga(int hitam, int merah)
        {
            hitamTenaga.text = $"TENAGA {hitam}/{AjianBook.MaxTenaga}";
            merahTenaga.text = $"TENAGA {merah}/{AjianBook.MaxTenaga}";
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

        public void SetCounts(int hitam, int merah)
        {
            hitamCount.text = hitam.ToString();
            merahCount.text = merah.ToString();
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

        static RectTransform Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
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

