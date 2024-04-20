using System;
using System.Numerics;
using HarmonyLib;

namespace PlanBuild
{
    /// <summary>
    ///     Event args class GameCamera events
    /// </summary>
    internal class GameCamEventArgs : EventArgs
    {
        /// <summary>
        ///     Reference to the hud instance
        /// </summary>
        public GameCamera gameCam { get; set; }

        /// <summary>
        ///     Constructor for easier use when invoking events.
        /// </summary>
        /// <param name="player"></param>
        public GameCamEventArgs(GameCamera gameCam)
        {
            this.gameCam = gameCam;
        }
    }

    /// <summary>
    ///     Event args class Hud events
    /// </summary>
    internal class HudEventArgs : EventArgs
    {
        /// <summary>
        ///     Reference to the hud instance
        /// </summary>
        public Hud hud { get; set; }

        /// <summary>
        ///     Constructor for easier use when invoking events.
        /// </summary>
        /// <param name="player"></param>
        public HudEventArgs(Hud hud)
        {
            this.hud = hud;
        }
    }

    /// <summary>
    ///     Event args class Player events
    /// </summary>
    internal class PlayerEventArgs : EventArgs
    {
        /// <summary>
        ///     Reference to the player instance
        /// </summary>
        public Player player { get; set; }

        /// <summary>
        ///     Constructor for easier use when invoking events.
        /// </summary>
        /// <param name="player"></param>
        public PlayerEventArgs(Player player)
        {
            this.player = player;
        }
    }

    /// <summary>
    ///     Event args class PieceRayTest method
    internal class RayTestEventArgs : PlayerEventArgs
    {
        /// <summary>
        ///     Reference to the result of the ray test.
        /// </summary>
        public Piece piece { get; set; }

        public Vector3 point { get; set; }

        public bool result { get; set; }

        /// <summary>
        ///     Constructor for easier use when invoking events.
        /// </summary>
        /// <param name="player"></param>
        public RayTestEventArgs(Player player, Piece piece) : base(player)
        {
            this.piece = piece;
        }

        /// <summary>
        ///     Constructor for easier use when invoking events.
        /// </summary>
        /// <param name="player"></param>
        public RayTestEventArgs(Player player, Piece piece, Vector3 point, bool result) : base(player)
        {
            this.piece = piece;
            this.point = point;
            this.result = result;
        }
    }

    /// <summary>
    ///     Event args for placing a piece
    /// </summary>
    internal class UpdatePlacementEventArgs : PlayerEventArgs
    {
        /// <summary>
        ///     Reference to the result of the ray test.
        /// </summary>
        public bool takeInput { get; set; }

        /// <summary>
        ///     Constructor for easier use when invoking events.
        /// </summary>
        /// <param name="player"></param>
        public UpdatePlacementEventArgs(Player player, bool takeInput) : base(player)
        {
            this.takeInput = takeInput;
        }
    }

    [HarmonyPatch(typeof(EventHooks))]
    internal static class EventHooks
    {
        /// <summary>
        ///     Fires after PieceRayTest is complete.
        /// </summary>
        public static event EventHandler<RayTestEventArgs> OnPlayerPieceRayTestComplete;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.PieceRayTest))]
        private static void PieceRayTestPostfix(Player __instance, ref Piece piece, ref Vector3 point, ref bool __result)
        {
            OnPlayerPieceRayTestComplete.SafeInvoke(null, new RayTestEventArgs(__instance, piece, point, __result));
        }

        /// <summary>
        ///     Fires before a player places a piece.
        /// </summary>
        public static event EventHandler<PlayerEventArgs> OnPlayerPlacePiece;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        private static void PlacePiecePrefix()
        {
            OnPlayerPlacePiece.SafeInvoke(null, new PlayerEventArgs(null));
        }

        /// <summary>
        ///     Fires after Player.UpdatePlacement
        /// </summary>
        public static event EventHandler<UpdatePlacementEventArgs> OnPlayerUpdatedPlacement;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacement))]
        private static void UpdatePlacementPostfix(Player __instance, bool takeInput)
        {
            OnPlayerUpdatedPlacement.SafeInvoke(null, new UpdatePlacementEventArgs(__instance, takeInput));
        }

        /// <summary>
        ///     Fires before Player.SetUpPlacementGhost
        /// </summary>
        public static event EventHandler<PlayerEventArgs> OnPlayerSettingUpPlacementGhost;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
        private static void SetUpPlacementGhostPrefix()
        {
            OnPlayerSettingUpPlacementGhost.SafeInvoke(null, new PlayerEventArgs(null));
        }

        /// <summary>
        ///     Fires after Player.UpdatePlacementGhost
        /// </summary>
        public static event EventHandler<PlayerEventArgs> OnPlayerUpdatedPlacementGhost;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        private static void UpdatePlacementGhostPostfix(Player __instance)
        {
            OnPlayerUpdatedPlacementGhost.SafeInvoke(null, new PlayerEventArgs(__instance));
        }

        /// <summary>
        ///     Fires after Hud.SetupPieceInfo
        /// </summary>
        public static event EventHandler<HudEventArgs> OnHudSetupPieceInfo;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), nameof(Hud.SetupPieceInfo))]
        private static void HudSetupPieceInfoPostfix(Hud __instance)
        {
            OnHudSetupPieceInfo.SafeInvoke(null, new HudEventArgs(__instance));
        }

        /// <summary>
        ///     Fires after GameCamera.UpdateCamera
        /// </summary>
        public static event EventHandler<GameCamEventArgs> OnGameCameraUpdateCamera;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
        private static void GameCameraUpdateCamera(GameCamera __instance)
        {
            OnGameCameraUpdateCamera.SafeInvoke(null, new GameCamEventArgs(__instance));
        }
    }
}