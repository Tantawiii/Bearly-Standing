# Bearly Standing — Task List (3-Day Jam)

**Pitch:** Six teddy bears. One room. Infinite pillow violence. Be the last bear standing.

**Engine:** Unity 6000.5.3f1 (URP) · **Networking:** Mirror · **Players:** 1–6 · **Match length:** ~3–7 min

**Golden rule:** the MVP is teddy + room + pillows + hit + throw + knockback + 3 hearts + elimination + 5 dumb bears. Get that fun in single-player first, then network the exact same mechanics. Everything below the MVP is seasoning — cut it before you cut that.

---

## Day 0 — Setup (current project is a blank Unity URP template)

Confirmed state: no Mirror, no Cinemachine, no voice/networking package, no 3D art/animation/prefab/material assets, and no gameplay code exist yet. `Assets/Scripts/` folder exists but is empty. New Input System is active (`InputSystem_Actions.inputactions`). `com.unity.ai.navigation` (NavMesh) is already installed — useful for AI later.

- [x] Remove template cruft: `Assets/TutorialInfo/`, default `Readme.asset`, unused `SampleScene`
- [x] Create folder structure under `Assets/Scripts/`:
  - [x] `Player/` — PlayerController, PlayerCombat, PlayerHealth, PlayerAnimator
  - [x] `Combat/` — Pillow, PillowProjectile, Hitbox, IHoldable
  - [x] `AI/` — TeddyAI, AIState, AITargeting
  - [x] `Game/` — GameManager, MatchManager, SpawnManager
  - [x] `Network/` — NetworkPlayer, NetworkGameManager
  - [x] `UI/` — MainMenuUI, LobbyUI, GameHUD, GameOverUI
  - [x] `Pickups/` — Pickup, PickupEffectType (new, for stretch power-up system)
- [x] Install Mirror (Package Manager → git URL)
- [x] Confirm `InputSystem_Actions.inputactions` covers WASD / mouse / LMB / RMB / E / Esc bindings from the control scheme below
  - WASD (Move), Mouse (Look), LMB (`Attack`), E (`Interact`) already existed — changed `Interact` from a "Hold" interaction to a plain press, since a fast brawler wants instant pickup, not hold-to-grab
  - Added missing **RMB → `Throw`** action (was only wired in the UI action map for menu right-clicks, not gameplay)
  - Added missing **Esc → `Pause`** action to the Player map (previously only existed as `Cancel` in the UI map, which wouldn't fire mid-match)
  - Renamed the Space-bound `Jump` action to `Dodge` to match the GDD (jumping is disabled; Space is reserved for the optional dodge)

### Controls Reference
| Input | Action Name | Action |
|---|---|---|
| WASD | Move | Move |
| Mouse | Look | Camera |
| Left Mouse | Attack | Pillow attack |
| Right Mouse | Throw | Throw pillow |
| E | Interact | Pick up / interact (pillow **or** downed player) |
| Space | Dodge | Dodge (cut first if tight) |
| Esc | Pause | Pause |

---

## Day 1 — Core Single-Player Loop

> **Status (2026-08-30):** Day 1 + most of Day 2 are code-complete and wired by menu tools.
> Bears use the **`ted_low` FBX + Mixamo rig** with a **real Idle / Walk / Fast-Run / Jump / Throw**
> Animator (procedural placeholders only fill gaps), each bear gets its **own colour-variant material**,
> the camera is a **Cinemachine third-person orbit**, movement now has **hold-Shift sprint** and
> **Space jump** (GDD "no jump" overridden per request). The arena is a bigger **closed pillow-fort
> room** clad in tiled pillow blocks with pillow-tower cover. There's a **cute main menu** (Host /
> Join-by-code / Practice / Quit) and **Mirror host + room-code join + bot-fill** multiplayer.
> Pillow models are data-driven — drop `pil1_low`…`pil4_low` / `pil3_low` into `Assets/Graphics` and
> rebuild, no code change. **Nothing has been playtested in-editor yet** (Unity was open; builders +
> code are verified by inspection). Networking is an MVP — see Day 2 notes.

### How to run
1. Let Unity finish compiling (no console errors — expect a couple of small fixes on first compile).
2. Menu: **Bearly Standing ▸ Build Everything** — regenerates the arena (`Day1_TestArena.unity`),
   `MainMenu.unity`, the `Bear_Human` / `Bear_AI` / `Bear_Net` / `Pillow` prefabs,
   `Ted.controller` (+ generated `.anim`s), the 6 `Ted_*.mat` variants, and injects the arena's
   network objects. (Or run `Build Day 1 Test Scene` / `Build Main Menu` / `Build Networking` individually.)
3. **Offline:** open `Day1_TestArena`, press Play — 1 human vs 5 AI. Or open `MainMenu` → **Practice**.
4. **Multiplayer:** open `MainMenu`, Play → **Host** (shows a room code) or **Join** (paste a
   friend's code, or `ip` / `ip:port`). Empty seats fill with bots after ~4 s, then the match starts.
   Two editors/builds on a LAN, or port-forward `7777/UDP` over the internet.
- Controls: **WASD** move · **Shift** run · **Space** jump · **Mouse** orbit · **LMB** swing ·
  **RMB** throw · **E** grab (pillow / downed bear — offline only).

### Morning
- [x] Project setup — URP + Mirror + Cinemachine + Input System already in place; folder structure exists
- [x] Placeholder bear — capsule path kept as the automatic fallback in `Day1SceneBuilder` when no rig is available; **`ted_low` FBX + Mixamo rig is now the default** (user delivered art early)
- [x] Player movement (`PlayerController`: CharacterController, 4.5 m/s walk, **8 m/s hold-Shift run**, accel 25, RotateTowards 720°/s)
  - [x] Gravity is `baseGravity` + `GlobalGravityMultiplier` on `PlayerController` — no hardcoded constant (Gravity Well pickup will drive the multiplier)
  - [x] **Jump** (`PlayerController.TryJump`, `jumpHeight` + coyote-time, `Space`/`Dodge` action) + `OnJumped` → Jump animation state. *Overrides the GDD "no jump" pillar at the user's request.*
- [x] Third-person camera — `CM Bear Camera` = `CinemachineCamera` + `CinemachineOrbitalFollow` (WorldSpace bind, radius 5.2, **position damping 0.45**) + `CinemachineRotationComposer` (**aim damping 0.35**) + `CinemachineDeoccluder`. `CinemachineLookInput` now **low-pass filters** the mouse delta and uses gentler sensitivity (0.075 / 0.05) so the camera glides. `CameraTarget` lowered to y=1.1 so the shot is **centred on the bear**, not its head.
- [x] Arena blockout → **fully-clad pillow fort** — 28×28 closed room; **every surface (floor, 4 walls, ceiling) is tiled** with the user's `pil1_low`..`pil4_low` prefabs (`BuildPillowFortArena`, ~230 visual tiles, no shadow pass; structural primitives keep collision). Centre is kept **clear** — cover is 4 corner cushion piles only. Loose **`pil3_low` throwables** (16 of them) scattered on the floor, not part of the cladding. Resolver now prefers `Assets/Prefabs/*.prefab` over the FBX.
- [x] Day 1 dev/test scene — `Day1_TestArena.unity`, one-click regenerated, added to Build Settings
- [x] **Main menu** — `MainMenu.unity` (`MainMenuBuilder`): Chunky-Sprout title, Host / Join-by-code / Practice / Quit, room-code + copy panel, slowly turning menu Ted (`MenuSpin`)

### Midday
- [x] Pillow pickup — `Pillow.cs` `Available → Held`, `PlayerCombat.TryInteract` OverlapSphere scan
- [x] Pillow holding — parented to `HoldSocket`, rigidbody kinematic + collider off while held
- [x] Melee swing — `PlayerCombat.SwingRoutine`, 0.15 s active window, 0.4 s cooldown, ~1 m sphere hitbox at +1 z
- [x] Hit detection — `Hitbox.cs` trigger (not the pillow mesh), one hit per target per swing, self-hit filtered
- [x] Knockback — `PlayerController.ApplyKnockback` (horizontal dir + force, separate vertical), decays over time

### Afternoon
- [x] Throw attack — `Pillow` `Held → Thrown`, `PillowProjectile` flight, lands → `Available`; human throws along camera aim (`PlayerCombat.aimSource`), AI along facing
- [x] Health system — `PlayerHealth`, 3 hearts, 1 per hit
- [x] Elimination on 3rd hit — base path via `FinalizeElimination`; kept behind the Downed extension below (kill-switch `downedStateEnabled`)
- [x] Basic UI — `GameHUD`: hearts (❤), `BEARS LEFT: n`, win/lose banner
- [x] Match win condition — `MatchManager.NotifyEliminated` → last non-Eliminated bear wins

### Evening — AI
- [x] AI state machine — `TeddyAI` / `AIState`: Idle → SearchForPillow → MoveToPillow → SearchForTarget → ChaseTarget → Attack/ThrowAtTarget → Recover
- [x] AI behavior — no pillow → nearest pillow (or attack if a target is point-blank); holding → melee if close, throw if in range; target eliminated → retarget (`AITargeting`)
- [x] Light randomness — 10% retarget drift, **40% skipped/whiffed throws**, randomised reaction delay per decision
- [x] Anti-clump — boids-style **separation** nudge (`TeddyAI.Separation`, radius 2.6) so bots stop grinding into each other; never pushes away from the current target
- [x] Spawn 5 AI bears — `GameManager` instantiates 1 human + 5 AI from prefabs and starts the match

### Downed State — single-player only, no networking yet
Extends the health system: 3rd hit no longer instantly eliminates — it knocks the bear **down**, and another player can pick them up and use them as a weapon.

- [x] `PlayerHealth` 3-state model — `Alive → Downed → Eliminated`
  - [x] `Downed` — disables own movement (`MovementEnabled`) + combat + input (`PlayerInputHandler.CanAct`); local downed timer + `DownedUseCount`; `PlayerHealth.IsDowned` flag; capsule/rig reacts via `PlayerAnimator` (freezes the Animator so the "lie down" transform pose shows)
  - [x] `FinalizeElimination()` — the only path that calls `MatchManager.NotifyEliminated`; fires on use-count `maxDownedUses` **or** `downedDuration` timeout, whichever first (both serialized/tunable); Downed still counts as in-contention
  - [x] No self-recovery — Downed only ever ends in elimination
- [x] `IHoldable` — `CanBePickedUp` / `Transform` / `OnPickedUp` / `OnDropped` / `GetSwingImpact` / `GetThrowImpact` / `OnThrown`, implemented by both `Pillow.cs` and `DownedPlayerHandle.cs`; `PlayerCombat` never branches on pillow-vs-person *(named the throw method `GetThrowImpact`, not `GetThrowPayload`)*
- [x] E/interact scan — `PlayerCombat.TryInteract` OverlapSphere picks the nearest `CanBePickedUp` `IHoldable`, so nearby `Downed` bears are found alongside `Available` pillows
- [x] Melee/throw a carried downed bear — reuses the same `Hitbox` + throw path, parameterised heavier (12/20 knockback vs the pillow's 8/14)
- [x] "Downed" + "carried" placeholder poses — transform-only (`downedLocalEuler` tilt, reparent to `HoldSocket` with `carriedLocalPosition/Euler`); no ragdoll; real clips deferred to Day 3
- [x] Validate the pickup → carry → swing/throw → finalize loop vs AI — **code-complete; final in-editor playtest pending** (see How to run)

**Day 1 Milestone:** 1 player vs. 5 AI, playable start to finish to one bear remaining, including a working (unnetworked) downed/pickup-throw loop. — *code-complete, awaiting playtest confirmation.*

---

## Day 2 — Multiplayer

> **Status:** MVP wired via `BearlyNetworkManager` + `NetworkBear` + `NetworkMatchController`
> (`Assets/Scripts/Network/`), built by **Bearly Standing ▸ Build Networking**. Host mode only
> (no dedicated server). Client-authoritative movement, server-replayed hits. **Untested in-editor** —
> the offline Practice path is the solid one; expect to shake bugs out of MP on first real playtest.

### Morning
- [x] Mirror setup — `BearlyNetworkManager : NetworkManager` + `KcpTransport`, lives in `MainMenu`, `onlineScene = Day1_TestArena`
- [x] Network bear prefab — `Bear_Net.prefab` (one prefab for players **and** bots): `NetworkIdentity`, `NetworkTransformUnreliable` (ClientToServer), `NetworkAnimator` (clientAuthority), `NetworkBear`, + all the offline gameplay scripts (toggled by role)
- [x] Host flow — `HostGame()` → `StartHost()`; `OnStartHost` packs LAN IPv4 + port into a **10-char room code** (`RoomCodeUtil`, Crockford base32)
- [x] Client joining — `JoinGame(code)` accepts the room code, `ip`, or `ip:port`
- [x] Player spawning — `OnServerAddPlayer` spawns a `Bear_Net` at a `NetworkStartPosition`, assigns a colour index
- [x] **Bot fill** — `botFillDelay` (~4 s) after the last join, the server tops the arena up to 6 with bots **owned by the host connection** (host simulates them), then starts the match

### Midday
- [x] Networked movement — client-auth `NetworkTransform` for the owner; host-sim for bots; `NetworkBear.ConfigureRole` enables the CharacterController only on the driver and swaps in a plain `CapsuleCollider` on remotes so hits can land
- [x] Networked animation — `NetworkAnimator` replicates Speed/Grounded/Jump/Throw/Swing; `PlayerAnimator.DriveParametersLocally` gates the driver
- [ ] Networked pillows (Available/Held/Thrown synced) — **cut for MVP.** Online every bear is permanently "armed" (`PlayerCombat.alwaysArmed`); throw is a **server hitscan** (`NetworkBear.CmdThrow` → `SphereCast`), no networked pickup. Full pillow-state sync is still a to-do.
- [x] Networked combat — melee `Hitbox` on the driver → `NetworkBear.TryRelayHit` → `CmdApplyHit`/`ServerApplyHit` + `RpcApplyHit`; `PlayerHealth` runs on every peer and converges
- [x] Networked health/damage/knockback — hearts decrement once per peer from the replayed `ApplyHit`; knockback is applied by the victim's own driver and carried by `NetworkTransform`

### Afternoon
- [~] Lobby UI — minimal: the menu shows the room code + copy button; no player list / ready / manual Start yet (auto-starts on bot-fill)
- [x] Match state sync — `NetworkMatchController` (`playing` SyncVar) starts every client's `MatchManager` together
- [ ] Countdown (3-2-1-FIGHT!) — `MatchManager` has the countdown window; no on-screen 3-2-1 UI yet

### Evening
- [x] Elimination synchronization — replayed `ApplyHit` → each peer's `PlayerHealth.FinalizeElimination` → `PlayerAnimator` hides the bear everywhere
- [x] Winner detection — each peer's `MatchManager.NotifyEliminated` converges on the last bear; `GameHUD` end card
- [~] Return to lobby / rematch flow — `LeaveGame()` + HUD **Menu** button work; **Rematch** is offline-only (scene reload). Networked rematch = leave to menu and re-host.
- [ ] Multiplayer testing (2–6 players) — **not done** (can't test networking here)

### Network the pillows + Downed state — **STILL TO DO (biggest open item)**
- [ ] **Full networked pillow state** — online is still the MVP: bears are permanently armed (`PlayerCombat.alwaysArmed`) and throw is a server hitscan (`NetworkBear.CmdThrow`). Real `Available/Held/Thrown` SyncVar sync + `CmdPickupPillow`/`CmdThrowPillow` on a `Pillow : NetworkBehaviour` is not built. `PlayerHealth.ForceState(...)` is staged for it.
- [ ] **Networked Downed** — still on the kill-switch (`NetworkBear.Awake` → `PlayerHealth.SetDownedStateEnabled(false)`); the carry/throw loop stays an offline "Practice" feature. SyncVars (`healthState`, `carriedBy`, `downedUseCount`, synced start-time) + `CmdPickupDownedPlayer` / `CmdSwingWithCarriedDownedPlayer` / `CmdThrowDownedPlayer` / `RpcOnDownedStateChanged`, all server-validated, not built.
- [x] Hard kill-switch — `PlayerHealth.SetDownedStateEnabled(false)` from `NetworkBear`.
- [ ] **Double-check** on wiring it up: `MatchManager` win check treats `Downed` as in-contention; only `Eliminated` removes a bear.

**Day 2 Milestone:** Six players can join → arena → fight → eliminate → winner. — *core loop wired (untested); full networked pillows + downed still open.*

---

## Day 3 — Juice, New Features, Submission

> **Status:** the offline "Practice" game is now feature-complete and polished; audio is wired
> but needs clips; a few items need real animation/art or hands-on testing. Built by the same
> **Bearly Standing ▸ Build Everything** menu.

### Morning — Game Feel
- [x] Real teddy model + rig — `ted_low` + Mixamo, in since Day 1 (capsule fallback kept)
- [~] Animations — real **Idle / Walk / Fast-Run / Jump / Throw**; **Swing / HitReact** are procedural placeholders; **Knockout / Hold-Pillow / Carried** poses are still the transform-based Day 1 placeholders. Swap real clips onto the `Ted.controller` states.
- [ ] Pillow animations — none (pillow is a rigid mesh)
- [x] Hit effects — `HitFeedback`: pooled expanding dust puff on every hit + cartoon **BONK!/POW!** world text on strong/final hits
- [x] Knockback polish — `PlayerHealth.knockbackByHit` tiers (×1 / ×1.7 / ×3), downing hit is dramatically stronger
- [x] Camera shake — `ScreenShake` (Cinemachine Impulse + listener on the vcam), scaled by hit tier, heavy on knockout
- [x] Hit pause — `HitStop.Freeze(0.08s)` on the downing hit (unscaled-time thaw)

### Midday — Audio & UI Polish
- [~] SFX — `SfxManager` + `SfxBank` wired to swing / hit / heavy / throw / knockout / jump / footstep / pickup / countdown / victory / UI. **Empty slots** — create `Assets/Resources/SfxBank.asset` (`Create ▸ Bearly Standing ▸ SFX Bank`) and drop clips in. Wall-impact + teddy-grunt hooks not placed yet.
- [ ] Music track — no asset; add an `AudioSource` loop where you like
- [~] Menu / HUD / victory UI — functional and themed (Chunky Sprout font, colour palette, end card); not a polish pass
- [x] Countdown UI + **BONK!/POW!** — `GameHUD` 3-2-1-FIGHT! with scale punch; cartoon words via `HitFeedback`
- [x] Control-hint overlay — bottom strip at match start, auto-hides after 10 s or on first movement

### Afternoon — AI, Environment, Menus
- [x] AI polish — sprint-while-chasing, boids separation (no more clumping), 40% miss chance, existing randomness
- [x] Environment — wooden furniture removed for the full pillow-fort look; cover is 4 corner cushion piles, centre kept clear; warm lamp + soft directional
- [~] Menu polish — see HUD note

### Proximity Chat — **NOT STARTED**
- [ ] No voice package added yet. Needs a package choice (DIY raw-mic `Cmd`/`Rpc` PCM, or an asset) and hands-on mic testing before it's worth wiring. Mute toggle + speaking-indicator UI + lobby→match channel switch all still to do.

### Pickup / Power-Up System — **DONE**
- [x] `Pickup.cs` — `NetworkBehaviour`, glowing orb + dimming point-light despawn timer, consume-on-touch, server-authoritative (`CmdConsume` + `consumed` SyncVar hook); offline-safe. `PickupSpawner` drips them in (offline Instantiate / server Spawn).
- [x] `PickupEffectType` enum + single dispatcher (`BearBuffs`) over `PlayerController` / `PlayerCombat` / `PlayerHealth`
- [x] **Tanky armor** — `PlayerHealth.DamageMultiplier` (shrug-off chance) + `KnockbackMultiplier`
- [x] **Speed boost** — `PlayerController.SpeedMultiplier` + `PlayerCombat.AttackSpeedMultiplier`
- [x] **Giant pillow** — `PlayerCombat.GiantSwingsRemaining` → heavier knockback + slower swing for N swings
- [x] **Gravity Well** — `ArenaEffectManager` local timer drives `PlayerController.GlobalGravityMultiplier` on every peer; server `RpcGravityWell` triggers it for all
- [x] Buffed "tell" — glowing sphere over any buffed bear (`BearBuffs`)

### Evening — Bug Fixing & Submission
- [ ] Bug-fixing pass — needs the first in-editor playtest
- [ ] Full multiplayer test — needs 2+ machines
- [x] Build tooling — **Bearly Standing ▸ Build Player ▸ Windows x64 / WebGL** (`BuildTools`)
- [ ] Final submission page, trailer/screenshots — manual

**Day 3 Milestone:** Playable → Funny → Readable → Presentable → Shippable. — *offline Playable/Funny/Readable done; Presentable needs audio clips; Shippable needs a playtest + build.*

---

## Definition of Done

> Legend: [x] code-complete & reviewed · [~] partial / placeholder · [ ] not done.
> **Nothing has been playtested in-editor** — Unity was open on the machine, so the code + one-click
> builders are verified by inspection only. Run **Bearly Standing ▸ Build Everything**, let it compile,
> then play `Day1_TestArena` (or MainMenu → Practice).

### Gameplay
- [x] Player can move (walk / run / jump)
- [x] Player can pick up pillow *(offline; online bears are permanently armed)*
- [x] Player can swing pillow
- [x] Player can throw pillow *(offline: physics projectile · online: server hitscan)*
- [x] Pillow can hit players
- [x] Players receive damage
- [x] Players are knocked back (tiered by hit number)
- [x] Players go Downed on 3rd hit and are eventually Eliminated *(offline; online = instant elim, kill-switch)*
- [x] Last player wins

### Downed State
- [x] Downed players can be picked up *(offline)*
- [x] A carried downed player can be swung to hit others *(offline)*
- [x] A carried downed player can be thrown *(offline)*
- [ ] Networked (server-authoritative) version works with 2+ real players
- [x] Kill-switch reverts to instant-elimination cleanly (`PlayerHealth.SetDownedStateEnabled(false)`)

### AI
- [x] Bots move (`TeddyAI`, offline + host-simulated online)
- [x] Bots find pillows *(offline)*
- [x] Bots attack
- [x] Bots throw
- [x] Bots can be eliminated
- [x] Bots can win

### Multiplayer
- [x] Host can create match (room code)
- [x] Clients can join (room code / ip / ip:port)
- [x] Up to 6 players supported (bots fill empty seats)
- [x] Players synchronize (`NetworkTransform`)
- [x] Combat synchronizes (`Cmd`/`Rpc` hit relay)
- [x] Eliminations synchronize
- [x] Winner synchronizes
- [ ] *All of the above verified only by inspection — untested with real clients*

### Proximity Chat
- [ ] Works in lobby
- [ ] Works in-match with proximity attenuation
- [ ] Mute + speaking-indicator UI present
- [ ] *Not started — needs a voice package + mic testing*

### Pickups
- [x] Tanky armor works
- [x] Speed boost works
- [x] Giant pillow swap works
- [x] Gravity Well works (global effect, all peers)

### Presentation
- [x] Main menu
- [~] Lobby — room code + copy only; no player list / ready
- [x] HUD (hearts, bears-left)
- [x] Countdown (3-2-1-FIGHT!)
- [x] Victory screen (end card + Rematch/Menu)
- [~] Sound — system wired, `SfxBank.asset` clips needed
- [ ] Music
- [x] Basic VFX (dust puffs, BONK text, camera shake, hit-stop, buff glow, pickup orbs)
- [ ] Final build works — build menu exists; not run

---

## Scope Cut Order

### Original MVP features — cut in this order if desperate
1. ❌ Multiple arenas
2. ❌ Charge attack
3. ❌ Dodge
4. ❌ Special pillow types (beyond the base pillow)
5. ❌ Spectator mode
6. ❌ Match statistics
7. ❌ Difficulty selection
8. ❌ Advanced AI

**Never cut:** one arena, teddy, pillow, melee, throw, knockback, health, elimination, last-bear-standing, single-player, multiplayer, basic lobby, basic UI.

### New feature cut order (safest to drop first)
1. **Gravity Well pickup** — the single riskiest sub-item (global shared-state effect touching core movement)
2. **Giant Pillow swap** pickup effect
3. **Remaining pickups** (Tanky armor, Speed boost) — simple, additive, inert if cut; drop before touching Downed-state or Chat
4. **Downed-state pickup/throw mechanic** — cut before Chat, but only via its kill-switch, never half-wired (it touches core elimination/win-condition logic)

---

## Open Tunables (not blockers — pick values during implementation)
- Downed-state exit: use-count limit vs. timer duration vs. whichever triggers first
- Pickup consume trigger: walk-into vs. E-press
- Exact buff durations/multipliers for Tanky armor, Speed boost, Giant pillow swap, Gravity Well
