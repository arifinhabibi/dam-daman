using System;
using System.Collections.Generic;
using Pamor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pamor
{
    /// <summary>
    /// The in-match HUD, laid out like a lobby battler: a player roster on the right, a round header on
    /// top, an icon column and a battle log on the left, the Tenaga Dalam orb bottom-left and the
    /// Commander button bottom-right. Pamor's own batik-and-wayang styling throughout.
    /// </summary>
    public partial class HudView
    {
        public event Action HelpPressed;

        // Round header
        TextMeshProUGUI roundBadge;
        Image hitamBar, merahBar;
        // Roster
        RectTransform rosterRoot;
        readonly List<RosterRow> rows = new List<RosterRow>();
        // Orb and Commander button
        Image tenagaFill;
        TextMeshProUGUI tenagaNumber, coinsText, commanderCaption;
        // Log
        GameObject logPanel;
        TextMeshProUGUI logText, logTitle;
        readonly List<string> logLines = new List<string>();
        bool showingGuide;

        // Two-player model for vs AI and Lawan Teman (Sayembara supplies a full roster instead).
        readonly string[] names = { "HITAM", "MERAH" };
        readonly Commander?[] commanderOf = new Commander?[2];
        readonly int[] counts = { 16, 16 };
        readonly int[] tenaga = new int[2];
        List<RosterEntry> lobbyRoster;
        Side viewer = Side.Hitam;

        /// <summary>One line of the roster.</summary>
        public struct RosterEntry
        {
            public string Name, Subtitle, ValueText;
            public float Fill;
            public Color Avatar;
            public bool Highlight, Out;
        }

        sealed class RosterRow
        {
            public GameObject Go;
            public Image Frame, Avatar, Bar;
            public TextMeshProUGUI Initial, Name, Subtitle, Value;
        }

        void BuildMatchHud(Transform parent)
        {
            var circle = MakeCircleSprite();

            // --- Round header: badge, timer pill, a strength bar for each side, whose turn under it
            var pill = FramedPanel(parent, "Round Pill", new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(150, 80));
            timerText = Text(pill, "Timer", "", 50, Palette.AccentPrada, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 76));
            roundBadge = Text(FramedPanel(parent, "Round Badge", new Vector2(0.5f, 1f), new Vector2(-560, -56), new Vector2(150, 56)), "Label", "", 28, Palette.Kelir, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 50));
            // Hitam's own colour is near-black, so its bar uses the light kelir tone to stay readable.
            hitamBar = Bar(parent, "Hitam Bar", new Vector2(-300, -56), Palette.Kelir, Image.OriginHorizontal.Right);
            merahBar = Bar(parent, "Merah Bar", new Vector2(300, -56), Palette.PieceB, Image.OriginHorizontal.Left);
            turnDisc = Img(parent, "Turn Disc", Palette.PieceA, new Vector2(0.5f, 1f), new Vector2(-120, -128), new Vector2(34, 34));
            turnDisc.sprite = circle;
            turnText = Text(parent, "Turn", "", 30, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(20, -128), new Vector2(360, 40));
            turnText.characterSpacing = 6;

            // --- Roster (top-right)
            var rosterGo = new GameObject("Roster", typeof(RectTransform));
            rosterRoot = (RectTransform)rosterGo.transform;
            rosterRoot.SetParent(parent, false);
            rosterRoot.anchorMin = rosterRoot.anchorMax = new Vector2(1f, 1f);
            rosterRoot.anchoredPosition = new Vector2(-180, -44);
            rosterRoot.sizeDelta = Vector2.zero; // rows hang from this point, not from the top of a default 100x100 box
            for (int i = 0; i < Sayembara.Players; i++) rows.Add(MakeRow(rosterRoot, i, circle));

            // --- Icon column (left) and the battle log beside it
            IconButton(parent, "Pause", "II", new Vector2(70, -70), () => PausePressed?.Invoke());
            IconButton(parent, "Log", "LOG", new Vector2(70, -170), () => ToggleLog(false));
            IconButton(parent, "Help", "?", new Vector2(70, -270), () => { ToggleLog(true); HelpPressed?.Invoke(); });
            var log = FramedPanel(parent, "Log Panel", new Vector2(0f, 1f), new Vector2(330, -240), new Vector2(320, 380));
            logPanel = log.parent.gameObject;
            logTitle = Text(log, "Title", "CATATAN", 26, Palette.AccentPrada, new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(300, 36));
            logText = Text(log, "Lines", "", 20, Palette.Kelir, new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(296, 320));
            logText.alignment = TextAlignmentOptions.TopLeft;
            logText.textWrappingMode = TextWrappingModes.Normal;

            // --- Tenaga Dalam orb (bottom-left)
            var orbBack = Img(parent, "Tenaga Orb", Palette.AccentPrada, new Vector2(0f, 0f), new Vector2(130, 140), new Vector2(170, 170));
            orbBack.sprite = circle; orbBack.raycastTarget = false;
            var orbInner = Img(orbBack.transform, "Inner", Palette.FrameWood, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            orbInner.sprite = circle; orbInner.raycastTarget = false;
            tenagaFill = Img(orbBack.transform, "Fill", WithAlpha(Palette.AccentPrada, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            tenagaFill.sprite = circle; tenagaFill.type = Image.Type.Filled; tenagaFill.fillMethod = Image.FillMethod.Vertical; tenagaFill.raycastTarget = false;
            tenagaNumber = Text(orbBack.transform, "Number", "0", 64, Palette.Kelir, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(150, 80));
            Text(orbBack.transform, "Label", "TENAGA", 20, Palette.Kelir, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(150, 30)).characterSpacing = 4;
            coinsText = Text(parent, "Coins", "", 28, Palette.AccentPrada, new Vector2(0f, 0f), new Vector2(130, 34), new Vector2(220, 40));

            // --- Commander button (bottom-right), with a caption above it
            var cmdGo = new GameObject("Commander", typeof(RectTransform));
            var cmdRoot = (RectTransform)cmdGo.transform;
            cmdRoot.SetParent(parent, false);
            cmdRoot.anchorMin = cmdRoot.anchorMax = new Vector2(1f, 0f);
            cmdRoot.anchoredPosition = new Vector2(-140, 140);
            commanderPanel = cmdGo;
            var ring = Img(cmdRoot, "Ring", Palette.AccentPrada, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 180));
            ring.sprite = circle;
            var face = Img(ring.transform, "Face", Palette.FrameWood, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156, 156));
            face.sprite = circle; face.raycastTarget = false;
            var btn = ring.gameObject.AddComponent<Button>();
            btn.targetGraphic = ring;
            btn.onClick.AddListener(() => CommanderPressed?.Invoke());
            commanderButton = ring.gameObject;
            commanderTitle = Text(ring.transform, "Cost", "", 56, Palette.AccentPrada, new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(150, 70));
            commanderStatus = Text(ring.transform, "Status", "", 18, Palette.Kelir, new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(140, 40));
            commanderStatus.textWrappingMode = TextWrappingModes.Normal;
            commanderCaption = Text(cmdRoot, "Caption", "", 22, Palette.Kelir, new Vector2(0.5f, 0.5f), new Vector2(0, 112), new Vector2(300, 40));
            commanderDesc = commanderCaption; // the long description is shown in the hint when tapped
            commanderPanel.SetActive(false);
        }

        Image Bar(Transform parent, string name, Vector2 pos, Color color, Image.OriginHorizontal origin)
        {
            var back = Img(parent, name, Palette.FrameWood, new Vector2(0.5f, 1f), pos, new Vector2(380, 22));
            back.raycastTarget = false;
            var trim = back.gameObject.AddComponent<Outline>();
            trim.effectColor = Palette.AccentPrada; trim.effectDistance = new Vector2(2, -2);
            var fill = Img(back.transform, "Fill", color, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(372, 14));
            fill.sprite = MakeSquareSprite();
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = (int)origin;
            fill.raycastTarget = false;
            return fill;
        }

        void IconButton(Transform parent, string name, string label, Vector2 pos, Action onClick)
        {
            var ring = Img(parent, name, Palette.AccentPrada, new Vector2(0f, 1f), pos, new Vector2(84, 84));
            ring.sprite = MakeCircleSprite();
            var face = Img(ring.transform, "Face", Palette.FrameWood, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));
            face.sprite = MakeCircleSprite(); face.raycastTarget = false;
            var b = ring.gameObject.AddComponent<Button>();
            b.targetGraphic = ring;
            b.onClick.AddListener(() => onClick());
            var t = Text(ring.transform, "Label", label, label.Length > 2 ? 22 : 34, Palette.Kelir, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 60));
            t.characterSpacing = 2;
        }

        RosterRow MakeRow(RectTransform parent, int index, Sprite circle)
        {
            var frame = Img(parent, "Row " + index, Palette.FrameWood, new Vector2(0.5f, 1f), new Vector2(0, -index * 62), new Vector2(320, 56));
            frame.raycastTarget = false;
            var trim = frame.gameObject.AddComponent<Outline>();
            trim.effectColor = WithAlpha(Palette.AccentPrada, 0.6f); trim.effectDistance = new Vector2(2, -2);
            var row = new RosterRow { Go = frame.gameObject, Frame = frame };
            var ring = Img(frame.transform, "Avatar Ring", Palette.AccentPrada, new Vector2(1f, 0.5f), new Vector2(-30, 0), new Vector2(48, 48));
            ring.sprite = circle; ring.raycastTarget = false;
            row.Avatar = Img(ring.transform, "Avatar", Palette.PieceA, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            row.Avatar.sprite = circle; row.Avatar.raycastTarget = false;
            row.Initial = Text(ring.transform, "Initial", "", 22, Palette.Kelir, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            row.Name = Text(frame.transform, "Name", "", 21, Palette.Kelir, new Vector2(0f, 1f), new Vector2(130, -14), new Vector2(240, 26));
            row.Name.alignment = TextAlignmentOptions.Right;
            row.Subtitle = Text(frame.transform, "Subtitle", "", 16, Palette.AccentPrada, new Vector2(0f, 0f), new Vector2(66, 12), new Vector2(120, 20));
            row.Subtitle.alignment = TextAlignmentOptions.Left;
            var barBack = Img(frame.transform, "Bar Back", Palette.Shadow, new Vector2(0f, 0f), new Vector2(160, 27), new Vector2(180, 8));
            barBack.raycastTarget = false;
            row.Bar = Img(barBack.transform, "Bar", Palette.AccentPrada, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 8));
            row.Bar.sprite = MakeSquareSprite();
            row.Bar.type = Image.Type.Filled; row.Bar.fillMethod = Image.FillMethod.Horizontal; row.Bar.fillOrigin = (int)Image.OriginHorizontal.Left;
            row.Bar.raycastTarget = false;
            row.Value = Text(frame.transform, "Value", "", 20, Palette.Kelir, new Vector2(0f, 0f), new Vector2(224, 12), new Vector2(70, 20));
            row.Value.alignment = TextAlignmentOptions.Right;
            return row;
        }

        // ---------- updates

        /// <summary>Whose Tenaga Dalam the orb shows and whose Commander the button belongs to.</summary>
        public void SetViewer(Side side) { viewer = side; RenderOrb(); }

        public void SetPlayerNames(string hitam, string merah) { names[0] = hitam; names[1] = merah; RenderRoster(); }

        public void SetCommanders(Commander? hitam, Commander? merah) { commanderOf[0] = hitam; commanderOf[1] = merah; RenderRoster(); }

        public void SetCounts(int hitam, int merah)
        {
            counts[0] = hitam; counts[1] = merah;
            hitamBar.fillAmount = hitam / 16f;
            merahBar.fillAmount = merah / 16f;
            RenderRoster();
        }

        public void SetTenaga(int hitam, int merah) { tenaga[0] = hitam; tenaga[1] = merah; RenderOrb(); RenderRoster(); }

        /// <summary>Sayembara: the full 8-player roster; null returns to the two-player roster.</summary>
        public void SetLobbyRoster(List<RosterEntry> entries) { lobbyRoster = entries; RenderRoster(); }

        public void SetCoins(int? coins) => coinsText.text = coins.HasValue ? $"KOIN {coins.Value}" : "";

        /// <summary>The badge left of the timer (e.g. "RONDE 3" or "GILIRAN 7").</summary>
        public void SetRound(string text) => roundBadge.text = text;

        /// <summary>Header number: the turn timer in Sayembara (negative hides it).</summary>
        public void SetTimer(int seconds)
        {
            timerText.text = seconds < 0 ? "" : seconds.ToString();
            timerText.color = seconds >= 0 && seconds <= 5 ? Palette.PieceB : Palette.AccentPrada;
        }

        public void SetTurn(Side side, string label, string hint)
        {
            turnText.text = label;
            turnDisc.color = side == Side.Hitam ? Palette.PieceA : Palette.PieceB;
            SetHint(hint);
        }

        void RenderOrb()
        {
            if (tenagaFill == null) return;
            int t = tenaga[viewer == Side.Hitam ? 0 : 1];
            tenagaFill.fillAmount = t / (float)AjianBook.MaxTenaga;
            tenagaNumber.text = t.ToString();
        }

        void RenderRoster()
        {
            if (rows.Count == 0) return;
            var entries = lobbyRoster ?? TwoPlayerRoster();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool on = i < entries.Count;
                row.Go.SetActive(on);
                if (!on) continue;
                var e = entries[i];
                row.Name.text = e.Name;
                row.Subtitle.text = e.Subtitle;
                row.Value.text = e.ValueText;
                row.Bar.fillAmount = Mathf.Clamp01(e.Fill);
                row.Avatar.color = e.Avatar;
                row.Initial.text = string.IsNullOrEmpty(e.Name) ? "" : e.Name.Substring(0, 1);
                row.Frame.color = e.Highlight ? Color.Lerp(Palette.FrameWood, Palette.AccentPrada, 0.35f) : Palette.FrameWood;
                float a = e.Out ? 0.4f : 1f;
                row.Name.alpha = row.Subtitle.alpha = row.Value.alpha = a;
            }
        }

        List<RosterEntry> TwoPlayerRoster()
        {
            var list = new List<RosterEntry>();
            for (int i = 0; i < 2; i++)
                list.Add(new RosterEntry
                {
                    Name = names[i],
                    Subtitle = commanderOf[i].HasValue ? CommanderBook.Name(commanderOf[i].Value) : "",
                    ValueText = $"{counts[i]} · T{tenaga[i]}",
                    Fill = counts[i] / 16f,
                    Avatar = i == 0 ? Palette.PieceA : Palette.PieceB,
                    Highlight = (i == 0 ? Side.Hitam : Side.Merah) == viewer,
                });
            return list;
        }

        /// <summary>Shows the player's Commander button: cost on the face, active name above, status under the cost.</summary>
        public void ShowCommander(Commander c, string blocker)
        {
            commanderPanel.SetActive(true);
            commanderTitle.text = CommanderBook.ActiveCost(c).ToString();
            commanderCaption.text = CommanderBook.ActiveName(c).ToUpperInvariant();
            commanderStatus.text = blocker == null ? "SIAP" : "";
            commanderButton.GetComponent<Image>().color = blocker == null ? Palette.AccentPrada : WithAlpha(Palette.AccentPrada, 0.45f);
        }

        public void HideCommander() => commanderPanel.SetActive(false);

        // ---------- battle log and role guide

        public void ClearLog() { logLines.Clear(); RenderLog(); }

        public void AddLog(string line)
        {
            logLines.Insert(0, line);
            if (logLines.Count > 10) logLines.RemoveAt(logLines.Count - 1);
            if (!showingGuide) RenderLog();
        }

        void RenderLog()
        {
            logTitle.text = "CATATAN";
            logText.text = logLines.Count == 0 ? "Belum ada langkah." : string.Join("\n", logLines);
        }

        void ToggleLog(bool guide)
        {
            if (logPanel.activeSelf && showingGuide == guide) { logPanel.SetActive(false); return; }
            logPanel.SetActive(true);
            showingGuide = guide;
            if (!guide) { RenderLog(); return; }
            logTitle.text = "PERAN";
            logText.text = "<b>Prajurit</b> 1 titik maju/samping\n<b>Pendekar</b> 1 titik ke segala arah\n<b>Senopati</b> sampai 2 titik\n<b>Dukun</b> tak makan, melindungi\n<b>Telik Sandi</b> menyamar\n<b>Patih</b> gugur = kalah\n<b>Dam</b> terbang sepanjang garis";
        }

        static Sprite squareSprite;
        static Sprite MakeSquareSprite()
        {
            if (squareSprite != null) return squareSprite;
            var tex = new Texture2D(4, 4);
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return squareSprite;
        }
    }
}
