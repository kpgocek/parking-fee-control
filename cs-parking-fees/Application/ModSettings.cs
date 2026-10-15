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
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

// The game's settings store keys off this type's identity; keep it in the root namespace.
namespace ParkingFeeControl
{
    /// <summary>
    /// Settings for Parking Fee Control mod that are managed via in-game UI.
    /// These are NOT persisted to parking-config.json.
    /// </summary>
    [FileLocation("ModsSettings/ParkingFeeControl/Settings")]
    [SettingsUITabOrder("Settings")]
    [SettingsUIGroupOrder("General", "About")]
    [SettingsUIShowGroupName("General", "About")]
    public class ModSettings(IMod mod) : ModSetting(mod)
    {
        public override void SetDefaults()
        {
            Enabled = true;
            DebugLogging = false;
            UpdateFrequencyMinutes = UpdateFrequency.Minutes5;
            IgnoreTag = IgnoreTagType.Npf;
        }

        // Setters must stay public: the Options UI and AssetDatabase write these properties.
        [SettingsUISection("Settings", "General")]
        public bool Enabled { get; set; } = true;

        [SettingsUISection("Settings", "General")]
        public bool DebugLogging { get; set; }

        [SettingsUISection("Settings", "General")]
        public UpdateFrequency UpdateFrequencyMinutes { get; set; } = UpdateFrequency.Minutes5;

        [SettingsUISection("Settings", "General")]
        public IgnoreTagType IgnoreTag { get; set; } = IgnoreTagType.Npf;

        // Must be an instance property: the settings UI only reflects instance members.
        [SettingsUISection("Settings", "About")]
#pragma warning disable CA1822 // Mark members as static
        public string Version => Mod.ModVersion;
#pragma warning restore CA1822

        /// <summary>
        /// Get update frequency in seconds.
        /// </summary>
        public int GetUpdateFrequencySeconds() => (int)UpdateFrequencyMinutes * 60;

        /// <summary>
        /// Check if building should be ignored based on custom name.
        /// </summary>
        public bool ShouldIgnoreByName(string customName)
        {
            if (string.IsNullOrEmpty(customName))
            {
                return false;
            }

            var tag = GetIgnoreTagString();

            return !string.IsNullOrEmpty(tag) && customName.Contains(tag, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the ignore tag as string.
        /// </summary>
        public string GetIgnoreTagString()
        {
            return IgnoreTag switch
            {
                IgnoreTagType.Npf => "[npf]",
                IgnoreTagType.Nofee => "[nofee]",
                IgnoreTagType.PipeNpf => "|npf|",
                _ => "[npf]"
            };
        }

        /// <summary>
        /// Update frequency enum.
        /// </summary>
        public enum UpdateFrequency
        {
            Minutes1 = 1,
            Minutes3 = 3,
            Minutes5 = 5,
            Minutes10 = 10,
            Minutes15 = 15,
            Minutes30 = 30,
            Minutes60 = 60
        }

        /// <summary>
        /// Ignore tag type enum.
        /// </summary>
        public enum IgnoreTagType
        {
            Npf,
            Nofee,
            PipeNpf
        }
    }
}
