# GTA V Ped Tasks & Animation Quick Reference Cheatsheet

This cheatsheet provides a quick lookup for native hashes, animation flags, scenario strings, clipsets, and combat attributes commonly used when programming Peds with SHVDN3 and LemonUI.

---

## 1. Native Task Hashes

| Native Hash | Name | Description |
| :--- | :--- | :--- |
| `0xE1ACC3E488A68DDD` | `CLEAR_PED_TASKS` | Smoothly stops current primary task; ped exits cleanly. |
| `0xAAA34F8A7CB32098` | `CLEAR_PED_TASKS_IMMEDIATELY` | Aborts tasks immediately; snaps to idle/ragdoll reset. |
| `0x176CE9C0A01E6090` | `CLEAR_PED_SECONDARY_TASKS` | Clears upper-body, gestures, or secondary animations. |
| `0xEA9A52394AA0F426` | `TASK_PLAY_ANIM` | Plays animation with blending, duration, and flags. |
| `0x142F4026E3A53B64` | `TASK_START_SCENARIO_IN_PLACE` | Starts a pre-baked world scenario at current location. |
| `0x295E3CCEC879CCD2` | `TASK_FOLLOW_NAV_MESH_TO_COORD`| Pathfinds to coordinates along GTA V navmesh. |
| `0x5955832818586230` | `TASK_GO_STRAIGHT_TO_COORD` | Direct line-of-sight walk towards coordinates. |
| `0x915B06450A9211D5` | `TASK_WANDER_STANDARD` | Ambient wandering in surrounding area. |
| `0xC313379CA0FC5BB7` | `TASK_ENTER_VEHICLE` | Navigates to and enters a specific vehicle seat. |
| `0xD3DB4B809E484529` | `TASK_LEAVE_VEHICLE` | Exits the current vehicle. |
| `0xF28965D04F570D4C` | `TASK_COMBAT_PED` | Engages an enemy ped in combat. |
| `0x4524A9C37E107321` | `TASK_STAND_STILL` | Freezes ped in place for specified time (-1 for indefinite). |

---

## 2. Animation Flags (Bitmask)

Pass these to `TASK_PLAY_ANIM` (param `flags`):

```csharp
[Flags]
public enum AnimationFlags
{
    Normal = 0,
    Looping = 1,
    StopOnLastFrame = 2,
    OnlyAnimateUpperBody = 16,
    AllowPlayerControl = 32,
    Cancellable = 128,
    UpperBodyControllableLoop = Looping | OnlyAnimateUpperBody | AllowPlayerControl // 49
}
```

---

## 3. High-Quality Curated Scenarios

```csharp
public static readonly string[] AmbientScenarios = new[]
{
    "WORLD_HUMAN_SMOKING",
    "WORLD_HUMAN_SMOKING_POT",
    "WORLD_HUMAN_DRINKING",
    "WORLD_HUMAN_PARTYING",
    "WORLD_HUMAN_STAND_MOBILE",
    "WORLD_HUMAN_CHEERING",
    "WORLD_HUMAN_SUNBATHE",
    "WORLD_HUMAN_SUNBATHE_BACK",
    "WORLD_HUMAN_YOGA",
    "WORLD_HUMAN_PUSH_UPS",
    "WORLD_HUMAN_SIT_UPS",
    "WORLD_HUMAN_MUSCLE_FLEX",
    "WORLD_HUMAN_COP_IDLES",
    "WORLD_HUMAN_GUARD_STAND",
    "WORLD_HUMAN_LEANING",
    "WORLD_HUMAN_HANG_OUT_STREET",
    "WORLD_HUMAN_PROSTITUTE_HIGH_CLASS",
    "WORLD_HUMAN_PROSTITUTE_LOW_CLASS",
    "WORLD_HUMAN_STRIP_WATCH_STAND"
};
```

---

## 4. Popular Walk Styles (Movement Clipsets)

| Clipset String | Style Description |
| :--- | :--- |
| `move_m@gangster@ng` | Street gangster swagger |
| `move_f@sexy` | High-energy feminine walk |
| `move_f@posh@` | Sophisticated / high-class posture |
| `move_f@heels@c` | Walking in high heels |
| `move_m@business@a` | Confident business stroll |
| `move_m@brave` | Upright heroic stance |
| `move_m@drunk@a` | Heavy stumbling drunk |
| `move_f@injured` | Limping / injured gait |
| `move_m@sad@a` | Depressed / slouched walk |
| `move_m@hurry@a` | Fast-paced hurry |

---

## 5. Combat Attribute Hashes & Constants

Pass to `Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, attributeIndex, value)`:

- **0:** `BF_CanUseCover` (Enable/disable taking cover behind obstacles)
- **1:** `BF_CanUseVehicles` (Enable/disable driving into combat)
- **2:** `BF_CanDoDrivebys` (Enable shooting from vehicles)
- **5:** `BF_AlwaysFight` (Never surrender or run away)
- **17:** `BF_DisableFleeFromCombat` (Prevents fleeing even when outmatched)
- **46:** `BF_AlwaysEquipBestWeapon` (Automatically switches to highest DPS weapon in inventory)
- **52:** `BF_CanTauntInCombat` (Plays taunts and combat vocalizations)

---

## 6. Common Weapon Hashes

- `WeaponHash.Pistol`
- `WeaponHash.CombatPistol`
- `WeaponHash.MicroSMG`
- `WeaponHash.SMG`
- `WeaponHash.AssaultRifle`
- `WeaponHash.CarbineRifle`
- `WeaponHash.PumpShotgun`
- `WeaponHash.SniperRifle`
