# 🕺 AdvancedPedStudio for Grand Theft Auto V

[![Platform: PC](https://img.shields.io/badge/Platform-GTA%20V%20(PC)-blue.svg)](#)
[![ScriptHookVDotNet](https://img.shields.io/badge/SHVDN-v3-orange.svg)](https://github.com/scripthookvdotnet/scripthookvdotnet)
[![LemonUI](https://img.shields.io/badge/UI-LemonUI.SHVDN3-green.svg)](https://github.com/Lemon-UI/LemonUI)
[![Target Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](#)
[![Language: C#](https://img.shields.io/badge/Language-C%23-239120.svg)](#)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](#)

**AdvancedPedStudio** is a comprehensive, in-game 3D ped customization studio, wardrobe manager, and animation testing suite for **Grand Theft Auto V**. Built with C#, **Script Hook V .NET v3**, and **LemonUI**, it allows modders, creators, and machinima directors to spawn, inspect, dress, animate, and persist ped outfits and accessories in real-time.

---

## 📖 Table of Contents

1. [🌟 Key Highlights](#-key-highlights)
2. [📋 Walkthrough: Prerequisites & Setup](#-walkthrough-prerequisites--setup)
3. [🎮 Walkthrough: Studio Controls](#-walkthrough-studio-controls)
4. [🚶 Step-by-Step Studio Walkthrough](#-step-by-step-studio-walkthrough)
   - [Step 1: Launching the Studio Stage](#step-1-launching-the-studio-stage)
   - [Step 2: Selecting & Hot-Reloading Models](#step-2-selecting--hot-reloading-models)
   - [Step 3: Setting Movement & Walk Styles](#step-3-setting-movement--walk-styles)
   - [Step 4: Customizing Clothing & Drawables](#step-4-customizing-clothing--drawables)
   - [Step 5: Styling Props & Headwear](#step-5-styling-props--headwear)
   - [Step 6: Rolling the Randomizer](#step-6-rolling-the-randomizer)
   - [Step 7: Saving Custom Presets](#step-7-saving-custom-presets)
   - [Step 8: Loading & Deleting Presets](#step-8-loading--deleting-presets)
   - [Step 9: Cayo Inlet Swimmer Background Task](#step-9-cayo-inlet-swimmer-background-task)
5. [📁 File Schemas & Configuration](#-file-schemas--configuration)
   - [pedmodels.txt](#1-pedmodelstxt)
   - [customizedpeds.ini](#2-customizedpedsini)
   - [AdvancedPedStudio.log](#3-advancedpedstudiolog)
6. [🛠️ Building from Source](#️-building-from-source)
7. [❓ Troubleshooting & FAQ](#-troubleshooting--faq)
8. [📜 Credits & License](#-credits--license)

---

## 🌟 Key Highlights

- **Live 3D Rotating Preview Stage:** Automatically spawns a frozen, invincible, collision-safe preview ped ~10 feet in front of your character with smooth 360° rotation for 3D inspection.
- **Hot-Reloadable Ped Model Pool:** Load any base game or add-on ped model from `pedmodels.txt` without restarting GTA V.
- **45+ Movement Clipsets & Walk Styles:** Preview walk styles (e.g. `move_f@sexy`, `move_f@posh@`, `move_f@gangster@ng`, `move_f@heels@c`) on the fly.
- **Comprehensive 12-Slot Clothing Control:** Full drawable and texture sliders for Head, Mask, Hair, Torso/Arms, Legs, Hands/Bags, Shoes, Accessories, Undershirt, Armor, Decals, and Tops.
- **Smart Component Preservation:** Automatically preserves Torso/Arms (Slot 3) and Pants (Slot 4) when cycling Tops (Slot 11) or Undershirts (Slot 8), completely preventing invisible limb glitches.
- **Full Props & Accessories Manager:** Equip or strip hats, helmets, sunglasses, ear pieces, watches, and bracelets (`-1` for none).
- **Direct Disk-Write INI Persistence:** Thread-safe, immediate disk-flush saving with smart suffix generation (`model_a`, `model_b`, etc.) and friendly in-game name prompts.
- **Dependency-Ordered Preset Loader:** Outfits load with an engineered multi-pass sequence so layered clothing never overrides underlying drawables.
- **Ambient Cayo Perico Inlet Swimmer:** Built-in ambient task where beach peds naturally swim out to the ocean inlet, swim circular patterns, and return to shore.

---

## 📋 Walkthrough: Prerequisites & Setup

### Requirements

Before installing AdvancedPedStudio, verify that your GTA V installation contains the following modding runtimes:

| Requirement | Purpose | Download |
| :--- | :--- | :--- |
| **Script Hook V** | Core GTA V ASI runtime hook (`ScriptHookV.dll`) | [dev-c.com](http://www.dev-c.com/gtav/scripthookv/) |
| **Script Hook V .NET v3** | C# .NET scripting backend (`ScriptHookVDotNet3.dll`) | [GitHub Releases](https://github.com/scripthookvdotnet/scripthookvdotnet/releases) |
| **LemonUI for SHVDN3** | Responsive UI and menu framework (`LemonUI.SHVDN3.dll`) | [GitHub Releases](https://github.com/Lemon-UI/LemonUI/releases) |
| **.NET Framework 4.8** | Microsoft Windows .NET Runtime | [Microsoft](https://dotnet.microsoft.com/download/dotnet-framework/net48) |

### Installation Steps

1. Locate your Grand Theft Auto V root installation directory (e.g., `C:\Program Files\Rockstar Games\Grand Theft Auto V\` or `D:\SteamLibrary\steamapps\common\Grand Theft Auto V\`).
2. Create a folder named `scripts` in your GTA V root directory if one does not already exist.
3. Place the compiled mod files into your `scripts/` folder:
   ```text
   Grand Theft Auto V/
   ├── ScriptHookV.dll
   ├── ScriptHookVDotNet.asi
   ├── ScriptHookVDotNet3.dll
   └── scripts/
       ├── AdvancedPedStudio.dll       <-- Mod binary
       ├── LemonUI.SHVDN3.dll          <-- UI dependency
       ├── pedmodels.txt               <-- Model list (auto-created if missing)
       └── customizedpeds.ini          <-- Presets file (auto-created upon save)
   ```
4. Launch GTA V and enter Story Mode. A subtitle will notify you when AdvancedPedStudio is initialized:
   ```text
   Advanced Ped Studio ready! Press F6 to open studio.
   ```

---

## 🎮 Walkthrough: Studio Controls

| Key / Input | Action | Description |
| :--- | :--- | :--- |
| **F6** | **Open / Close Studio** | Toggles the studio UI and automatically spawns/despawns the 3D preview ped. |
| **↑ / ↓** | **Menu Navigation** | Moves up and down across menu items and submenus. |
| **← / →** | **Change Value / Sliders** | Cycles through ped models, walk styles, drawable IDs, and texture indices. |
| **Enter** / **Numpad 5** | **Select / Activate** | Enters submenus, triggers the randomizer, or confirms save/load actions. |
| **Backspace** / **Esc** | **Back / Close Submenu** | Navigates back up one menu level or closes the current view. |

> [!NOTE]
> When the Studio menu is active, GTA V controls that conflict with menu browsing (such as opening the player phone) are automatically disabled.

---

## 🚶 Step-by-Step Studio Walkthrough

Follow this step-by-step walkthrough to master every feature of AdvancedPedStudio.

```
       [ PLAYER ]
           |
        10 Feet (~3.05m)
           |
           v
     +-----------+
     |  PREVIEW  |  <--- Spawns facing player, frozen in place,
     |    PED    |       rotating smoothly 360° continuously
     +-----------+
```

### Step 1: Launching the Studio Stage

1. Walk your player character to an open, level area with clear visibility.
2. Press **`F6`** on your keyboard.
3. The studio camera stage activates immediately:
   - A preview ped spawns exactly **10 feet (~3.05 meters)** directly in front of your character, facing you.
   - The preview ped is made invincible, frozen in place, has collisions turned off, and starts rotating smoothly at 0.85° per tick.
   - The **Ped Studio** main menu appears in the top-left corner.

---

### Step 2: Selecting & Hot-Reloading Models

1. In the main menu, select the **Ped Model** list item.
2. Press **`←`** or **`→`** to cycle through models in your list (e.g., `s_f_y_stripper_01`, `mp_f_freemode_01`, `a_f_y_beach_01`, etc.).
3. The preview ped updates instantly to the selected model.
4. **Need to add custom or add-on models?**
   - Open `scripts/pedmodels.txt` in any text editor.
   - Add your desired model names separated by commas, spaces, or newlines.
   - In the Ped Studio menu, scroll down and press **Reload pedmodels.txt**.
   - Your new models are loaded into the picker immediately without having to reload scripts or restart the game.

---

### Step 3: Setting Movement & Walk Styles

1. Highlight the **Movement Style** list item (located directly under Ped Model).
2. Press **`←`** or **`→`** to audition over 45 custom walk styles and clipsets:
   - Feminine & Glamour: `move_f@sexy`, `move_f@heels@c`, `move_f@posh@`, `move_f@sassy`, `move_f@chichi`
   - Expressive: `move_f@arrogant@a`, `move_f@depressed@a`, `move_f@exhausted`, `move_f@drunk@a`
   - Athletic & Energetic: `move_f@jogger`, `move_f@runner`, `move_f@hiking`, `move_f@hurry@a`
   - Neutral & Tough: `move_f@generic`, `move_f@business@a`, `move_f@gangster@ng`, `move_f@tough_guy@`
   - Default: `(Default)`
3. The preview ped seamlessly loads the clipset and assumes the personality animation. The chosen style is saved alongside your outfit presets.

---

### Step 4: Customizing Clothing & Drawables

1. Open the **Clothing Variations** submenu.
2. You will see sliders for all 12 GTA V ped component slots, paired with corresponding **Texture** sliders:

| Slot # | Component | Description |
| :---: | :--- | :--- |
| **0** | **Head / Face** | Head shape, facial variation, or base face drawable |
| **1** | **Mask / Beard** | Facial hair, masks, or neck scarves |
| **2** | **Hair** | Hairstyle and haircut variations |
| **3** | **Torso (Arms)** | Arms, upper body skin, and sleeve cutouts |
| **4** | **Legs / Pants** | Jeans, skirts, shorts, swimsuits, and leggings |
| **5** | **Hands / Bags** | Gloves, handbags, backpacks, and tactical gear |
| **6** | **Shoes** | High heels, sneakers, boots, sandals, or barefoot |
| **7** | **Accessories** | Necklaces, scarves, ties, badges, and chains |
| **8** | **Undershirt** | T-shirts, bras, bikinis, crop tops, and inner layers |
| **9** | **Body Armor** | Vests, plate carriers, and tactical armor |
| **10** | **Decals** | Emblems, patches, insignias, and rank stripes |
| **11** | **Top / Shirt** | Outer jackets, coats, button-ups, and tops |

3. Use **`←` / `→`** to change the drawable variation number.
4. Use the **Texture** slider directly below it to adjust the colorway, fabric, or pattern.

> [!TIP]
> **Invisible Arm / Body Part Protection:** In GTA V, changing a top or undershirt often resets or glitches torso drawables, causing arms to disappear. AdvancedPedStudio features a **Smart Component Preservation** engine that locks and reapplies torso pairings and pants whenever tops or undershirts are modified.

---

### Step 5: Styling Props & Headwear

1. Open the **Props & Accessories** submenu.
2. Browse the dedicated prop slots:
   - **Hats / Helmets (Head)** (Slot 0)
   - **Glasses (Eyes)** (Slot 1)
   - **Ear Accessories** (Slot 2)
   - **Watches** (Slot 6)
   - **Bracelets** (Slot 7)
3. Set any prop slider to **`-1`** to remove the accessory completely.
4. For equipped props (`≥ 0`), use the accompanying **Texture** slider to pick colors (e.g., tinted lenses, gold/silver watch finishes).

---

### Step 6: Rolling the Randomizer

1. Navigate back to the main menu.
2. Select **Randomize Variations**.
3. Press **Enter**.
4. The studio rolls a fresh, random combination of components and props on the preview ped.
5. All clothing and prop sliders automatically refresh to match the new random appearance, allowing you to fine-tune individual pieces from the rolled outfit.

---

### Step 7: Saving Custom Presets

1. Once your ped looks perfect, select **Save to customizedpeds.ini**.
2. Press **Enter**.
3. An on-screen prompt appears:
   - AdvancedPedStudio inspects `customizedpeds.ini` and automatically suggests a sequential name (e.g. `s_f_y_stripper_01_a`, `s_f_y_stripper_01_b`, etc.).
   - Keep the suggested name or type your own custom profile name (e.g. `Bikini_Beach_Guard_01`).
   - Press **Enter** to confirm.
4. The preset is flushed directly to `scripts/customizedpeds.ini` with thread-safe atomic disk-write guarantees.
5. A green log entry confirms that all 12 clothing components, textures, props, and movement styles have been recorded.

---

### Step 8: Loading & Deleting Presets

#### To Load a Preset:
1. Open the **Saved Customizations** submenu.
2. Highlight any saved profile to view its summary:
   ```text
   Model: s_f_y_stripper_01 (Option a) | Click to preview this saved appearance.
   ```
3. Press **Enter**.
4. The studio immediately swaps to the preset's model, loads its movement clipset, and applies every component in an engineered dependency order:
   ```text
   Dependency Apply Sequence:
   Undershirt (8) -> Top (11) -> Pants (4) -> Shoes (6) -> Head (0) -> Mask (1) ->
   Hair (2) -> Hands (5) -> Accessories (7) -> Armor (9) -> Decals (10) -> Torso Pass (3, 4, 8, 11, 3)
   ```
   This ensures no base layers are hidden or erased during loading.

#### To Delete a Preset:
1. Within **Saved Customizations**, enter the **Delete Customization** submenu.
2. Select the profile you wish to remove and press **Enter**.
3. The section is cleanly excised from `customizedpeds.ini`, and the menus refresh instantly.

---

### Step 9: Cayo Inlet Swimmer Background Task

AdvancedPedStudio includes an autonomous ambient controller for Cayo Perico's west beach inlet:

```
                          [ WEST BEACH INLET ]
                                  *
                             (Center Point)
                                  ^  |
                   Leg 1: Swim Out|  |Leg 2: Circle Swim (5s)
                                  |  v
                      Leg 3: Return to Shore Target
                                  |
                                  v
                        [ SHORE EXIT COORD ]
                                  |
               Resume Scenario / PedCreator Task Sequence
```

1. In the main menu, look for the **Cayo Inlet Swimmer** checkbox (enabled by default).
2. **How it works:**
   - **Proximity-Gated:** Only active when the player is within 350 meters of the inlet (`X: 4845.2, Y: -4935.8`) to keep CPU usage near zero elsewhere.
   - **Autonomous Selection:** Scans for ambient or spawned beach peds within a 70-foot (~21.3m) radius of the inlet center.
   - **Phase 0 (Swim Out):** Instructs the ped to walk into the water and swim directly toward the inlet center.
   - **Phase 1 (Circular Swim):** Upon reaching the center, the ped swims smoothly in a circle for 5 seconds.
   - **Phase 2 (Shore Exit):** The ped swims directly to designated shore coordinates (`X: 4863.06, Y: -4927.76, Z: 1.50`).
   - **Phase 3 (Sequence Resume):** Once out of the water, peds return to their original beach scenario or seamlessly resume their spawned `PedCreator` task sequence.
3. Uncheck **Cayo Inlet Swimmer** at any time to immediately disable the routine and return any active swimmers to shore.

---

## 📁 File Schemas & Configuration

All data files live in your GTA V `scripts/` folder.

```text
scripts/
├── pedmodels.txt        # Plaintext list of model names
├── customizedpeds.ini   # INI configuration containing saved outfits
└── AdvancedPedStudio.log # Debug and operation log
```

### 1. `pedmodels.txt`

Plaintext file containing ped model names. Entries can be separated by commas, newlines, semicolons, or tabs.

```text
s_f_y_stripper_01, s_f_y_stripper_02, s_f_y_stripperlite, mp_f_freemode_01, mp_m_freemode_01,
a_f_y_topless_01, a_f_y_beach_01, a_f_y_fitness_01, a_f_y_tourist_01, a_f_y_hippie_01,
a_m_y_beach_01, s_m_y_dealer_01, s_m_y_cop_01, u_f_y_danceburl_01
```

---

### 2. `customizedpeds.ini`

Structured INI configuration storing full outfit definitions. Supports dual format (both comma-separated and discrete keys) for compatibility with external script tools:

```ini
; ============================================================
; AdvancedPedStudio - Saved Customized Peds
; Last Updated: 2026-09-03 16:15:00
; ============================================================

[s_f_y_stripper_01_a]
FriendlyName = s_f_y_stripper_01_a
Model = s_f_y_stripper_01
Option = a
MovementStyle = move_f@sexy
SavedAt = 2026-09-03 16:15:00
Component_0 = 0,0
Component_0_Drawable = 0
Component_0_Texture = 0
Component_1 = 0,0
Component_1_Drawable = 0
Component_1_Texture = 0
Component_2 = 1,0
Component_2_Drawable = 1
Component_2_Texture = 0
Component_3 = 0,0
Component_3_Drawable = 0
Component_3_Texture = 0
Component_4 = 0,1
Component_4_Drawable = 0
Component_4_Texture = 1
Component_5 = 0,0
Component_5_Drawable = 0
Component_5_Texture = 0
Component_6 = 0,0
Component_6_Drawable = 0
Component_6_Texture = 0
Component_7 = 0,0
Component_7_Drawable = 0
Component_7_Texture = 0
Component_8 = 0,0
Component_8_Drawable = 0
Component_8_Texture = 0
Component_9 = 0,0
Component_9_Drawable = 0
Component_9_Texture = 0
Component_10 = 0,0
Component_10_Drawable = 0
Component_10_Texture = 0
Component_11 = 0,1
Component_11_Drawable = 0
Component_11_Texture = 1
Prop_0 = -1,0
Prop_0_Index = -1
Prop_0_Texture = 0
Prop_1 = -1,0
Prop_1_Index = -1
Prop_1_Texture = 0
Prop_2 = -1,0
Prop_2_Index = -1
Prop_2_Texture = 0
Prop_6 = -1,0
Prop_6_Index = -1
Prop_6_Texture = 0
Prop_7 = -1,0
Prop_7_Index = -1
Prop_7_Texture = 0
```

---

### 3. `AdvancedPedStudio.log`

Contains detailed timestamped event traces:
```text
[2026-09-03 16:00:10.123] Loaded 27 models from pedmodels.txt
[2026-09-03 16:00:10.145] AdvancedPedStudio initialized successfully. Press F6 to open menu.
[2026-09-03 16:01:25.450] Successfully saved customized ped [s_f_y_stripper_01_a] (Friendly: s_f_y_stripper_01_a, Model: s_f_y_stripper_01) to scripts/customizedpeds.ini
```

---

## 🛠️ Building from Source

### Repository Structure

```text
AdvancedPedStudio/
├── AdvancedPedStudio.csproj       # Visual Studio Project (.NET Framework 4.8)
├── AdvancedPedStudio.slnx         # Solution file
├── Properties/
│   └── AssemblyInfo.cs           # Assembly metadata & versioning
├── class.cs                       # Complete source code (UI, Sliders, INI, Swimmer)
├── pedmodels.txt                  # Default model list
├── customizedpeds.ini             # Preset storage file
└── README.md                      # Project documentation & walkthrough
```

### Build Instructions

1. Open `AdvancedPedStudio.csproj` or `AdvancedPedStudio.slnx` in **Visual Studio 2019 / 2022**.
2. Verify that references to the following libraries are properly resolved:
   - `ScriptHookVDotNet3.dll`
   - `LemonUI.SHVDN3.dll`
3. Set the build configuration to **Release | x64** or **Debug | x64**.
4. Build the solution (**Ctrl + Shift + B**).
5. The post-build event in `AdvancedPedStudio.csproj` automatically copies the output DLL to your GTA V `scripts/` directory:
   ```xml
   <PostBuildEvent>COPY "$(TargetPath)" "D:\SteamLibrary\steamapps\common\Grand Theft Auto V\scripts"</PostBuildEvent>
   ```

---

## ❓ Troubleshooting & FAQ

### Q: Pressing `F6` does not open the menu.
- Ensure **Script Hook V** (`ScriptHookV.dll`) and **Script Hook V .NET v3** (`ScriptHookVDotNet3.dll`) are correctly installed in your root GTA V directory.
- Verify `LemonUI.SHVDN3.dll` is present in your `scripts/` folder.
- Press **`Insert`** in-game to reload ScriptHookVDotNet scripts, then check `ScriptHookVDotNet.log` and `scripts/AdvancedPedStudio.log` for initialization errors.

### Q: A model fails to spawn or shows "Invalid ped model name".
- Verify that the model name spelled in `pedmodels.txt` matches a valid GTA V ped hash or add-on model name.
- If using an add-on ped, ensure the DLC pack containing the ped is loaded and enabled in your `dlclist.xml`.

### Q: When changing tops, why don't the arms or pants vanish like in other menus?
- AdvancedPedStudio includes active **Torso & Leg Safeguarding**. Whenever top (Slot 11) or undershirt (Slot 8) variations are cycled, the script synchronizes and reapplies the correct arm/torso drawable and leg texture to eliminate gaps in the 3D model.

### Q: Can other scripts read `customizedpeds.ini`?
- Yes. `customizedpeds.ini` uses standard Windows INI formatting with both indexed values (`Component_X = drawable,texture`) and explicit keys (`Component_X_Drawable = drawable`, `Component_X_Texture = texture`), making it effortless to parse from C#, Lua, or Python scripts.

---

## 📜 Credits & License

- **AdvancedPedStudio** is distributed under the [MIT License](LICENSE).
- Developed for Grand Theft Auto V modding and custom ped creation.
- Powered by **[Script Hook V](http://www.dev-c.com/gtav/scripthookv/)** by Alexander Blade.
- Powered by **[Script Hook V .NET](https://github.com/scripthookvdotnet/scripthookvdotnet)** by crosire and contributors.
- UI built with **[LemonUI](https://github.com/Lemon-UI/LemonUI)** by Lemon and contributors.

