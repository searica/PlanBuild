using Jotunn.Managers;
using PlanBuild.Blueprints;
using PlanBuild.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace PlanBuild.Plans
{
    internal static class PlanManager
    {
        internal static void Init()
        {
            Logger.LogInfo("Initializing PlanManager");
            
            // Init blacklist
            PlanBlacklist.Init();

            // Init commands
            PlanCommands.Init();
        }

        /// <summary>
        ///     Trigger initial scan after DungeonDB.Start
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DungeonDB), nameof(DungeonDB.Start))]
        private static void DungeonDBStartHook()
            {
                PlanDB.Instance.ScanPieceTables();
        }
        
        public static void UpdateKnownRecipes()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            Logger.LogDebug("Updating known Recipes");
            foreach (PlanPiecePrefab planPiece in PlanDB.Instance.GetPlanPiecePrefabs())
            {
                if (PlanBlacklist.Contains(planPiece) ||
                    (!Config.ShowAllPieces.Value && !PlayerKnowsPiece(player, planPiece.OriginalPiece)))
                {
                    if (player.m_knownRecipes.Contains(planPiece.Piece.m_name))
                    {
                        player.m_knownRecipes.Remove(planPiece.Piece.m_name);
                        Logger.LogDebug($"Removing planned piece from m_knownRecipes: {planPiece.Piece.m_name}");
                    }
                }
                else if (!player.m_knownRecipes.Contains(planPiece.Piece.m_name))
                {
                    player.m_knownRecipes.Add(planPiece.Piece.m_name);
                    Logger.LogDebug($"Adding planned piece to m_knownRecipes: {planPiece.Piece.m_name}");
                }
            }

            PieceManager.Instance.GetPieceTable(PlanHammerPrefab.PieceTableName)
                .UpdateAvailable(player.m_knownRecipes, player, true, false);
        }

        public static void UpdateAllPlanPieceTextures()
        {
            Player self = Player.m_localPlayer;
            if (self && self.m_placementGhost &&
                (self.m_placementGhost.name.StartsWith(Blueprint.PieceBlueprintName) ||
                 self.m_placementGhost.name.Split('(')[0].EndsWith(PlanPiecePrefab.PlannedSuffix)))
            {
                if (PlanCrystalPrefab.ShowRealTextures || !Config.ConfigTransparentGhostPlacement.Value)
                {
                    ShaderHelper.UpdateTextures(self.m_placementGhost, ShaderHelper.ShaderState.Skuld);
                }
                else
                {
                    ShaderHelper.UpdateTextures(self.m_placementGhost, ShaderHelper.ShaderState.Supported);
                }
            }
            foreach (PlanPiece planPiece in Object.FindObjectsOfType<PlanPiece>())
            {
                planPiece.UpdateTextures();
            }
        }

        public static void UpdateAllPlanTotems()
        {
            PlanTotemPrefab.UpdateGlowColor(PlanTotemPrefab.PlanTotemKitbash?.Prefab);
            foreach (PlanTotem planTotem in PlanTotem.m_allPlanTotems)
            {
                PlanTotemPrefab.UpdateGlowColor(planTotem.gameObject);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.AddKnownPiece))]
        private static bool Player_AddKnownPiece(Player __instance, Piece piece)
        {
            if (piece.name.EndsWith(PlanPiecePrefab.PlannedSuffix))
            {
#if DEBUG
                Jotunn.Logger.LogDebug($"Prevent notification for {piece.name}");
#endif
                Player.m_localPlayer.m_knownRecipes.Add(piece.m_name);
                return false;
            }
            return true;
        }

        /// <summary>
        ///     Check if the player knows this piece
        ///     Has some additional handling for pieces with duplicate m_name
        /// </summary>
        /// <param name="player"></param>
        /// <param name="originalPiece"></param>
        /// <returns></returns>
        private static bool PlayerKnowsPiece(Player player, Piece originalPiece)
        {
            if (!PlanDB.Instance.FindOriginalByPieceName(originalPiece.m_name, out List<Piece> originalPieces))
            {
                return player.HaveRequirements(originalPiece, Player.RequirementMode.IsKnown);
            }
            foreach (Piece piece in originalPieces)
            {
                if (player.HaveRequirements(piece, Player.RequirementMode.IsKnown))
                {
                    return true;
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements))]
        private static bool Player_HaveRequirements(
            ref Piece piece,
            ref Player.RequirementMode mode,
            ref bool __result
        )
        {
            if (!piece || !PlanDB.Instance.FindOriginalByPrefabName(piece.gameObject.name, out Piece originalPiece))
            {
                return true;
            }

            try
            {
                    if (PlanBlacklist.Contains(originalPiece))
                    {
                    __result = false;
                        return false;
                    }
                    if (Config.ShowAllPieces.Value)
                    {
                    __result = true;
                    return false;
                }

                // modify arguments and run original method
                mode = Player.RequirementMode.IsKnown;
                piece = originalPiece;
                        return true;

                    }
            catch (Exception e)
            {
                Logger.LogWarning($"Error while executing Player.HaveRequirements({piece},{mode}): {e}");
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        private static void Player_SetupPlacementGhostPrefix(Player __instance)
        {
            PlanPiece.m_forceDisableInit = true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        private static void Player_SetupPlacementGhostPostfix(Player __instance)
        {
            try
            {
                if (__instance.m_placementGhost)
                {
                    if (PlanCrystalPrefab.ShowRealTextures)
                    {
                        ShaderHelper.UpdateTextures(__instance.m_placementGhost, ShaderHelper.ShaderState.Skuld);
                    }
                    else if (Config.ConfigTransparentGhostPlacement.Value
                             && (__instance.m_placementGhost.name.StartsWith(Blueprint.PieceBlueprintName)
                                 || __instance.m_placementGhost.name.Split('(')[0].EndsWith(PlanPiecePrefab.PlannedSuffix))
                    )
                    {
                        ShaderHelper.UpdateTextures(__instance.m_placementGhost, ShaderHelper.ShaderState.Supported);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception caught while executing Player.SetupPlacementGhost(): {ex}");
            }
            finally
            {
                PlanPiece.m_forceDisableInit = false;
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.CheckCanRemovePiece))]
        private static bool Player_CheckCanRemovePiece(Player __instance, Piece piece, ref bool __result)
        {
            var planHammer = __instance.m_visEquipment.m_rightItem.Equals(PlanHammerPrefab.PlanHammerName);
            var planPiece = piece.TryGetComponent<PlanPiece>(out _);

            if (planHammer)
            {
                __result = planPiece;
                return false;
            }

            if (planPiece)
            {
                __result = false;
                return false;
            }

            return true;
        }

        private static void WearNTear_Highlight(On.WearNTear.orig_Highlight orig, WearNTear self)
        {
            if (!PlanCrystalPrefab.ShowRealTextures && self.TryGetComponent(out PlanPiece planPiece))
            {
                planPiece.Highlight();
                return;
            }
            orig(self);
        }
        
        private static void WearNTear_Destroy(On.WearNTear.orig_Destroy orig, WearNTear wearNTear)
        {
            // Check if actually destoyed, not removed by middle clicking with Hammer
            if (wearNTear.m_nview && wearNTear.m_nview.IsOwner()
                                  && wearNTear.GetHealthPercentage() <= 0f
                                  && PlanDB.Instance.FindPlanByPrefabName(wearNTear.name, out PlanPiecePrefab planPrefab))
            {
                foreach (PlanTotem planTotem in PlanTotem.m_allPlanTotems)
                {
                    if (!planTotem.GetEnabled())
                    {
                        continue;
                    }
                    GameObject gameObject = wearNTear.gameObject;
                    if (planTotem.InRange(gameObject))
                    {
                        planTotem.Replace(gameObject, planPrefab);
                        break;
                    }
                }
            }
            orig(wearNTear);
        }
    }
}