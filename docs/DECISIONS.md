# Pamor — decision log

Decisions made after GDD Draft 1 ([GDD.md](GDD.md)). Where an entry here conflicts with the GDD, this log wins until the GDD is updated.

| Date | Decision | Overrides GDD | Status in code |
|---|---|---|---|
| 2026-11-24 | **Board: the traditional dam-daman line board** (pieces on line intersections, moves along the lines, triangle wings on two sides), from the owner's reference photo. Exact layout and rules still to be researched and confirmed. | "Aturan dasar" 1–3 (8x8, dark squares, diagonal moves); role movement patterns are written for 8x8 diagonals and need redesigning | Not started. Current prototype is 8x8 |
| 2026-11-24 | **Wajib makan: "dibakar" (huffing), per GDD.** A player may skip an available capture; the piece that could have captured is then removed. | — (confirms GDD rule 4) | Not done. Prototype forces captures |
| 2026-11-24 | Look: 3D board and pieces, camera above the board that the player can orbit around; tilt limited to 40° from straight down. Original Pamor art (wayang, batik), not a copy of another game's look. | "Gaya art: siluet ala wayang kulit, flat dan tegas" now applies to 3D pieces and boards | Not started |
| 2026-11-24 | Order of work: vs AI first (done), then board rules, then 3D. Lobby flow built offline first; Unity Cloud linked only when real matchmaking starts. | Matches the GDD roadmap (online last) | — |
| 2026-11-24 | Sayembara = 8-player round format per GDD (100 HP, rotating 1v1 pairings, shop phase). | — (confirms GDD) | Not started |

## Prototype choices not in the GDD (to confirm or replace)

- AI difficulty names are Mudah / Sedang / Sulit; the GDD names them **Prajurit / Senopati / Patih**.
- Men capture backward; the GDD only lets Pendekar move backward and doesn't say for captures.
- Draw ("seri") after 50 turns of only Dams moving; the GDD instead uses the passive limit (5 turns without a capture collapses the outer edge of the board).
- Not yet built from the GDD's base rules: Dam demotion after 3 turns without a capture, the 3-capture combo bonus move, Patih and the roles.
