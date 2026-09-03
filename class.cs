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

    public static string GetScriptsDirectory()
    {
        // 1. Resolve to the directory where this script assembly is located
        try
        {
            string asmPath = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(asmPath))
            {
                string asmDir = Path.GetDirectoryName(asmPath);
                if (!string.IsNullOrEmpty(asmDir) && Directory.Exists(asmDir))
                {
                    return asmDir;
                }
            }
        }
        catch { }

        // 2. Fall back to the "scripts" directory under the GTA V main directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        if (baseDir.EndsWith("scripts", StringComparison.OrdinalIgnoreCase))
        {
            return baseDir;
        }

        string scriptsDir = Path.Combine(baseDir, "scripts");
        if (!Directory.Exists(scriptsDir))
        {
            try { Directory.CreateDirectory(scriptsDir); } catch { }
        }

        return scriptsDir;
    }

    private static readonly string LogFilePath = Path.Combine(GetScriptsDirectory(), "AdvancedPedStudio.log");

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
                string dir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                using (var fs = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
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
                    string[] lines = File.ReadAllLines(_filePath);
                    string currentSection = "";

                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.Trim();
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
                if (!_sections.ContainsKey(section))
                {
                    _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                _sections[section][key] = value?.ToString() ?? "";
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

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("; ============================================================");
                    sb.AppendLine("; AdvancedPedStudio - Saved Customized Peds");
                    sb.AppendLine($"; Last Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine("; ============================================================");
                    sb.AppendLine();

                    foreach (var section in _sections)
                    {
                        sb.AppendLine($"[{section.Key}]");
                        foreach (var kvp in section.Value)
                        {
                            sb.AppendLine($"{kvp.Key} = {kvp.Value}");
                        }
                        sb.AppendLine();
                    }

                    using (var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                    using (var sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        sw.Write(sb.ToString());
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

    public class AdvancedPedStudio : Script
    {
        private const float SpawnDistanceFeet = 10.0f;
        private const float SpawnDistanceMeters = SpawnDistanceFeet * 0.3048f; // ~3.048 meters
        private const float PreviewRotationSpeed = 0.85f;

        // Dependency order for applying components so that Tops/Undershirts never wipe out Torso/Arms/Legs
        private static readonly int[] ComponentApplyOrder = { 8, 11, 4, 6, 0, 1, 2, 5, 7, 9, 10, 3, 4, 8, 11, 3 };

        private readonly string _pedModelsPath;
        private readonly string _customizedPedsIniPath;

        private readonly List<string> _pedModels = new List<string>();

        private readonly ObjectPool _menuPool;
        private readonly NativeMenu _mainMenu;
        private readonly NativeMenu _clothingMenu;
        private readonly NativeMenu _propsMenu;
        private readonly NativeMenu _savedPedsMenu;
        private readonly NativeMenu _deleteSavedMenu;

        private NativeListItem<string> _modelListItem;
        private NativeListItem<string> _movementStyleListItem;
        private string _currentMovementStyle = "(Default)";

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

        private readonly Dictionary<int, NativeListItem<int>> _drawableSliders = new Dictionary<int, NativeListItem<int>>();
        private readonly Dictionary<int, NativeListItem<int>> _textureSliders = new Dictionary<int, NativeListItem<int>>();
        private readonly Dictionary<int, NativeListItem<int>> _propSliders = new Dictionary<int, NativeListItem<int>>();
        private readonly Dictionary<int, NativeListItem<int>> _propTextureSliders = new Dictionary<int, NativeListItem<int>>();

        private readonly int[] _componentIds = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        private readonly int[] _propIds = { 0, 1, 2, 6, 7 };

        private Ped _previewPed;
        private bool _isUpdatingSliders = false;
        private readonly CayoInletSwimController _cayoInletSwimController = new CayoInletSwimController();

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

                _menuPool = new ObjectPool();
                _mainMenu = new NativeMenu("Ped Studio", "Advanced Ped Customizer");
                _clothingMenu = new NativeMenu("Clothing Variations", "Customize Drawable Components");
                _propsMenu = new NativeMenu("Props & Accessories", "Customize Hats, Glasses & Props");
                _savedPedsMenu = new NativeMenu("Saved Customizations", "Load Saved Peds from INI");
                _deleteSavedMenu = new NativeMenu("Delete Customization", "Remove Profile from INI");

                LoadPedModels();

                // 1. Model Selection Picker
                _modelListItem = new NativeListItem<string>("Ped Model", "Scroll left/right to select and preview a ped model.", _pedModels.ToArray());
                _modelListItem.ItemChanged += (sender, e) => UpdatePreview();
                _mainMenu.Add(_modelListItem);

                // Movement Style Slider directly under Ped Model
                _movementStyleListItem = new NativeListItem<string>("Movement Style", "Select movement & walk personality style.", MovementStyleList);
                _movementStyleListItem.ItemChanged += (sender, e) =>
                {
                    _currentMovementStyle = _movementStyleListItem.SelectedItem ?? "(Default)";
                    Ped target = GetCustomizationTargetPed();
                    if (target != null && target.Exists())
                    {
                        ApplyMovementStyle(target, _currentMovementStyle);
                    }
                };
                _mainMenu.Add(_movementStyleListItem);

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
                SetupPropSlider("Hats / Helmets (Head)", 0);
                SetupPropSlider("Glasses (Eyes)", 1);
                SetupPropSlider("Ear Accessories", 2);
                SetupPropSlider("Watches", 6);
                SetupPropSlider("Bracelets", 7);

                _clothingMenu.Opening += (sender, e) => RefreshClothingSliders();
                _propsMenu.Opening += (sender, e) => RefreshClothingSliders();

                // 4. Randomize Button
                NativeItem randomizeButton = new NativeItem("Randomize Variations", "Randomizes clothing components and props on preview ped.");
                randomizeButton.Activated += (sender, e) => RandomizeVariations();
                _mainMenu.Add(randomizeButton);

                // 5. Save Customization Button
                NativeItem saveButton = new NativeItem("~g~Save to customizedpeds.ini", "Saves model and current appearance parameters to customizedpeds.ini.");
                saveButton.Activated += (sender, e) => SaveCustomizedPed();
                _mainMenu.Add(saveButton);

                // 6. Submenus
                _mainMenu.AddSubMenu(_clothingMenu);
                _mainMenu.AddSubMenu(_propsMenu);
                _mainMenu.AddSubMenu(_savedPedsMenu);

                _savedPedsMenu.AddSubMenu(_deleteSavedMenu);

                _savedPedsMenu.Opening += (sender, e) => RefreshSavedPedsMenu();
                _deleteSavedMenu.Opening += (sender, e) => RefreshSavedPedsMenu();

                // 7. Reload pedmodels.txt button
                NativeItem reloadModelsButton = new NativeItem("Reload pedmodels.txt", "Reloads the model list from scripts/pedmodels.txt.");
                reloadModelsButton.Activated += (sender, e) =>
                {
                    LoadPedModels();
                    RefreshModelList();
                };
                _mainMenu.Add(reloadModelsButton);

                // Background Cayo Inlet Swimmer task toggle
                NativeCheckboxItem cayoSwimCheckbox = new NativeCheckboxItem("Cayo Inlet Swimmer", "Background task: ambient peds at Cayo Perico inlet periodically swim out and back.", _cayoInletSwimController.Enabled);
                cayoSwimCheckbox.CheckboxChanged += (s, e) => _cayoInletSwimController.Enabled = cayoSwimCheckbox.Checked;
                _mainMenu.Add(cayoSwimCheckbox);

                // Add to menu pool
                _menuPool.Add(_mainMenu);
                _menuPool.Add(_clothingMenu);
                _menuPool.Add(_propsMenu);
                _menuPool.Add(_savedPedsMenu);
                _menuPool.Add(_deleteSavedMenu);

                Tick += OnTick;
                KeyDown += OnKeyDown;
                Aborted += (sender, e) =>
                {
                    ClearPreview();
                    _cayoInletSwimController.CleanUp();
                };

                Logger.Write("AdvancedPedStudio initialized successfully. Press F11 to open menu.");
                GTA.UI.Screen.ShowSubtitle("~g~Advanced Ped Studio~s~ ready! Press ~y~F11~s~ to open studio.", 3500);
            }
            catch (Exception ex)
            {
                Logger.Error("Error during AdvancedPedStudio initialization", ex);
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
            _cayoInletSwimController.OnTick();

            bool anyMenuOpen = _menuPool.AreAnyVisible;

            if (anyMenuOpen && _previewPed != null && _previewPed.Exists())
            {
                _previewPed.Heading += PreviewRotationSpeed;
                Game.DisableControlThisFrame(Control.Phone);
            }
            else if (!anyMenuOpen && _previewPed != null)
            {
                ClearPreview();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                if (_menuPool.AreAnyVisible)
                {
                    CloseAllMenus();
                }
                else
                {
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

        private Vector3 GetSpawnPosition()
        {
            Ped player = Game.Player.Character;
            Vector3 targetPos = player.Position + player.ForwardVector * SpawnDistanceMeters;
            float groundZ = World.GetGroundHeight(new Vector2(targetPos.X, targetPos.Y));
            if (Math.Abs(groundZ) > 0.001f)
            {
                targetPos.Z = groundZ;
            }
            return targetPos;
        }

        private void ApplyMovementStyle(Ped ped, string clipSet)
        {
            if (ped == null || !ped.Exists() || !ped.IsAlive) return;

            if (string.IsNullOrEmpty(clipSet) || clipSet.Equals("(Default)", StringComparison.OrdinalIgnoreCase))
            {
                Function.Call(Hash.RESET_PED_MOVEMENT_CLIPSET, ped, 0.25f);
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
                Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, ped, clipSet, 0.25f);
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
            _previewPed = World.CreatePed(model, spawnPos);
            model.MarkAsNoLongerNeeded();

            if (_previewPed != null && _previewPed.Exists())
            {
                _previewPed.Heading = (Game.Player.Character.Heading + 180.0f) % 360.0f;
                _previewPed.IsInvincible = true;
                _previewPed.IsPositionFrozen = true;
                _previewPed.Task.StandStill(-1);
                _previewPed.IsCollisionEnabled = false;
                _previewPed.BlockPermanentEvents = true;

                ApplyMovementStyle(_previewPed, _currentMovementStyle);
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
        }

        private void SetupClothingSlider(string componentName, int componentId)
        {
            Ped targetPed = GetCustomizationTargetPed();

            int currentDrawable = (targetPed != null && targetPed.Exists())
                ? Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed, componentId)
                : 0;
            int currentTexture = (targetPed != null && targetPed.Exists())
                ? Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed, componentId)
                : 0;

            int maxDrawables = (targetPed != null && targetPed.Exists())
                ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, targetPed, componentId))
                : 1;

            List<int> drawableItems = new List<int>();
            for (int i = 0; i < maxDrawables; i++) drawableItems.Add(i);

            NativeListItem<int> drawableSlider = new NativeListItem<int>(componentName, $"Select {componentName.ToLower()} model / variation.", drawableItems.ToArray());
            int dIdx = drawableItems.IndexOf(currentDrawable);
            drawableSlider.SelectedIndex = dIdx >= 0 ? dIdx : 0;

            int maxTextures = (targetPed != null && targetPed.Exists())
                ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, targetPed, componentId, currentDrawable))
                : 1;

            List<int> textureItems = new List<int>();
            for (int i = 0; i < maxTextures; i++) textureItems.Add(i);

            NativeListItem<int> textureSlider = new NativeListItem<int>("Texture", $"Select {componentName.ToLower()} color / texture.", textureItems.ToArray());
            int tIdx = textureItems.IndexOf(currentTexture);
            textureSlider.SelectedIndex = tIdx >= 0 ? tIdx : 0;

            drawableSlider.ItemChanged += (sender, e) =>
            {
                if (_isUpdatingSliders) return;

                Ped currentPed = GetCustomizationTargetPed();
                if (currentPed == null || !currentPed.Exists()) return;

                int selectedDrawable = drawableSlider.SelectedItem;
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed, componentId, selectedDrawable, 0, 0);

                // Preserve Torso/Arms (3) when changing Tops (11) or Undershirts (8)
                if ((componentId == 11 || componentId == 8) && _drawableSliders.ContainsKey(3))
                {
                    int torsoDrawable = _drawableSliders[3].SelectedItem;
                    int torsoTexture = _textureSliders.ContainsKey(3) ? _textureSliders[3].SelectedItem : 0;
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed, 3, torsoDrawable, torsoTexture, 0);
                }

                // Preserve Legs/Shoes pairing
                if (componentId == 11 && _drawableSliders.ContainsKey(4))
                {
                    int legsDrawable = _drawableSliders[4].SelectedItem;
                    int legsTexture = _textureSliders.ContainsKey(4) ? _textureSliders[4].SelectedItem : 0;
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed, 4, legsDrawable, legsTexture, 0);
                }

                int newMaxTextures = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, currentPed, componentId, selectedDrawable));
                List<int> newTextureItems = new List<int>();
                for (int i = 0; i < newMaxTextures; i++) newTextureItems.Add(i);

                textureSlider.Items.Clear();
                foreach (var item in newTextureItems) textureSlider.Add(item);
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
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed, componentId, selectedDrawable, selectedTexture, 0);

                    // Preserve Torso/Arms (3) when changing Tops (11) or Undershirts (8)
                    if ((componentId == 11 || componentId == 8) && _drawableSliders.ContainsKey(3))
                    {
                        int torsoDrawable = _drawableSliders[3].SelectedItem;
                        int torsoTexture = _textureSliders.ContainsKey(3) ? _textureSliders[3].SelectedItem : 0;
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, currentPed, 3, torsoDrawable, torsoTexture, 0);
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
                ? Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed, propId)
                : -1;
            int currentTexture = (targetPed != null && targetPed.Exists() && currentProp >= 0)
                ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed, propId)
                : 0;

            int maxProps = (targetPed != null && targetPed.Exists())
                ? Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS, targetPed, propId)
                : 0;

            List<int> propItems = new List<int> { -1 };
            for (int i = 0; i < maxProps; i++) propItems.Add(i);

            NativeListItem<int> propSlider = new NativeListItem<int>(propName, $"Change {propName.ToLower()} model (-1 = None).", propItems.ToArray());
            int pIdx = propItems.IndexOf(currentProp);
            propSlider.SelectedIndex = pIdx >= 0 ? pIdx : 0;

            int maxTextures = (targetPed != null && targetPed.Exists() && currentProp >= 0)
                ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, targetPed, propId, currentProp))
                : 1;

            List<int> textureItems = new List<int>();
            for (int i = 0; i < maxTextures; i++) textureItems.Add(i);

            NativeListItem<int> textureSlider = new NativeListItem<int>("Texture", $"Change {propName.ToLower()} texture / color.", textureItems.ToArray());
            int tIdx = textureItems.IndexOf(currentTexture);
            textureSlider.SelectedIndex = tIdx >= 0 ? tIdx : 0;

            propSlider.ItemChanged += (sender, e) =>
            {
                if (_isUpdatingSliders) return;

                Ped currentPed = GetCustomizationTargetPed();
                if (currentPed == null || !currentPed.Exists()) return;

                int selectedProp = propSlider.SelectedItem;
                if (selectedProp == -1)
                {
                    Function.Call(Hash.CLEAR_PED_PROP, currentPed, propId);
                    textureSlider.Items.Clear();
                    textureSlider.Add(0);
                    textureSlider.SelectedIndex = 0;
                }
                else
                {
                    Function.Call(Hash.SET_PED_PROP_INDEX, currentPed, propId, selectedProp, 0, true);
                    int newMaxTextures = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, currentPed, propId, selectedProp));
                    List<int> newTextureItems = new List<int>();
                    for (int i = 0; i < newMaxTextures; i++) newTextureItems.Add(i);

                    textureSlider.Items.Clear();
                    foreach (var item in newTextureItems) textureSlider.Add(item);
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
                    Function.Call(Hash.SET_PED_PROP_INDEX, currentPed, propId, selectedProp, selectedTexture, true);
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
                    var tSlider = _textureSliders.ContainsKey(compId) ? _textureSliders[compId] : null;

                    int maxDrawables = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, targetPed, compId));
                    int actualDrawable = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed, compId);

                    dSlider.Items.Clear();
                    for (int i = 0; i < maxDrawables; i++) dSlider.Add(i);

                    int dIdx = dSlider.Items.IndexOf(actualDrawable);
                    dSlider.SelectedIndex = dIdx >= 0 ? dIdx : 0;

                    if (tSlider != null)
                    {
                        int maxTextures = Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, targetPed, compId, actualDrawable));
                        int actualTexture = Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed, compId);

                        tSlider.Items.Clear();
                        for (int i = 0; i < maxTextures; i++) tSlider.Add(i);

                        int tIdx = tSlider.Items.IndexOf(actualTexture);
                        tSlider.SelectedIndex = tIdx >= 0 ? tIdx : 0;
                    }
                }

                foreach (var kvp in _propSliders)
                {
                    int propId = kvp.Key;
                    var pSlider = kvp.Value;
                    var tSlider = _propTextureSliders.ContainsKey(propId) ? _propTextureSliders[propId] : null;

                    int maxProps = Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS, targetPed, propId);
                    int actualProp = Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed, propId);

                    pSlider.Items.Clear();
                    pSlider.Add(-1);
                    for (int i = 0; i < maxProps; i++) pSlider.Add(i);

                    int pIdx = pSlider.Items.IndexOf(actualProp);
                    pSlider.SelectedIndex = pIdx >= 0 ? pIdx : 0;

                    if (tSlider != null)
                    {
                        int maxTextures = (actualProp >= 0)
                            ? Math.Max(1, Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, targetPed, propId, actualProp))
                            : 1;
                        int actualTexture = (actualProp >= 0)
                            ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed, propId)
                            : 0;

                        tSlider.Items.Clear();
                        for (int i = 0; i < maxTextures; i++) tSlider.Add(i);

                        int tIdx = tSlider.Items.IndexOf(actualTexture);
                        tSlider.SelectedIndex = tIdx >= 0 ? tIdx : 0;
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
                Function.Call(Hash.SET_PED_RANDOM_COMPONENT_VARIATION, targetPed, 0);
                Function.Call(Hash.SET_PED_RANDOM_PROPS, targetPed);
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
                var ini = new SimpleIniFile(_customizedPedsIniPath);
                List<string> existingSections = ini.GetSectionNames();

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

                ini.SetValue(sectionName, "FriendlyName", friendlyName);
                ini.SetValue(sectionName, "Model", modelName);
                ini.SetValue(sectionName, "Option", suffix);
                ini.SetValue(sectionName, "MovementStyle", _currentMovementStyle);
                ini.SetValue(sectionName, "SavedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                // Save Components (0-11) directly from sliders & ped state
                foreach (int compId in _componentIds)
                {
                    int drawable = _drawableSliders.ContainsKey(compId) && _drawableSliders[compId].Items.Count > 0
                        ? _drawableSliders[compId].SelectedItem
                        : Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, targetPed, compId);

                    int texture = _textureSliders.ContainsKey(compId) && _textureSliders[compId].Items.Count > 0
                        ? _textureSliders[compId].SelectedItem
                        : Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, targetPed, compId);

                    ini.SetValue(sectionName, $"Component_{compId}", $"{drawable},{texture}");
                    ini.SetValue(sectionName, $"Component_{compId}_Drawable", drawable);
                    ini.SetValue(sectionName, $"Component_{compId}_Texture", texture);
                }

                // Save Props (0, 1, 2, 6, 7) directly from sliders & ped state
                foreach (int propId in _propIds)
                {
                    int propIndex = _propSliders.ContainsKey(propId) && _propSliders[propId].Items.Count > 0
                        ? _propSliders[propId].SelectedItem
                        : Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed, propId);

                    int propTexture = _propTextureSliders.ContainsKey(propId) && _propTextureSliders[propId].Items.Count > 0
                        ? _propTextureSliders[propId].SelectedItem
                        : (propIndex >= 0 ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed, propId) : 0);

                    ini.SetValue(sectionName, $"Prop_{propId}", $"{propIndex},{propTexture}");
                    ini.SetValue(sectionName, $"Prop_{propId}_Index", propIndex);
                    ini.SetValue(sectionName, $"Prop_{propId}_Texture", propTexture);
                }

                bool saved = ini.Save();

                if (saved && File.Exists(_customizedPedsIniPath))
                {
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

        private void RefreshSavedPedsMenu()
        {
            _savedPedsMenu.Clear();
            _deleteSavedMenu.Clear();

            _savedPedsMenu.AddSubMenu(_deleteSavedMenu);

            var ini = new SimpleIniFile(_customizedPedsIniPath);
            List<string> sections = ini.GetSectionNames();

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
                string friendlyName = ini.GetValue(section, "FriendlyName", section);
                string model = ini.GetValue(section, "Model", section);
                string option = ini.GetValue(section, "Option", "");
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

        private void LoadCustomizedPedProfile(string sectionName)
        {
            var ini = new SimpleIniFile(_customizedPedsIniPath);
            string friendlyName = ini.GetValue(sectionName, "FriendlyName", sectionName);
            string modelName = ini.GetValue(sectionName, "Model", "");
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = sectionName;
            }

            string movementStyle = ini.GetValue(sectionName, "MovementStyle", "(Default)");
            _currentMovementStyle = movementStyle;
            if (_movementStyleListItem != null)
            {
                int movIdx = Array.IndexOf(MovementStyleList, movementStyle);
                _movementStyleListItem.SelectedIndex = movIdx >= 0 ? movIdx : 0;
            }

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

            // 1. Read all component variations
            var compDrawables = new Dictionary<int, int>();
            var compTextures = new Dictionary<int, int>();

            foreach (int compId in _componentIds)
            {
                int drawable = -1;
                int texture = 0;

                string rawDrawable = ini.GetValue(sectionName, $"Component_{compId}_Drawable", "");
                if (!string.IsNullOrEmpty(rawDrawable)) int.TryParse(rawDrawable, out drawable);

                string rawTexture = ini.GetValue(sectionName, $"Component_{compId}_Texture", "");
                if (!string.IsNullOrEmpty(rawTexture)) int.TryParse(rawTexture, out texture);

                if (drawable < 0)
                {
                    string combo = ini.GetValue(sectionName, $"Component_{compId}", "");
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
                    int texture = compTextures.ContainsKey(compId) ? compTextures[compId] : 0;
                    try
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, targetPed, compId, drawable, texture, 0);
                    }
                    catch { }
                }
            }

            // 3. Read & Apply Props
            foreach (int propId in _propIds)
            {
                int propIndex = -2;
                int propTexture = 0;

                string rawIndex = ini.GetValue(sectionName, $"Prop_{propId}_Index", "");
                if (!string.IsNullOrEmpty(rawIndex)) int.TryParse(rawIndex, out propIndex);

                string rawPropTexture = ini.GetValue(sectionName, $"Prop_{propId}_Texture", "");
                if (!string.IsNullOrEmpty(rawPropTexture)) int.TryParse(rawPropTexture, out propTexture);

                if (propIndex < -1)
                {
                    string combo = ini.GetValue(sectionName, $"Prop_{propId}", "");
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
                        Function.Call(Hash.CLEAR_PED_PROP, targetPed, propId);
                    }
                    else if (propIndex >= 0)
                    {
                        Function.Call(Hash.SET_PED_PROP_INDEX, targetPed, propId, propIndex, propTexture, true);
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
                    int tVal = compTextures.ContainsKey(compId) ? compTextures[compId] : 0;

                    if (_drawableSliders.ContainsKey(compId) && dVal >= 0)
                    {
                        var dSlider = _drawableSliders[compId];
                        int idx = dSlider.Items.IndexOf(dVal);
                        if (idx >= 0) dSlider.SelectedIndex = idx;
                    }

                    if (_textureSliders.ContainsKey(compId))
                    {
                        var tSlider = _textureSliders[compId];
                        int idx = tSlider.Items.IndexOf(tVal);
                        if (idx >= 0) tSlider.SelectedIndex = idx;
                    }
                }

                foreach (int propId in _propIds)
                {
                    int actualProp = Function.Call<int>(Hash.GET_PED_PROP_INDEX, targetPed, propId);
                    int actualTexture = actualProp >= 0 ? Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, targetPed, propId) : 0;

                    if (_propSliders.ContainsKey(propId))
                    {
                        var pSlider = _propSliders[propId];
                        int idx = pSlider.Items.IndexOf(actualProp);
                        if (idx >= 0) pSlider.SelectedIndex = idx;
                    }

                    if (_propTextureSliders.ContainsKey(propId))
                    {
                        var ptSlider = _propTextureSliders[propId];
                        int idx = ptSlider.Items.IndexOf(actualTexture);
                        if (idx >= 0) ptSlider.SelectedIndex = idx;
                    }
                }
            }
            finally
            {
                _isUpdatingSliders = false;
            }

        }

        private void DeleteCustomizedPedProfile(string sectionName)
        {
            try
            {
                var ini = new SimpleIniFile(_customizedPedsIniPath);
                string friendlyName = ini.GetValue(sectionName, "FriendlyName", sectionName);
                bool deleted = ini.DeleteSection(sectionName);

                if (deleted)
                {
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

            RefreshSavedPedsMenu();
            _deleteSavedMenu.Visible = false;
            _savedPedsMenu.Visible = true;
        }
    }

    // ============================================================
    // Global Swim State (Tracked for sequence coordination across scripts)
    // ============================================================
    public static class CayoSwimState
    {
        public static readonly HashSet<int> ActiveSwimmerHandles = new HashSet<int>();
        public static bool IsPedSwimming(int handle) => ActiveSwimmerHandles.Contains(handle);
    }

    // ============================================================
    // Background Task: Cayo Perico Inlet Ambient Swimmer Task
    // ============================================================
    public class CayoInletSwimController
    {
        // Cayo Perico West Beach Inlet center point
        private static readonly Vector3 BeachCenter = new Vector3(4845.2031f, -4935.862f, 0f);
        private const float SearchRadiusFeet = 70.0f;
        private const float SearchRadiusMeters = SearchRadiusFeet * 0.3048f; // ~21.336 meters (70 feet)

        // Target coordinate on shore where peds exit the water
        private static readonly Vector3 ReturnShoreTarget = new Vector3(4863.0664f, -4927.760f, 1.504f);

        private const int CheckIntervalMs = 2000;
        private const int SwimChancePercent = 18; // Increased chance per 2-second check
        private int _lastCheckTime = 0;
        private readonly Random _random = new Random();

        public bool Enabled { get; set; } = true;

        private class ActiveSwimmer
        {
            public Ped Ped;
            public Vector3 OriginalPosition;
            public float OriginalHeading;
            public int StartTime;
            public int Phase; // 0 = straight to center point, 1 = swimming in circle (5s), 2 = straight back towards shore exit, 3 = returned
            public int PhaseStartTime;
            public float CircleStartAngle;
            public int LastCircleUpdate;
        }

        private readonly List<ActiveSwimmer> _activeSwimmers = new List<ActiveSwimmer>();

        public void OnTick()
        {
            if (!Enabled)
            {
                if (_activeSwimmers.Count > 0) CleanUp();
                return;
            }

            int now = Game.GameTime;

            // Process active swimmers state machine
            for (int i = _activeSwimmers.Count - 1; i >= 0; i--)
            {
                var swimmer = _activeSwimmers[i];
                Ped ped = swimmer.Ped;

                // Safety guard: ped deleted, dead, or watchdog timeout (60 seconds)
                if (ped == null || !ped.Exists() || !ped.IsAlive || (now - swimmer.StartTime) > 60000)
                {
                    ReleasePed(swimmer);
                    _activeSwimmers.RemoveAt(i);
                    continue;
                }

                int phaseElapsed = now - swimmer.PhaseStartTime;

                switch (swimmer.Phase)
                {
                    case 0:
                        // Leg 1: Moving in the straightest line possible to BeachCenter
                        float distToCenter = ped.Position.DistanceTo(BeachCenter);
                        bool isSwimming = Function.Call<bool>(Hash.IS_PED_SWIMMING, ped.Handle);

                        // Reached center point
                        if (distToCenter <= 3.2f || (isSwimming && distToCenter <= 4.2f && phaseElapsed > 6000))
                        {
                            swimmer.Phase = 1;
                            swimmer.PhaseStartTime = now;
                            swimmer.CircleStartAngle = (float)Math.Atan2(ped.Position.Y - BeachCenter.Y, ped.Position.X - BeachCenter.X);
                            swimmer.LastCircleUpdate = 0;
                            Logger.Write("Ped [{0}] reached center point. Swimming in circle for 5 seconds.", ped.Handle);
                        }
                        else if (phaseElapsed > 25000)
                        {
                            // Stalled or took too long, transition directly to return leg
                            swimmer.Phase = 2;
                            swimmer.PhaseStartTime = now;
                            Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, ped.Handle, ReturnShoreTarget.X, ReturnShoreTarget.Y, ReturnShoreTarget.Z, 1.4f, -1, 0.0f, 0.0f);
                        }
                        break;

                    case 1:
                        // Leg 2: Swimming in a circle for 5 seconds
                        const float circleDurationMs = 5000.0f;
                        const float circleRadius = 3.5f;

                        if (phaseElapsed < (int)circleDurationMs)
                        {
                            if (now - swimmer.LastCircleUpdate > 600)
                            {
                                swimmer.LastCircleUpdate = now;
                                float progress = (phaseElapsed / circleDurationMs) * (float)(2.0 * Math.PI);
                                float angle = swimmer.CircleStartAngle + progress;
                                float targetX = BeachCenter.X + circleRadius * (float)Math.Cos(angle);
                                float targetY = BeachCenter.Y + circleRadius * (float)Math.Sin(angle);

                                Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, ped.Handle, targetX, targetY, 0.0f, 1.3f, -1, 0.0f, 0.0f);
                            }
                        }
                        else
                        {
                            // 5 seconds completed! Swim straight back towards ReturnShoreTarget
                            swimmer.Phase = 2;
                            swimmer.PhaseStartTime = now;
                            Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, ped.Handle, ReturnShoreTarget.X, ReturnShoreTarget.Y, ReturnShoreTarget.Z, 1.4f, -1, 0.0f, 0.0f);
                            Logger.Write("Ped [{0}] finished circle swim. Swimming straight towards shore target {1}", ped.Handle, ReturnShoreTarget);
                        }
                        break;

                    case 2:
                        // Leg 3: Swimming straight back towards x4863.0664 y-4927.760 z1.504
                        float distToShore = ped.Position.DistanceTo(ReturnShoreTarget);
                        bool inWater = Function.Call<bool>(Hash.IS_PED_SWIMMING, ped.Handle) || Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, ped.Handle);

                        // Once ped reaches the shore target or exits the water
                        if ((!inWater && distToShore < 3.5f) || distToShore < 2.0f || phaseElapsed > 30000)
                        {
                            ReleasePed(swimmer);
                            _activeSwimmers.RemoveAt(i);
                            Logger.Write("Ped [{0}] exited water at shore target. Resumed assigned task sequence.", ped.Handle);
                        }
                        break;
                }
            }

            // Periodic search for a random nearby ped within 70 feet of BeachCenter
            if (now - _lastCheckTime > CheckIntervalMs)
            {
                _lastCheckTime = now;

                if (_activeSwimmers.Count < 2)
                {
                    if (_random.Next(0, 100) < SwimChancePercent)
                    {
                        TryStartRandomPedSwim();
                    }
                }
            }
        }

        private void TryStartRandomPedSwim()
        {
            Ped player = Game.Player.Character;
            // Only execute if player is within 350m of Cayo Perico inlet to minimize CPU usage
            if (player == null || !player.Exists() || player.Position.DistanceTo(BeachCenter) > 350.0f)
            {
                return;
            }

            // Find peds within 70 feet (~21.336m) of BeachCenter
            Ped[] nearby = World.GetNearbyPeds(BeachCenter, SearchRadiusMeters);
            var eligible = new List<Ped>();

            foreach (var p in nearby)
            {
                if (p == null || !p.Exists() || !p.IsAlive || p == player || p.IsInVehicle())
                    continue;

                // Don't select if already swimming or active
                if (_activeSwimmers.Any(s => s.Ped == p))
                    continue;

                // Don't interrupt peds already in combat or ragdolling
                if (p.IsInCombat || p.IsRagdoll)
                    continue;

                eligible.Add(p);
            }

            if (eligible.Count > 0)
            {
                // Select a random ped from the eligible candidates within 70 feet
                Ped selectedPed = eligible[_random.Next(eligible.Count)];
                StartSwimRoutine(selectedPed);
            }
        }

        private void StartSwimRoutine(Ped ped)
        {
            Vector3 pedPos = ped.Position;
            float pedHeading = ped.Heading;

            CayoSwimState.ActiveSwimmerHandles.Add(ped.Handle);

            // Setup ped attributes for safe swimming
            Function.Call(Hash.CLEAR_PED_TASKS, ped.Handle);
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 17, false);
            Function.Call(Hash.SET_PED_DIES_IN_WATER, ped.Handle, false);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 64, true); // CPED_CONFIG_FLAG_DrownsInWater = false
            Function.Call(Hash.SET_PED_MAX_TIME_UNDERWATER, ped.Handle, 600.0f);

            // Leg 1: Move in the straightest line possible to BeachCenter
            Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, ped.Handle, BeachCenter.X, BeachCenter.Y, BeachCenter.Z, 1.4f, -1, 0.0f, 0.0f);

            _activeSwimmers.Add(new ActiveSwimmer
            {
                Ped = ped,
                OriginalPosition = pedPos,
                OriginalHeading = pedHeading,
                StartTime = Game.GameTime,
                Phase = 0,
                PhaseStartTime = Game.GameTime
            });

            Logger.Write("Started Cayo inlet swim for ped [{0}] at {1} -> BeachCenter {2} (straight line)", ped.Handle, pedPos, BeachCenter);
        }

        private void ReleasePed(ActiveSwimmer swimmer)
        {
            if (swimmer == null) return;
            Ped ped = swimmer.Ped;

            if (ped != null && ped.Exists())
            {
                CayoSwimState.ActiveSwimmerHandles.Remove(ped.Handle);

                if (ped.IsAlive)
                {
                    Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
                    Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 64, false); // DrownsInWater = default
                    Function.Call(Hash.SET_PED_DIES_IN_WATER, ped.Handle, true);

                    // Resume assigned task sequence:
                    // 1. If spawned via PedCreator, resume its sequence steps directly
                    bool resumedViaPedCreator = TryResumePedCreatorSequence(ped);

                    if (!resumedViaPedCreator)
                    {
                        // 2. Ambient/world ped: walk in straight line back to original spot and resume scenario
                        float distToOrig = ped.Position.DistanceTo(swimmer.OriginalPosition);
                        if (distToOrig > 2.0f)
                        {
                            Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, ped.Handle, swimmer.OriginalPosition.X, swimmer.OriginalPosition.Y, swimmer.OriginalPosition.Z, 1.3f, -1, swimmer.OriginalHeading, 0.0f);
                        }
                        else
                        {
                            Function.Call(Hash.TASK_USE_NEAREST_SCENARIO_TO_COORD, ped.Handle, swimmer.OriginalPosition.X, swimmer.OriginalPosition.Y, swimmer.OriginalPosition.Z, 15.0f, 0);
                        }
                    }
                }
            }
        }

        private static MethodInfo _resumeMethod = null;
        private static bool _triedResolvingPedCreator = false;

        private static bool TryResumePedCreatorSequence(Ped ped)
        {
            try
            {
                if (!_triedResolvingPedCreator)
                {
                    _triedResolvingPedCreator = true;
                    var pcAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "PedCreator");
                    if (pcAsm != null)
                    {
                        var pcType = pcAsm.GetType("PedCreator.PedCreatorScript") ?? pcAsm.GetTypes().FirstOrDefault(t => t.Name == "PedCreatorScript");
                        if (pcType != null)
                        {
                            _resumeMethod = pcType.GetMethod("ResumeSpawnedPedSequence", BindingFlags.Public | BindingFlags.Static);
                        }
                    }
                }

                if (_resumeMethod != null)
                {
                    object res = _resumeMethod.Invoke(null, new object[] { ped.Handle });
                    if (res is bool b && b) return true;
                }
            }
            catch { }
            return false;
        }

        public void CleanUp()
        {
            foreach (var swimmer in _activeSwimmers)
            {
                ReleasePed(swimmer);
            }
            _activeSwimmers.Clear();
            CayoSwimState.ActiveSwimmerHandles.Clear();
        }
    }
}

