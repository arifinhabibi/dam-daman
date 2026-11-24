# Pamor — decision log

Decisions made after GDD Draft 1 ([GDD.md](GDD.md)). Where an entry here conflicts with the GDD, this log wins until the GDD is updated.

| Date | Decision | Overrides GDD | Status in code |
|---|---|---|---|
| 2026-11-24 | **Board: the traditional dam-daman line board** (pieces on line intersections, moves along the lines, triangle wings on two sides), from the owner's reference photo. Layout and rules confirmed: see "Board rules" below. | "Aturan dasar" 1–3 (8x8, dark squares, diagonal moves); role movement patterns are written for 8x8 diagonals and need redesigning | Done (2D view; 3D next) |
| 2026-11-24 | **Wajib makan: "dibakar" (huffing), per GDD.** A player may skip an available capture; the piece that could have captured is then removed. | — (confirms GDD rule 4) | Done. With several possible capturers, the one with the biggest capture burns (implementation choice) |
| 2026-11-24 | Look: 3D board and pieces, camera above the board that the player can orbit around; tilt limited to 40° from straight down. Original Pamor art (wayang, batik), not a copy of another game's look. | "Gaya art: siluet ala wayang kulit, flat dan tegas" now applies to 3D pieces and boards | Not started |
| 2026-11-24 | Order of work: vs AI first (done), then board rules, then 3D. Lobby flow built offline first; Unity Cloud linked only when real matchmaking starts. | Matches the GDD roadmap (online last) | — |
| 2026-11-24 | Sayembara = 8-player round format per GDD (100 HP, rotating 1v1 pairings, shop phase). | — (confirms GDD) | Not started |

## Board rules (confirmed 2026-11-24)

Researched from Indonesian sources on dam-daman / dham-dhaman (16 pieces each, a square plus two triangles, moves along the lines) and the related alquerque-family boards (Sixteen Soldiers, Permainan Tabal); sources disagree on details, so the owner picked each rule.

- **Board: 37 points.** A 5x5 alquerque grid (25 points): orthogonal lines everywhere, plus the two long diagonals and the diamond joining the edge midpoints. A **6-point triangle** is attached to the middle point of each of two opposite edges: a middle row of 3 points and an outer row of 3 points, joined by three rays from the attachment point and by a line across each row.
- **Setup: 16 pieces each.** Own triangle (6) + the two nearest columns of the square (10). The centre column (5 points) starts empty.
- **Movement (ordinary pieces / Prajurit):** one step along a line to an empty adjacent point, **forward, forward-diagonal or sideways; never backward.**
- **Capture:** jump an adjacent enemy along a line to the empty point straight beyond it, in the **same directions as movement** (no backward capture). Chains allowed in one turn. Skipping an available capture follows the GDD's "dibakar" rule.
- **Promotion:** a piece that reaches the **outer edge of the opponent's triangle** becomes a **Dam**. The Dam flies any distance along a straight line (and captures that way), per the GDD.
- **Win:** per the GDD (kill the Patih, or remove every enemy piece). Occupying the enemy triangle does **not** win by itself.

Still open: how the GDD's passive-limit "outer edge collapse" maps onto this board; role counts for 16 pieces (the GDD splits 12); "diagonal" role moves become "along a line" (e.g. Senopati: up to 2 points along a line).

## Roles on the line board (confirmed 2026-11-24)

- **Counts for 16 pieces:** Prajurit 10, Pendekar 2, Telik Sandi 1, Dukun 1, Senopati 1, Patih 1 (the GDD's 12 plus 4 Prajurit).
- **"Diagonal" in the GDD's role table becomes "along a line"** on this board (e.g. Senopati: up to 2 points along a line; Patih: 1 point along any line, any direction).
- **Capture chains go straight:** ordinary pieces keep jumping in one straight direction; only **Pendekar** (and the Dam) may turn between jumps, per the Pendekar passive.
- **Characters:** every role gets its own placeholder 3D figure in a wayang-like silhouette (Patih: tall crown; Senopati: spear; Dukun: staff; etc.) so roles read in play; real art replaces them later.
- **Fase Susun and hidden roles:** enemy roles are hidden (shown as Prajurit) until revealed. Local PvP uses a pass-the-phone cover screen between setups (and between turns, so each player only sees their own roles). vs AI: the AI arranges its own army.

**Implemented 2026-11-24 (commit 8d897cd), with these implementation choices to confirm:**
- Senopati moves up to 2 points but never backward; Pendekar and Patih move and capture in any direction.
- Dukun shield triggers **when the Dukun itself moves**: it shields its most important neighbour (Patih > Senopati > Pendekar > Dam > Dukun > Telik Sandi > Prajurit) for the opponent's next turn. The shield is visible and reveals the Dukun.
- Reveal rule as proposed below (any capture; Pendekar/Patih moving backward; Senopati moving 2; Dukun shielding; promotion).
- A Senopati's fall blocks its side's (non-Dam) Prajurit from capturing on their next turn; Telik Sandi is not affected.
- Only Prajurit promote to Dam (not Telik Sandi, even though it looks like one).
- Draw counter counts only turns where a backward-capable piece (Dam, Pendekar, Patih) moved without capture or burn.
- The AI currently sees hidden enemy roles.
- Fase Susun (built): each player swaps roles between their own 16 starting points (tap two pieces), or presses ACAK for a random valid army, then SIAP. The last arrangement is kept for the next match. The AI arranges at random every match (Patih never on its front column). Lawan Teman shows a "Serahkan ke …" cover between the two setups and between every turn (not between a combo's bonus move), and the camera turns to the player to move. All roles are shown when the match ends.

Still open: the exact reveal rule (proposal: a piece is revealed when it does something only its role can do, captures, or is captured; Telik Sandi stays hidden until it captures or is captured, per the GDD), and where Fase Susun may place pieces (the GDD says "3 baris awal"; proposal: the player's own 16 starting points).

## Tenaga Dalam and ajian v1 (built 2026-11-24)

Per the GDD (one ajian per role, no Ajian Pamungkas; Dukun gets Santet, not Rajah Penolak) and its "Detail cooldown dan jangkauan" table. Implementation choices to confirm:
- Tenaga Dalam: 0 at start, +1 at the end of each own turn, +2 per captured piece, max 10. One ajian per turn, cast before the move; it doesn't use the move.
- Cooldown N = the next N own turns can't cast that ajian.
- **Tameng Waja** (Prajurit, not a Dam): immune to capture during the opponent's next turn.
- **Saipi Angin**: the Pendekar may move twice; the second move must be that Pendekar (skipped if it has no legal move).
- **Panglimunan**: the Telik Sandi is invisible to the opponent and can't be captured for 2 opponent turns; casting doesn't reveal it. **Not yet: "bisa lewat di antara bidak lawan"** (passing through enemy pieces).
- **Santet**: an enemy within 3 steps along lines can't move or capture during its owner's next 2 turns.
- **Gelar Supit Urang**: every allied Prajurit within 2 steps of the Senopati steps straight forward (along its row) if that point is free; no captures.
- **Pancasona**: a reaction. When a move is about to kill a Patih whose side has 8 Tenaga and hasn't used it, that player is asked (the AI always says yes). If used, the Patih comes back at its starting point (or the first free own starting point). Once per match. In Lawan Teman the prompt appears on the mover's screen.
- Casting reveals the caster's role, except Panglimunan.
- The AI considers casting each turn (shorter search with vs without each option; casts only when clearly better). Its search assumes Pancasona is always used when affordable and doesn't look ahead at future ajian.

## Commanders (built 2026-11-24)

Each player picks a Commander before Fase Susun (vs AI: the AI picks at random). The choice is public: both side panels show it. Line-board readings and choices to confirm:
- **The Commander active shares the one-ability-per-turn limit with ajian**, costs Tenaga Dalam, doesn't use the move, and has a 3-turn cooldown (the GDD sets none).
- **Sangkuriang:** passive: 2 blockers on free centre points (column e) during Fase Susun, for the whole match. Blockers and walls can't be entered, landed on, jumped or flown through. Active **Perahu Semalam (6):** pick a free point; it and the points directly above and below it (if free) become a wall for 2 opponent turns.
- **Si Pitung:** passive: steals 1 Tenaga Dalam per captured piece. Active **Bayangan Pitung (4):** a revealed Telik Sandi becomes hidden again.
- **Calon Arang:** passive: Santet costs 3. Active **Teluh Massal (7):** 3 enemy pieces (not unseen ones) can't move on their owner's next turn. "Random" is deterministic per position (seeded by the turn count), so replays and the AI see the same pick.
- **Jaka Tingkir:** passive: every Pendekar survives the first capture that hits it (the capturer still lands beyond). Active **Kerbau Ngamuk (5):** one piece charges up to 3 points along a line and takes every enemy it passes (stops at own pieces, blockers, immune/shielded/unseen enemies). It can kill the Patih; Pancasona is offered.
- **Sawerigading:** passive: pieces on the edge rows (row 1 and 5) step one point farther. Active **Layar Pinisi (5):** two of your pieces jump to the same column on the opposite edge row (row y becomes 4 − y) if that point is free.
- **Lutung Kasarung:** passive: the Dukun shields its two most important neighbours. Active **Wujud Asli (6):** one Prajurit becomes a Dam.
- The AI weighs its Commander active each turn with the same shorter search it uses for ajian (Kerbau charges are only tried when they take something; options are capped at 40).

## Sayembara, offline (built 2026-11-24)

The full lobby loop runs offline against 7 AI players, as the GDD roadmap suggests ("mekanik lobby bisa diuji penuh dengan AI sebelum ada server"). Owner decisions: simple shop, Commander once per lobby, GDD timers, local Pamor.
- **Flow:** SAYEMBARA on the title → Commander (kept for the lobby) → standings → each round: Fase Belanja (30 s) → pairings → Fase Susun → your 1v1 → results → next round, until you're out or win → final place and Pamor.
- **Shop:** income 5 coins per round + streak bonus (+1 at 2 wins in a row, +2 at 3, +3 at 4+); unspent coins carry over. Upgrades turn a Prajurit into Pendekar (3), Senopati (5), Dukun (3) or Telik Sandi (2); limits Pendekar 4, others 2, at least 4 Prajurit. Upgrades carry over between rounds. Bots buy at random (sometimes saving).
- **Pairing:** random, no immediate rematch; whoever has had fewer home games is home. Home moves second; away moves first. With an odd number left, one player faces a "bayangan" (an eliminated player's army, played by the AI; it takes no damage). No board skins exist yet, so "home board" changes nothing visible.
- **Rounds:** 10 turns per player (20 turns total), then the winner is decided by army value: Prajurit 1, Telik Sandi/Dukun 2, Pendekar/Senopati 3, Dam 4, Patih 5. Equal value = draw: both lose HP equal to the round number. Loser loses HP = winner's pieces left + round number.
- **Timer:** 20 s per turn for you; at 0 the autopilot moves (biggest capture, else a random safe move). The GDD's strikes and AFK penalties are not built (offline has no one to penalise).
- **Elimination and places:** at 0 HP you're out; several out in the same round place by HP (lower places worse). The lobby ends for you when you're out (your place is final then).
- **Pamor:** saved on the device (`pamor.rating`, starts 1000), GDD table +40..−40 scaled by lobby average ÷ your Pamor (clamped 0.5..1.5); rank titles per the GDD. Bot Pamor is your Pamor ± 150.
- **Bot-vs-bot rounds** are resolved instantly with a fast shallow AI without ajian or Commander actives. Your opponent in your own round uses the full AI at the chosen difficulty, with ajian and its Commander. A Sangkuriang bot places no blockers in Sayembara.
- Leaving a round (pause → ULANG/JUDUL) leaves the lobby.

## Prototype choices not in the GDD (to confirm or replace)

- AI difficulty names are Mudah / Sedang / Sulit; the GDD names them **Prajurit / Senopati / Patih**.
- Draw ("seri") after 50 turns of only Dams moving; the GDD instead uses the passive limit (5 turns without a capture collapses the outer edge of the board).
- Capture chains are always completed (the GDD says chains are allowed, not required).
- Built from the GDD's base rules: Dam demotion after 3 own turns without capturing, the 3-capture combo bonus move (a bonus move can't chain into another). Not yet: Patih and the roles.
