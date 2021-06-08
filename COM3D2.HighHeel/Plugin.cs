﻿using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using COM3D2.HighHeel.UI;
using COM3D2.Lilly.Plugin;
using COM3D2API;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

namespace COM3D2.HighHeel
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.habeebweeb.com3d2.highheel";
        public const string PluginName = "COM3D2.HighHeel";
        public const string PluginVersion = "1.0.0";
        public const string PluginString = PluginName + " " + PluginVersion;

        private const string ConfigName = "Configuration.cfg";

        private static readonly string ConfigPath = Path.Combine(Paths.ConfigPath, PluginName);
        private static readonly string ShoeConfigPath = Path.Combine(ConfigPath, "Configurations");
        public readonly PluginConfig Configuration;
        public new readonly ManualLogSource Logger;
        private readonly UI.MainWindow mainWindow;

        public static bool IsDance { get; private set; }

        public Dictionary<string, Core.ShoeConfig> ShoeDatabase { get; private set; }
        public Core.ShoeConfig EditModeConfig = new();

        public bool EditMode { get; set; }

        public static Plugin? Instance { get; private set; }

        public Plugin()
        {
            EditMode = true;
            Instance = this;
            try
            {
                Harmony.CreateAndPatchAll(typeof(Core.Hooks));
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogWarning($"COM3D2.HighHeel.Plugin().Harmony.CreateAndPatchAll fail : {e.Message}");
            }

            Configuration = new(new(Path.Combine(ConfigPath, ConfigName), false, Info.Metadata));
            Logger = base.Logger;

            if (!Directory.Exists(ShoeConfigPath))
            {
                Directory.CreateDirectory(ShoeConfigPath);
            }

            mainWindow = new();
            mainWindow.ReloadEvent += (_, _) => ShoeDatabase = LoadShoeDatabase();

            mainWindow.ExportEvent += (_, args) => ExportConfiguration(EditModeConfig, args.Text);

            mainWindow.ImportEvent += (_, args) => { ImportConfiguration(ref EditModeConfig, args.Text); mainWindow.editModeConfigUpdate(); };

            SceneManager.sceneLoaded += (_, _) => IsDance = FindObjectOfType<DanceMain>() != null;
            
            ShoeDatabase = LoadShoeDatabase();

            ImportConfiguration(ref EditModeConfig, "");
            mainWindow.editModeConfigUpdate();


        }

        private void Start()
        {
            SystemShortcutAPI.AddButton("HighHeel", new Action(delegate () { mainWindow.Visible = !mainWindow.Visible; }), "HighHeel", MyUtill.ExtractResource(Properties.Resources.icon));
        }

        private void Update()
        {
            if (Configuration.UIShortcut.Value.IsUp()) mainWindow.Visible = !mainWindow.Visible;

            mainWindow.Update();
        }

        private void OnGUI() {
            mainWindow.Draw();
        }

        private static Dictionary<string, Core.ShoeConfig> LoadShoeDatabase()
        {
            var database = new Dictionary<string, Core.ShoeConfig>(StringComparer.OrdinalIgnoreCase);

            var shoeConfigs = Directory.GetFiles(ShoeConfigPath, "hhmod_*.json", SearchOption.AllDirectories);

            foreach (var configPath in shoeConfigs)
            {
                try
                {
                    var key = Path.GetFileNameWithoutExtension(configPath);

                    if (database.ContainsKey(key))
                    {
                        Instance!.Logger.LogWarning($"Duplicate configuration filename found: {configPath}. Skipping");
                        continue;
                    }

                    var configJson = File.ReadAllText(configPath);
                    database[key] = JsonConvert.DeserializeObject<Core.ShoeConfig>(configJson);
                }
                catch (Exception e)
                {
                    var errorVerb = e is IOException ? "load" : "parse";
                    Instance!.Logger.LogWarning($"Could not {errorVerb} '{configPath}' because: {e.Message}");
                }
            }

            return database;
        }

        private static void ExportConfiguration(Core.ShoeConfig config, string filename)
        {
            var sanitizedFilename = SanitizeFilename(filename.ToLowerInvariant());

            if (string.IsNullOrEmpty(sanitizedFilename)) sanitizedFilename = "hhmod_configuration";
            else if (!sanitizedFilename.StartsWith("hhmod_")) sanitizedFilename = "hhmod_" + sanitizedFilename;

            var fullPath = Path.Combine(ShoeConfigPath, sanitizedFilename);

            //if (File.Exists(fullPath + ".json")) fullPath += $"{DateTime.Now:yyyyMMddHHmmss}";

            var jsonText = JsonConvert.SerializeObject(config, Formatting.Indented);

            File.WriteAllText(fullPath + ".json", jsonText);

            static string SanitizeFilename(string path)
            {
                var invalid = Path.GetInvalidFileNameChars();
                path = path.Trim();
                return string.Join("_", path.Split(invalid)).Replace(".", "").Trim('_');
            }
        }

        private static void ImportConfiguration(ref Core.ShoeConfig config, string filename)
        {
            var sanitizedFilename = SanitizeFilename(filename.ToLowerInvariant());

            if (string.IsNullOrEmpty(sanitizedFilename)) sanitizedFilename = "hhmod_configuration";
            else if (!sanitizedFilename.StartsWith("hhmod_")) sanitizedFilename = "hhmod_" + sanitizedFilename;

            var fullPath = Path.Combine(ShoeConfigPath, sanitizedFilename);

            //if (File.Exists(fullPath + ".json")) fullPath += $"{DateTime.Now:yyyyMMddHHmmss}";

            //var jsonText = JsonConvert.SerializeObject(config, Formatting.Indented);

            //File.WriteAllText(fullPath + ".json", jsonText);

            if (!File.Exists(fullPath + ".json")) return;

            string jsonText = File.ReadAllText(fullPath + ".json");

            config = JsonConvert.DeserializeObject<Core.ShoeConfig>(jsonText);

            static string SanitizeFilename(string path)
            {
                var invalid = Path.GetInvalidFileNameChars();
                path = path.Trim();
                return string.Join("_", path.Split(invalid)).Replace(".", "").Trim('_');
            }            
        }
    }
}
