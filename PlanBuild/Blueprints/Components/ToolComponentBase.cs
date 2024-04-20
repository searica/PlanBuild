using HarmonyLib;
using PlanBuild.ModCompat;
using PlanBuild.Utils;
using UnityEngine;

namespace PlanBuild.Blueprints.Components
{
    [HarmonyPatch(typeof(ToolComponentBase))]
    internal class ToolComponentBase : MonoBehaviour
    {
        public static ShapedProjector SelectionProjector;
        public static float SelectionRadius = 10.0f;
        public static int SelectionRotation;
        public static float CameraOffset;
        public static Vector3 PlacementOffset = Vector3.zero;
        public static Vector3 MarkerOffset = Vector3.zero;

        public static int ToolComponentCounter = 0;

        internal bool SuppressGizmo = true;
        internal bool SuppressPieceHighlight = true;
        internal bool ResetPlacementOffset = true;
        internal bool ResetMarkerOffset = true;

        [HarmonyPatch(typeof(ToolComponentPatches))]
        internal static class ToolComponentPatches
        {
            /// <summary>
            ///     Dont highlight pieces while capturing when enabled
            /// </summary>
            [HarmonyPrefix]
            [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Highlight))]
            private static bool WearNTearHighlightPrefix(WearNTear __instance)
            {
                if (__instance)
                {
                    var toolCompBase = __instance.gameObject.GetComponentInChildren<ToolComponentBase>();
                    if (toolCompBase && !toolCompBase.SuppressPieceHighlight)
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>
            ///     Apply the MarkerOffset and react on piece hover
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), nameof(Player.PieceRayTest))]
            private static void Player_PieceRayTest(Player __instance, ref Vector3 point, ref Piece piece, bool __result)
            {
                if (ToolComponentCounter > 0 && __result && __instance.m_placementGhost && MarkerOffset != Vector3.zero)
                {
                    point += __instance.m_placementGhost.transform.TransformDirection(MarkerOffset);
                }
                // TODO: Not sure what this line does since I can't find an overrride, will mess with later
                //OnPieceHovered(piece);
            }

            /// <summary>
            ///     Intercept placing of the meta pieces.
            ///     Cancels the real placement of the placeholder pieces.
            /// </summary>
            /// </summary>
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
            private static bool Player_PlacePiece(Player __instance, Piece piece, ref bool __result)
            {
                if (__instance && piece)
                {
                    var toolComp = piece.gameObject.GetComponentInChildren<ToolComponentBase>();
                    if (toolComp)
                    {
                        toolComp.OnPlacePiece(__instance, piece);
                        __result = false;
                        return false;
                    }
                }
                return true;
            }
        }

        private void Start()
        {
            OnStart();

            if (ResetPlacementOffset)
            {
                PlacementOffset = Vector3.zero;
            }

            if (ResetMarkerOffset)
            {
                MarkerOffset = Vector3.zero;
            }

            ToolComponentCounter += 1;

            EventHooks.OnPlayerUpdatedPlacement += Player_UpdatePlacement;
            EventHooks.OnPlayerUpdatedPlacementGhost += Player_UpdatePlacementGhost;
            EventHooks.OnGameCameraUpdateCamera += GameCamera_UpdateCamera;
            EventHooks.OnHudSetupPieceInfo += Hud_SetupPieceInfo;

            Jotunn.Logger.LogDebug($"{gameObject.name} started");
        }

        public virtual void OnStart()
        {
        }

        private void OnDestroy()
        {
            if (!ZNetScene.instance)
            {
                Jotunn.Logger.LogDebug("Skipping destroy because the game is exiting");
                return;
            }

            OnOnDestroy();
            DisableSelectionProjector();

            ToolComponentCounter -= 1;

            EventHooks.OnPlayerUpdatedPlacement -= Player_UpdatePlacement;
            EventHooks.OnPlayerUpdatedPlacementGhost -= Player_UpdatePlacementGhost;
            EventHooks.OnGameCameraUpdateCamera -= GameCamera_UpdateCamera;
            EventHooks.OnHudSetupPieceInfo -= Hud_SetupPieceInfo;

            Jotunn.Logger.LogDebug($"{gameObject.name} destroyed");
        }

        public virtual void OnOnDestroy()
        {
        }

        /// <summary>
        ///     Update the tool's placement
        /// </summary>
        private void Player_UpdatePlacement(object sender, UpdatePlacementEventArgs args)
        {
            if (args.player.m_placementGhost && args.takeInput)
            {
                OnUpdatePlacement(args.player);
            }
        }

        /// <summary>
        ///     Default UpdatePlacement when subclass does not override.
        /// </summary>
        public virtual void OnUpdatePlacement(Player self)
        {
            PlacementOffset = Vector3.zero;
            MarkerOffset = Vector3.zero;
            CameraOffset = 0f;
            DisableSelectionProjector();
        }

        public float GetPlacementOffset(float scrollWheel)
        {
            bool scrollingDown = scrollWheel < 0f;
            if (Config.InvertPlacementOffsetScrollConfig.Value)
            {
                scrollingDown = !scrollingDown;
            }
            if (scrollingDown)
            {
                return -Config.PlacementOffsetIncrementConfig.Value;
            }
            else
            {
                return Config.PlacementOffsetIncrementConfig.Value;
            }
        }

        public void UndoRotation(Player player, float scrollWheel)
        {
            if (scrollWheel < 0f)
            {
                player.m_placeRotation++;
            }
            else
            {
                player.m_placeRotation--;
            }
        }

        public void UpdateSelectionRadius(float scrollWheel)
        {
            if (SelectionProjector == null)
            {
                return;
            }

            bool scrollingDown = scrollWheel < 0f;
            if (Config.InvertSelectionScrollConfig.Value)
            {
                scrollingDown = !scrollingDown;
            }
            if (scrollingDown)
            {
                SelectionRadius -= Config.SelectionIncrementConfig.Value;
            }
            else
            {
                SelectionRadius += Config.SelectionIncrementConfig.Value;
            }

            SelectionRadius = Mathf.Clamp(SelectionRadius, 2f, 100f);
            SelectionProjector.SetRadius(SelectionRadius);
        }

        public void UpdateSelectionRotation(float scrollWheel)
        {
            if (SelectionProjector == null)
            {
                return;
            }

            bool scrollingDown = scrollWheel < 0f;
            if (Config.InvertRotationScrollConfig.Value)
            {
                scrollingDown = !scrollingDown;
            }
            if (scrollingDown)
            {
                SelectionRotation -= Config.RotationIncrementConfig.Value;
            }
            else
            {
                SelectionRotation += Config.RotationIncrementConfig.Value;
            }

            SelectionProjector.SetRotation(SelectionRotation);
        }

        public void EnableSelectionProjector(Player self, bool enableMask = false)
        {
            if (SelectionProjector == null)
            {
                SelectionProjector = self.m_placementMarkerInstance.AddComponent<ShapedProjector>();
                SelectionProjector.Enable();
                SelectionProjector.SetRadius(SelectionRadius);
                SelectionProjector.SetRotation(SelectionRotation);
            }
            if (enableMask)
            {
                SelectionProjector.EnableMask();
            }
            else
            {
                SelectionProjector.DisableMask();
            }
        }

        public void DisableSelectionProjector()
        {
            if (SelectionProjector != null)
            {
                SelectionProjector.Disable();
                DestroyImmediate(SelectionProjector);
            }
        }

        public void UpdateCameraOffset(float scrollWheel)
        {
            // TODO: base min/max off of selected piece dimensions
            float minOffset = 0f;
            float maxOffset = 30f;
            bool scrollingDown = scrollWheel < 0f;
            if (Config.InvertCameraOffsetScrollConfig.Value)
            {
                scrollingDown = !scrollingDown;
            }
            if (scrollingDown)
            {
                CameraOffset = Mathf.Clamp(CameraOffset + Config.CameraOffsetIncrementConfig.Value, minOffset, maxOffset);
            }
            else
            {
                CameraOffset = Mathf.Clamp(CameraOffset - Config.CameraOffsetIncrementConfig.Value, minOffset, maxOffset);
            }
        }

        /// <summary>
        ///     Flatten placement marker and apply the PlacementOffset
        /// </summary>
        private void Player_UpdatePlacementGhost(object sender, PlayerEventArgs args)
        {
            var self = args.player;

            if (self.m_placementMarkerInstance)
            {
                self.m_placementMarkerInstance.transform.up = Vector3.back;

                if (self.m_placementGhost && PlacementOffset != Vector3.zero)
                {
                    var pos = self.m_placementGhost.transform.position;
                    var rot = self.m_placementGhost.transform.rotation;
                    pos += rot * Vector3.right * PlacementOffset.x;
                    pos += rot * Vector3.up * PlacementOffset.y;
                    pos += rot * Vector3.forward * PlacementOffset.z;
                    self.m_placementGhost.transform.position = pos;
                }
            }
        }

        public virtual void OnPieceHovered(Piece hoveredPiece)
        {
        }

        public virtual void OnPlacePiece(Player self, Piece piece)
        {
        }

        /// <summary>
        ///     Adjust camera height
        /// </summary>
        private void GameCamera_UpdateCamera(object sender, GameCamEventArgs args)
        {
            if (PatcherBuildCamera.UpdateCamera
                && Player.m_localPlayer
                && Player.m_localPlayer.InPlaceMode()
                && Player.m_localPlayer.m_placementGhost) { }
            {
                args.gameCam.transform.position += new Vector3(0, CameraOffset, 0);
            }
        }

        /// <summary>
        ///     Hook SetupPieceInfo to alter the piece description per tool.
        /// </summary>
        private void Hud_SetupPieceInfo(object sender, HudEventArgs args)
        {
            if (!args.hud.m_pieceSelectionWindow.activeSelf)
            {
                UpdateDescription();
            }
        }

        public virtual void UpdateDescription()
        {
        }
    }
}