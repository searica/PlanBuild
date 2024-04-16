using System;
using HarmonyLib;

namespace PlanBuild
{

    /// <summary>
    ///     Event args class Player events
    /// </summary>
    internal class PlayerEventArgs : EventArgs
    {
        /// <summary>
        ///     Reference to the player instance
        /// </summary>
        public Player player { get; set; }
    }


    /// <summary>
    ///     Event args class PieceRayTest method
    internal class RayTestEventArgs : EventArgs
    {
        /// <summary>
        ///     Reference to the player instance
        /// </summary>
        public Player player { get; set; }

        /// <summary>
        ///     Refence to the result of the ray test.
        /// </summary>
        public Piece piece { get; set; }

        /// <summary>
        ///     Constructor for easier use when invoking events.
        /// </summary>
        /// <param name="player"></param>
        public RayTestEventArgs(Player player, Piece piece) 
        { 
            this.player = player;
            this.piece = piece;
        }
    }

    /// <summary>
    ///     Event args for placing a piece
    /// </summary>
    internal class PlacePieceEventArgs : EventArgs
    {
        /// <summary>
        ///     Reference to the player instance
        /// </summary>
        public Player player { get; set; }

    [HarmonyPatch(typeof(EventHooks))]
    internal static class EventHooks
    {


        /// <summary>
        ///     Prefix
        /// </summary>
        public static event EventHandler<PlayerEventArgs> OnPlayerSettingUpPlacementGhost;

        /// <summary>
        ///     Prefix
        /// </summary>
        public static event EventHandler<PlayerEventArgs> OnPlayerUpdatingPlacementGhost;

        public static event EventHandler<PlayerEventArgs> OnPlayerAddKnownPiece;

        public static event EventHandler<PlayerEventArgs> OnPlayerHaveRequirements_Piece_RequirementMode;

    }
}
