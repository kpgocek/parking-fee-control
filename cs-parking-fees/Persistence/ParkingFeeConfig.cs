// This file is part of Parking Fee Control mod.
// Copyright (C) 2026 thiago-rcarvalho
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ParkingFeeControl.Diagnostics;

namespace ParkingFeeControl.Persistence
{
    /// <summary>
    /// Configuration class for Parking Fee Control mod.
    /// Only contains fee settings that are persisted to JSON.
    /// </summary>
    public class ParkingFeeConfig
    {
        public const string DistrictsCategoryType = "districts";
        public class PrefabEntry
        {
            [JsonProperty("name")]
            public string Name
            {
                get; set;
            }

            // Optional override fee for this prefab
            [JsonProperty("fee")]
            public int? Fee
            {
                get; set;
            }

            public PrefabEntry(string name, int? fee = null)
            {
                Name = name;
                Fee = fee;
            }
        }

        public class Category
        {
            [JsonProperty("type")]
            public string Type
            {
                get; set;
            }

            [JsonProperty("icon")]
            public string? Icon
            {
                get; set;
            }

            [JsonProperty("defaultFee")]
            public int DefaultFee
            {
                get; set;
            }

            [JsonProperty("prefabs")]
            public List<PrefabEntry> Prefabs { get; set; } = new List<PrefabEntry>();

            public Category(string type, int defaultFee, string? icon = null)
            {
                Type = type;
                DefaultFee = defaultFee;
                Icon = icon;
            }
        }

        [JsonProperty("categories")]
        public List<Category> Categories { get; set; } = new List<Category>();

        /// <summary>
        /// Create a new ParkingFeeConfig with default categories.
        /// </summary>
        public static ParkingFeeConfig CreateDefault()
        {
            return new ParkingFeeConfig
            {
                Categories = new List<Category>
                {
                    new("car", 10) {
                        Prefabs = new()
                        {
                            new("AutomatedParkingBuilding01"),
                            new("ParkingHall01"),
                            new("ParkingHall02"),
                            new("ParkingHall03"),
                            new("ParkingHall04"),
                            new("ParkingLot01"),
                            new("ParkingLot02"),
                            new("ParkingLot03"),
                            new("ParkingLot04"),
                            new("ParkingLot06"),
                            new("ParkingLot07"),
                            new("ParkingLot08"),
                            new("ParkingLot09"),
                            new("ParkingLot10"),
                            new("ParkingLot11"),
                            new("ParkingLot12"),
                            new("ParkingLot13"),
                            new("ParkingLot14"),
                            new("ParkingLot15"),
                            new("ParkingLot16"),
                            new("ParkingLot17"),
                        },
                    },
                    new("bicycle", 0) {
                        Prefabs = new()
                        {
                            new("BicycleParkingArea01") { Name = "BicycleParkingArea01" },
                            new("BicycleParkingArea02") { Name = "BicycleParkingArea02" },
                            new("BicycleParkingArea03") { Name = "BicycleParkingArea03" },
                            new("BicycleParkingHall01") { Name = "BicycleParkingHall01" },
                            new("BicycleParkingHall02") { Name = "BicycleParkingHall02" },
                            new("BicycleParkingHall03") { Name = "BicycleParkingHall03" },
                            new("BicycleStorage01") { Name = "BicycleStorage01" },
                            new("BicycleStorage02") { Name = "BicycleStorage02" },
                            new("BicycleStorage03") { Name = "BicycleStorage03" },
                        },
                    },
                    new("motorcycle", 10) {
                        Prefabs = new()
                        {
                            new("ParkingLot05"),
                        },
                    }
                }
            };
        }

        /// <summary>
        /// Load configuration from JSON file.
        /// </summary>
        /// <param name="showLog">Force showing filter log even if DebugLogging is disabled</param>
        public static ParkingFeeConfig Load(bool showLog = false)
        {
            try
            {
                var configPath = Path.Combine(Mod.ModPath, "parking-config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<ParkingFeeConfig>(json);
                    if (config != null)
                    {
                        // Merge with shipping/embedded parking-data so updates can add new prefabs/categories
                        var parkingData = ParkingDataLoader.Load(Mod.ModPath, showLog);
                        if (parkingData != null)
                        {
                            var changed = MergeWithParkingData(config, parkingData);
                            if (changed)
                            {
                                config.Save();
                                ModLogger.Debug("parking-config.json updated with new entries from parking-data.json");
                            }
                        }

                        ModLogger.Debug("Configuration loaded from JSON file");

                        return config;
                    }
                }
                else
                {
                    ModLogger.Debug("Configuration file not found, creating default from parking-data.json if available");

                    // Try to create a default config from parking-data.json (allows adding supported prefabs without code changes)
                    var parkingData = ParkingDataLoader.Load(Mod.ModPath, showLog);
                    ParkingFeeConfig defaultConfig;
                    if (parkingData != null && parkingData.Categories.Count > 0)
                    {
                        defaultConfig = new ParkingFeeConfig();
                        foreach (var cat in parkingData.Categories)
                        {
                            var newCat = new Category(cat.Type, cat.DefaultFee, cat.Icon);

                            foreach (var prefab in cat.Prefabs)
                            {
                                newCat.Prefabs.Add(new PrefabEntry(prefab.Name));
                            }

                            defaultConfig.Categories.Add(newCat);
                        }
                        defaultConfig.Save();
                        ModLogger.Debug("Created default configuration from parking-data.json");
                    }
                    else
                    {
                        defaultConfig = CreateDefault();
                        defaultConfig.Save();
                        ModLogger.Debug("Created default configuration from embedded defaults");
                    }

                    return defaultConfig;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Failed to load configuration: {ex.Message}");
            }

            return CreateDefault();
        }

        /// <summary>
        /// Save configuration to JSON file.
        /// </summary>
        public void Save()
        {
            try
            {
                var configPath = Path.Combine(Mod.ModPath, "parking-config.json");
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Failed to save configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Log current configuration settings.
        /// </summary>
        public void LogSettings()
        {
            if (Mod.Settings?.DebugLogging != true)
            {
                return;
            }

            ModLogger.Debug("Configuration settings:");
            LogGeneralSettings();
            LogFeeSettings();
        }

        private static void LogGeneralSettings()
        {
            try
            {
                var settings = Mod.Settings;
                if (settings != null)
                {
                    ModLogger.Debug($"  - Enabled: {settings.Enabled}");
                    ModLogger.Debug($"  - DebugLogging: {settings.DebugLogging}");
                    ModLogger.Debug($"  - UpdateFrequency: {settings.UpdateFrequencyMinutes} ({settings.GetUpdateFrequencySeconds()}s)");
                    ModLogger.Debug($"  - IgnoreTag: {settings.GetIgnoreTagString()}");
                }
                else
                {
                    ModLogger.Debug("  - General settings: <not initialized>");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"Failed to log general settings: {ex.Message}");
            }
        }

        private void LogFeeSettings()
        {
            ModLogger.Debug("  - Categories:");
            foreach (var cat in Categories)
            {
                try
                {
                    ModLogger.Debug($"    - {cat.Type}: default ${cat.DefaultFee}, prefabs: {string.Join(", ", cat.Prefabs.Select(p => p.Name))}");
                }
                catch (Exception ex)
                {
                    ModLogger.Warn($"Failed to log category '{cat?.Type ?? "<null>"}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get the parking fee for a specific prefab name. If a prefab-specific fee exists, return it; otherwise use the category default.
        /// </summary>
        public int GetParkingFeeForPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return 10; // fallback default
            }

            foreach (var cat in Categories)
            {
                // Look for prefab entry
                var match = cat.Prefabs.FirstOrDefault(p => string.Equals(p.Name, prefabName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match.Fee ?? cat.DefaultFee;
                }
            }

            // No category matched - return fallback default
            return 10;
        }

        /// <summary>
        /// Get the default parking fee for the districts category.
        /// Used as fallback for districts without a DistrictParkingFee ECS component.
        /// </summary>
        public int GetDistrictDefaultFee()
        {
            var category = Categories.FirstOrDefault(c => string.Equals(c.Type, DistrictsCategoryType, StringComparison.OrdinalIgnoreCase));
            return category?.DefaultFee ?? 0;
        }

        /// <summary>
        /// Create a deep clone of the configuration for UI binding.
        /// </summary>
        private static bool MergeWithParkingData(ParkingFeeConfig config, ParkingDataLoader.ParkingData parkingData)
        {
            var changed = false;

            // Build lookup for incoming data (case-insensitive)
            var dataCatsByType = parkingData.Categories
                .ToDictionary(dc => dc.Type, StringComparer.OrdinalIgnoreCase);

            // Remove categories that no longer exist in parking-data
            var toRemoveCats = config.Categories
                .Where(c => !dataCatsByType.ContainsKey(c.Type))
                .ToList();
            foreach (var rem in toRemoveCats)
            {
                config.Categories.Remove(rem);
                changed = true;
            }

            // Sync existing categories and add new ones
            foreach (var dataCat in parkingData.Categories)
            {
                var existingCat = config.Categories.FirstOrDefault(c => string.Equals(c.Type, dataCat.Type, StringComparison.OrdinalIgnoreCase));
                if (existingCat == null)
                {
                    // New category -> add with prefabs (no fees)
                    var newCat = new Category(dataCat.Type, dataCat.DefaultFee, dataCat.Icon)
                    {
                        Prefabs = dataCat.Prefabs.Select(p => new PrefabEntry(p.Name)).ToList()
                    };

                    config.Categories.Add(newCat);
                    changed = true;
                    continue;
                }

                // Do not override user-configured default fee for existing categories

                if (!string.Equals(existingCat.Icon, dataCat.Icon, StringComparison.OrdinalIgnoreCase))
                {
                    existingCat.Icon = dataCat.Icon ?? string.Empty;
                    changed = true;
                }

                if (!string.Equals(existingCat.Type, DistrictsCategoryType, StringComparison.OrdinalIgnoreCase))
                {
                    // Build set of allowed prefabs for this category (already filtered by installed mods)
                    HashSet<string> allowedPrefabs = new(dataCat.PrefabNames, StringComparer.OrdinalIgnoreCase);

                    // Remove prefabs that authors removed from parking-data (even if user changed fee)
                    var prefabsToRemove = existingCat.Prefabs.Where(p => !allowedPrefabs.Contains(p.Name)).ToList();
                    foreach (var rem in prefabsToRemove)
                    {
                        existingCat.Prefabs.Remove(rem);
                        changed = true;
                    }

                    // Add new prefabs present in parking-data but missing in config
                    foreach (var prefab in dataCat.Prefabs)
                    {
                        if (!existingCat.Prefabs.Any(p => string.Equals(p.Name, prefab.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            existingCat.Prefabs.Add(new PrefabEntry(prefab.Name));
                            changed = true;
                        }
                    }
                }
            }

            return changed;
        }

    }
}
