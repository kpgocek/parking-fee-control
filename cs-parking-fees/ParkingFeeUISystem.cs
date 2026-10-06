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
using System.Linq;
using System.Reflection;
using System.Text;
using Colossal.UI.Binding;
using Game.Areas;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;

namespace ParkingFeeControl.UI
{
    public class ParkingFeeUIData : IJsonWritable
    {
        public List<CategoryData> Categories { get; set; } = new List<CategoryData>();

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("categories");
            writer.ArrayBegin(Categories.Count);
            foreach (var category in Categories)
            {
                category.Write(writer);
            }
            writer.ArrayEnd();
            writer.TypeEnd();
        }

        public class CategoryData : IJsonWritable
        {
            public string Type { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public float DefaultFee
            {
                get; set;
            }
            public List<PrefabData> Prefabs { get; set; } = new List<PrefabData>();

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(GetType().FullName);
                writer.PropertyName("type");
                writer.Write(Type);
                writer.PropertyName("icon");
                writer.Write(Icon);
                writer.PropertyName("defaultFee");
                writer.Write(DefaultFee);
                writer.PropertyName("prefabs");
                writer.ArrayBegin(Prefabs.Count);
                foreach (var prefab in Prefabs)
                {
                    prefab.Write(writer);
                }
                writer.ArrayEnd();
                writer.TypeEnd();
            }
        }

        public class PrefabData : IJsonWritable
        {
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Thumbnail { get; set; } = string.Empty;
            public float Fee
            {
                get; set;
            }

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin(GetType().FullName);
                writer.PropertyName("name");
                writer.Write(Name);
                writer.PropertyName("displayName");
                writer.Write(DisplayName);
                writer.PropertyName("thumbnail");
                writer.Write(Thumbnail);
                writer.PropertyName("fee");
                writer.Write(Fee);
                writer.TypeEnd();
            }
        }
    }

    public struct CategoryFeeUpdate : IJsonReadable
    {
        public string categoryType;
        public float newFee;

        public void Read(IJsonReader reader)
        {
            _ = reader.ReadMapBegin();

            if (reader.ReadProperty("categoryType"))
            {
                reader.Read(out categoryType);
            }

            if (reader.ReadProperty("newFee"))
            {
                reader.Read(out newFee);
            }

            reader.ReadMapEnd();
        }
    }

    public struct PrefabFeeUpdate : IJsonReadable
    {
        public string categoryType;
        public string prefabName;
        public float newFee;

        public void Read(IJsonReader reader)
        {
            _ = reader.ReadMapBegin();

            if (reader.ReadProperty("categoryType"))
            {
                reader.Read(out categoryType);
            }

            if (reader.ReadProperty("prefabName"))
            {
                reader.Read(out prefabName);
            }

            if (reader.ReadProperty("newFee"))
            {
                reader.Read(out newFee);
            }

            reader.ReadMapEnd();
        }
    }

    public partial class ParkingFeeUISystem : UISystemBase
    {
        private const string DistrictPrefabIconPath = "Media/Game/Policies/PaidParking.svg";
        private const string DistrictEntityKeyPrefix = "district:";

        private const string DefaultFallbackIcon = "Media/Game/Icons/Parking.svg";

        private ValueBinding<ParkingFeeUIData> ConfigBinding => _configBinding ?? throw new InvalidOperationException("ConfigBinding is not initialized");
        private ValueBinding<ParkingFeeUIData>? _configBinding;

        private TriggerBinding<CategoryFeeUpdate>? _updateCategoryFeeTrigger;
        private TriggerBinding<PrefabFeeUpdate>? _updatePrefabFeeTrigger;
        private TriggerBinding? _applyNowTrigger;
        private TriggerBinding? _refreshConfigTrigger;

        private ParkingFeeUIData CurrentConfig => _currentConfig ?? throw new InvalidOperationException("CurrentConfig is not initialized");
        private ParkingFeeUIData? _currentConfig;

        private PrefabSystem PrefabSystem => _prefabSystem ?? throw new InvalidOperationException("PrefabSystem not initialized");
        private PrefabSystem? _prefabSystem;

        private PrefabUISystem PrefabUISystem => _prefabUISystem ?? throw new InvalidOperationException("PrefabUISystem not initialized");
        private PrefabUISystem? _prefabUISystem;

        private ImageSystem ImageSystem => _imageSystem ?? throw new InvalidOperationException("ImageSystem not initialized");
        private ImageSystem? _imageSystem;

        private ParkingPolicyModifierSystem PolicySystem => _policySystem ?? throw new InvalidOperationException("ParkingPolicyModifierSystem not initialized");
        private ParkingPolicyModifierSystem? _policySystem;

        private Dictionary<string, PrefabBase> PrefabByNameCache => _prefabByNameCache ?? throw new InvalidOperationException("PrefabByNameCache not initialized");
        private Dictionary<string, PrefabBase>? _prefabByNameCache;

        private EntityQuery _districtQuery;

        private NameSystem NameSystem => _nameSystem ?? throw new InvalidOperationException("NameSystem not initialized");
        private NameSystem? _nameSystem;

        /// <summary>
        /// Maps Entity.Index to Entity for district fee lookups within the current session.
        /// Rebuilt on each UI refresh. Not persisted — entity handles are session-stable.
        /// </summary>
        private readonly Dictionary<int, Entity> _districtEntityMap = new();

        protected override void OnCreate()
        {
            base.OnCreate();

            // Get required systems
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _prefabUISystem = World.GetOrCreateSystemManaged<PrefabUISystem>();
            _imageSystem = World.GetOrCreateSystemManaged<ImageSystem>();
            _policySystem = World.GetOrCreateSystemManaged<ParkingPolicyModifierSystem>();
            _nameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            _districtQuery = GetEntityQuery(
                ComponentType.ReadOnly<District>()
            );

            LoadConfigFromMod();

            AddBinding(_configBinding = new ValueBinding<ParkingFeeUIData>(
                "parkingfee",
                "config",
                CurrentConfig
            ));

            AddBinding(_updateCategoryFeeTrigger = new TriggerBinding<CategoryFeeUpdate>(
                "parkingfee",
                "updateCategoryFee",
                UpdateCategoryFee
            ));

            AddBinding(_updatePrefabFeeTrigger = new TriggerBinding<PrefabFeeUpdate>(
                "parkingfee",
                "updatePrefabFee",
                UpdatePrefabFee
            ));

            AddBinding(_applyNowTrigger = new TriggerBinding(
                "parkingfee",
                "applyNow",
                ApplyNow
            ));

            AddBinding(_refreshConfigTrigger = new TriggerBinding(
                "parkingfee",
                "refreshConfig",
                RefreshConfigFromMod
            ));

            ModLogger.Info("UI System initialized");
        }

        private void LoadConfigFromMod()
        {
            // Initialize _currentConfig with data from Mod.Config
            // This ensures the binding has a valid object to work with
            var config = Mod.Config;
            _currentConfig = new ParkingFeeUIData
            {
                Categories = config.Categories.Select(c =>
                {
                    List<ParkingFeeUIData.PrefabData> prefabList;

                    prefabList =
                        string.Equals(c.Type, ParkingFeeConfig.DistrictsCategoryType, StringComparison.OrdinalIgnoreCase)
                        ? BuildDistrictPrefabData(c)
                        : c.Prefabs.Select(p => new ParkingFeeUIData.PrefabData
                        {
                            Name = p.Name,
                            DisplayName = GetDisplayName(p.Name),
                            Thumbnail = GetThumbnail(p.Name),
                            Fee = p.Fee ?? c.DefaultFee
                        }).ToList();

                    var sorted = prefabList.OrderBy(p => p.DisplayName ?? p.Name, StringComparer.OrdinalIgnoreCase).ToList();

                    return new ParkingFeeUIData.CategoryData
                    {
                        Type = c.Type,
                        Icon = c.Icon ?? string.Empty,
                        DefaultFee = c.DefaultFee,
                        Prefabs = sorted
                    };
                }).ToList()
            };
        }

        /// <summary>
        /// Builds UI data for all districts. Fee is read from the DistrictParkingFee
        /// ECS component on each entity (persisted in the save file). Districts without
        /// the component use the category default fee.
        /// </summary>
        private List<ParkingFeeUIData.PrefabData> BuildDistrictPrefabData(ParkingFeeConfig.Category category)
        {
            var result = new List<ParkingFeeUIData.PrefabData>();
            _districtEntityMap.Clear();

            if (_districtQuery == null)
            {
                return result;
            }

            var districts = _districtQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var district in districts)
                {
                    _districtEntityMap[district.Index] = district;

                    var displayName = GetDistrictDisplayName(district);
                    var entityKey = $"{DistrictEntityKeyPrefix}{district.Index}";

                    var hasComponent = EntityManager.HasComponent<DistrictParkingFee>(district);
                    var fee = hasComponent
                        ? EntityManager.GetComponentData<DistrictParkingFee>(district).m_Fee
                        : category.DefaultFee;

                    ModLogger.Debug($"  [UI] District '{displayName}' (#{district.Index}): hasComponent={hasComponent}, fee=${fee}{(hasComponent ? "" : " (default)")}");

                    result.Add(new ParkingFeeUIData.PrefabData
                    {
                        Name = entityKey,
                        DisplayName = displayName,
                        Thumbnail = DistrictPrefabIconPath,
                        Fee = fee
                    });
                }
            }
            finally
            {
                districts.Dispose();
            }

            return result;
        }

        private string GetDistrictDisplayName(Entity district)
        {
            try
            {
                if (_nameSystem != null)
                {
                    return _nameSystem.GetRenderedLabelName(district);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Failed to resolve district display name for entity #{district.Index}: {ex}");
            }

            return $"District #{district.Index}";
        }

        private void RefreshConfigFromMod()
        {
            try
            {
                Mod.ReloadConfig();
                _prefabByNameCache = null;
                LoadConfigFromMod();
                NotifyConfigChanged();

                ModLogger.Debug("UI config refreshed on panel open");
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"Failed to refresh UI config: {ex.Message}");
            }
        }

        private void ApplyNow()
        {
            try
            {
                PolicySystem.ApplyNow();
                ModLogger.Debug("Apply now requested");
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"Failed to apply fees immediately: {ex.Message}");
            }
        }

        private void NotifyConfigChanged()
        {
            _currentConfig = CloneConfig(CurrentConfig);
            ConfigBinding.Update(CurrentConfig);
        }

        private static ParkingFeeUIData CloneConfig(ParkingFeeUIData source)
        {
            return new ParkingFeeUIData
            {
                Categories = source.Categories.Select(c => new ParkingFeeUIData.CategoryData
                {
                    Type = c.Type,
                    Icon = c.Icon,
                    DefaultFee = c.DefaultFee,
                    Prefabs = c.Prefabs.Select(p => new ParkingFeeUIData.PrefabData
                    {
                        Name = p.Name,
                        DisplayName = p.DisplayName,
                        Thumbnail = p.Thumbnail,
                        Fee = p.Fee
                    }).ToList()
                }).ToList()
            };
        }

        private void UpdateCategoryFee(CategoryFeeUpdate update)
        {
            var category = CurrentConfig.Categories.FirstOrDefault(c => c.Type == update.categoryType);
            if (category == null)
            {
                return;
            }

            var oldDefaultFee = category.DefaultFee;

            // Update category default fee
            category.DefaultFee = (float)Math.Round(update.newFee);

            // Update all prefabs in this category maintaining the difference from category
            foreach (var prefab in category.Prefabs)
            {
                var difference = oldDefaultFee - prefab.Fee;
                var newFee = update.newFee - difference;
                // Clamp between 0 and 50
                newFee = Math.Max(0, Math.Min(50, newFee));
                prefab.Fee = (float)Math.Round(newFee);
            }

            NotifyConfigChanged();

            // Districts: persist fees to ECS components on the entity (saved with the game)
            if (IsDistrictsCategory(update.categoryType))
            {
                foreach (var prefab in category.Prefabs)
                {
                    if (TryParseDistrictEntityIndex(prefab.Name, out var entityIndex))
                    {
                        SetDistrictFee(entityIndex, (int)Math.Round(prefab.Fee));
                    }
                }

                // Save only the default fee to config (for newly created districts)
                var districtCategory = Mod.Config.Categories.FirstOrDefault(c =>
                    string.Equals(c.Type, ParkingFeeConfig.DistrictsCategoryType, StringComparison.OrdinalIgnoreCase));
                if (districtCategory != null)
                {
                    districtCategory.DefaultFee = (int)Math.Round(update.newFee);
                }
                Mod.Config.Save();
                return;
            }

            // Non-district categories: persist per-prefab fees to JSON config
            var modCategory = Mod.Config.Categories.FirstOrDefault(c => c.Type == update.categoryType);
            if (modCategory != null)
            {
                modCategory.DefaultFee = (int)Math.Round(update.newFee);

                // Update prefabs in mod config maintaining the difference
                foreach (var modPrefab in modCategory.Prefabs)
                {
                    if (modPrefab.Fee.HasValue)
                    {
                        var difference = oldDefaultFee - modPrefab.Fee.Value;
                        var newFee = update.newFee - difference;
                        // Clamp between 0 and 50
                        newFee = Math.Max(0, Math.Min(50, newFee));
                        modPrefab.Fee = (int)Math.Round(newFee);
                    }
                }
            }

            // Save config after changes
            Mod.Config.Save();
        }

        private void UpdatePrefabFee(PrefabFeeUpdate update)
        {
            var category = CurrentConfig.Categories.FirstOrDefault(c => c.Type == update.categoryType);
            if (category == null)
            {
                return;
            }

            var prefab = category.Prefabs.FirstOrDefault(p => p.Name == update.prefabName);
            if (prefab == null)
            {
                return;
            }

            prefab.Fee = (float)Math.Round(update.newFee);
            NotifyConfigChanged();

            // Districts: persist fee to ECS component on the entity (saved with the game)
            if (IsDistrictsCategory(update.categoryType))
            {
                if (TryParseDistrictEntityIndex(update.prefabName, out var entityIndex))
                {
                    SetDistrictFee(entityIndex, (int)Math.Round(update.newFee));
                }
                return;
            }

            // Non-district: persist to JSON config
            var modCategory = Mod.Config.Categories.FirstOrDefault(c => c.Type == update.categoryType);
            if (modCategory != null)
            {
                var modPrefab = modCategory.Prefabs.FirstOrDefault(p => p.Name == update.prefabName);
                if (modPrefab != null)
                {
                    modPrefab.Fee = (int)Math.Round(update.newFee);
                }
            }

            // Save config after changes
            Mod.Config.Save();
        }

        // SaveConfig method intentionally removed; saving is handled via Mod.Config.Save()

        protected override void OnUpdate()
        {
            // UI System doesn't need constant updates
        }

        /// <summary>
        /// Gets the localized display name for a prefab, falling back to a
        /// friendly rendering of the raw prefab name when no localization entry exists.
        /// </summary>
        /// <param name="prefabName">The internal prefab name</param>
        /// <returns>The localized display name, or the original name if localization fails</returns>
        private string GetDisplayName(string prefabName)
        {
            var prefabBase = SafeResolvePrefab(prefabName);
            if (prefabBase == null)
            {
                return ToFriendlyName(prefabName);
            }

            var localizedTitle = TryGetLocalizedTitle(prefabBase);
            return localizedTitle ?? ToFriendlyName(prefabBase.name);
        }

        private string? TryGetLocalizedTitle(PrefabBase prefabBase)
        {
            try
            {
                var entity = PrefabSystem.GetEntity(prefabBase);
                PrefabUISystem.GetTitleAndDescription(entity, out var titleId, out _);

                var dictionary = GameManager.instance.localizationManager.activeDictionary;
                return dictionary.TryGetValue(titleId, out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : null;
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Failed to resolve localized display name for prefab '{prefabBase.name}': {ex}");
                return null;
            }
        }

        /// <summary>
        /// Gets the thumbnail image path for a prefab using the game's ImageSystem,
        /// falling back to the prefab group icon and finally to a mod-provided icon.
        /// </summary>
        /// <param name="prefabName">The internal prefab name</param>
        /// <returns>The thumbnail image path, or the default fallback icon if not found</returns>
        private string GetThumbnail(string prefabName)
        {

            var prefabBase = SafeResolvePrefab(prefabName);

            return
                prefabBase == null
                ? DefaultFallbackIcon
                : TryGetPrefabThumbnail(prefabBase) ?? TryGetPrefabGroupIcon(prefabBase) ?? DefaultFallbackIcon;
        }

        private static string? TryGetPrefabThumbnail(PrefabBase prefabBase)
        {
            try
            {
                var thumbnail = ImageSystem.GetThumbnail(prefabBase);
                return !string.IsNullOrEmpty(thumbnail) && !IsPlaceholderThumbnail(thumbnail) ? thumbnail : null;
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Failed to resolve thumbnail for prefab '{prefabBase.name}': {ex}");
                return null;
            }
        }

        private string? TryGetPrefabGroupIcon(PrefabBase prefabBase)
        {
            if (_imageSystem == null)
            {
                return null;
            }

            try
            {
                var entity = PrefabSystem.GetEntity(prefabBase);
                var groupIcon = ImageSystem.GetGroupIcon(entity);
                return !string.IsNullOrEmpty(groupIcon) ? groupIcon : null;
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Failed to resolve group icon for prefab '{prefabBase.name}': {ex}");
                return null;
            }
        }

        private PrefabBase? SafeResolvePrefab(string prefabName)
        {
            try
            {
                return TryResolvePrefabByName(prefabName);
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Failed to resolve prefab '{prefabName}': {ex}");
                return null;
            }
        }


        private static bool IsPlaceholderThumbnail(string value) => string.Equals(value, "Media/Placeholder.svg", StringComparison.OrdinalIgnoreCase);

        private PrefabBase? TryResolvePrefabByName(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName) || _prefabSystem == null)
            {
                return null;
            }

            EnsurePrefabCache();

            if (_prefabByNameCache != null && _prefabByNameCache.TryGetValue(prefabName, out var prefab))
            {
                return prefab;
            }

            if (_prefabByNameCache != null && _prefabByNameCache.Count == 0)
            {
                _prefabByNameCache = null;
                EnsurePrefabCache();

                if (_prefabByNameCache != null && _prefabByNameCache.TryGetValue(prefabName, out prefab))
                {
                    return prefab;
                }
            }

            return null;
        }

        private void EnsurePrefabCache()
        {
            if (_prefabByNameCache != null || _prefabSystem == null)
            {
                return;
            }

            _prefabByNameCache = new Dictionary<string, PrefabBase>(StringComparer.OrdinalIgnoreCase);

            try
            {
                IEnumerable<PrefabBase>? prefabs = null;

                var prefabsProperty = typeof(PrefabSystem).GetProperty("prefabs", BindingFlags.Instance | BindingFlags.NonPublic);
                if (prefabsProperty?.GetValue(_prefabSystem) is IEnumerable<PrefabBase> propPrefabs)
                {
                    prefabs = propPrefabs;
                }
                else
                {
                    var prefabsField = typeof(PrefabSystem).GetField("m_Prefabs", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (prefabsField?.GetValue(_prefabSystem) is IEnumerable<PrefabBase> fieldPrefabs)
                    {
                        prefabs = fieldPrefabs;
                    }
                }

                if (prefabs == null)
                {
                    return;
                }

                foreach (var prefab in prefabs)
                {
                    if (prefab?.name == null)
                    {
                        continue;
                    }

                    if (!PrefabByNameCache.TryAdd(prefab.name, prefab))
                    {
                        ModLogger.Debug($"Duplicate prefab name detected in cache: '{prefab.name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Failed to build prefab cache via reflection: {ex}");
            }
        }

        // ── District ECS helpers ──────────────────────────────────────────────

        private static bool IsDistrictsCategory(string categoryType) => string.Equals(categoryType, ParkingFeeConfig.DistrictsCategoryType, StringComparison.OrdinalIgnoreCase);

        private static bool TryParseDistrictEntityIndex(string key, out int entityIndex)
        {
            entityIndex = 0;

            return
                !string.IsNullOrEmpty(key)
                && key.StartsWith(DistrictEntityKeyPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(key.AsSpan(DistrictEntityKeyPrefix.Length), out entityIndex);
        }

        /// <summary>
        /// Writes or updates the DistrictParkingFee component on a district entity.
        /// </summary>
        private void SetDistrictFee(int entityIndex, int fee)
        {
            if (!_districtEntityMap.TryGetValue(entityIndex, out var entity))
            {
                return;
            }

            var districtName = GetDistrictDisplayName(entity);
            var feeComponent = new DistrictParkingFee(fee);
            if (EntityManager.HasComponent<DistrictParkingFee>(entity))
            {
                EntityManager.SetComponentData(entity, feeComponent);
                ModLogger.Debug($"  District '{districtName}' (#{entityIndex}): updated fee to ${fee}");
            }
            else
            {
                if (EntityManager.AddComponentData(entity, feeComponent))
                {
                    ModLogger.Debug($"  District '{districtName}' (#{entityIndex}): added component with fee ${fee}");
                }
                else
                {

                    ModLogger.Warn($"  District '{districtName}' (#{entityIndex}): failed to add DistrictParkingFee component");
                }
            }
        }

        // ── Shared UI helpers ────────────────────────────────────────────────

        private static string ToFriendlyName(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return prefabName ?? string.Empty;
            }

            var sb = new StringBuilder(prefabName.Length + 8);
            var prev = '\0';

            foreach (var ch in prefabName)
            {
                if (ch is '_' or '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    {
                        sb = sb.Append(' ');
                    }
                    prev = ch;
                    continue;
                }

                if (sb.Length > 0)
                {
                    var addSpace = (char.IsUpper(ch) && (char.IsLower(prev) || char.IsDigit(prev)))
                        || (char.IsDigit(ch) && !char.IsDigit(prev) && prev != ' ')
                        || (char.IsLetter(ch) && char.IsDigit(prev));

                    if (addSpace && sb[sb.Length - 1] != ' ')
                    {
                        sb = sb.Append(' ');
                    }
                }

                sb = sb.Append(ch);
                prev = ch;
            }

            return sb.ToString().Trim();
        }
    }
}
