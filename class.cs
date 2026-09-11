using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using LemonUI;
using LemonUI.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Control = GTA.Control;
using Hash = GTA.Native.Hash;

public static class Logger
{
    private static readonly object LogLock = new object();
    private static string _scriptsDir;
    private static string _logFilePath;
    private static bool _dirChecked;

    public static string GetScriptsDirectory()
    {
        if (_scriptsDir != null) return _scriptsDir;

        const string explicitPath = @"D:\SteamLibrary\steamapps\common\Grand Theft Auto V\scripts";
        if (Directory.Exists(explicitPath))
        {
            _scriptsDir = explicitPath;
            return _scriptsDir;
        }

        // 1. Resolve to the directory where this script assembly is located
        try
        {
            string asmPath = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(asmPath))
            {
                string asmDir = Path.GetDirectoryName(asmPath);
                if (!string.IsNullOrEmpty(asmDir) && Directory.Exists(asmDir))
                {
                    _scriptsDir = asmDir;
                    return _scriptsDir;
                }
            }
        }
        catch { }

        // 2. Fall back to the "scripts" directory under the GTA V main directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        if (baseDir.EndsWith("scripts", StringComparison.OrdinalIgnoreCase))
        {
            _scriptsDir = baseDir;
            return _scriptsDir;
        }

        string scriptsDir = Path.Combine(baseDir, "scripts");
        if (!Directory.Exists(scriptsDir))
        {
            try { Directory.CreateDirectory(scriptsDir); } catch { }
        }

        _scriptsDir = scriptsDir;
        return _scriptsDir;
    }

    private static string LogFilePath
    {
        get
        {
            if (_logFilePath == null)
            {
                _logFilePath = Path.Combine(GetScriptsDirectory(), "AdvancedPedStudio.log");
            }
            return _logFilePath;
        }
    }

    public static void Write(object message)
    {
        Log(message);
    }

    public static void Write(string format, params object[] args)
    {
        Log(string.Format(format, args));
    }

    public static void WriteLine(object message)
    {
        Log(message);
    }

    public static void Log(object message)
    {
        try
        {
            lock (LogLock)
            {
                string path = LogFilePath;
                if (!_dirChecked)
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    _dirChecked = true;
                }

                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(logLine);
                    sw.Flush();
                    fs.Flush(true);
                }
            }
        }
        catch (Exception)
        {
            // Ignore file access conflicts
        }
    }

    public static void Error(string message, Exception ex = null)
    {
        if (ex != null)
        {
            Log($"[ERROR] {message} | Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
        else
        {
            Log($"[ERROR] {message}");
        }
    }
}

namespace AdvancedPedStudio
{
    /// <summary>
    /// Direct, robust INI file reader & writer that guarantees immediate persistence to disk with FileOptions.WriteThrough.
    /// </summary>
    public class SimpleIniFile
    {
        private readonly string _filePath;
        private readonly object _fileLock = new object();
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public SimpleIniFile(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public void Load()
        {
            lock (_fileLock)
            {
                _sections.Clear();
                if (!File.Exists(_filePath)) return;

                try
                {
                    using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs, Encoding.UTF8))
                    {
                        string currentSection = "";
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                            {
                                continue;
                            }

                            if (line.StartsWith("[") && line.EndsWith("]"))
                            {
                                currentSection = line.Substring(1, line.Length - 2).Trim();
                                if (!_sections.ContainsKey(currentSection))
                                {
                                    _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                }
                            }
                            else if (!string.IsNullOrEmpty(currentSection))
                            {
                                int eqIdx = line.IndexOf('=');
                                if (eqIdx > 0)
                                {
                                    string key = line.Substring(0, eqIdx).Trim();
                                    string val = line.Substring(eqIdx + 1).Trim();
                                    _sections[currentSection][key] = val;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading INI file: {_filePath}", ex);
                }
            }
        }

        public List<string> GetSectionNames()
        {
            lock (_fileLock)
            {
                return _sections.Keys.ToList();
            }
        }

        public string GetValue(string section, string key, string defaultValue = "")
        {
            lock (_fileLock)
            {
                if (_sections.TryGetValue(section, out var keys) && keys.TryGetValue(key, out var val))
                {
                    return val;
                }
                return defaultValue;
            }
        }

        public void SetValue(string section, string key, object value)
        {
            lock (_fileLock)
            {
                if (!_sections.TryGetValue(section, out var keys))
                {
                    keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _sections[section] = keys;
                }
                keys[key] = value?.ToString() ?? "";
            }
        }

        public bool Save()
        {
            lock (_fileLock)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    using (var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                    using (var sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        sw.WriteLine("; ============================================================");
                        sw.WriteLine("; AdvancedPedStudio - Saved Customized Peds");
                        sw.WriteLine($"; Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        sw.WriteLine("; ============================================================");
                        sw.WriteLine();

                        foreach (var section in _sections)
                        {
                            sw.WriteLine($"[{section.Key}]");
                            foreach (var kvp in section.Value)
                            {
                                sw.WriteLine($"{kvp.Key} = {kvp.Value}");
                            }
                            sw.WriteLine();
                        }

                        sw.Flush();
                        fs.Flush(true);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error saving INI file: {_filePath}", ex);
                    return false;
                }
            }
        }

        public bool DeleteSection(string sectionName)
        {
            lock (_fileLock)
            {
                if (_sections.Remove(sectionName))
                {
                    return Save();
                }
                return false;
            }
        }
    }

    public class AnimationEntry
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Dictionary { get; set; }
        public string Clip { get; set; }

        public AnimationEntry(string category, string name, string dict, string clip)
        {
            Category = category;
            Name = name;
            Dictionary = dict;
            Clip = clip;
        }

        public override string ToString() => Name;
    }

    public class ScenarioEntry
    {
        public string Name { get; set; }
        public string Scenario { get; set; }

        public ScenarioEntry(string name, string scenario)
        {
            Name = name;
            Scenario = scenario;
        }

        public override string ToString() => Name;
    }

    public class AdvancedPedStudio : Script
    {
        private const float SpawnDistanceFeet = 10.0f;
        private const float SpawnDistanceMeters = SpawnDistanceFeet * 0.3048f; // ~3.048 meters
        private const float PreviewRotationSpeed = 0.85f;

        // Dependency order for applying components so that Tops/Undershirts never wipe out Torso/Arms/Legs
        private static readonly int[] ComponentApplyOrder = { 8, 11, 4, 6, 0, 1, 2, 5, 7, 9, 10, 3, 4, 8, 11, 3 };

        private static readonly string[] MovementStyleList = new string[]
        {
            "(Default)",
            "move_f@arrogant@a", "move_f@arrogant@b", "move_f@arrogant@c", "move_f@business@a",
            "move_f@chichi", "move_f@chubby@a", "move_f@depressed@a", "move_f@depressed@b",
            "move_f@depressed@c", "move_f@drunk@a", "move_f@exhausted", "move_f@fat@a",
            "move_f@femme@", "move_f@film_reel", "move_f@film_reel_arms", "move_f@flee@a",
            "move_f@flee@b", "move_f@flee@c", "move_f@flee@generic", "move_f@gangster@ng",
            "move_f@generic", "move_f@handbag", "move_f@heels@c", "move_f@heels@d",
            "move_f@hiking", "move_f@hurry@a", "move_f@hurry@b", "move_f@injured",
            "move_f@jogger", "move_f@maneater", "move_f@multiplayer", "move_f@posh@",
            "move_f@runner", "move_f@sad@a", "move_f@sad@b", "move_f@sassy",
            "move_f@scared@a", "move_f@sexy", "move_f@sexy@a", "move_f@shy@a",
            "move_f@shy@b", "move_f@shy@c", "move_f@shy@d", "move_f@tool_belt@a",
            "move_f@tough_guy@"
        };

        private static readonly AnimationEntry[] CuratedAnimations = new[]
        {
            // Pole Dance & Strip Club
            new AnimationEntry("Pole Dances", "Pole Dance 1", "mini@strip_club@pole_dance@pole_dance1", "base"),
            new AnimationEntry("Pole Dances", "Pole Dance 2", "mini@strip_club@pole_dance@pole_dance2", "base"),
            new AnimationEntry("Pole Dances", "Pole Dance 3", "mini@strip_club@pole_dance@pole_dance3", "base"),
            new AnimationEntry("Pole Dances", "Private Dance (Part 1)", "mini@strip_club@private_dance@part1", "priv_dance_p1"),
            new AnimationEntry("Pole Dances", "Private Dance (Part 2)", "mini@strip_club@private_dance@part2", "priv_dance_p2"),
            new AnimationEntry("Pole Dances", "Private Dance (Part 3)", "mini@strip_club@private_dance@part3", "priv_dance_p3"),
            new AnimationEntry("Pole Dances", "Private Dance (Idle)", "mini@strip_club@private_dance@idle", "idle"),

            // Stripper Idles
            new AnimationEntry("Stripper Idles", "Stripper Idle 01", "mini@strip_club@idles@stripper", "stripper_idle_01"),
            new AnimationEntry("Stripper Idles", "Stripper Idle 02", "mini@strip_club@idles@stripper", "stripper_idle_02"),
            new AnimationEntry("Stripper Idles", "Stripper Idle 03", "mini@strip_club@idles@stripper", "stripper_idle_03"),
            new AnimationEntry("Stripper Idles", "Stripper Idle 04", "mini@strip_club@idles@stripper", "stripper_idle_04"),
            new AnimationEntry("Stripper Idles", "Stripper Idle 05", "mini@strip_club@idles@stripper", "stripper_idle_05"),
            new AnimationEntry("Stripper Idles", "Stripper Idle 06", "mini@strip_club@idles@stripper", "stripper_idle_06"),

            // Nightclub Dances
            new AnimationEntry("Nightclub Dances", "Nightclub Solo Dance", "anim@amb@nightclub@mini@dance@dance_solo@female@var_a@", "med_center"),
            new AnimationEntry("Nightclub Dances", "Podium Dancer", "anim@amb@nightclub@dancers@podium_dancers@", "hi_dance_facedj_11_v1_female^1"),
            new AnimationEntry("Nightclub Dances", "Crowd / Lowrider Dance", "anim@amb@nightclub@dancers@crowddancers@lowriders@", "hi_dance_facedj_09_v1_male^1"),
            new AnimationEntry("Nightclub Dances", "Partying with Beer", "amb@world_human_partying@female@partying_beer@base", "base"),
            new AnimationEntry("Nightclub Dances", "Cheering (Crowd)", "amb@world_human_cheering@female_a", "base"),

            // Drinks & Social
            new AnimationEntry("Drinks & Social", "Drink Beer", "amb@world_human_drinking@beer@female@idle_a", "idle_a"),
            new AnimationEntry("Drinks & Social", "Drink Beer & Wander", "amb@code_human_wander_drinking@beer@female@base", "idle_a"),
            new AnimationEntry("Drinks & Social", "Smoke Cigarette", "amb@world_human_smoking@female@idle_a", "idle_a"),
            new AnimationEntry("Drinks & Social", "Smoke Weed / Pot", "amb@world_human_smoking_pot@female@idle_a", "idle_a"),
            new AnimationEntry("Drinks & Social", "Mobile Phone (Texting)", "amb@world_human_stand_mobile@female@text@base", "idle_a"),
            new AnimationEntry("Drinks & Social", "Mobile Phone (Calling)", "amb@world_human_stand_mobile@female@call@base", "idle_a"),

            // Sunbathing
            new AnimationEntry("Sunbathing", "Sunbathing (Front)", "amb@world_human_sunbathe@female@front@base", "idle_a"),
            new AnimationEntry("Sunbathing", "Sunbathing (Back)", "amb@world_human_sunbathe@female@back@base", "idle_a"),

            // Fitness
            new AnimationEntry("Fitness", "Push Ups", "amb@world_human_push_ups@male@base", "base"),
            new AnimationEntry("Fitness", "Sit Ups", "amb@world_human_sit_ups@male@base", "base"),
            new AnimationEntry("Fitness", "Yoga Pose", "amb@world_human_yoga@female@base", "base"),
            new AnimationEntry("Fitness", "Muscle Flex (Front)", "amb@world_human_muscle_flex@arms_in_front@base", "base"),
            new AnimationEntry("Fitness", "Muscle Flex (Sides)", "amb@world_human_muscle_flex@arms_at_side@base", "base")
        };

        private static readonly ScenarioEntry[] CuratedScenarios = new[]
        {
            new ScenarioEntry("Party / Dance", "WORLD_HUMAN_PARTYING"),
            new ScenarioEntry("Smoke Cigarette", "WORLD_HUMAN_SMOKING"),
            new ScenarioEntry("Smoke Weed / Pot", "WORLD_HUMAN_SMOKING_POT"),
            new ScenarioEntry("Drink Beer", "WORLD_HUMAN_DRINKING"),
            new ScenarioEntry("Cheer Crowd", "WORLD_HUMAN_CHEERING"),
            new ScenarioEntry("Sunbathe (Front)", "WORLD_HUMAN_SUNBATHE"),
            new ScenarioEntry("Sunbathe (Back)", "WORLD_HUMAN_SUNBATHE_BACK"),
            new ScenarioEntry("Yoga Exercise", "WORLD_HUMAN_YOGA"),
            new ScenarioEntry("Mobile Phone", "WORLD_HUMAN_STAND_MOBILE"),
            new ScenarioEntry("Muscle Flex", "WORLD_HUMAN_MUSCLE_FLEX"),
            new ScenarioEntry("Push Ups", "WORLD_HUMAN_PUSH_UPS"),
            new ScenarioEntry("Sit Ups", "WORLD_HUMAN_SIT_UPS"),
            new ScenarioEntry("Cop Idle", "WORLD_HUMAN_COP_IDLES"),
            new ScenarioEntry("Guard Stand", "WORLD_HUMAN_GUARD_STAND"),
            new ScenarioEntry("Strip Watch", "WORLD_HUMAN_STRIP_WATCH_STAND"),
            new ScenarioEntry("Prostitute (High Class)", "WORLD_HUMAN_PROSTITUTE_HIGH_CLASS"),
            new ScenarioEntry("Prostitute (Low Class)", "WORLD_HUMAN_PROSTITUTE_LOW_CLASS"),
            new ScenarioEntry("Hang Out Street", "WORLD_HUMAN_HANG_OUT_STREET"),
            new ScenarioEntry("Jog in Place", "WORLD_HUMAN_JOG_STANDING"),
            new ScenarioEntry("Lean Against Wall", "WORLD_HUMAN_LEANING"),
            new ScenarioEntry("Look with Binoculars", "WORLD_HUMAN_BINOCULARS"),
            new ScenarioEntry("Tourist with Map", "WORLD_HUMAN_TOURIST_MAP")
        };

        private static readonly string[] AnimCategories = new[]
        {
            "(None)",
            "Pole Dances",
            "Stripper Idles",
            "Nightclub Dances",
            "Drinks & Social",
            "Sunbathing",
            "Fitness",
            "Scenarios",
            "Custom"
        };

        // Pre-indexed lookup caches for O(1) animation and scenario searches
        private static readonly Dictionary<string, List<AnimationEntry>> AnimationsByCategory = new Dictionary<string, List<AnimationEntry>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AnimationEntry> AnimationsByName = new Dictionary<string, AnimationEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ScenarioEntry> ScenariosByName = new Dictionary<string, ScenarioEntry>(StringComparer.OrdinalIgnoreCase);

        static AdvancedPedStudio()
        {
            foreach (var anim in CuratedAnimations)
            {
                if (!AnimationsByCategory.TryGetValue(anim.Category, out var list))
                {
                    list = new List<AnimationEntry>();
                    AnimationsByCategory[anim.Category] = list;
                }
                list.Add(anim);
                AnimationsByName[anim.Name] = anim;
            }

            foreach (var sc in CuratedScenarios)
            {
                ScenariosByName[sc.Name] = sc;
            }
        }

        private readonly string _pedModelsPath;
        private readonly string _customizedPedsIniPath;
        private readonly string _configIniPath;
        private readonly SimpleIniFile _customizedPedsIni;

        private Keys _activationKey = Keys.F11;

        private readonly List<string> _pedModels = new List<string>();

        private readonly ObjectPool _menuPool;
        private readonly NativeMenu _mainMenu;
        private readonly NativeMenu _clothingMenu;
        private readonly NativeMenu _propsMenu;
        private readonly NativeMenu _animMenu;
        private readonly NativeMenu _savedPedsMenu;
        private readonly NativeMenu _deleteSavedMenu;

        private NativeListItem<string> _modelListItem;
        private NativeListItem<string> _movementStyleListItem;
        private NativeListItem<string> _animCategoryListItem;
        private NativeListItem<string> _animItem;
        private NativeItem _customDictItem;
        private NativeItem _customClipItem;
        private NativeListItem<string> _spawnProfileListItem;
        private NativeItem _spawnButton;
        private NativeItem _reloadSettingsButton;

        private string _currentMovementStyle = "(Default)";
        private string _currentAnimType = "None"; // "None", "Animation", "Scenario"
        private string _currentAnimDict = "";
        private string _currentAnimClip = "";
        private string _currentScenario = "";
        private string _customAnimDict = "mini@strip_club@pole_dance@pole_dance1";
        private string _customAnimClip = "base";
        private string _lastSavedSection = "";

        private readonly Dictionary<int, NativeListItem<int>> _drawableSliders = new Dictionary<int, NativeListItem<int>>();
        private readonly Dictionary<int, NativeListItem<int>> _textureSliders = new Dictionary<int, NativeListItem<int>>();
        private readonly Dictionary<int, NativeListItem<int>> _propSliders = new Dictionary<int, NativeListItem<int>>();
        private readonly Dictionary<int, NativeListItem<int>> _propTextureSliders = new Dictionary<int, NativeListItem<int>>();

        private readonly int[] _componentIds = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        private readonly int[] _propIds = { 0, 1, 2, 6, 7 };

        private Ped _previewPed;
        private Vector3 _previewAnchorPos = Vector3.Zero;
        private bool _isUpdatingSliders = false;

        public AdvancedPedStudio()
        {
            try
            {
                string scriptsDir = Logger.GetScriptsDirectory();
                if (!Directory.Exists(scriptsDir))
                {
                    try { Directory.CreateDirectory(scriptsDir); } catch { }
                }

                _pedModelsPath = Path.Combine(scriptsDir, "pedmodels.txt");
                _customizedPedsIniPath = Path.Combine(scriptsDir, "customizedpeds.ini");
                _configIniPath = Path.Combine(scriptsDir, "AdvancedPedStudio.ini");
                _customizedPedsIni = new SimpleIniFile(_customizedPedsIniPath);

                LoadSettings();

                _menuPool = new ObjectPool();
                _mainMenu = new NativeMenu("Ped Studio", "Ped Customizer & Spawner");
                _clothingMenu = new NativeMenu("Clothing", "Clothing Variations");
                _propsMenu = new NativeMenu("Props", "Props & Accessories");
                _animMenu = new NativeMenu("Animations", "Movement & Animations");
                _savedPedsMenu = new NativeMenu("Saved Outfits", "Load Saved Profiles");
                _deleteSavedMenu = new NativeMenu("Delete Outfits", "Remove Profiles from INI");

                LoadPedModels();

                // 1. Model Selection Picker
                _modelListItem = new NativeListItem<string>("Model", "Select and preview ped model.", _pedModels.ToArray());
                _modelListItem.ItemChanged += (sender, e) => UpdatePreview();
                _mainMenu.Add(_modelListItem);

                // 2. Setup Clothing Sliders
                SetupClothingSlider("Head / Face", 0);
                SetupClothingSlider("Mask / Beard", 1);
                SetupClothingSlider("Hair", 2);
                SetupClothingSlider("Torso (Arms)", 3);
                SetupClothingSlider("Legs / Pants", 4);
                SetupClothingSlider("Hands / Bags", 5);
                SetupClothingSlider("Shoes", 6);
                SetupClothingSlider("Accessories", 7);
                SetupClothingSlider("Undershirt", 8);
                SetupClothingSlider("Body Armor", 9);
                SetupClothingSlider("Decals", 10);
                SetupClothingSlider("Top / Shirt", 11);

                // 3. Setup Prop Sliders
                SetupPropSlider("Hats / Helmets", 0);
                SetupPropSlider("Glasses", 1);
                SetupPropSlider("Earrings", 2);
                SetupPropSlider("Watches", 6);
                SetupPropSlider("Bracelets", 7);

                _clothingMenu.Opening += (sender, e) => RefreshClothingSliders();
                _propsMenu.Opening += (sender, e) => RefreshClothingSliders();

                _mainMenu.AddSubMenu(_clothingMenu);
                _mainMenu.AddSubMenu(_propsMenu);

                // 4. Save Appearance to customizedpeds.ini Button
                NativeItem saveAppearanceButton = new NativeItem("~g~Save Appearance~s~", "Saves model, clothing, and props to customizedpeds.ini.");
                saveAppearanceButton.Activated += (sender, e) => SaveCustomizedPed();
                _mainMenu.Add(saveAppearanceButton);

                // 5. Movement Style & Animations Submenu Setup
                SetupMovementAndAnimationMenu();
                _mainMenu.AddSubMenu(_animMenu);

                // 6. Interactive Spawner Slider: Choose fully customized & animated model, press Enter to spawn
                _spawnProfileListItem = new NativeListItem<string>("Spawn Profile", "Select saved profile. Press Enter to spawn into world.", new string[] { "[Current Ped]" });
                _spawnProfileListItem.ItemChanged += (sender, e) =>
                {
                    if (_isUpdatingSliders) return;
                    string selected = _spawnProfileListItem.SelectedItem;
                    if (!string.IsNullOrWhiteSpace(selected) && selected != "[Current Ped]")
                    {
                        LoadCustomizedPedProfile(selected);
                    }
                };
                _spawnProfileListItem.Activated += (sender, e) =>
                {
                    string selected = _spawnProfileListItem.SelectedItem;
                    SpawnCustomizedPed(selected);
                };
                _mainMenu.Add(_spawnProfileListItem);

                // Direct Spawn Button
                _spawnButton = new NativeItem("~g~Spawn into World~s~", "Spawns chosen customized & animated model into the world.");
                _spawnButton.Activated += (sender, e) =>
                {
                    string selected = _spawnProfileListItem.SelectedItem;
                    SpawnCustomizedPed(selected);
                };
                _mainMenu.Add(_spawnButton);

                // 7. Submenus & Utilities
                _mainMenu.AddSubMenu(_savedPedsMenu);
                _savedPedsMenu.AddSubMenu(_deleteSavedMenu);

                _savedPedsMenu.Opening += (sender, e) => RefreshSavedPedsMenu();
                _deleteSavedMenu.Opening += (sender, e) => RefreshSavedPedsMenu();

                // Randomize Button
                NativeItem randomizeButton = new NativeItem("Randomize Clothing", "Randomizes clothing components and props on preview ped.");
                randomizeButton.Activated += (sender, e) => RandomizeVariations();
                _mainMenu.Add(randomizeButton);

                // Reload pedmodels.txt button
                NativeItem reloadModelsButton = new NativeItem("Reload Models", "Reloads model list from scripts/pedmodels.txt.");
                reloadModelsButton.Activated += (sender, e) =>
                {
                    LoadPedModels();
                    RefreshModelList();
                };
                _mainMenu.Add(reloadModelsButton);

                // Reload Settings button
                _reloadSettingsButton = new NativeItem("Reload Settings", $"Reloads {_configIniPath} (Activation Key: {_activationKey}).");
                _reloadSettingsButton.Activated += (sender, e) =>
                {
                    LoadSettings();
                    _reloadSettingsButton.Description = $"Reloads {_configIniPath} (Activation Key: {_activationKey}).";
                    Notification.Show($"~g~Settings Reloaded~s~\nActivation Key: ~y~{_activationKey}~s~");
                };
                _mainMenu.Add(_reloadSettingsButton);

                // Add to menu pool
                _menuPool.Add(_mainMenu);
                _menuPool.Add(_clothingMenu);
                _menuPool.Add(_propsMenu);
                _menuPool.Add(_animMenu);
                _menuPool.Add(_savedPedsMenu);
                _menuPool.Add(_deleteSavedMenu);

                RefreshSpawnerSlider();

                Tick += OnTick;
                KeyDown += OnKeyDown;
                Aborted += (sender, e) =>
                {
                    ClearPreview();
                };

                Logger.Write("AdvancedPedStudio initialized successfully. Activation key: {0}.", _activationKey);
                GTA.UI.Screen.ShowSubtitle($"~g~Advanced Ped Studio~s~ ready! Press ~y~{_activationKey}~s~ to open studio.", 3500);
            }
            catch (Exception ex)
            {
                Logger.Error("Error during AdvancedPedStudio initialization", ex);
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_configIniPath))
                {
                    string defaultContent = "; ============================================================\r\n" +
                                           "; AdvancedPedStudio - Configuration Settings\r\n" +
                                           "; ============================================================\r\n\r\n" +
                                           "[Settings]\r\n" +
                                           "; Key to open and close the Advanced Ped Studio menu.\r\n" +
                                           "; Default: F11\r\n" +
                                           "; Supported keys include any valid .NET System.Windows.Forms.Keys name:\r\n" +
                                           "; Examples: F11, F10, F9, F8, F7, F6, F5, F3, K, O, J, Insert, PageUp\r\n" +
                                           "ActivationKey = F11\r\n";
                    File.WriteAllText(_configIniPath, defaultContent, Encoding.UTF8);
                }

                var configIni = new SimpleIniFile(_configIniPath);
                string keyVal = configIni.GetValue("Settings", "ActivationKey", "");

                // Backwards compatibility: check customizedpeds.ini if not found in AdvancedPedStudio.ini
                if (string.IsNullOrWhiteSpace(keyVal) && File.Exists(_customizedPedsIniPath))
                {
                    keyVal = _customizedPedsIni.GetValue("Settings", "ActivationKey", "");
                }

                if (!string.IsNullOrWhiteSpace(keyVal) && Enum.TryParse<Keys>(keyVal.Trim(), true, out Keys parsedKey) && parsedKey != Keys.None)
                {
                    _activationKey = parsedKey;
                    Logger.Write("Loaded ActivationKey: {0}", _activationKey);
                }
                else
                {
                    _activationKey = Keys.F11;
                    Logger.Write("Using default ActivationKey: {0}", _activationKey);
                }
            }
            catch (Exception ex)
            {
                _activationKey = Keys.F11;
                Logger.Error("Error loading settings from INI", ex);
            }
        }

        private void LoadPedModels()
        {
            _pedModels.Clear();

            try
            {
                if (!File.Exists(_pedModelsPath))
                {
                    string defaultContent = "s_f_y_stripper_01, s_f_y_stripper_02, s_f_y_stripperlite, mp_f_freemode_01, mp_m_freemode_01, a_f_y_topless_01, a_f_y_beach_01, a_f_y_fitness_01, a_f_y_tourist_01, a_f_y_hippie_01, a_f_y_bevhills_01, a_f_y_clubcust_01, a_f_y_gencaspat_01, a_f_y_smartcaspat_01, a_m_y_beach_01, a_m_y_sunbathe_01, a_m_y_runner_01, a_m_y_clubcust_01, s_m_y_dealer_01, s_m_y_cop_01, ig_trv_stripper_01, ig_trv_stripper_02, csb_stripper_01, csb_stripper_02, u_f_y_poppymich_02, u_f_y_danceburl_01, u_f_y_dancelthr_01";
                    File.WriteAllText(_pedModelsPath, defaultContent);
                }

                string rawText = File.ReadAllText(_pedModelsPath);
                string[] entries = rawText.Split(new[] { ',', '\n', '\r', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string entry in entries)
                {
                    string trimmed = entry.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !_pedModels.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                    {
                        _pedModels.Add(trimmed);
                    }
                }

                Logger.Write("Loaded {0} models from pedmodels.txt", _pedModels.Count);
            }
            catch (Exception ex)
            {
                Logger.Error("Error reading pedmodels.txt", ex);
            }

            if (_pedModels.Count == 0)
            {
                _pedModels.Add("s_f_y_stripper_01");
            }
        }

        private void RefreshModelList()
        {
            string previousSelected = _modelListItem.SelectedItem;
            _modelListItem.Items.Clear();

            foreach (var model in _pedModels)
            {
                _modelListItem.Add(model);
            }

            int idx = _modelListItem.Items.IndexOf(previousSelected);
            _modelListItem.SelectedIndex = idx >= 0 ? idx : 0;

            if (_menuPool.AreAnyVisible)
            {
                UpdatePreview();
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            _menuPool.Process();

            bool anyMenuOpen = _menuPool.AreAnyVisible;

            if (anyMenuOpen && _previewPed != null && _previewPed.Exists())
            {
                _previewPed.Heading += PreviewRotationSpeed;
                Game.DisableControlThisFrame(Control.Phone);

                // Anti-fall and anti-drift safeguard for preview ped
                if (_previewAnchorPos != Vector3.Zero)
                {
                    if (!_previewPed.IsCollisionEnabled)
                    {
                        _previewPed.IsCollisionEnabled = true;
                        Function.Call(Hash.SET_ENTITY_COLLISION, _previewPed.Handle, true, true);
                    }

                    Vector3 currentPos = _previewPed.Position;
                    bool fellThroughGround = currentPos.Z < (_previewAnchorPos.Z - 0.15f);
                    float dx = currentPos.X - _previewAnchorPos.X;
                    float dy = currentPos.Y - _previewAnchorPos.Y;
                    bool driftedAway = (dx * dx + dy * dy) > (0.35f * 0.35f);

                    if (fellThroughGround || driftedAway)
                    {
                        Function.Call(Hash.SET_PED_COORDS_KEEP_VEHICLE, _previewPed.Handle, _previewAnchorPos.X, _previewAnchorPos.Y, _previewAnchorPos.Z);
                        _previewPed.Velocity = Vector3.Zero;
                    }
                }
            }
            else if (!anyMenuOpen && _previewPed != null)
            {
                ClearPreview();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == _activationKey)
            {
                if (_menuPool.AreAnyVisible)
                {
                    CloseAllMenus();
                }
                else
                {
                    RefreshSpawnerSlider();
                    _mainMenu.Visible = true;
                    UpdatePreview();
                }
            }
        }

        private void CloseAllMenus()
        {
            _mainMenu.Visible = false;
            _clothingMenu.Visible = false;
            _propsMenu.Visible = false;
            _animMenu.Visible = false;
            _savedPedsMenu.Visible = false;
            _deleteSavedMenu.Visible = false;
            ClearPreview();
        }

        private Ped GetCustomizationTargetPed()
        {
            if (_previewPed != null && _previewPed.Exists() && _previewPed.IsAlive)
            {
                return _previewPed;
            }
            return Game.Player.Character;
        }

        private Vector3 GetAccurateGroundPosition(Vector3 targetPos)
        {
            Ped player = Game.Player.Character;
            Vector3 playerPos = player.Position;
            float playerZ = playerPos.Z;

            // Request collision mesh loading at the target coordinate
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, targetPos.X, targetPos.Y, playerZ);

            OutputArgument outZ = new OutputArgument();
            // Raycast downward from eye level (playerPos.Z + 1.5f) to detect immediate ground/floor
            bool found = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, targetPos.X, targetPos.Y, playerZ + 1.5f, outZ, false);
            if (found)
            {
                float gz = outZ.GetResult<float>();
                // Only accept if ground is reasonably close to player's elevation (+- 3.5m)
                if (Math.Abs(gz - playerZ) < 3.5f)
                {
                    targetPos.Z = gz;
                    return targetPos;
                }
            }

            // Fallback 1: Calculate ground from player's feet elevation
            float heightAboveGround = player.HeightAboveGround;
            if (heightAboveGround > 0.05f && heightAboveGround < 4.0f)
            {
                targetPos.Z = playerZ - heightAboveGround;
                return targetPos;
            }

            // Fallback 2: Standard human center-of-mass to floor offset (~0.95m)
            targetPos.Z = playerZ - 0.95f;
            return targetPos;
        }

        private Vector3 GetSpawnPosition()
        {
            Ped player = Game.Player.Character;
            Vector3 targetPos = player.Position + player.ForwardVector * SpawnDistanceMeters;
            return GetAccurateGroundPosition(targetPos);
        }

        private void ApplyMovementStyle(Ped ped, string clipSet)
        {
            if (ped == null || !ped.Exists() || !ped.IsAlive) return;

            if (string.IsNullOrEmpty(clipSet) || clipSet.Equals("(Default)", StringComparison.OrdinalIgnoreCase))
            {
                Function.Call(Hash.RESET_PED_MOVEMENT_CLIPSET, ped.Handle, 0.25f);
                return;
            }

            if (!Function.Call<bool>(Hash.HAS_CLIP_SET_LOADED, clipSet))
            {
                Function.Call(Hash.REQUEST_CLIP_SET, clipSet);
                int start = Game.GameTime;
                while (!Function.Call<bool>(Hash.HAS_CLIP_SET_LOADED, clipSet) && (Game.GameTime - start) < 1000)
                {
                    Script.Wait(0);
                }
            }

            if (Function.Call<bool>(Hash.HAS_CLIP_SET_LOADED, clipSet))
            {
                Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, ped.Handle, clipSet, 0.25f);
                Function.Call(Hash.REMOVE_CLIP_SET, clipSet);
            }
        }

        private void SetupMovementAndAnimationMenu()
        {
            _animMenu.Clear();

            // Movement Style Slider
            _movementStyleListItem = new NativeListItem<string>("Walk Style", "Select movement personality style.", MovementStyleList);
            _movementStyleListItem.ItemChanged += (sender, e) =>
            {
                _currentMovementStyle = _movementStyleListItem.SelectedItem ?? "(Default)";
                Ped target = GetCustomizationTargetPed();
                if (target != null && target.Exists())
                {
                    ApplyMovementStyle(target, _currentMovementStyle);
                }
            };
            _animMenu.Add(_movementStyleListItem);

            // Animation Category Selector
            _animCategoryListItem = new NativeListItem<string>("Category", "Filter animations by theme or scenario.", AnimCategories);
            _animCategoryListItem.ItemChanged += (sender, e) => OnAnimCategoryChanged();
            _animMenu.Add(_animCategoryListItem);

            // Animation / Scenario Selector
            _animItem = new NativeListItem<string>("Animation", "Select animation or scenario to audition.", new string[] { "(Stand Still)" });
            _animItem.ItemChanged += (sender, e) => ApplySelectedAnimation();
            _animMenu.Add(_animItem);

            // Custom Dict & Clip fields (visible when category is Custom)
            _customDictItem = new NativeItem("Custom Dict", "Click to enter custom animation dictionary name.");
            _customDictItem.Enabled = false;
            _customDictItem.Activated += (sender, e) =>
            {
                string res = Game.GetUserInput(WindowTitle.EnterMessage60, _customAnimDict, 80);
                if (!string.IsNullOrWhiteSpace(res))
                {
                    _customAnimDict = res.Trim();
                    ApplySelectedAnimation();
                }
            };
            _animMenu.Add(_customDictItem);

            _customClipItem = new NativeItem("Custom Clip", "Click to enter custom animation clip name.");
            _customClipItem.Enabled = false;
            _customClipItem.Activated += (sender, e) =>
            {
                string res = Game.GetUserInput(WindowTitle.EnterMessage60, _customAnimClip, 80);
                if (!string.IsNullOrWhiteSpace(res))
                {
                    _customAnimClip = res.Trim();
                    ApplySelectedAnimation();
                }
            };
            _animMenu.Add(_customClipItem);

            // Play Animation button
            NativeItem playAnimButton = new NativeItem("Play Animation", "Re-triggers playback of selected animation or scenario.");
            playAnimButton.Activated += (sender, e) => ApplySelectedAnimation();
            _animMenu.Add(playAnimButton);

            // Stop Animation button
            NativeItem stopAnimButton = new NativeItem("Stop Animation", "Stops current animation and returns ped to standing idle.");
            stopAnimButton.Activated += (sender, e) =>
            {
                _currentAnimType = "None";
                _currentAnimDict = "";
                _currentAnimClip = "";
                _currentScenario = "";
                StopPreviewAnimation();
            };
            _animMenu.Add(stopAnimButton);

            // Save Movement & Animation button
            NativeItem saveMovementAnimButton = new NativeItem("~g~Save Movement & Animation~s~", "Saves walk style and animation to customizedpeds.ini.");
            saveMovementAnimButton.Activated += (sender, e) => SaveMovementAndAnimation();
            _animMenu.Add(saveMovementAnimButton);
        }

        private void OnAnimCategoryChanged()
        {
            if (_animCategoryListItem == null || _animItem == null) return;

            string cat = _animCategoryListItem.SelectedItem ?? "(None)";
            _animItem.Items.Clear();

            if (cat == "(None)")
            {
                _animItem.Add("(Stand Still)");
                _animItem.SelectedIndex = 0;
                if (_customDictItem != null) _customDictItem.Enabled = false;
                if (_customClipItem != null) _customClipItem.Enabled = false;
                _currentAnimType = "None";
                _currentAnimDict = "";
                _currentAnimClip = "";
                _currentScenario = "";
                StopPreviewAnimation();
                return;
            }

            if (cat == "Scenarios")
            {
                if (_customDictItem != null) _customDictItem.Enabled = false;
                if (_customClipItem != null) _customClipItem.Enabled = false;
                foreach (var sc in CuratedScenarios)
                {
                    _animItem.Add(sc.Name);
                }
                if (_animItem.Items.Count > 0)
                {
                    _animItem.SelectedIndex = 0;
                    ApplySelectedAnimation();
                }
                return;
            }

            if (cat == "Custom")
            {
                if (_customDictItem != null) _customDictItem.Enabled = true;
                if (_customClipItem != null) _customClipItem.Enabled = true;
                _animItem.Add("(Custom Animation)");
                _animItem.SelectedIndex = 0;
                _currentAnimType = "Animation";
                _currentAnimDict = _customAnimDict;
                _currentAnimClip = _customAnimClip;
                _currentScenario = "";
                PlayAnimationOnPreview(_customAnimDict, _customAnimClip);
                return;
            }

            if (_customDictItem != null) _customDictItem.Enabled = false;
            if (_customClipItem != null) _customClipItem.Enabled = false;

            if (AnimationsByCategory.TryGetValue(cat, out var matching))
            {
                foreach (var a in matching)
                {
                    _animItem.Add(a.Name);
                }
            }

            if (_animItem.Items.Count > 0)
            {
                _animItem.SelectedIndex = 0;
                ApplySelectedAnimation();
            }
        }

        private void ApplySelectedAnimation()
        {
            if (_animCategoryListItem == null || _animItem == null) return;

            string cat = _animCategoryListItem.SelectedItem ?? "(None)";
            if (cat == "(None)")
            {
                _currentAnimType = "None";
                _currentAnimDict = "";
                _currentAnimClip = "";
                _currentScenario = "";
                StopPreviewAnimation();
                return;
            }

            if (cat == "Scenarios")
            {
                string scName = _animItem.SelectedItem;
                if (!string.IsNullOrEmpty(scName) && ScenariosByName.TryGetValue(scName, out var entry))
                {
                    _currentAnimType = "Scenario";
                    _currentScenario = entry.Scenario;
                    _currentAnimDict = "";
                    _currentAnimClip = "";
                    PlayScenarioOnPreview(entry.Scenario);
                }
                return;
            }

            if (cat == "Custom")
            {
                _currentAnimType = "Animation";
                _currentAnimDict = _customAnimDict;
                _currentAnimClip = _customAnimClip;
                _currentScenario = "";
                PlayAnimationOnPreview(_customAnimDict, _customAnimClip);
                return;
            }

            string animName = _animItem.SelectedItem;
            if (!string.IsNullOrEmpty(animName) && AnimationsByName.TryGetValue(animName, out var animEntry))
            {
                _currentAnimType = "Animation";
                _currentAnimDict = animEntry.Dictionary;
                _currentAnimClip = animEntry.Clip;
                _currentScenario = "";
                PlayAnimationOnPreview(animEntry.Dictionary, animEntry.Clip);
            }
        }

        private void PlayAnimationOnPreview(string animDict, string animClip)
        {
            Ped target = GetCustomizationTargetPed();
            if (target == null || !target.Exists() || !target.IsAlive) return;

            if (string.IsNullOrWhiteSpace(animDict) || string.IsNullOrWhiteSpace(animClip)) return;

            if (!Function.Call<bool>(Hash.DOES_ANIM_DICT_EXIST, animDict))
            {
                Notification.Show($"~r~Anim dict not found:~w~ {animDict}");
                return;
            }

            Function.Call(Hash.REQUEST_ANIM_DICT, animDict);
            int startTime = Game.GameTime;
            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict) && (Game.GameTime - startTime) < 1500)
            {
                Script.Wait(0);
            }

            if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict))
            {
                target.IsCollisionEnabled = true;
                Function.Call(Hash.SET_ENTITY_COLLISION, target.Handle, true, true);
                target.IsPositionFrozen = true; // Anchored in place so model never sinks or drifts while animating
                Function.Call(Hash.CLEAR_PED_TASKS, target.Handle);
                Function.Call(Hash.TASK_PLAY_ANIM, target.Handle, animDict, animClip, 8.0f, -8.0f, -1, 1, 0.0f, false, false, false);
                Function.Call(Hash.REMOVE_ANIM_DICT, animDict);
            }
            else
            {
                Notification.Show($"~r~Failed to load anim dict:~w~ {animDict}");
            }
        }

        private void PlayScenarioOnPreview(string scenario)
        {
            Ped target = GetCustomizationTargetPed();
            if (target == null || !target.Exists() || !target.IsAlive) return;

            if (string.IsNullOrWhiteSpace(scenario)) return;

            target.IsCollisionEnabled = true;
            Function.Call(Hash.SET_ENTITY_COLLISION, target.Handle, true, true);
            target.IsPositionFrozen = false;
            Function.Call(Hash.CLEAR_PED_TASKS, target.Handle);
            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, target.Handle, scenario, 0, true);
        }

        private void StopPreviewAnimation()
        {
            Ped target = GetCustomizationTargetPed();
            if (target != null && target.Exists() && target.IsAlive)
            {
                Function.Call(Hash.CLEAR_PED_TASKS, target.Handle);
                target.Task.StandStill(-1);
                target.IsCollisionEnabled = true;
                Function.Call(Hash.SET_ENTITY_COLLISION, target.Handle, true, true);
                target.IsPositionFrozen = true;
                if (_previewAnchorPos != Vector3.Zero)
                {
                    Function.Call(Hash.SET_PED_COORDS_KEEP_VEHICLE, target.Handle, _previewAnchorPos.X, _previewAnchorPos.Y, _previewAnchorPos.Z);
                    target.Velocity = Vector3.Zero;
                }
            }
        }

        private void ReapplyCurrentAnimationOnPreview()
        {
            if (_previewPed == null || !_previewPed.Exists() || !_previewPed.IsAlive) return;

            if (_currentAnimType == "Animation" && !string.IsNullOrEmpty(_currentAnimDict) && !string.IsNullOrEmpty(_currentAnimClip))
            {
                PlayAnimationOnPreview(_currentAnimDict, _currentAnimClip);
            }
            else if (_currentAnimType == "Scenario" && !string.IsNullOrEmpty(_currentScenario))
            {
                PlayScenarioOnPreview(_currentScenario);
            }
            else
            {
                StopPreviewAnimation();
            }
        }

        private void SyncAnimationUiFromCurrent()
        {
            if (_animCategoryListItem == null || _animItem == null) return;

            _isUpdatingSliders = true;
            try
            {
                if (_currentAnimType == "Scenario" && !string.IsNullOrEmpty(_currentScenario))
                {
                    int catIdx = _animCategoryListItem.Items.IndexOf("Scenarios");
                    if (catIdx >= 0)
                    {
                        _animCategoryListItem.SelectedIndex = catIdx;
                        OnAnimCategoryChanged();
                        var scEntry = CuratedScenarios.FirstOrDefault(s => s.Scenario.Equals(_currentScenario, StringComparison.OrdinalIgnoreCase));
                        if (scEntry != null)
                        {
                            int sIdx = _animItem.Items.IndexOf(scEntry.Name);
                            if (sIdx >= 0) _animItem.SelectedIndex = sIdx;
                        }
                    }
                }
                else if (_currentAnimType == "Animation" && !string.IsNullOrEmpty(_currentAnimDict))
                {
                    var animEntry = CuratedAnimations.FirstOrDefault(a => a.Dictionary.Equals(_currentAnimDict, StringComparison.OrdinalIgnoreCase) && a.Clip.Equals(_currentAnimClip, StringComparison.OrdinalIgnoreCase));
                    if (animEntry != null)
                    {
                        int catIdx = _animCategoryListItem.Items.IndexOf(animEntry.Category);
                        if (catIdx >= 0)
                        {
                            _animCategoryListItem.SelectedIndex = catIdx;
                            OnAnimCategoryChanged();
                            int aIdx = _animItem.Items.IndexOf(animEntry.Name);
                            if (aIdx >= 0) _animItem.SelectedIndex = aIdx;
                        }
                    }
                    else
                    {
                        int catIdx = _animCategoryListItem.Items.IndexOf("Custom");
                        if (catIdx >= 0)
                        {
                            _animCategoryListItem.SelectedIndex = catIdx;
                            _customAnimDict = _currentAnimDict;
                            _customAnimClip = _currentAnimClip;
                            OnAnimCategoryChanged();
                        }
                    }
                }
                else
                {
                    int catIdx = _animCategoryListItem.Items.IndexOf("(None)");
                    if (catIdx >= 0)
                    {
                        _animCategoryListItem.SelectedIndex = catIdx;
                        OnAnimCategoryChanged();
                    }
                }
            }
            finally
            {
                _isUpdatingSliders = false;
            }
        }

        private void UpdatePreview()
        {
            ClearPreview();

            if (_modelListItem.Items.Count == 0) return;

            string modelName = _modelListItem.SelectedItem;
            if (string.IsNullOrWhiteSpace(modelName)) return;

            Model model = new Model(modelName);
            if (!model.IsValid)
            {
                Notification.Show($"~r~Invalid ped model name:~w~ {modelName}");
                return;
            }

            model.Request(1500);
            int startTime = Game.GameTime;
            while (!model.IsLoaded && (Game.GameTime - startTime) < 1500)
            {
                Script.Wait(0);
            }

            if (!model.IsLoaded)
            {
                Notification.Show($"~r~Failed to load model:~w~ {modelName}");
                model.MarkAsNoLongerNeeded();
                return;
            }

            Vector3 spawnPos = GetSpawnPosition();
            _previewAnchorPos = spawnPos;
            _previewPed = World.CreatePed(model, spawnPos);
            model.MarkAsNoLongerNeeded();

            if (_previewPed != null && _previewPed.Exists())
            {
                Ped player = Game.Player.Character;
                _previewPed.Heading = (player.Heading + 180.0f) % 360.0f;
                _previewPed.IsInvincible = true;
                _previewPed.IsCollisionEnabled = true;
                Function.Call(Hash.SET_ENTITY_COLLISION, _previewPed.Handle, true, true);
                _previewPed.IsPositionFrozen = true;
                _previewPed.Task.StandStill(-1);
                _previewPed.BlockPermanentEvents = true;

                // Snap to ground coordinates and disable collision with player character
                Function.Call(Hash.SET_PED_COORDS_KEEP_VEHICLE, _previewPed.Handle, spawnPos.X, spawnPos.Y, spawnPos.Z);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _previewPed.Handle, player.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.Handle, _previewPed.Handle, false);

                ApplyMovementStyle(_previewPed, _currentMovementStyle);
                ReapplyCurrentAnimationOnPreview();
                RefreshClothingSliders();
            }
        }

        private void ClearPreview()
        {
            if (_previewPed != null)
            {
                if (_previewPed.Exists())
                {
                    _previewPed.Delete();
                }
                _previewPed = null;
            }
            _previewAnchorPos = Vector3.Zero;
        }

        private void SetupClothingSlider(string componentName, int componentId)
        {
            Ped targetPed = GetCustomizationTargetPed();

            int currentDrawable = (targetPed != null && targetPed.Exists())
                ? Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed.Handle, componentId)
                : 0;
            int currentTexture = (targetPed != null && targetPed.Exists())
                ? Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed.Handle, componentId)
                : 0;

            int maxDrawables = (targetPed != null && targetPed.Exists())
                ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, targetPed.Handle, componentId))
                : 1;

            List<int> drawableItems = new List<int>(maxDrawables);
            for (int i = 0; i < maxDrawables; i++) drawableItems.Add(i);

            NativeListItem<int> drawableSlider = new NativeListItem<int>(componentName, $"Select {componentName.ToLower()} model / variation.", drawableItems.ToArray());
            drawableSlider.SelectedIndex = (currentDrawable >= 0 && currentDrawable < drawableItems.Count) ? currentDrawable : 0;

            int maxTextures = (targetPed != null && targetPed.Exists())
                ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, targetPed.Handle, componentId, currentDrawable))
                : 1;

            List<int> textureItems = new List<int>(maxTextures);
            for (int i = 0; i < maxTextures; i++) textureItems.Add(i);

            NativeListItem<int> textureSlider = new NativeListItem<int>("Texture", $"Select {componentName.ToLower()} color / texture.", textureItems.ToArray());
            textureSlider.SelectedIndex = (currentTexture >= 0 && currentTexture < textureItems.Count) ? currentTexture : 0;

            drawableSlider.ItemChanged += (sender, e) =>
            {
                if (_isUpdatingSliders) return;

                Ped currentPed = GetCustomizationTargetPed();
                if (currentPed == null || !currentPed.Exists()) return;

                int selectedDrawable = drawableSlider.SelectedItem;
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed.Handle, componentId, selectedDrawable, 0, 0);

                // Preserve Torso/Arms (3) when changing Tops (11) or Undershirts (8)
                if ((componentId == 11 || componentId == 8) && _drawableSliders.TryGetValue(3, out var torsoSlider))
                {
                    int torsoDrawable = torsoSlider.SelectedItem;
                    int torsoTexture = _textureSliders.TryGetValue(3, out var torsoTexSlider) ? torsoTexSlider.SelectedItem : 0;
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed.Handle, 3, torsoDrawable, torsoTexture, 0);
                }

                // Preserve Legs/Shoes pairing
                if (componentId == 11 && _drawableSliders.TryGetValue(4, out var legsSlider))
                {
                    int legsDrawable = legsSlider.SelectedItem;
                    int legsTexture = _textureSliders.TryGetValue(4, out var legsTexSlider) ? legsTexSlider.SelectedItem : 0;
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed.Handle, 4, legsDrawable, legsTexture, 0);
                }

                int newMaxTextures = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, currentPed.Handle, componentId, selectedDrawable));
                if (textureSlider.Items.Count != newMaxTextures)
                {
                    textureSlider.Items.Clear();
                    for (int i = 0; i < newMaxTextures; i++) textureSlider.Add(i);
                }
                textureSlider.SelectedIndex = 0;
            };

            textureSlider.ItemChanged += (sender, e) =>
            {
                if (_isUpdatingSliders) return;

                Ped currentPed = GetCustomizationTargetPed();
                if (currentPed == null || !currentPed.Exists()) return;

                if (textureSlider.Items.Count > 0)
                {
                    int selectedDrawable = drawableSlider.SelectedItem;
                    int selectedTexture = textureSlider.SelectedItem;
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed.Handle, componentId, selectedDrawable, selectedTexture, 0);

                    // Preserve Torso/Arms (3) when changing Tops (11) or Undershirts (8)
                    if ((componentId == 11 || componentId == 8) && _drawableSliders.TryGetValue(3, out var torsoSlider))
                    {
                        int torsoDrawable = torsoSlider.SelectedItem;
                        int torsoTexture = _textureSliders.TryGetValue(3, out var torsoTexSlider) ? torsoTexSlider.SelectedItem : 0;
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed.Handle, 3, torsoDrawable, torsoTexture, 0);
                    }
                }
            };

            _drawableSliders[componentId] = drawableSlider;
            _textureSliders[componentId] = textureSlider;

            _clothingMenu.Add(drawableSlider);
            _clothingMenu.Add(textureSlider);
        }

        private void SetupPropSlider(string propName, int propId)
        {
            Ped targetPed = GetCustomizationTargetPed();

            int currentProp = (targetPed != null && targetPed.Exists())
                ? Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed.Handle, propId)
                : -1;
            int currentTexture = (targetPed != null && targetPed.Exists() && currentProp >= 0)
                ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed.Handle, propId)
                : 0;

            int maxProps = (targetPed != null && targetPed.Exists())
                ? Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS, targetPed.Handle, propId)
                : 0;

            List<int> propItems = new List<int>(maxProps + 1) { -1 };
            for (int i = 0; i < maxProps; i++) propItems.Add(i);

            NativeListItem<int> propSlider = new NativeListItem<int>(propName, $"Change {propName.ToLower()} model (-1 = None).", propItems.ToArray());
            int pIdx = currentProp >= 0 ? (currentProp + 1) : 0;
            propSlider.SelectedIndex = (pIdx < propItems.Count) ? pIdx : 0;

            int maxTextures = (targetPed != null && targetPed.Exists() && currentProp >= 0)
                ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, targetPed.Handle, propId, currentProp))
                : 1;

            List<int> textureItems = new List<int>(maxTextures);
            for (int i = 0; i < maxTextures; i++) textureItems.Add(i);

            NativeListItem<int> textureSlider = new NativeListItem<int>("Texture", $"Change {propName.ToLower()} texture / color.", textureItems.ToArray());
            textureSlider.SelectedIndex = (currentTexture >= 0 && currentTexture < textureItems.Count) ? currentTexture : 0;

            propSlider.ItemChanged += (sender, e) =>
            {
                if (_isUpdatingSliders) return;

                Ped currentPed = GetCustomizationTargetPed();
                if (currentPed == null || !currentPed.Exists()) return;

                int selectedProp = propSlider.SelectedItem;
                if (selectedProp == -1)
                {
                    Function.Call(Hash.CLEAR_PED_PROP, currentPed.Handle, propId);
                    if (textureSlider.Items.Count != 1)
                    {
                        textureSlider.Items.Clear();
                        textureSlider.Add(0);
                    }
                    textureSlider.SelectedIndex = 0;
                }
                else
                {
                    Function.Call(Hash.SET_PED_PROP_INDEX, currentPed.Handle, propId, selectedProp, 0, true);
                    int newMaxTextures = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, currentPed.Handle, propId, selectedProp));
                    if (textureSlider.Items.Count != newMaxTextures)
                    {
                        textureSlider.Items.Clear();
                        for (int i = 0; i < newMaxTextures; i++) textureSlider.Add(i);
                    }
                    textureSlider.SelectedIndex = 0;
                }
            };

            textureSlider.ItemChanged += (sender, e) =>
            {
                if (_isUpdatingSliders) return;

                Ped currentPed = GetCustomizationTargetPed();
                if (currentPed == null || !currentPed.Exists()) return;

                int selectedProp = propSlider.SelectedItem;
                if (selectedProp >= 0 && textureSlider.Items.Count > 0)
                {
                    int selectedTexture = textureSlider.SelectedItem;
                    Function.Call(Hash.SET_PED_PROP_INDEX, currentPed.Handle, propId, selectedProp, selectedTexture, true);
                }
            };

            _propSliders[propId] = propSlider;
            _propTextureSliders[propId] = textureSlider;

            _propsMenu.Add(propSlider);
            _propsMenu.Add(textureSlider);
        }

        private void RefreshClothingSliders()
        {
            Ped targetPed = GetCustomizationTargetPed();
            if (targetPed == null || !targetPed.Exists()) return;

            _isUpdatingSliders = true;
            try
            {
                foreach (var kvp in _drawableSliders)
                {
                    int compId = kvp.Key;
                    var dSlider = kvp.Value;
                    _textureSliders.TryGetValue(compId, out var tSlider);

                    int maxDrawables = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, targetPed.Handle, compId));
                    int actualDrawable = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed.Handle, compId);

                    if (dSlider.Items.Count != maxDrawables)
                    {
                        dSlider.Items.Clear();
                        for (int i = 0; i < maxDrawables; i++) dSlider.Add(i);
                    }

                    dSlider.SelectedIndex = (actualDrawable >= 0 && actualDrawable < dSlider.Items.Count) ? actualDrawable : 0;

                    if (tSlider != null)
                    {
                        int maxTextures = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, targetPed.Handle, compId, actualDrawable));
                        int actualTexture = Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed.Handle, compId);

                        if (tSlider.Items.Count != maxTextures)
                        {
                            tSlider.Items.Clear();
                            for (int i = 0; i < maxTextures; i++) tSlider.Add(i);
                        }

                        tSlider.SelectedIndex = (actualTexture >= 0 && actualTexture < tSlider.Items.Count) ? actualTexture : 0;
                    }
                }

                foreach (var kvp in _propSliders)
                {
                    int propId = kvp.Key;
                    var pSlider = kvp.Value;
                    _propTextureSliders.TryGetValue(propId, out var tSlider);

                    int maxProps = Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS, targetPed.Handle, propId);
                    int actualProp = Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed.Handle, propId);
                    int expectedPropCount = maxProps + 1;

                    if (pSlider.Items.Count != expectedPropCount)
                    {
                        pSlider.Items.Clear();
                        pSlider.Add(-1);
                        for (int i = 0; i < maxProps; i++) pSlider.Add(i);
                    }

                    int pIdx = actualProp >= 0 ? (actualProp + 1) : 0;
                    pSlider.SelectedIndex = (pIdx < pSlider.Items.Count) ? pIdx : 0;

                    if (tSlider != null)
                    {
                        int maxTextures = (actualProp >= 0)
                            ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, targetPed.Handle, propId, actualProp))
                            : 1;
                        int actualTexture = (actualProp >= 0)
                            ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed.Handle, propId)
                            : 0;

                        if (tSlider.Items.Count != maxTextures)
                        {
                            tSlider.Items.Clear();
                            for (int i = 0; i < maxTextures; i++) tSlider.Add(i);
                        }

                        tSlider.SelectedIndex = (actualTexture >= 0 && actualTexture < tSlider.Items.Count) ? actualTexture : 0;
                    }
                }
            }
            finally
            {
                _isUpdatingSliders = false;
            }
        }

        private void RandomizeVariations()
        {
            Ped targetPed = GetCustomizationTargetPed();
            if (targetPed == null || !targetPed.Exists()) return;

            try
            {
                Function.Call(Hash.SET_PED_RANDOM_COMPONENT_VARIATION, targetPed.Handle, 0);
                Function.Call(Hash.SET_PED_RANDOM_PROPS, targetPed.Handle);
                RefreshClothingSliders();
            }
            catch (Exception ex)
            {
                Logger.Error("Error randomizing variations", ex);
            }
        }

        // ============================================================
        // Direct INI Persistence: customizedpeds.ini
        // ============================================================
        private void SaveCustomizedPed()
        {
            Ped targetPed = GetCustomizationTargetPed();
            if (targetPed == null || !targetPed.Exists())
            {
                Notification.Show("~r~No preview ped active to save!~w~");
                return;
            }

            string modelName = _modelListItem.SelectedItem;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = "UnknownPed";
            }

            try
            {
                _customizedPedsIni.Load();
                List<string> existingSections = _customizedPedsIni.GetSectionNames();

                // Find all existing suffixes used for this model name
                HashSet<string> usedSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string sec in existingSections)
                {
                    if (sec.StartsWith(modelName + "_", StringComparison.OrdinalIgnoreCase))
                    {
                        string suff = sec.Substring(modelName.Length + 1).Trim();
                        if (!string.IsNullOrEmpty(suff))
                        {
                            usedSuffixes.Add(suff);
                        }
                    }
                    else if (sec.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                    {
                        usedSuffixes.Add("a");
                    }
                }

                // Determine the next available suffix letter ('a' through 'z')
                string suffix = "a";
                for (int i = 0; i < 26; i++)
                {
                    char candidate = (char)('a' + i);
                    if (!usedSuffixes.Contains(candidate.ToString()))
                    {
                        suffix = candidate.ToString();
                        break;
                    }
                    if (i == 25)
                    {
                        suffix = (usedSuffixes.Count + 1).ToString();
                    }
                }

                string defaultSuggestion = $"{modelName}_{suffix}";

                // Prompt user for friendly name
                string input = Game.GetUserInput(WindowTitle.EnterMessage60, defaultSuggestion, 60);
                if (string.IsNullOrWhiteSpace(input))
                {
                    return;
                }

                string friendlyName = input.Trim();

                // Sanitize section name for INI compatibility
                string sectionName = friendlyName.Replace("[", "").Replace("]", "").Replace("=", "").Replace(";", "").Replace("#", "").Trim();
                if (string.IsNullOrWhiteSpace(sectionName))
                {
                    sectionName = defaultSuggestion;
                }

                _customizedPedsIni.SetValue(sectionName, "FriendlyName", friendlyName);
                _customizedPedsIni.SetValue(sectionName, "Model", modelName);
                _customizedPedsIni.SetValue(sectionName, "Option", suffix);
                _customizedPedsIni.SetValue(sectionName, "MovementStyle", _currentMovementStyle);
                _customizedPedsIni.SetValue(sectionName, "AnimationType", _currentAnimType ?? "None");
                _customizedPedsIni.SetValue(sectionName, "AnimDict", _currentAnimDict ?? "");
                _customizedPedsIni.SetValue(sectionName, "AnimClip", _currentAnimClip ?? "");
                _customizedPedsIni.SetValue(sectionName, "Scenario", _currentScenario ?? "");
                _customizedPedsIni.SetValue(sectionName, "SavedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                // Save Components (0-11) directly from sliders & ped state
                foreach (int compId in _componentIds)
                {
                    int drawable = _drawableSliders.TryGetValue(compId, out var dSlider) && dSlider.Items.Count > 0
                        ? dSlider.SelectedItem
                        : Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed.Handle, compId);

                    int texture = _textureSliders.TryGetValue(compId, out var tSlider) && tSlider.Items.Count > 0
                        ? tSlider.SelectedItem
                        : Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed.Handle, compId);

                    _customizedPedsIni.SetValue(sectionName, $"Component_{compId}", $"{drawable},{texture}");
                    _customizedPedsIni.SetValue(sectionName, $"Component_{compId}_Drawable", drawable);
                    _customizedPedsIni.SetValue(sectionName, $"Component_{compId}_Texture", texture);
                }

                // Save Props (0, 1, 2, 6, 7) directly from sliders & ped state
                foreach (int propId in _propIds)
                {
                    int propIndex = _propSliders.TryGetValue(propId, out var pSlider) && pSlider.Items.Count > 0
                        ? pSlider.SelectedItem
                        : Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed.Handle, propId);

                    int propTexture = _propTextureSliders.TryGetValue(propId, out var ptSlider) && ptSlider.Items.Count > 0
                        ? ptSlider.SelectedItem
                        : (propIndex >= 0 ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed.Handle, propId) : 0);

                    _customizedPedsIni.SetValue(sectionName, $"Prop_{propId}", $"{propIndex},{propTexture}");
                    _customizedPedsIni.SetValue(sectionName, $"Prop_{propId}_Index", propIndex);
                    _customizedPedsIni.SetValue(sectionName, $"Prop_{propId}_Texture", propTexture);
                }

                bool saved = _customizedPedsIni.Save();

                if (saved && File.Exists(_customizedPedsIniPath))
                {
                    _lastSavedSection = sectionName;
                    RefreshSpawnerSlider();
                    RefreshSavedPedsMenu();
                    Notification.Show($"~g~Saved customization [{friendlyName}] to INI!~s~");
                    Logger.Write("Successfully saved customized ped [{0}] (Friendly: {1}, Model: {2}) to {3}", sectionName, friendlyName, modelName, _customizedPedsIniPath);
                }
                else
                {
                    Notification.Show("~r~Failed to write to customizedpeds.ini. Check file permissions.~w~");
                    Logger.Error($"Save() returned false for {_customizedPedsIniPath}");
                }
            }
            catch (Exception ex)
            {
                Notification.Show("~r~Error saving ped customization. See log.~w~");
                Logger.Error("Error saving customized ped", ex);
            }
        }

        private void SaveMovementAndAnimation()
        {
            Ped targetPed = GetCustomizationTargetPed();
            if (targetPed == null || !targetPed.Exists())
            {
                Notification.Show("~r~No preview ped active to save!~w~");
                return;
            }

            try
            {
                _customizedPedsIni.Load();
                string sectionName = _lastSavedSection;

                // If no section has been saved yet in this session or current section does not exist in INI:
                if (string.IsNullOrWhiteSpace(sectionName) || !_customizedPedsIni.GetSectionNames().Contains(sectionName, StringComparer.OrdinalIgnoreCase))
                {
                    string modelName = _modelListItem.SelectedItem ?? "UnknownPed";
                    string defaultSuggestion = $"{modelName}_a";

                    string input = Game.GetUserInput(WindowTitle.EnterMessage60, defaultSuggestion, 60);
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        return;
                    }

                    sectionName = input.Replace("[", "").Replace("]", "").Replace("=", "").Replace(";", "").Replace("#", "").Trim();
                    if (string.IsNullOrWhiteSpace(sectionName))
                    {
                        sectionName = defaultSuggestion;
                    }

                    _customizedPedsIni.SetValue(sectionName, "FriendlyName", sectionName);
                    _customizedPedsIni.SetValue(sectionName, "Model", modelName);
                    _customizedPedsIni.SetValue(sectionName, "Option", "a");

                    // Save components and props as well so the new section is complete
                    foreach (int compId in _componentIds)
                    {
                        int drawable = _drawableSliders.TryGetValue(compId, out var dSlider) && dSlider.Items.Count > 0
                            ? dSlider.SelectedItem
                            : Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed.Handle, compId);

                        int texture = _textureSliders.TryGetValue(compId, out var tSlider) && tSlider.Items.Count > 0
                            ? tSlider.SelectedItem
                            : Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed.Handle, compId);

                        _customizedPedsIni.SetValue(sectionName, $"Component_{compId}", $"{drawable},{texture}");
                        _customizedPedsIni.SetValue(sectionName, $"Component_{compId}_Drawable", drawable);
                        _customizedPedsIni.SetValue(sectionName, $"Component_{compId}_Texture", texture);
                    }

                    foreach (int propId in _propIds)
                    {
                        int propIndex = _propSliders.TryGetValue(propId, out var pSlider) && pSlider.Items.Count > 0
                            ? pSlider.SelectedItem
                            : Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed.Handle, propId);

                        int propTexture = _propTextureSliders.TryGetValue(propId, out var ptSlider) && ptSlider.Items.Count > 0
                            ? ptSlider.SelectedItem
                            : (propIndex >= 0 ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed.Handle, propId) : 0);

                        _customizedPedsIni.SetValue(sectionName, $"Prop_{propId}", $"{propIndex},{propTexture}");
                        _customizedPedsIni.SetValue(sectionName, $"Prop_{propId}_Index", propIndex);
                        _customizedPedsIni.SetValue(sectionName, $"Prop_{propId}_Texture", propTexture);
                    }
                }

                _customizedPedsIni.SetValue(sectionName, "MovementStyle", _currentMovementStyle);
                _customizedPedsIni.SetValue(sectionName, "AnimationType", _currentAnimType ?? "None");
                _customizedPedsIni.SetValue(sectionName, "AnimDict", _currentAnimDict ?? "");
                _customizedPedsIni.SetValue(sectionName, "AnimClip", _currentAnimClip ?? "");
                _customizedPedsIni.SetValue(sectionName, "Scenario", _currentScenario ?? "");
                _customizedPedsIni.SetValue(sectionName, "SavedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                bool saved = _customizedPedsIni.Save();
                if (saved)
                {
                    _lastSavedSection = sectionName;
                    RefreshSpawnerSlider();
                    RefreshSavedPedsMenu();
                    Notification.Show($"~g~Saved movement & animation to [{sectionName}]!~s~");
                    Logger.Write("Saved movement style [{0}] and animation [{1} / {2} / {3}] to profile [{4}] in {5}",
                        _currentMovementStyle, _currentAnimType, _currentAnimDict, _currentScenario, sectionName, _customizedPedsIniPath);
                }
                else
                {
                    Notification.Show("~r~Failed to write to customizedpeds.ini.~w~");
                }
            }
            catch (Exception ex)
            {
                Notification.Show("~r~Error saving movement and animation.~w~");
                Logger.Error("Error saving movement and animation", ex);
            }
        }

        private void RefreshSavedPedsMenu()
        {
            _savedPedsMenu.Clear();
            _deleteSavedMenu.Clear();

            _savedPedsMenu.AddSubMenu(_deleteSavedMenu);

            _customizedPedsIni.Load();
            List<string> sections = _customizedPedsIni.GetSectionNames();

            if (sections.Count == 0)
            {
                NativeItem emptyItem = new NativeItem("No Saved Peds Found", "Save a ped using 'Save to customizedpeds.ini'.");
                emptyItem.Enabled = false;
                _savedPedsMenu.Add(emptyItem);

                NativeItem emptyDeleteItem = new NativeItem("No Profiles to Delete", "customizedpeds.ini has no saved profiles.");
                emptyDeleteItem.Enabled = false;
                _deleteSavedMenu.Add(emptyDeleteItem);
                return;
            }

            foreach (string section in sections)
            {
                string friendlyName = _customizedPedsIni.GetValue(section, "FriendlyName", section);
                string model = _customizedPedsIni.GetValue(section, "Model", section);
                string option = _customizedPedsIni.GetValue(section, "Option", "");
                string displayOption = !string.IsNullOrEmpty(option) ? $" (Option {option})" : "";

                string displayTitle = !string.IsNullOrEmpty(friendlyName) ? friendlyName : section;

                // Item for Loading Profile
                NativeItem profileItem = new NativeItem(displayTitle, $"Model: ~y~{model}~w~{displayOption} | Click to preview this saved appearance.");
                profileItem.Activated += (sender, e) => LoadCustomizedPedProfile(section);
                _savedPedsMenu.Add(profileItem);

                // Item for Deleting Profile
                NativeItem deleteItem = new NativeItem(displayTitle, $"~r~Delete [{displayTitle}]~w~ from customizedpeds.ini");
                deleteItem.Activated += (sender, e) => DeleteCustomizedPedProfile(section);
                _deleteSavedMenu.Add(deleteItem);
            }
        }

        private void RefreshSpawnerSlider()
        {
            if (_spawnProfileListItem == null) return;

            _isUpdatingSliders = true;
            try
            {
                string prevSelected = _spawnProfileListItem.SelectedItem;
                _spawnProfileListItem.Items.Clear();
                _spawnProfileListItem.Add("[Current Ped]");

                _customizedPedsIni.Load();
                List<string> sections = _customizedPedsIni.GetSectionNames();
                foreach (string sec in sections)
                {
                    if (!string.IsNullOrWhiteSpace(sec))
                    {
                        _spawnProfileListItem.Add(sec);
                    }
                }

                int idx = _spawnProfileListItem.Items.IndexOf(prevSelected);
                _spawnProfileListItem.SelectedIndex = idx >= 0 ? idx : 0;
            }
            finally
            {
                _isUpdatingSliders = false;
            }
        }

        private void LoadCustomizedPedProfile(string sectionName)
        {
            _customizedPedsIni.Load();
            string friendlyName = _customizedPedsIni.GetValue(sectionName, "FriendlyName", sectionName);
            string modelName = _customizedPedsIni.GetValue(sectionName, "Model", "");
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = sectionName;
            }

            string movementStyle = _customizedPedsIni.GetValue(sectionName, "MovementStyle", "(Default)");
            _currentMovementStyle = movementStyle;
            if (_movementStyleListItem != null)
            {
                int movIdx = Array.IndexOf(MovementStyleList, movementStyle);
                _movementStyleListItem.SelectedIndex = movIdx >= 0 ? movIdx : 0;
            }

            string animType = _customizedPedsIni.GetValue(sectionName, "AnimationType", "");
            string animDict = _customizedPedsIni.GetValue(sectionName, "AnimDict", "");
            string animClip = _customizedPedsIni.GetValue(sectionName, "AnimClip", "");
            string scenario = _customizedPedsIni.GetValue(sectionName, "Scenario", "");

            if (string.IsNullOrEmpty(animType))
            {
                if (!string.IsNullOrEmpty(scenario)) animType = "Scenario";
                else if (!string.IsNullOrEmpty(animDict)) animType = "Animation";
                else animType = "None";
            }

            _currentAnimType = animType;
            _currentAnimDict = animDict;
            _currentAnimClip = animClip;
            _currentScenario = scenario;

            SyncAnimationUiFromCurrent();

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                int modelIdx = _modelListItem.Items.IndexOf(modelName);
                if (modelIdx < 0)
                {
                    _modelListItem.Add(modelName);
                    modelIdx = _modelListItem.Items.IndexOf(modelName);
                }
                if (modelIdx >= 0)
                {
                    _modelListItem.SelectedIndex = modelIdx;
                }

                UpdatePreview();
            }

            Ped targetPed = GetCustomizationTargetPed();
            if (targetPed == null || !targetPed.Exists()) return;

            ApplyMovementStyle(targetPed, _currentMovementStyle);
            ReapplyCurrentAnimationOnPreview();

            // 1. Read all component variations
            var compDrawables = new Dictionary<int, int>();
            var compTextures = new Dictionary<int, int>();

            foreach (int compId in _componentIds)
            {
                int drawable = -1;
                int texture = 0;

                string rawDrawable = _customizedPedsIni.GetValue(sectionName, $"Component_{compId}_Drawable", "");
                if (!string.IsNullOrEmpty(rawDrawable)) int.TryParse(rawDrawable, out drawable);

                string rawTexture = _customizedPedsIni.GetValue(sectionName, $"Component_{compId}_Texture", "");
                if (!string.IsNullOrEmpty(rawTexture)) int.TryParse(rawTexture, out texture);

                if (drawable < 0)
                {
                    string combo = _customizedPedsIni.GetValue(sectionName, $"Component_{compId}", "");
                    if (!string.IsNullOrEmpty(combo))
                    {
                        string[] parts = combo.Split(',');
                        if (parts.Length >= 1) int.TryParse(parts[0], out drawable);
                        if (parts.Length >= 2) int.TryParse(parts[1], out texture);
                    }
                }

                compDrawables[compId] = drawable;
                compTextures[compId] = texture;
            }

            // 2. Apply components with strict dependency ordering
            foreach (int compId in ComponentApplyOrder)
            {
                if (compDrawables.TryGetValue(compId, out int drawable) && drawable >= 0)
                {
                    int texture = compTextures.TryGetValue(compId, out var tex) ? tex : 0;
                    try
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, targetPed.Handle, compId, drawable, texture, 0);
                    }
                    catch { }
                }
            }

            // 3. Read & Apply Props
            foreach (int propId in _propIds)
            {
                int propIndex = -2;
                int propTexture = 0;

                string rawIndex = _customizedPedsIni.GetValue(sectionName, $"Prop_{propId}_Index", "");
                if (!string.IsNullOrEmpty(rawIndex)) int.TryParse(rawIndex, out propIndex);

                string rawPropTexture = _customizedPedsIni.GetValue(sectionName, $"Prop_{propId}_Texture", "");
                if (!string.IsNullOrEmpty(rawPropTexture)) int.TryParse(rawPropTexture, out propTexture);

                if (propIndex < -1)
                {
                    string combo = _customizedPedsIni.GetValue(sectionName, $"Prop_{propId}", "");
                    if (!string.IsNullOrEmpty(combo))
                    {
                        string[] parts = combo.Split(',');
                        if (parts.Length >= 1) int.TryParse(parts[0], out propIndex);
                        if (parts.Length >= 2) int.TryParse(parts[1], out propTexture);
                    }
                }

                try
                {
                    if (propIndex == -1)
                    {
                        Function.Call(Hash.CLEAR_PED_PROP, targetPed.Handle, propId);
                    }
                    else if (propIndex >= 0)
                    {
                        Function.Call(Hash.SET_PED_PROP_INDEX, targetPed.Handle, propId, propIndex, propTexture, true);
                    }
                }
                catch { }
            }

            // 4. Update Sliders to reflect loaded values
            _isUpdatingSliders = true;
            try
            {
                foreach (var kvp in compDrawables)
                {
                    int compId = kvp.Key;
                    int dVal = kvp.Value;
                    int tVal = compTextures.TryGetValue(compId, out var tex) ? tex : 0;

                    if (_drawableSliders.TryGetValue(compId, out var dSlider) && dVal >= 0)
                    {
                        int idx = dSlider.Items.IndexOf(dVal);
                        if (idx >= 0) dSlider.SelectedIndex = idx;
                    }

                    if (_textureSliders.TryGetValue(compId, out var tSlider))
                    {
                        int idx = tSlider.Items.IndexOf(tVal);
                        if (idx >= 0) tSlider.SelectedIndex = idx;
                    }
                }

                foreach (int propId in _propIds)
                {
                    int actualProp = Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed.Handle, propId);
                    int actualTexture = actualProp >= 0 ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed.Handle, propId) : 0;

                    if (_propSliders.TryGetValue(propId, out var pSlider))
                    {
                        int idx = pSlider.Items.IndexOf(actualProp);
                        if (idx >= 0) pSlider.SelectedIndex = idx;
                    }

                    if (_propTextureSliders.TryGetValue(propId, out var ptSlider))
                    {
                        int idx = ptSlider.Items.IndexOf(actualTexture);
                        if (idx >= 0) ptSlider.SelectedIndex = idx;
                    }
                }
            }
            finally
            {
                _isUpdatingSliders = false;
            }

            _lastSavedSection = sectionName;
        }

        private void DeleteCustomizedPedProfile(string sectionName)
        {
            try
            {
                _customizedPedsIni.Load();
                string friendlyName = _customizedPedsIni.GetValue(sectionName, "FriendlyName", sectionName);
                bool deleted = _customizedPedsIni.DeleteSection(sectionName);

                if (deleted)
                {
                    if (_lastSavedSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastSavedSection = "";
                    }
                    Logger.Write("Deleted profile [{0}] ({1}) from {2}", sectionName, friendlyName, _customizedPedsIniPath);
                }
                else
                {
                    Notification.Show($"~r~Could not find [{friendlyName}] to delete.~w~");
                }
            }
            catch (Exception ex)
            {
                Notification.Show($"~r~Error deleting profile:~w~ {ex.Message}");
                Logger.Error("Error deleting profile", ex);
            }

            RefreshSpawnerSlider();
            RefreshSavedPedsMenu();
            _deleteSavedMenu.Visible = false;
            _savedPedsMenu.Visible = true;
        }

        private void SpawnCustomizedPed(string sectionOrSpecial)
        {
            try
            {
                string modelName = "";
                string movementStyle = "(Default)";
                string animType = "None";
                string animDict = "";
                string animClip = "";
                string scenario = "";
                string displayName = "";

                var compDrawables = new Dictionary<int, int>();
                var compTextures = new Dictionary<int, int>();
                var propIndices = new Dictionary<int, int>();
                var propTextures = new Dictionary<int, int>();

                if (string.IsNullOrWhiteSpace(sectionOrSpecial) || sectionOrSpecial == "[Current Ped]" || sectionOrSpecial == "[Current Studio Ped]")
                {
                    Ped targetPed = GetCustomizationTargetPed();
                    modelName = _modelListItem.SelectedItem ?? "s_f_y_stripper_01";
                    displayName = $"Studio {modelName}";
                    movementStyle = _currentMovementStyle;
                    animType = _currentAnimType;
                    animDict = _currentAnimDict;
                    animClip = _currentAnimClip;
                    scenario = _currentScenario;

                    foreach (int compId in _componentIds)
                    {
                        int drawable = _drawableSliders.TryGetValue(compId, out var dSlider) && dSlider.Items.Count > 0
                            ? dSlider.SelectedItem
                            : (targetPed != null && targetPed.Exists() ? Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed.Handle, compId) : 0);

                        int texture = _textureSliders.TryGetValue(compId, out var tSlider) && tSlider.Items.Count > 0
                            ? tSlider.SelectedItem
                            : (targetPed != null && targetPed.Exists() ? Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed.Handle, compId) : 0);

                        compDrawables[compId] = drawable;
                        compTextures[compId] = texture;
                    }

                    foreach (int propId in _propIds)
                    {
                        int propIndex = _propSliders.TryGetValue(propId, out var pSlider) && pSlider.Items.Count > 0
                            ? pSlider.SelectedItem
                            : (targetPed != null && targetPed.Exists() ? Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed.Handle, propId) : -1);

                        int propTexture = _propTextureSliders.TryGetValue(propId, out var ptSlider) && ptSlider.Items.Count > 0
                            ? ptSlider.SelectedItem
                            : (targetPed != null && targetPed.Exists() && propIndex >= 0 ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed.Handle, propId) : 0);

                        propIndices[propId] = propIndex;
                        propTextures[propId] = propTexture;
                    }
                }
                else
                {
                    _customizedPedsIni.Load();
                    displayName = _customizedPedsIni.GetValue(sectionOrSpecial, "FriendlyName", sectionOrSpecial);
                    modelName = _customizedPedsIni.GetValue(sectionOrSpecial, "Model", sectionOrSpecial);
                    movementStyle = _customizedPedsIni.GetValue(sectionOrSpecial, "MovementStyle", "(Default)");
                    animType = _customizedPedsIni.GetValue(sectionOrSpecial, "AnimationType", "");
                    animDict = _customizedPedsIni.GetValue(sectionOrSpecial, "AnimDict", "");
                    animClip = _customizedPedsIni.GetValue(sectionOrSpecial, "AnimClip", "");
                    scenario = _customizedPedsIni.GetValue(sectionOrSpecial, "Scenario", "");

                    if (string.IsNullOrEmpty(animType))
                    {
                        if (!string.IsNullOrEmpty(scenario)) animType = "Scenario";
                        else if (!string.IsNullOrEmpty(animDict)) animType = "Animation";
                        else animType = "None";
                    }

                    foreach (int compId in _componentIds)
                    {
                        int drawable = -1;
                        int texture = 0;

                        string rawDrawable = _customizedPedsIni.GetValue(sectionOrSpecial, $"Component_{compId}_Drawable", "");
                        if (!string.IsNullOrEmpty(rawDrawable)) int.TryParse(rawDrawable, out drawable);

                        string rawTexture = _customizedPedsIni.GetValue(sectionOrSpecial, $"Component_{compId}_Texture", "");
                        if (!string.IsNullOrEmpty(rawTexture)) int.TryParse(rawTexture, out texture);

                        if (drawable < 0)
                        {
                            string combo = _customizedPedsIni.GetValue(sectionOrSpecial, $"Component_{compId}", "");
                            if (!string.IsNullOrEmpty(combo))
                            {
                                string[] parts = combo.Split(',');
                                if (parts.Length >= 1) int.TryParse(parts[0], out drawable);
                                if (parts.Length >= 2) int.TryParse(parts[1], out texture);
                            }
                        }

                        compDrawables[compId] = drawable;
                        compTextures[compId] = texture;
                    }

                    foreach (int propId in _propIds)
                    {
                        int propIndex = -2;
                        int propTexture = 0;

                        string rawIndex = _customizedPedsIni.GetValue(sectionOrSpecial, $"Prop_{propId}_Index", "");
                        if (!string.IsNullOrEmpty(rawIndex)) int.TryParse(rawIndex, out propIndex);

                        string rawPropTexture = _customizedPedsIni.GetValue(sectionOrSpecial, $"Prop_{propId}_Texture", "");
                        if (!string.IsNullOrEmpty(rawPropTexture)) int.TryParse(rawPropTexture, out propTexture);

                        if (propIndex < -1)
                        {
                            string combo = _customizedPedsIni.GetValue(sectionOrSpecial, $"Prop_{propId}", "");
                            if (!string.IsNullOrEmpty(combo))
                            {
                                string[] parts = combo.Split(',');
                                if (parts.Length >= 1) int.TryParse(parts[0], out propIndex);
                                if (parts.Length >= 2) int.TryParse(parts[1], out propTexture);
                            }
                        }

                        propIndices[propId] = propIndex >= -1 ? propIndex : -1;
                        propTextures[propId] = propTexture;
                    }
                }

                Model model = new Model(modelName);
                if (!model.IsValid)
                {
                    Notification.Show($"~r~Invalid model:~w~ {modelName}");
                    return;
                }

                model.Request(1500);
                int startTime = Game.GameTime;
                while (!model.IsLoaded && (Game.GameTime - startTime) < 1500)
                {
                    Script.Wait(0);
                }

                if (!model.IsLoaded)
                {
                    Notification.Show($"~r~Failed to load model:~w~ {modelName}");
                    model.MarkAsNoLongerNeeded();
                    return;
                }

                Ped player = Game.Player.Character;
                Vector3 targetPos = player.Position + player.ForwardVector * 3.5f;
                Vector3 spawnPos = GetAccurateGroundPosition(targetPos);
                float heading = (player.Heading + 180.0f) % 360.0f;

                Ped spawnedPed = World.CreatePed(model, spawnPos, heading);
                model.MarkAsNoLongerNeeded();

                if (spawnedPed == null || !spawnedPed.Exists())
                {
                    Notification.Show("~r~Failed to create ped in world!~w~");
                    return;
                }

                Function.Call(Hash.SET_PED_COORDS_KEEP_VEHICLE, spawnedPed.Handle, spawnPos.X, spawnPos.Y, spawnPos.Z);

                spawnedPed.IsPersistent = true;
                spawnedPed.IsPositionFrozen = false;
                spawnedPed.IsCollisionEnabled = true;
                spawnedPed.BlockPermanentEvents = false;

                // Apply Clothing variations with multi-pass dependency order
                foreach (int compId in ComponentApplyOrder)
                {
                    if (compDrawables.TryGetValue(compId, out int drawable) && drawable >= 0)
                    {
                        int texture = compTextures.TryGetValue(compId, out var tex) ? tex : 0;
                        try
                        {
                            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, spawnedPed.Handle, compId, drawable, texture, 0);
                        }
                        catch { }
                    }
                }

                // Apply Props
                foreach (int propId in _propIds)
                {
                    if (propIndices.TryGetValue(propId, out int propIdx))
                    {
                        int pTex = propTextures.TryGetValue(propId, out var tex) ? tex : 0;
                        try
                        {
                            if (propIdx == -1)
                            {
                                Function.Call(Hash.CLEAR_PED_PROP, spawnedPed.Handle, propId);
                            }
                            else if (propIdx >= 0)
                            {
                                Function.Call(Hash.SET_PED_PROP_INDEX, spawnedPed.Handle, propId, propIdx, pTex, true);
                            }
                        }
                        catch { }
                    }
                }

                // Apply Movement Style
                ApplyMovementStyle(spawnedPed, movementStyle);

                // Register and attach APS decorators for task tracking across scripts
                try
                {
                    Function.Call(Hash.DECOR_REGISTER, "APS_HasTaskLoop", 2); // 2 = bool
                    Function.Call(Hash.DECOR_REGISTER, "APS_TaskType", 3);    // 3 = int (1=Anim, 2=Scenario, 0=None)
                    Function.Call(Hash.DECOR_REGISTER, "APS_ProfileHash", 3); // 3 = int (hash of friendly name)

                    bool hasLoop = (animType == "Animation" && !string.IsNullOrWhiteSpace(animDict) && !string.IsNullOrWhiteSpace(animClip))
                                || (animType == "Scenario" && !string.IsNullOrWhiteSpace(scenario));
                    Function.Call(Hash.DECOR_SET_BOOL, spawnedPed.Handle, "APS_HasTaskLoop", hasLoop);
                    Function.Call(Hash.DECOR_SET_INT, spawnedPed.Handle, "APS_TaskType", animType == "Animation" ? 1 : (animType == "Scenario" ? 2 : 0));
                    Function.Call(Hash.DECOR_SET_INT, spawnedPed.Handle, "APS_ProfileHash", Game.GenerateHash(displayName));
                }
                catch { }

                // Apply Animation or Scenario
                if (animType == "Animation" && !string.IsNullOrWhiteSpace(animDict) && !string.IsNullOrWhiteSpace(animClip))
                {
                    if (Function.Call<bool>(Hash.DOES_ANIM_DICT_EXIST, animDict))
                    {
                        Function.Call(Hash.REQUEST_ANIM_DICT, animDict);
                        int aStart = Game.GameTime;
                        while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict) && (Game.GameTime - aStart) < 1500)
                        {
                            Script.Wait(0);
                        }
                        if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animDict))
                        {
                            Function.Call(Hash.TASK_PLAY_ANIM, spawnedPed.Handle, animDict, animClip, 8.0f, -8.0f, -1, 1, 0.0f, false, false, false);
                            Function.Call(Hash.REMOVE_ANIM_DICT, animDict);
                        }
                    }
                }
                else if (animType == "Scenario" && !string.IsNullOrWhiteSpace(scenario))
                {
                    Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, spawnedPed.Handle, scenario, 0, true);
                }

                Notification.Show($"~g~Spawned {displayName}~s~ into the world!");
                Logger.Write("Spawned fully customized and animated ped [{0}] (Model: {1}) at {2}", displayName, modelName, spawnPos);
            }
            catch (Exception ex)
            {
                Notification.Show("~r~Error spawning ped in world.~w~");
                Logger.Error("Error in SpawnCustomizedPed", ex);
            }
        }
    }
}

