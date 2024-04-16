using HarmonyLib;
using Jotunn.Managers;
using PlanBuild.Plans;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using Logger = Jotunn.Logger;

namespace PlanBuild.Blueprints
{
    [HarmonyPatch(typeof(BlueprintManager))]
    internal static class BlueprintManager
    {
        public static BlueprintDictionary LocalBlueprints;
        public static BlueprintDictionary TemporaryBlueprints;
        public static BlueprintDictionary ServerBlueprints;

        public const float HighlightTimeout = 0.5f;
        public const float GhostTimeout = 10f;

        public static Piece LastHoveredPiece;

        private static float LastHighlightTime;
        private static float OriginalPlaceDistance;
        private static GameObject OriginalTooltip;

        public static void Init()
        {
            Logger.LogInfo("Initializing BlueprintManager");

            try
            {
                // Init stuff
                LocalBlueprints = new BlueprintDictionary();
                TemporaryBlueprints = new BlueprintDictionary();
                ServerBlueprints = new BlueprintDictionary();
                Selection.Init();
                SelectionCommands.Init();
                BlueprintSync.Init();
                BlueprintCommands.Init();
                UndoManager.Instance.CreateQueue(Config.BlueprintUndoQueueNameConfig.Value);

                // Hooks
                GUIManager.OnCustomGUIAvailable += GUIManager_OnCustomGUIAvailable;
                
                // Ghost watchdog
                IEnumerator watchdog()
                {
                    while (true)
                    {
                        foreach (var bp in LocalBlueprints.Values.Where(x => x.GhostActiveTime > 0f))
                        {
                            if (Time.time - bp.GhostActiveTime > GhostTimeout)
                            {
                                bp.DestroyGhost();
                            }
                        }

                        yield return new WaitForSeconds(GhostTimeout);
                    }
                }

                PlanBuildPlugin.Instance.StartCoroutine(watchdog());
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error caught while initializing: {ex}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown))]
        private static void ZNetSceneShutdownHook()
        {
            TemporaryBlueprints.Clear();
            Selection.Instance.Clear();
        }

        /// <summary>
        ///     Determine if a piece can be captured in a blueprint
        /// </summary>
        /// <param name="piece">Piece instance to be tested</param>
        /// <param name="onlyPlanned">When true, only pieces with the PlanPiece component return true</param>
        /// <returns></returns>
        public static bool CanCapture(Piece piece, bool onlyPlanned = false)
        {
            if (piece.name.StartsWith(BlueprintAssets.PieceSnapPointName) || piece.name.StartsWith(BlueprintAssets.PieceCenterPointName))
            {
                return true;
            }

            if (piece.name.StartsWith(Blueprint.PieceBlueprintName))
            {
                return false;
            }

            if (!SynchronizationManager.Instance.PlayerIsAdmin && PlanBlacklist.Contains(piece))
            {
                return false;
            }

            return piece.GetComponent<PlanPiece>() != null || (!onlyPlanned && PlanDB.Instance.CanCreatePlan(piece));
        }

        /// <summary>
        ///     Get all pieces on a given position in a given radius, optionally only planned ones
        /// </summary>
        /// <param name="position"></param>
        /// <param name="radius"></param>
        /// <param name="onlyPlanned"></param>
        /// <returns></returns>
        public static List<Piece> GetPiecesInRadius(Vector3 position, float radius, bool onlyPlanned = false)
        {
            List<Piece> result = new List<Piece>();
            foreach (var piece in Piece.s_allPieces)
            {
                Vector3 piecePos = piece.transform.position;
                if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(piecePos.x, piecePos.z)) <= radius
                    && CanCapture(piece, onlyPlanned))
                {
                    result.Add(piece);
                }
            }
            return result;
        }

        /// <summary>
        ///     "Highlights" pieces in a given radius with a given color.
        /// </summary>
        public static void HighlightPiecesInRadius(Vector3 startPosition, float radius, Color color, bool onlyPlanned = false)
        {
            if (Time.time < LastHighlightTime + HighlightTimeout)
            {
                return;
            }

            foreach (var piece in GetPiecesInRadius(startPosition, radius, onlyPlanned))
            {
                if (piece.TryGetComponent(out WearNTear wearNTear))
                {
                    wearNTear.Highlight(color, HighlightTimeout + 0.1f);
                }
            }
            LastHighlightTime = Time.time;
        }

        /// <summary>
        ///     "Highlights" the last hovered piece with a given color.
        /// </summary>
        public static void HighlightHoveredPiece(Color color, bool onlyPlanned = false)
        {
            if (Time.time < LastHighlightTime + HighlightTimeout)
            {
                return;
            }

            if (LastHoveredPiece)
            {
                if (onlyPlanned && !LastHoveredPiece.GetComponent<PlanPiece>())
                {
                    return;
                }
                if (LastHoveredPiece.TryGetComponent(out WearNTear wearNTear))
                {
                    wearNTear.Highlight(color, HighlightTimeout + 0.1f);
                }
            }
            LastHighlightTime = Time.time;
        }

        /// <summary>
        ///     Get the GameObject from a ZDOID via ZNetScene or force creation of one via ZDO
        /// </summary>
        public static GameObject GetGameObject(ZDOID zdoid, bool required = false)
        {
            GameObject go = ZNetScene.instance.FindInstance(zdoid);
            if (go)
            {
                return go;
            }
            return required ? ZNetScene.instance.CreateObject(ZDOMan.instance.GetZDO(zdoid)) : null;
        }

        public static bool ClearClipboard()
        {
            if (TemporaryBlueprints.Count == 0)
            {
                return false;
            }

            foreach (var tmp in TemporaryBlueprints)
            {
                tmp.Value.DestroyBlueprint();
            }
            TemporaryBlueprints.Clear();
            Player.m_localPlayer.UpdateKnownRecipesList();
            Player.m_localPlayer.UpdateAvailablePiecesList();
            BlueprintGUI.RefreshBlueprints(BlueprintLocation.Temporary);

            return true;
        }

        /// <summary>
        ///     Create pieces for all known local Blueprints
        /// </summary>
        public static void RegisterKnownBlueprints()
        {
            if (Player.m_localPlayer)
            {
                Logger.LogInfo("Registering known blueprints");

                foreach (var bp in LocalBlueprints.Values)
                {
                    bp.CreatePiece();
                }
                Player.m_localPlayer.UpdateKnownRecipesList();
                Player.m_localPlayer.UpdateAvailablePiecesList();
            }
        }

        /// <summary>
        ///     Create blueprint pieces on player spawn
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static void Player_OnSpawned(Player __instance)
        {
            if (__instance && __instance == Player.m_localPlayer)
            {
                RegisterKnownBlueprints();

                if (!Config.AllowBlueprintRune.Value && !SynchronizationManager.Instance.PlayerIsAdmin)
                {
                    Player.m_localPlayer.SetBuildCategory(0);
                    Player.m_localPlayer.SetSelectedPiece(new Vector2Int(9, 9));
                }
            }
        }

        /// <summary>
        ///     Reorder pieces in local blueprint categories by name.
        ///     Remove "placeholder pieces" from blueprint categories
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
        private static void PieceTableUpdateAvailableHook(PieceTable __instance)
        {
            if (!__instance)
            {
                return;
            }

            if (__instance.name.Equals(BlueprintAssets.PieceTableName))
            {
                foreach (var cats in LocalBlueprints.Values.GroupBy(x => x.Category))
                {
                    Piece.PieceCategory? cat = PieceManager.Instance.GetPieceCategory(cats.Key);
                    if (cat.HasValue)
                    {
                        List<Piece> reorder = new List<Piece>();
                        reorder.Add(BlueprintAssets.PlaceholderObject.GetComponent<Piece>());
                        reorder.AddRange(__instance.m_availablePieces[(int)cat]
                            .OrderBy(x => x.m_name)
                            .Where(x => !x.name.Equals(BlueprintAssets.PiecePlaceholderName))
                            .ToList());
                        __instance.m_availablePieces[(int)cat] = reorder;
                    }
                }
            }
        }

        /// <summary>
        ///     Lazy ghost instantiation
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        private static void SetupPlacementGhostHook(Player __instance)
        {
            if (!__instance || __instance.m_buildPieces == null)
            {
                return;
            }

            GameObject prefab = __instance.m_buildPieces.GetSelectedPrefab();
            if (!prefab || !prefab.name.StartsWith(Blueprint.PieceBlueprintName))
            {
                return;
            }

            string bpname = prefab.name.Substring(Blueprint.PieceBlueprintName.Length + 1);
            if (LocalBlueprints.TryGetValue(bpname, out var bp))
            {
                bp.InstantiateGhost();
            }
        }

        /// <summary>
        ///     Timed ghost destruction
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        private static void UpdatePlacementGhostHook(Player __instance)
        {
            if (!__instance || __instance.m_buildPieces == null)
            {
                return;
            }

            GameObject prefab = __instance.m_buildPieces.GetSelectedPrefab();
            if (!prefab || !prefab.name.StartsWith(Blueprint.PieceBlueprintName))
            {
                return;
            }

            string bpname = prefab.name.Substring(Blueprint.PieceBlueprintName.Length + 1);
            if (LocalBlueprints.TryGetValue(bpname, out var bp))
            {
                bp.GhostActiveTime = Time.time;
            }
        }

        /// <summary>
        ///     Save the reference to the last hovered piece
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.PieceRayTest))]
        private static void Player_PieceRayTest(Piece piece)
        {
            LastHoveredPiece = piece;
        }

        /// <summary>
        ///     BlueprintRune equip
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        private static void Humanoid_EquipItem(bool __result)
        {
            if (__result && Player.m_localPlayer?.m_rightItem?.m_shared.m_name == BlueprintAssets.BlueprintRuneItemName)
            {
                OriginalPlaceDistance = Math.Max(Player.m_localPlayer.m_maxPlaceDistance, 8f);
                Player.m_localPlayer.m_maxPlaceDistance = Config.RayDistanceConfig.Value;

                var desc = Hud.instance.m_buildHud.transform.Find("SelectedInfo/selected_piece/piece_description");
                if (desc is RectTransform rect)
                {
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -30f);
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, 110f);
                }

                if (!Config.AllowBlueprintRune.Value && !SynchronizationManager.Instance.PlayerIsAdmin)
                {
                    if (Hud.IsPieceSelectionVisible())
                    {
                        Hud.HidePieceSelection();
                    }
                    Player.m_localPlayer.SetBuildCategory(0);
                    Player.m_localPlayer.SetSelectedPiece(new Vector2Int(9, 9));
                }
            }
        }

        /// <summary>
        ///     BlueprintRune uneqip
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
        private static void Humanoid_UnequipItem(ItemDrop.ItemData item)
        {

            if (Player.m_localPlayer &&
                item != null &&
                item.m_shared.m_name == BlueprintAssets.BlueprintRuneItemName
            )
            {
                Player.m_localPlayer.m_maxPlaceDistance = OriginalPlaceDistance;

                var desc = Hud.instance.m_buildHud.transform.Find("SelectedInfo/selected_piece/piece_description");
                if (desc is RectTransform rect)
                {
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, 36.5f);
                }
            }
        }

        /// <summary>
        ///     Prevent opening the build menu when the rune is selected and globally disabled
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Hud), nameof(Hud.TogglePieceSelection))]
        private static bool Hud_TogglePieceSelection()
        {
            var player = Player.m_localPlayer;
            if (!player)
            {
                return false;
            }

            if (player.m_rightItem?.m_shared.m_name == BlueprintAssets.BlueprintRuneItemName &&
                !Hud.IsPieceSelectionVisible() &&
                !Config.AllowBlueprintRune.Value &&
                !SynchronizationManager.Instance.PlayerIsAdmin
            )
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$msg_blueprintrune_disabled");
                player.SetBuildCategory(0);
                player.SetSelectedPiece(new Vector2Int(9, 9));
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
        private static void Piece_Awake(Piece __instance)
        {
            Selection.Instance.OnPieceAwake(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
        private static void Piece_OnDestroy(Piece __instance)
        {
            Selection.Instance.OnPieceUnload(__instance);
        }

        // Get all prefabs for this GUI session
        private static void GUIManager_OnCustomGUIAvailable()
        {
            OriginalTooltip = PrefabManager.Instance.GetPrefab("Tooltip");
        }


        /// <summary>
        ///     Display the blueprint tooltip panel when a blueprint building item is hovered
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UITooltip), nameof(UITooltip.OnHoverStart))]
        private static void UITooltip_OnHoverStart(UITooltip __instance, out Blueprint __state)
        {

            if (!BlueprintAssets.BlueprintTooltip || !Hud.IsPieceSelectionVisible())
            {
                __state = null;
                return;
            }

            var piece = Hud.instance.m_hoveredPiece;
            if (ZInput.IsGamepadActive() && !ZInput.IsMouseActive())
            {
                piece = Player.m_localPlayer.GetSelectedPiece();
            }

            if (Config.TooltipEnabledConfig.Value &&
                piece &&
                piece.name.StartsWith(Blueprint.PieceBlueprintName) &&
                LocalBlueprints.TryGetValue(piece.name.Substring(Blueprint.PieceBlueprintName.Length + 1), out var bp) &&
                bp.Thumbnail != null
            )
            {
                __instance.m_tooltipPrefab = BlueprintAssets.BlueprintTooltip;
                __state = bp;
            }
            else
            {
                __instance.m_tooltipPrefab = OriginalTooltip;
                __state = null;
            }
        }

        /// <summary>
        ///     Display the blueprint tooltip panel when a blueprint building item is hovered
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITooltip), nameof(UITooltip.OnHoverStart))]
        private static void UITooltip_OnHoverStartPostfix(UITooltip __instance, ref Blueprint __state)
        {
            if (__state != null)
            {
                var bp = __state;
                UITooltip.m_tooltip.transform.Find("Background")
                    .GetComponent<Image>().color = Config.TooltipBackgroundConfig.Value;
                UITooltip.m_tooltip.transform.Find("Background/BPImage")
                    .GetComponent<Image>().sprite = Sprite.Create(bp.Thumbnail, new Rect(0, 0, bp.Thumbnail.width, bp.Thumbnail.height), Vector2.zero);
                UITooltip.m_tooltip.transform.Find("Background/BPText")
                    .GetComponent<Text>().text = bp.Name;
            }
        }
    }
}