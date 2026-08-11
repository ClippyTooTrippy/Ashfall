# Game Design

This document establishes the aesthetic, visual identity, and game feel specifications for the 3D retro action game. The designs here are explicitly tailored to complement the existing PS1-style render pipeline, which features a point-filtered low resolution (240p-256p), dithered shading, fog, and dim moonlight horror lighting. 

To ensure efficient production, all features, visual components, and feedback layers are categorized into:
- **(core)**: The minimum viable specification required for a fully playable, cohesive, and readable prototype.
- **(optional)**: Polish layers, auxiliary cues, and secondary visual assets that enrich the oppressive atmosphere but are not strictly required for the core game loop.

---

## UI Design (Soulslike HUD)

To preserve the PS1 retro-render aesthetic, the UI elements are designed to be drawn either as crisp, screen-space elements (providing razor-sharp readability over the low-res 3D scene) or snapped to pixel-art boundaries to emulate authentic late-90s interfaces. 

### Color Palette & Visual Theme
The interface uses a weathered, dark metallic color scheme. All colors are tinted away from pure grays to maintain the eerie, moonlit horror tone.

- **Panel Background / Borders:** `#131111` with an 85% opacity layer `(core)`.
- **Player Health (Foreground):** Muted Crimson `#8A1C1C` `(core)`.
- **Player Health (Damage Shave / Flash):** Bright Crimson `#D13636` `(core)`.
- **Player Health (Background):** Deep Maroon `#3A0C0C` `(core)`.
- **Player Stamina (Foreground):** Muted Green-Gold `#738030` `(core)`.
- **Player Stamina (Fatigue Flash):** Pale Warning Gold `#B2C248` `(optional)`.
- **Player Stamina (Background):** Dark Forest Amber `#2C3314` `(core)`.
- **Boss Health (Foreground):** Deep Gothic Crimson `#9E1B1B` `(core)`.
- **Boss Health (Background):** Charcoal Black `#0F0E0E` `(core)`.
- **Boss Name / Antique Highlights:** Weathered Silver `#D9D2C9` `(core)`.
- **Death Screen Text:** Pure Blood Crimson `#8A0303` `(core)`.
- **Death Screen Vignette:** Pitch Black `#000000` fading dynamically to center `(core)`.

### Typography
- **Display / Header Font (YOU DIED, Boss Names):** Weathered Serif `(core)`. High letter-spacing (approx. 120%), elegant Roman-style letterforms with eroded edges (reminiscent of *Trajan* or *Garamond Bold*). High-stakes text is always fully uppercase.
- **Body / Label Font (Flask Count, Level Numbers):** Classic Book Serif `(core)`. Symmetrical, high-contrast, easily readable at small sizes (reminiscent of *Galliard* or *Garamond Italic*). Snapped to a pixel-grid to prevent retro rendering shimmer.

### Component Specifications

#### 1. Player Vitals HUD (Top-Left Corner) `(core)`
- **Placement:** Positioned in the upper-left screen boundaries. Screen offset of `x = 40px`, `y = 40px` (relative to a 1080p canvas).
- **Ornate Frame:** A simple, thin, weathered iron border (`#2B2A27`) with beveled corners, wrapping both health and stamina bars.
- **Health Bar Bar Dimensions:**
  - Width: 320px (representing standard max health).
  - Height: 18px.
  - Scale Behavior: Scales horizontally based on the normalized health value. Left-aligned.
  - Damage Shave Layer `(optional)`: A secondary bar of bright crimson (`#D13636`) sits behind the main bar and slowly catches up (drains over 1.2 seconds with an ease-in-out curve) after the player takes damage to visualize chunk losses.
- **Stamina Bar Dimensions:**
  - Width: 250px (tapered shorter than health to emphasize secondary importance).
  - Height: 12px.
  - Position: Slotted 6px below the health bar inside the ornate frame, left-aligned.
  - Scale Behavior: Scales instantly with spend/regen.

#### 2. Estus / Healing Flask Counter (Bottom-Left Corner) `(core)`
- **Container Placement:** Offset at `x = 40px`, `y = Screen.height - 104px`.
- **Flask Graphic:** A low-res icon of a cracked, weathered clay flask wrapped in iron bands.
- **Fluid Fill Level `(optional)`:** An internal mask overlay containing a glowing amber gradient (`#E65C00` to `#FF9900`) that drains downwards as charges are depleted.
- **Charge Counter Text:** Positioned at the bottom-right corner of the flask container. 
  - Size: 24pt, bold antique gold (`#D4AF37`), using the secondary Roman Serif font.
  - Behavior: Fades slightly to 50% opacity when charges reach `0`, and the icon shifts to a monochrome weathered pewter tone (`#5E5952`).

#### 3. Boss Health HUD (Bottom-Center) `(core)`
- **Bar Dimensions:**
  - Width: 60% of total screen width (1152px at 1080p).
  - Height: 14px.
- **Placement:** Centered horizontally on the screen, offset from the bottom by `y = 100px`.
- **Boss Name Text:**
  - Position: Centered horizontally, exactly 12px above the health bar.
  - Font: Uppercase Serif, 18pt, colored in antique silver (`#D9D2C9`).
- **Engagement Transition:**
  - Triggered exclusively when the player enters the boss arena bounds.
  - HUD slides up vertically from the bottom of the screen (0.6s duration, exponential ease-out) while the red health bar fills from left to right (1.5s linear fill-up sound effects).

#### 4. 'YOU DIED' Screen `(core)`
- **Darkened Vignette:** A full-screen gradient overlay that fades in, masking the outer edges of the scene in pure black (`#000000`) and leaving a dithered 40% translucent moonlit center.
- **Text Element:** The words **YOU DIED** centered on the screen.
  - Font: Display Weathered Serif, large 84pt scale, blood crimson (`#8A0303`).
- **Timing & Motion:**
  - **0.0s - 0.5s:** Dynamic vignette fades in from 0% to 100% border density.
  - **0.5s - 2.5s:** **YOU DIED** text slowly fades in (alpha `0` to `1`).
  - **2.5s - 6.5s:** The text scales up extremely slowly (a mere 4% increase, 1.00 to 1.04 scale) using a subtle camera-drift simulation.
  - **6.5s - 8.0s:** Entire screen fades to solid black as the reload operation begins.

---

## Asset Design: Undead Knight Boss

The Undead Knight boss, named **"The Ash-Bound Knight"**, is designed to stand as a grim, melancholic, and imposing obstacle. He is positioned in a decayed cathedral arena following the defeat of the Wolf.

### Visual Identity & Anatomy
- **Physical Build:** A towering, skeletal humanoid shape. He possesses broad, hunched shoulders, giving him an aggressive, menacing posture. 
- **Plate Armor:** Encased in dark, ancient steel plate armor that is cracked, heavily pitted, and coated in rust and patches of pale, moldy lichen. The chestplate is split open, revealing a ribcage of mummified dark skin and cold, dead ash where a heart would be.
- **Visor Flare:** A pitch-black heavy iron helmet with a thin horizontal visor slit. From deep within the slit, two small, high-contrast, pinpoint glowing ash-white eyes (`#E5E5E5`) pierce the darkness, giving the player a clear focal point.
- **Tattered Cloak:** A tattered, dirt-streaked navy blue cloak (`#1C2A30`) hangs from his shoulders, draped loosely on the stone floor to emphasize slow, dramatic, lumbering movements.
- **Weapon:** Holds a rusted, chipped medieval greatsword, chipped along the blade as if used to block countless heavy blows. He drags it along the stone floor during idle states.

### Palette Constraints
To keep the colors unified within the dithered moonlight horror environment, the asset uses three strict palettes:
- **Dominant (65%):** Rusted Steel & Corrosion (`#3E3F40` / `#8C593B`) — Armor plates and helmet.
- **Secondary (25%):** Decayed Navy / Dark Fabric (`#151C21`) — Tattered cloak and crest.
- **Accent (10%):** Pale Ash & Ghostly Eyes (`#DEDFE0`) — Visor flare, bone fragments, and blade highlights.

### Proportions & Composition
- **Scale:** Imposing and towering, standing at **1.45x the height of the player** (approx. 2.6 meters). This forces the camera to tilt slightly upward when locked-on, highlighting his dominance.
- **Silhouette:** Designed with stark, jagged edges. The tattered cloak and massive diagonal sword break the standard human proportions, creating a distinct silhouette that remains easily readable even at low PS1 resolutions and through dense moonlight fog.
- **Material properties (Built-in Render Pipeline):** Metallic maps should utilize heavy, hand-painted noise rather than smooth specular highlights. The steel is entirely non-reflective except for rough dithered specular peaks that catch the dim moonlight, making the rust feel textured and tactile.

---

## Game Feedback Design (Polishment)

Feedback in a soulslike game must emphasize weight, consequence, and high-energy impact, contrasted by moments of terrifying stillness. Every action has a heavy cost.

### Rationale & Tone
This system uses a **High-Energy Action Hybrid** profile. Grounded, heavy, slow movement is combined with sharp, explosive, instantaneous visual and audio spikes when hits land. Silence and screen freezes emphasize structural impact.

### Interaction Map

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Input | Rationale |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Player Light Attack (Hit on Knight)** | Core | Light | — | Hitstop: **0.03s** freeze frame on contact. | Weapon recoil: Sword model pauses at contact point. | Sparse, sharp, gray metal spark particles `(core)`. | High-pitched metallic "clink" layered with flesh impact `(core)`. | Short, low-intensity rumble pulse `(core)`. | Confirms weapon hit without interrupting the player's attack momentum. |
| **Player Heavy Attack (Stagger Knight)** | Core | Medium | Slight vertical jolt (10px, decaying over 0.15s) `(core)`. | Hitstop: **0.06s** freeze frame. | Boss model tilts backward 5% at the torso `(core)`. | Larger shower of orange dithered sparks `(core)`. | Heavy, hollow, echoing plate crushing "thud" `(core)`. | Direct, sharp dual-motor rumble impulse `(core)`. | Rewards the stamina-expensive heavy swing with heavy visual stagger. |
| **Knight Attacks Player (Direct Hit)** | Core | Heavy | Directional shake away from hit (0.3s duration, exponential decay) `(core)`. | Hitstop: **0.10s** dramatic freeze frame on player. | Player character is thrown back in a sliding knockback state `(core)`. | Full-screen outer border dark-red flash (2 frames) `(core)`. | Bone-crushing armor impact, followed by player grunt `(core)`. | Maximum intensity rumble (left motor heavy, right motor light) `(core)`. | Forces player panic and drives home the immense threat of the boss. |
| **Player Dodge Roll (i-frames active)** | Core | Light | — | — | Player model squashes down vertically by 15% during spin `(core)`. | A very subtle, dithered ghost trail of the player (lasts 3 frames) `(core)`. | Swift, soft "whoosh" of fabric cutting air `(core)`. | — | Visually signals when the player is invulnerable to damage. |
| **Stamina Depleted (Fatigue State)** | Core | Medium | — | — | Stamina bar turns orange and flashes 3 times rapidly `(core)`. | Action lock: Player cannot roll/attack for 0.5s `(core)`. | Heavy, exhausted armor clink + panting breath `(core)`. | Small persistent buzz rumble `(optional)`. | Punishes greedy attacks and roll-spamming with clear warning. |
| **Boss Death Moment** | Core | Critical | Intense radial shake decaying over 1.5s, slow zoom in `(core)`. | Global slowdown: Speed drops to 30% for 2.5s `(core)`. | Knight falls to knees, drops massive sword `(core)`. | Knight dissolves into dark ash cloud particles `(core)`. | Echoing, fading metal groan, cathedral music cuts out instantly `(core)`. | Long, decaying, rumbling haptic vibration (1.5s) `(core)`. | Provides the ultimate physical relief and reward for victory. |

### Feedback Sequences

#### 1. "The Ash-Bound Knight" Intro Sequence `(core)`
- **0ms:** Player steps past the gray cathedral fog wall. Player inputs are immediately locked. UI HUD fades out completely (0.4s).
- **500ms:** Camera detaches from the player, smoothly panning up toward the ruined cathedral altar where a dusty stone statue sits (1.5s pan, ease-in-out).
- **2000ms:** A low, rumbling bass hum begins. The "statue" jerks forward. Dust and loose pebble particles cascade off its shoulders.
- **3000ms:** The Knight stands tall, towering over the ruins. visor slits flare open with a pale, cold ash-white glow (`#E5E5E5`).
- **4200ms:** The Knight takes his first heavy step forward, dragging his massive greatsword along the stone floor, kicking up dithered metal sparks and making a harsh, grinding scrape sound.
- **5200ms:** Heavy gothic choir and cellos rise. The boss health bar slides up from the bottom of the screen (`x = centered`, `y = 100px`). The title **THE ASH-BOUND KNIGHT** slowly fades in over the health bar (0.8s).
- **6000ms:** Camera returns to standard third-person lock behind the player. Control is returned, and the Player HUD fades back in.

#### 2. Player Death Sequence `(core)`
- **0ms:** Player health reaches `0`. Hitstop freezes the game for `0.10s`.
- **100ms:** Player inputs are permanently disabled. Camera slowly detaches, tilting upward to look down at the player as the character falls to their knees and collapses onto the cold cathedral stone.
- **1000ms:** All game sounds (boss combat, steps, music) are run through a heavy low-pass filter, creating a muffled, underwater auditory sensation. A deep, cold wind sound effect fades in, paired with a slow, echoing church bell toll.
- **1500ms:** Full-screen dithered vignette fades in from the screen borders, drowning out the scene except for the immediate vicinity of the dead player.
- **2500ms:** The text **YOU DIED** fades in at the exact center of the screen (2.0s fade-in duration). The crimson text (`#8A0303`) slowly scales up from `1.0` to `1.05` to create an imposing feeling of finality.
- **6500ms:** The entire screen fades to pure black (1.0s duration).
- **7500ms:** The screen remains black for 1.0s of silence before reloading the scene to the last bonfire/respawn point.

---

## Asset Checklist & Specifications

To assist modeling, sound design, and sprite artists, the following assets are required to realize this design.

- **[UI] Health/Stamina Frame & Fill Sprites `(core)`**
  - **Type:** 2D Sprite Texture.
  - **Style:** Hand-painted, low-res dithered borders, medieval metallic trim.
  - **Resolution:** 256x64 px sprites, point-filtered, raw uncompressed.
- **[UI] Estus Flask Icon `(core)`**
  - **Type:** 2D Sprite.
  - **Style:** Weathered, cracked green glass/clay flask, pixel art styling.
  - **Resolution:** 64x64 px.
- **[UI] 'YOU DIED' Vignette Mask `(core)`**
  - **Type:** 2D Texture.
  - **Style:** Soft radial gradient, custom dither pattern on gradient steps.
  - **Resolution:** 512x512 px dither-masked texture.
- **[Model] The Ash-Bound Knight `(core)`**
  - **Type:** Low-Poly 3D Mesh (FBX).
  - **Style:** 90s console style, under 1800 polygons. Visor eyes should use an emissive unlit material.
  - **Material:** PS1 retro-diffuse shader with custom dither-specular noise. Textures mapped at 256x256 px.
- **[Audio] Cathedral Scrape / Greatsword Drag `(core)`**
  - **Type:** WAV Audio Clip.
  - **Style:** Heavy, grinding metallic friction against stone, layered with dry dust debris rustles.
- **[Audio] Bell of Transgression (Death Toll) `(core)`**
  - **Type:** WAV Audio Clip.
  - **Style:** Heavy, distant, vibrating brass bell toll with a very long tail decay and pitch flutter.
- **[Audio] Rusted Knight Stagger `(core)`**
  - **Type:** WAV Audio Clip.
  - **Style:** A violent clash of heavy sheet metal plates combined with a deep, muffled hollow echo.
