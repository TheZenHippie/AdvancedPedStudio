---
name: gtav-ped-control
description: >-
  Comprehensive guide and cheatsheet for controlling Ped behavior, tasks,
  animations, combat AI, and navigation in GTA V using C#, ScriptHookVDotNet v3
  (SHVDN3), and LemonUI. Use when implementing, scripting, or debugging Ped
  behavior, AI routines, task sequences, or LemonUI ped control menus.
---

# GTA V Ped Behavior Control (C# / SHVDN3 / LemonUI)

This skill provides reference patterns, native calls, and architectural practices for spawning, controlling, animating, and commanding Peds (NPCs and companions) in **Grand Theft Auto V** using **ScriptHookVDotNet v3 (SHVDN3)** and **LemonUI**.

> [!TIP]
> For a quick lookup of native hashes, animation bitmasks, scenarios, and combat attributes, see the [Ped Tasks Cheatsheet](./references/ped_tasks_cheatsheet.md).

---

## 1. Ped Lifecycle, Streaming & Ground Stability

### 1.1 Safe Model Streaming & Spawning
Always stream models asynchronously with a timeout loop and call `Script.Wait(0)`. Never call `World.CreatePed` before the model is loaded. Always mark the model as no longer needed after creation to prevent memory leaks.

```csharp
public static Ped SpawnSafePed(string modelName, Vector3 position, float heading = 0f)
{
    Model model = new Model(modelName);
    if (!model.IsValid) return null;

    model.Request(1500);
    int startTime = Game.GameTime;
    while (!model.IsLoaded && (Game.GameTime - startTime) < 1500)
    {
        Script.Wait(0);
    }

    if (!model.IsLoaded)
    {
        model.MarkAsNoLongerNeeded();
        return null;
    }

    Ped ped = World.CreatePed(model, position, heading);
    model.MarkAsNoLongerNeeded();

    if (ped != null && ped.Exists())
    {
        ped.IsPersistent = true;          // Prevents GTA engine despawn sweeps
        ped.BlockPermanentEvents = true;  // Prevents ambient AI events (gunshots/fleeing) from aborting tasks
    }

    return ped;
}
```

### 1.2 Anti-Fall Ground Z Raycasting
Peds spawned at raw camera or player coordinates will fall through map geometry or interior floors if the collision mesh is still streaming. Use `GET_GROUND_Z_FOR_3D_COORD` raycasting and coordinate clamping:

```csharp
public static Vector3 GetAccurateGroundPosition(Vector3 targetPos)
{
    // Request collision around the target coordinates
    Function.Call(Hash.REQUEST_COLLISION_AT_COORD, targetPos.X, targetPos.Y, targetPos.Z);

    float groundZ;
    // Multi-height downward raycast test
    for (float zOffset = 5.0f; zOffset >= -5.0f; zOffset -= 2.0f)
    {
        unsafe
        {
            if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, targetPos.X, targetPos.Y, targetPos.Z + zOffset, &groundZ, false))
            {
                return new Vector3(targetPos.X, targetPos.Y, groundZ);
            }
        }
    }

    return targetPos;
}

// Snap ped onto ground and ensure collision:
public static void SnapPedToGround(Ped ped, Vector3 groundPos)
{
    ped.Position = groundPos;
    Function.Call(Hash.SET_PED_COORDS_KEEP_VEHICLE, ped.Handle, groundPos.X, groundPos.Y, groundPos.Z);
    Function.Call(Hash.SET_ENTITY_COLLISION, ped.Handle, true, true);
    ped.IsCollisionEnabled = true;
}
```

---

## 2. Ped Task System & Navigation

### 2.1 Task Clearing Hierarchy
GTA V tasks operate on priority layers. If a ped refuses to take a new task, clear conflicting layers:

| Method | Behavior | Best Used For |
| :--- | :--- | :--- |
| `ped.Task.ClearAll()` / `CLEAR_PED_TASKS` | Smoothly exits current primary task (allows exit animations). | Switching idle behaviors, walking to a new point. |
| `CLEAR_PED_TASKS_IMMEDIATELY` | Aborts current task instantly (ped snaps to idle/ragdoll reset). | Panic stops, teleporting, switching combat states. |
| `ped.Task.ClearSecondary()` / `CLEAR_PED_SECONDARY_TASKS` | Clears upper-body/facial/secondary animations only. | Stopping phone calls, drinking, or gestures while keeping locomotion. |

```csharp
// Smooth transition
Function.Call(Hash.CLEAR_PED_TASKS, ped.Handle);

// Instant hard reset
Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, ped.Handle);
```

### 2.2 Locomotion & Navigation Tasks

```csharp
// 1. Move to coordinate using pathfinding / navmesh
ped.Task.FollowNavMeshTo(targetCoord, 1.0f, -1); // 1.0f = walk, 2.0f = run, 3.0f = sprint

// 2. Walk to coordinate in a straight line (ignoring navmesh)
ped.Task.GoStraightTo(targetCoord);

// 3. Wander in radius
ped.Task.WanderAround(centerCoord, 20.0f);

// 4. Follow an entity (companion / bodyguard behavior)
// (targetEntity, offsetVector, speed, timeout, stoppingRange, persistFollowing)
ped.Task.FollowToOffsetFromEntity(Game.Player.Character, new Vector3(0, -2.0f, 0), 1.5f, -1, 1.5f, true);

// 5. Enter/Exit Vehicle
ped.Task.EnterVehicle(vehicle, VehicleSeat.Passenger, -1, 2.0f, EnterVehicleFlags.None);
ped.Task.LeaveVehicle(vehicle, LeaveVehicleFlags.None);
```

### 2.3 Task Sequences (Choreographed Multi-Step Routines)
Task sequences queue multiple tasks in native memory so the ped progresses through them automatically without requiring per-frame C# state polling.

```csharp
public static void RunPatrolSequence(Ped ped, Vector3 pointA, Vector3 pointB, string scenarioName)
{
    TaskSequence sequence = new TaskSequence();

    // Step 1: Walk to Point A
    sequence.AddTask.FollowNavMeshTo(pointA, 1.0f, -1);

    // Step 2: Perform scenario at Point A for 15 seconds
    sequence.AddTask.Wait(15000);

    // Step 3: Run to Point B
    sequence.AddTask.FollowNavMeshTo(pointB, 2.0f, -1);

    // Close the sequence to finalize compilation
    sequence.Close();

    // Assign and execute
    ped.Task.PerformSequence(sequence);

    // Dispose managed sequence handle (native memory is retained by ped)
    sequence.Dispose();
}
```

---

## 3. Animations, Scenarios & Movement Sets

### 3.1 Streaming & Playing Animations
Always request the dictionary, wait for it to stream, play the clip, and remove the dictionary reference:

```csharp
public static void PlayAnimationSafe(Ped ped, string dict, string clip, int flags = 1)
{
    if (!Function.Call<bool>(Hash.DOES_ANIM_DICT_EXIST, dict)) return;

    Function.Call(Hash.REQUEST_ANIM_DICT, dict);
    int start = Game.GameTime;
    while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict) && (Game.GameTime - start) < 1500)
    {
        Script.Wait(0);
    }

    if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
    {
        // blendIn=8.0f, blendOut=-8.0f, duration=-1 (infinite), flags, startPhase=0.0f
        Function.Call(Hash.TASK_PLAY_ANIM, ped.Handle, dict, clip, 8.0f, -8.0f, -1, flags, 0.0f, false, false, false);
        Function.Call(Hash.REMOVE_ANIM_DICT, dict);
    }
}
```

### 3.2 Animation Bitmask Flags
The `flags` integer controls how the animation blends with movement:

| Flag | Name | Functionality |
| :--- | :--- | :--- |
| `0` | Default | Plays once, freezes or resets when done, overrides full body. |
| `1` | Repeat / Loop | Loops continuously until explicitly cleared. |
| `16` | Secondary Task | Plays in secondary task slot (e.g. gesturing or pointing). |
| `32` | Upper Body Only | Only moves torso, head, and arms. Lower body stays under normal navigation/walking control! |
| `48` | Looping Upper Body | Combines `16 + 32`. Loops upper body animation while ped can walk freely. |
| `49` | Controllable Loop | Combines `1 + 16 + 32`. Upper-body loop with ped controllable locomotion. |

### 3.3 Scenarios
Scenarios are pre-baked GTA V world actions (smoking, drinking, leaning, yoga, cop inspecting). They include props, sound, and animations automatically:

```csharp
// Play scenario at ped's current position:
Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, "WORLD_HUMAN_SMOKING", 0, true);

// Check if currently active:
bool isUsing = Function.Call<bool>(Hash.IS_PED_USING_SCENARIO, ped.Handle, "WORLD_HUMAN_SMOKING");

// Stop scenario:
Function.Call(Hash.CLEAR_PED_TASKS, ped.Handle);
```

### 3.4 Movement Clipsets (Walk Styles)
Movement clipsets alter how the ped walks, runs, and idles (e.g. `move_f@sexy`, `move_m@gangster@ng`):

```csharp
public static void ApplyWalkStyle(Ped ped, string clipset)
{
    if (string.IsNullOrEmpty(clipset) || clipset == "(Default)")
    {
        Function.Call(Hash.RESET_PED_MOVEMENT_CLIPSET, ped.Handle, 0.25f);
        return;
    }

    Function.Call(Hash.REQUEST_CLIP_SET, clipset);
    int start = Game.GameTime;
    while (!Function.Call<bool>(Hash.HAS_CLIP_SET_LOADED, clipset) && (Game.GameTime - start) < 1500)
    {
        Script.Wait(0);
    }

    if (Function.Call<bool>(Hash.HAS_CLIP_SET_LOADED, clipset))
    {
        Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, ped.Handle, clipset, 0.25f);
        Function.Call(Hash.REMOVE_CLIP_SET, clipset);
    }
}
```

---

## 4. Combat AI, Relationships & Weapons

### 4.1 Relationship Groups
Relationships determine if peds help the player, attack enemies, or remain passive:

```csharp
public static void SetupCompanionRelationship(Ped companion)
{
    // Create or retrieve relationship groups
    int playerGroup = Game.Player.Character.RelationshipGroup;
    int companionGroup = World.AddRelationshipGroup("APS_COMPANIONS");

    companion.RelationshipGroup = companionGroup;

    // Set mutual respect between Player and Companions
    World.SetRelationshipBetweenGroups(Relationship.Companion, companionGroup, playerGroup);
    World.SetRelationshipBetweenGroups(Relationship.Companion, playerGroup, companionGroup);

    // Companion hates enemy groups
    int copGroup = Game.GenerateHash("COP");
    World.SetRelationshipBetweenGroups(Relationship.Hate, companionGroup, copGroup);
}
```

### 4.2 Combat Behavior & Attributes
Configure how aggressive or tactically intelligent the ped acts:

```csharp
public static void ConfigureCombatAI(Ped ped, bool aggressive = true)
{
    // Combat Movement: Stationary, Defensive, WillAdvance, Charging
    ped.CombatMovement = aggressive ? CombatMovement.WillAdvance : CombatMovement.Defensive;
    
    // Combat Range: Near, Medium, Far
    ped.CombatRange = CombatRange.Medium;

    // Set combat attributes using natives:
    // 5 = BF_AlwaysFight, 46 = BF_AlwaysEquipBestWeapon, 0 = BF_CanUseCover
    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 5, aggressive); // Always fight
    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 46, true);       // Equip best weapon
    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 0, true);        // Use cover

    ped.Accuracy = 60; // 0 - 100
    ped.Armor = 100;
}
```

### 4.3 Weapon Arming
```csharp
// Give weapon with ammo and equip immediately
ped.Weapons.Give(WeaponHash.CombatPistol, 500, true, true);

// Prevent dropping weapon on death
Function.Call(Hash.SET_PED_DROPS_WEAPONS_WHEN_DEAD, ped.Handle, false);
```

---

## 5. Decorators: Cross-Script & Inter-Tick State Tracking

Decorators store custom values directly on the native GTA V entity handle, surviving script reloads and enabling decoupled state machine execution.

```csharp
// 1. Register decorators once on script initialization
public static void InitDecorators()
{
    Function.Call(Hash.DECOR_REGISTER, "APS_HasTaskLoop", 2); // 2 = bool
    Function.Call(Hash.DECOR_REGISTER, "APS_TaskType", 3);    // 3 = int (1=Anim, 2=Scenario, 0=None)
    Function.Call(Hash.DECOR_REGISTER, "APS_ProfileId", 3);   // 3 = int
}

// 2. Set decorator values on ped
public static void TagPedTask(Ped ped, int taskType, int profileId)
{
    Function.Call(Hash.DECOR_SET_BOOL, ped.Handle, "APS_HasTaskLoop", true);
    Function.Call(Hash.DECOR_SET_INT, ped.Handle, "APS_TaskType", taskType);
    Function.Call(Hash.DECOR_SET_INT, ped.Handle, "APS_ProfileId", profileId);
}

// 3. Query decorator in tick loop
public static bool IsCustomPedWithTask(Ped ped)
{
    return Function.Call<bool>(Hash.DECOR_EXIST_ON, ped.Handle, "APS_HasTaskLoop") &&
           Function.Call<bool>(Hash.DECOR_GET_BOOL, ped.Handle, "APS_HasTaskLoop");
}
```

---

## 6. LemonUI Reactive Integration Patterns

### 6.1 Menu Architecture
In LemonUI, create an `ObjectPool` to manage menus. Process the pool on every `Script.Tick`:

```csharp
public class PedStudioScript : Script
{
    private readonly ObjectPool _menuPool = new ObjectPool();
    private readonly NativeMenu _mainMenu = new NativeMenu("Ped Studio", "Control Peds & Behavior");

    public PedStudioScript()
    {
        _menuPool.Add(_mainMenu);
        BuildBehaviorMenu();

        Tick += OnTick;
        KeyDown += OnKeyDown;
    }

    private void OnTick(object sender, EventArgs e)
    {
        _menuPool.Process();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11)
        {
            _mainMenu.Visible = !_mainMenu.Visible;
        }
    }
}
```

### 6.2 Reactive Sliders & Live Modification
Use `NativeListItem` and `NativeCheckboxItem`. Update the target ped reactively in event handlers without recreating the ped:

```csharp
private void BuildBehaviorMenu()
{
    // Task Type Selector
    string[] tasks = new[] { "Stand Still", "Wander", "Smoke Scenario", "Dance Animation", "Follow Player" };
    var taskListItem = new NativeListItem<string>("Action", "Assign immediate routine to ped", tasks);
    
    taskListItem.ItemChanged += (sender, e) =>
    {
        Ped target = GetTargetPed();
        if (target == null || !target.Exists() || !target.IsAlive) return;

        string selected = taskListItem.SelectedItem;
        Function.Call(Hash.CLEAR_PED_TASKS, target.Handle);

        switch (selected)
        {
            case "Stand Still":
                target.Task.StandStill(-1);
                break;
            case "Wander":
                target.Task.WanderAround();
                break;
            case "Smoke Scenario":
                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, target.Handle, "WORLD_HUMAN_SMOKING", 0, true);
                break;
            case "Dance Animation":
                PlayAnimationSafe(target, "mini@strip_club@pole_dance@pole_dance1", "base", 1);
                break;
            case "Follow Player":
                target.Task.FollowToOffsetFromEntity(Game.Player.Character, new Vector3(0, -2f, 0), 1.5f, -1, 1.5f, true);
                break;
        }
    };

    // Invincible / Godmode Toggle
    var godmodeCheckbox = new NativeCheckboxItem("Invincible", "Toggle ped godmode", false);
    godmodeCheckbox.CheckboxChanged += (sender, e) =>
    {
        Ped target = GetTargetPed();
        if (target != null && target.Exists())
        {
            target.IsInvincible = godmodeCheckbox.Checked;
        }
    };

    _mainMenu.Add(taskListItem);
    _mainMenu.Add(godmodeCheckbox);
}
```

---

## 7. Critical Pitfalls & Solutions

1. **Missing Torso or Invisible Limbs on Outfit Change:**
   - Always apply clothing components in the multi-pass dependency order:
     `{ 8, 11, 4, 6, 0, 1, 2, 5, 7, 9, 10, 3, 4, 8, 11, 3 }`
   - Component 3 (Torso/Arms), 8 (Undershirt), and 11 (Tops) interact tightly. Reapplying torso (`3`) after tops (`11`) fixes invisible arms.

2. **Ped Sinking Through Interiors or Custom Maps:**
   - Interior floors often require `REQUEST_COLLISION_AT_COORD` followed by a multi-step downward raycast. Anchor coordinate with `SET_PED_COORDS_KEEP_VEHICLE`.

3. **Peds Running Away from Sirens / Gunshots:**
   - Always set `ped.BlockPermanentEvents = true;`. Ambient GTA AI will override custom script tasks unless this flag is active.

4. **Script Freezing or Deadlocks:**
   - Never call `System.Threading.Thread.Sleep()` inside `Script.Tick` or event handlers! This freezes the GTA V rendering pipeline. Always use `Script.Wait(ms)` or game-time delta counters (`Game.GameTime - lastTime`).
