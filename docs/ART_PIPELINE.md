# Pamor — Commander 3D models: how to make and add them

The game already has a slot for every Commander's 3D model. Until a model is added, a placeholder (a gold-trimmed wayang figure on a stone pedestal) stands at each player's corner of the board and plays the same moments with simple motion.

## What each model needs

| | Spec |
|---|---|
| Format | **FBX** (preferred) or glTF/GLB, one file per Commander, with its animations in the same file or as separate FBX clips |
| Style | Match the concept art (chibi proportions, bold colours, gold ornaments) |
| Size on mobile | ~8,000–15,000 triangles; **one** material; one 1024×1024 or 2048×2048 base-colour texture (normal map optional) |
| Orientation | Faces **+Z** (forward), stands on the origin, Y up, real-world scale ~1.6–1.8 m |
| Rig | Humanoid skeleton for the human Commanders (Mixamo works). **Lutung Kasarung** (monkey with tail) needs a custom/generic rig with tail bones |
| Shader | Any standard PBR material; it is converted to URP Lit on import |

## The five animations the game plays

Make these clips and set them up in an **Animator Controller** with **Idle** as the default state and these **Trigger** parameters (exact names):

| Trigger | When the game fires it | Clip suggestion |
|---|---|---|
| *(Idle, default state, loops)* | Always | Breathing, small sway; keep it 2–4 s and seamless |
| `Cast` | The player uses an ajian or the Commander active | 1–1.5 s gesture or attack, back to Idle |
| `Hit` | One of the player's pieces is captured or burned | 0.5 s flinch, back to Idle |
| `Victory` | The player wins the match | 2–3 s celebration (may end in a held pose) |
| `Defeat` | The player loses | 1.5–2 s slump into a held pose |

Each trigger's transition goes from *Any State* (or Idle) into its clip; Cast and Hit transition back to Idle on exit time.

## A quick route from the concept art

1. **Image to 3D:** generate a base model from the concept art with an image-to-3D service (for example Meshy or Tripo), or have a 3D artist model it. Clean up and reduce to the triangle budget above.
2. **Rig and animate:** for the humanoids, upload to **Mixamo**, auto-rig, and download Idle, a casting/attack move, a hit reaction, a victory and a defeat animation (FBX for Unity, "with skin" for the first one, "without skin" for the rest). Lutung Kasarung needs rigging in Blender (or a service that supports quadrupeds/tails).
3. **Put the files in the project:** `Assets/Art/Commanders/<CommanderName>/` (for example `Assets/Art/Commanders/Sangkuriang/Sangkuriang.fbx` plus its clips).
4. **Tell Claude** (or do it yourself): set the FBX rig to *Humanoid* (Generic for Lutung), build the Animator Controller with the five triggers, and assign the model in **`Assets/Resources/CommanderArt.asset`** (slot per Commander: model, scale, rotation offset). Nothing else in the game needs to change.

## Status

| Commander | Concept art | 3D model |
|---|---|---|
| Sangkuriang | yes | waiting |
| Si Pitung | yes | waiting |
| Calon Arang | yes | waiting |
| Jaka Tingkir | **not yet** | waiting |
| Sawerigading | yes | waiting |
| Lutung Kasarung | yes (needs tail rig) | waiting |

Save the concept sheet itself to `Assets/Art/Commanders/concept-sheet.png` so it lives with the project.
