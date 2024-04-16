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
