# Souls-Like Horror — Retro PS1-Style Prototype

A vertical slice: third-person souls-like combat (lock-on, dodge-roll i-frames,
stamina-gated attacks) wrapped in a PS1/PS2-style low-poly renderer, built for Unity.

## What's included

```
Assets/
  Scripts/
    Player/PlayerController.cs     - movement, dodge roll (i-frames), light/heavy attacks
    Camera/LockOnSystem.cs         - souls-like target lock (Mouse3 / middle-click by default)
    Camera/ThirdPersonCamera.cs    - orbiting camera, frames the target when locked on
    Enemy/EnemyAI.cs               - Idle/Patrol/Chase/Attack/Stagger state machine, NavMesh-driven
    Systems/Health.cs              - shared health + damage + i-frame flag
    Systems/Stamina.cs             - shared stamina pool with regen delay
    Systems/PS1RenderEffect.cs     - low internal resolution + dithering post-process
    Systems/GameHUD.cs             - IMGUI health/stamina bars, no Canvas needed
    Editor/TestSceneBuilder.cs     - Tools > Souls-Like Horror > Build Test Scene
  Shaders/
    PS1VertexJitter.shader         - vertex snapping, flat lighting, crushed color depth
    PS1Dither.shader               - post-process dither + palette crush
```

## Setup (Unity 2022 LTS or newer, Built-in Render Pipeline)

1. Create a new **3D (Built-in Render Pipeline)** project and copy the `Assets` folder
   from this project into it (merge if you already have an `Assets` folder).
2. Install the **AI Navigation** package (Window > Package Manager > search
   "AI Navigation") if `NavMeshAgent` / NavMesh baking isn't already available in
   your Unity version.
3. Open **Edit > Project Settings > Player**:
   - Set Color Space to **Gamma** (closer to PS1-era rendering, though Linear also works).
   - Under Graphics, add `Hidden/SoulsLike/PS1Dither` to **Always Included Shaders**
     so it survives builds even though nothing references it directly in a scene.
4. Run **Tools > Souls-Like Horror > Build Test Scene** from the menu bar. This creates:
   - a ground plane, a capsule player with `CharacterController` + combat scripts,
   - an orbiting camera with the PS1 post-process attached,
   - one capsule enemy with `NavMeshAgent` + `EnemyAI`,
   - dim horror lighting and exponential fog.
5. **Bake a NavMesh**: Window > AI > Navigation > Bake tab > Bake (with the Ground
   plane selected/marked as Navigation Static). Without this the enemy won't move.
6. Press Play.

## Controls (defaults, all rebindable in `PlayerController` / `LockOnSystem`)

| Action | Key |
|---|---|
| Move | WASD |
| Run | Hold Left Shift |
| Camera look | Mouse |
| Lock on / clear lock | Middle Mouse Button |
| Dodge roll | Space |
| Light attack | Left Mouse Button |
| Heavy attack | Right Mouse Button |

## Design notes / what to build next

- **Combat feel is currently timer-based, not animation-driven.** Hit windows and
  roll distance are placeholder curves in `PlayerController`. Once you bring in
  animations, drive these off animation events / root motion instead of `Time.deltaTime`
  timers for proper weight and readability.
- **Enemy variety**: `EnemyAI` is a single reusable state machine. Different enemy
  "classes" should come from data (a ScriptableObject with speed/damage/range/aggro
  values) rather than new scripts, to keep it souls-like in spirit (readable, telegraphed
  patterns) without exploding your codebase.
- **PS1 look**: `PS1VertexJitter.shader` gives you the geometric wobble and crushed
  lighting; `PS1RenderEffect.cs` gives you the low internal resolution + dither. For
  the full effect, also: disable anti-aliasing in Quality settings, use small (64-128px)
  point-filtered textures, and keep draw distance short (fog hides the seam).
- **Horror pacing**: the current test scene sets fog + a dim directional light as a
  starting point. Consider a flashlight/lantern mechanic tied to a limited resource
  (batteries/oil) — classic survival-horror tension that also fits a souls-like's
  resource-management DNA.
- **Death/respawn loop**: `Health.OnDeath` fires but nothing currently restarts the
  player — that's the natural next system to add (bonfire/checkpoint respawn,
  resetting enemies, dropping/recovering currency on death, etc.).

## Known limitations of this pass

- `PS1RenderEffect` uses `OnRenderImage`, which requires the **Built-in Render Pipeline**
  (not URP/HDRP). Porting to URP means moving this into a Renderer Feature instead.
- True PS1 affine texture mapping (warped UVs from lack of perspective correction)
  isn't replicated here — that requires a custom rasterizer or shader tricks beyond a
  standard vertex/fragment shader. The vertex-snap + flat lighting + color crush combo
  gets you most of the way visually.
- Combat hit detection uses `Physics.OverlapSphere`, which is fine for a prototype
  but doesn't account for weapon swing arcs — upgrade to a moving hitbox/trail collider
  once you have actual attack animations.
