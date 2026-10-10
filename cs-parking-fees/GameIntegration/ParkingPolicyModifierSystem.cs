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
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Policies;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using ParkingFeeControl.Diagnostics;
using ParkingFeeControl.Persistence;

namespace ParkingFeeControl.GameIntegration
{
    /// <summary>
    /// System to modify parking policies on buildings (parking lots, facilities).
    /// Policies control the actual parking fees shown in UI.
    /// </summary>
    public partial class ParkingPolicyModifierSystem : SystemBase
    {
        // Known policy prefab name for parking fees
        private const string PARKING_FEE_POLICY_NAME = "Lot Parking Fee";
        private const string DISTRICT_PARKING_FEE_POLICY_NAME = "Roadside Parking Fee";

        // Queries
        private EntityQuery m_DistrictQuery;
        private EntityQuery m_CreatedDistrictQuery;
        private EntityQuery m_ParkingFacilityQuery;
        private EntityQuery m_CreatedParkingFacilityQuery;

        // Systems

        private PrefabSystem PrefabSystem => m_PrefabSystem ?? throw new ArgumentNullException(nameof(m_PrefabSystem));
        private PrefabSystem? m_PrefabSystem;

        private NameSystem NameSystem => m_NameSystem ?? throw new ArgumentNullException(nameof(m_NameSystem));
        private NameSystem? m_NameSystem;

        private PoliciesUISystem PoliciesUISystem => m_PoliciesUISystem ?? throw new ArgumentNullException(nameof(m_PoliciesUISystem));
        private PoliciesUISystem? m_PoliciesUISystem;

        // Requests
        private bool m_ParkingRefreshRequested;
        private bool m_DistrictRefreshRequested;

        // States
        private bool m_InitialSetupCompleted;

        // Cache of parking fee policy entity (obtained from prefab)
        private Entity m_ParkingFeePolicyEntity = Entity.Null;
        private Entity m_DistrictParkingFeePolicyEntity = Entity.Null;

        public void ApplyNow()
        {
            m_ParkingRefreshRequested = true;
            m_DistrictRefreshRequested = true;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            // Get PrefabSystem reference to access prefab names
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_PoliciesUISystem = World.GetOrCreateSystemManaged<PoliciesUISystem>();

            // buildings with ParkingFacility component and Policy buffer
            m_ParkingFacilityQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<Game.Buildings.ParkingFacility>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadWrite<Policy>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );

            m_CreatedParkingFacilityQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<Game.Buildings.ParkingFacility>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadWrite<Policy>(),
                ComponentType.ReadOnly<Created>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );

            // districts
            m_DistrictQuery = GetEntityQuery(
                ComponentType.ReadWrite<District>(),
                ComponentType.ReadWrite<Policy>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );

            m_CreatedDistrictQuery = GetEntityQuery(
                ComponentType.ReadWrite<District>(),
                ComponentType.ReadWrite<Policy>(),
                ComponentType.ReadOnly<Created>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );

            ModLogger.Info("ParkingPolicyModifierSystem created");
        }
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            if (m_InitialSetupCompleted)
            {
                return;
            }

            if (!EnsurePolicyEntities())
            {
                ModLogger.Warn("Parking policy prefabs are not available yet; will retry.");
            }

            ApplyNow();

            m_InitialSetupCompleted = true;
        }
        protected override void OnUpdate()
        {
            if (Mod.Settings?.Enabled != true)
            {
                return;
            }

            if (!EnsurePolicyEntities())
            {
                ModLogger.Warn("Parking policy prefabs are not available yet; will retry.");
            }

            if (m_ParkingFeePolicyEntity != Entity.Null)
            {
                ProcessCreatedParkingFacilities();

                if (m_ParkingRefreshRequested)
                {
                    ModifyAllParkingFacilities();
                    m_ParkingRefreshRequested = false;
                }
            }

            if (m_DistrictParkingFeePolicyEntity != Entity.Null)
            {
                ProcessCreatedDistricts();

                if (m_DistrictRefreshRequested)
                {
                    ModifyAllDistricts();
                    m_DistrictRefreshRequested = false;
                }
            }
        }

        private void ProcessCreatedParkingFacilities()
        {
            using var entities = m_CreatedParkingFacilityQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                ApplyParkingFacilityFee(entity);
            }
        }
        private void ProcessCreatedDistricts()
        {
            using var districts = m_CreatedDistrictQuery.ToEntityArray(Allocator.Temp);

            var defaultFee = Mod.Config.GetDistrictDefaultFee();

            foreach (var district in districts)
            {
                ApplyDistrictPolicy(district, defaultFee);
            }
        }

        private void ModifyAllParkingFacilities()
        {
            // If we don't have the policy entity, we cannot apply any changes
            if (m_ParkingFeePolicyEntity == Entity.Null)
            {
                return;
            }

            using var facilities = m_ParkingFacilityQuery.ToEntityArray(Allocator.Temp);

            foreach (var facility in facilities)
            {
                ApplyParkingFacilityFee(facility);
            }
        }
        private void ModifyAllDistricts()
        {
            // If we don't have the policy entity, we cannot apply any changes
            if (m_DistrictParkingFeePolicyEntity == Entity.Null)
            {
                return;
            }

            var defaultFee = Mod.Config.GetDistrictDefaultFee();

            using var districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);

            foreach (var district in districts)
            {
                ApplyDistrictPolicy(district, defaultFee);
            }
        }

        private void ApplyParkingFacilityFee(Entity entity)
        {
            if (ShouldIgnore(entity))
            {
                return;
            }

            var prefabName = GetPrefabName(entity);
            var fee = Mod.Config.GetParkingFeeForPrefab(prefabName);

            ApplyPolicy(
                entity,
                m_ParkingFeePolicyEntity,
                fee);
        }
        private void ApplyDistrictPolicy(Entity entity, int defaultFee)
        {
            if (ShouldIgnore(entity))
            {
                return;
            }

            var fee = EntityManager.HasComponent<DistrictParkingFee>(entity)
                ? EntityManager.GetComponentData<DistrictParkingFee>(entity).m_Fee
                : defaultFee;

            ApplyPolicy(
                entity,
                m_DistrictParkingFeePolicyEntity,
                fee);
        }

        private void ApplyPolicy(Entity entity, Entity policyEntity, int targetFee)
        {
            if (targetFee is < 0 or > 50)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetFee),
                    targetFee,
                    "Parking fee must be between 0 and 50.");
            }

            if (policyEntity == Entity.Null)
            {
                return;
            }

            if (!EntityManager.Exists(entity)
                || EntityManager.HasComponent<Deleted>(entity))
            {
                return;
            }

            var active = targetFee > 0;

            // Nieaktywna polityka reprezentuje opłatę 0.
            // Wartość samego suwaka polityki pozostaje w zakresie 1–50.
            var adjustment = Math.Max(1, targetFee);

            PoliciesUISystem.SetPolicy(
                entity,
                policyEntity,
                active,
                adjustment);
        }

        private string GetPrefabName(Entity buildingEntity)
        {
            try
            {
                var prefabRef =
                    EntityManager.GetComponentData<PrefabRef>(buildingEntity);

                var prefabData =
                    EntityManager.GetComponentData<PrefabData>(prefabRef.m_Prefab);

                return PrefabSystem
                           .GetPrefab<PrefabBase>(prefabData)
                           ?.name
                       ?? $"Prefab#{prefabData.m_Index}";
            }
            catch (Exception ex)
            {
                ModLogger.Debug(
                    $"Failed to resolve prefab for #{buildingEntity.Index}: {ex}");

                return "Unknown";
            }
        }

        private bool ShouldIgnore(Entity entity)
        {
            try
            {
                return NameSystem.TryGetCustomName(entity, out var customName)
                    && !string.IsNullOrWhiteSpace(customName)
                    && Mod.Settings.ShouldIgnoreByName(customName);
            }
            catch (Exception ex)
            {
                ModLogger.Debug(
                    $"Failed to read custom name for entity #{entity.Index}: {ex}");

                return false;
            }
        }

        /// <summary>
        /// Get the parking fee policy entity directly from the PrefabSystem.
        /// Uses PolicySliderPrefab type with the known policy name.
        /// </summary>
        private Entity GetParkingFeePolicyFromPrefab(string policyName)
        {
            try
            {
                var prefabId = new PrefabID(
                    "PolicySliderPrefab",
                    policyName);

                if (PrefabSystem.TryGetPrefab(prefabId, out var prefab) &&
                    PrefabSystem.TryGetEntity(prefab, out var entity))
                {
                    return entity;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(
                    $"Failed resolving policy prefab '{policyName}': {ex}");
            }

            return Entity.Null;
        }


        private bool EnsurePolicyEntities()
        {
            if (m_ParkingFeePolicyEntity == Entity.Null)
            {
                m_ParkingFeePolicyEntity =
                    GetParkingFeePolicyFromPrefab(PARKING_FEE_POLICY_NAME);

                if (m_ParkingFeePolicyEntity != Entity.Null)
                {
                    ModLogger.Info(
                        $"Found parking lot fee policy prefab: {m_ParkingFeePolicyEntity}");
                }
            }

            if (m_DistrictParkingFeePolicyEntity == Entity.Null)
            {
                m_DistrictParkingFeePolicyEntity =
                    GetParkingFeePolicyFromPrefab(DISTRICT_PARKING_FEE_POLICY_NAME);

                if (m_DistrictParkingFeePolicyEntity != Entity.Null)
                {
                    ModLogger.Info(
                        $"Found district parking fee policy prefab: {m_DistrictParkingFeePolicyEntity}");
                }
            }

            return
                m_ParkingFeePolicyEntity != Entity.Null &&
                m_DistrictParkingFeePolicyEntity != Entity.Null;
        }
    }
}
