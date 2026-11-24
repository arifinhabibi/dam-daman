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

## Prototype choices not in the GDD (to confirm or replace)

- AI difficulty names are Mudah / Sedang / Sulit; the GDD names them **Prajurit / Senopati / Patih**.
- Draw ("seri") after 50 turns of only Dams moving; the GDD instead uses the passive limit (5 turns without a capture collapses the outer edge of the board).
- Capture chains are always completed (the GDD says chains are allowed, not required).
- Built from the GDD's base rules: Dam demotion after 3 own turns without capturing, the 3-capture combo bonus move (a bonus move can't chain into another). Not yet: Patih and the roles.
