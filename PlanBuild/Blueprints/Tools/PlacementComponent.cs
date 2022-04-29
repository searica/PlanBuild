﻿using Jotunn.Managers;
using PlanBuild.Plans;
using System;
using System.Linq;
using UnityEngine;

namespace PlanBuild.Blueprints.Tools
{
    internal class PlacementComponent : ToolComponentBase
    {
        public override void OnStart()
        {
            SuppressPieceHighlight = false;
            ResetPlacementOffset = false;
        }

        public override void OnUpdatePlacement(Player self)
        {
            DisableSelectionProjector();

            float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
            if (scrollWheel != 0f)
            {
                bool radiusModifier = ZInput.GetButton(Config.RadiusModifierButton.Name);
                bool deleteModifier = ZInput.GetButton(Config.DeleteModifierButton.Name);
                if (radiusModifier && deleteModifier)
                {
                    PlacementOffset.y += GetPlacementOffset(scrollWheel);
                    UndoRotation(self, scrollWheel);
                }
                else if (deleteModifier)
                {
                    PlacementOffset.x += GetPlacementOffset(scrollWheel);
                    UndoRotation(self, scrollWheel);
                }
                else if (radiusModifier)
                {
                    PlacementOffset.z += GetPlacementOffset(scrollWheel);
                    UndoRotation(self, scrollWheel);
                }
                else if (ZInput.GetButton(Config.CameraModifierButton.Name))
                {
                    UpdateCameraOffset(scrollWheel);
                    UndoRotation(self, scrollWheel);
                }
            }
        }

        public override void OnPlacePiece(Player self, Piece piece)
        {
            if (self.m_placementStatus == Player.PlacementStatus.Valid)
            {
                try
                {
                    PlaceBlueprint(self, piece);
                }
                catch (Exception ex)
                {
                    Jotunn.Logger.LogWarning($"Exception caught while placing {piece.gameObject.name}: {ex}\n{ex.StackTrace}");
                }
            }
        }

        private void PlaceBlueprint(Player player, Piece piece)
        {
            string id = piece.gameObject.name.Substring(Blueprint.PieceBlueprintName.Length + 1);
            Blueprint bp = BlueprintManager.LocalBlueprints[id];
            var transform = player.m_placementGhost.transform;
            var position = transform.position;
            var rotation = transform.rotation;

            bool placeDirect = Config.DirectBuildDefault;
            placeDirect ^= ZInput.GetButton(Config.RadiusModifierButton.Name);
            if (placeDirect
                && !Config.AllowDirectBuildConfig.Value
                && !SynchronizationManager.Instance.PlayerIsAdmin)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$msg_direct_build_disabled");
                return;
            }

            uint cntEffects = 0u;
            uint maxEffects = 10u;
            
            BlueprintInstance blueprintInstance = new BlueprintInstance(id);

            for (int i = 0; i < bp.PieceEntries.Length; i++)
            {
                PieceEntry entry = bp.PieceEntries[i];

                // Dont place an erroneously captured piece_blueprint
                if (entry.name == Blueprint.PieceBlueprintName)
                {
                    continue;
                }

                // Final position
                Vector3 entryPosition = transform.TransformPoint(entry.GetPosition());

                // Final rotation
                Quaternion entryQuat = transform.rotation * entry.GetRotation();

                // Dont place blacklisted pieces
                if (!SynchronizationManager.Instance.PlayerIsAdmin && PlanBlacklist.Contains(entry.name))
                {
                    Jotunn.Logger.LogWarning($"{entry.name} is blacklisted, not placing @{entryPosition}");
                    continue;
                }

                // Get the prefab of the piece or the plan piece
                string prefabName = entry.name;
                if (!placeDirect)
                {
                    prefabName += PlanPiecePrefab.PlannedSuffix;
                }

                GameObject prefab = PrefabManager.Instance.GetPrefab(prefabName);
                if (!prefab)
                {
                    Jotunn.Logger.LogWarning($"{prefabName} not found, you are probably missing a dependency for blueprint {bp.Name}, not placing @{entryPosition}");
                    continue;
                }

                // No Terrain stuff unless allowed
                if (!(SynchronizationManager.Instance.PlayerIsAdmin || Config.AllowTerrainmodConfig.Value)
                    && (prefab.GetComponent<TerrainModifier>() || prefab.GetComponent<TerrainOp>()))
                {
                    Jotunn.Logger.LogWarning("Flatten not allowed, not placing terrain modifiers");
                    continue;
                }

                // Instantiate a new object with the prefab
                GameObject gameObject = Instantiate(prefab, entryPosition, entryQuat);
                if (!gameObject)
                {
                    Jotunn.Logger.LogWarning($"Invalid PieceEntry: {entry.name}");
                    continue;
                }
                OnPiecePlaced(gameObject);

                ZNetView zNetView = gameObject.GetComponent<ZNetView>();
                if (!zNetView)
                {
                    Jotunn.Logger.LogWarning($"No ZNetView for {gameObject}!!??");
                }
                else
                {
                    if (!placeDirect)
                    {
                        zNetView.m_zdo.Set(Blueprint.AdditionalInfo, entry.additionalInfo);
                    }
                    blueprintInstance.AddZDOID(zNetView.m_zdo.m_uid);
                    zNetView.SetLocalScale(entry.GetScale());
                }

                // Register special effects
                CraftingStation craftingStation = gameObject.GetComponentInChildren<CraftingStation>();
                if (craftingStation)
                {
                    player.AddKnownStation(craftingStation);
                }
                Piece newpiece = gameObject.GetComponent<Piece>();
                if (newpiece)
                {
                    newpiece.SetCreator(player.GetPlayerID());

                    if (placeDirect && cntEffects < maxEffects)
                    {
                        newpiece.m_placeEffect.Create(gameObject.transform.position, rotation, gameObject.transform);
                        player.AddNoise(50f);
                        cntEffects++;
                    }

                    if (placeDirect)
                    {
                        Game.instance.GetPlayerProfile().m_playerStats.m_builds++;
                    }
                }
                PrivateArea privateArea = gameObject.GetComponent<PrivateArea>();
                if (privateArea)
                {
                    privateArea.Setup(Game.instance.GetPlayerProfile().GetName());
                }
                WearNTear wearntear = gameObject.GetComponent<WearNTear>();
                if (wearntear)
                {
                    wearntear.OnPlaced();
                }
                TextReceiver textReceiver = gameObject.GetComponent<TextReceiver>();
                if (textReceiver != null)
                {
                    textReceiver.SetText(entry.additionalInfo);
                }
            }

            if (blueprintInstance.Any())
            {
                BlueprintInstance.Instances.Add(blueprintInstance);
            }
        }

        /// <summary>
        ///     Hook for patching
        /// </summary>
        internal virtual void OnPiecePlaced(GameObject placedPiece)
        {
        }
    }
}