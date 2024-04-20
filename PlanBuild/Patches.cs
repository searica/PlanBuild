using BepInEx.Bootstrap;
using HarmonyLib;
using PlanBuild.Blueprints;
using PlanBuild.Blueprints.Components;
using PlanBuild.Plans;

namespace PlanBuild
{
    internal class Patches
    {
        public const string BuildCameraGUID = "org.gittywithexcitement.plugins.valheim.buildCamera";
        public const string CraftFromContainersGUID = "aedenthorn.CraftFromContainers";
        public const string GizmoGUID = "com.rolopogo.gizmo.comfy";
        public const string ValheimRaftGUID = "BepIn.Sarcen.ValheimRAFT";
        public const string ItemDrawersGUID = "mkz.itemdrawers";

        private static Harmony Harmony;

        internal static void Apply()
        {
            Harmony = new Harmony(PlanBuildPlugin.PluginGUID);

            // Apply patches for PlanBuild functionality
            Harmony.PatchAll(typeof(EventHooks));
            Harmony.PatchAll(typeof(PlanPiece));
            Harmony.PatchAll(typeof(BlueprintSync));
            Harmony.PatchAll(typeof(PlanManager));
            Harmony.PatchAll(typeof(BlueprintManager));
            Harmony.PatchAll(typeof(ToolComponentBase));
            Harmony.PatchAll(typeof(PlanTotem));
            Harmony.PatchAll(typeof(PlanHammerPrefab));

            if (Chainloader.PluginInfos.ContainsKey(BuildCameraGUID))
            {
                Jotunn.Logger.LogInfo("Applying BuildCamera patches");
                Harmony.PatchAll(typeof(ModCompat.PatcherBuildCamera));
            }

            if (Chainloader.PluginInfos.ContainsKey(CraftFromContainersGUID))
            {
                Jotunn.Logger.LogInfo("Applying CraftFromContainers patches");
                Harmony.PatchAll(typeof(ModCompat.PatcherCraftFromContainers));
            }

            if (Chainloader.PluginInfos.ContainsKey(GizmoGUID))
            {
                Jotunn.Logger.LogInfo("Applying Gizmo patches");
                Harmony.PatchAll(typeof(ModCompat.PatcherGizmo));
            }

            if (Chainloader.PluginInfos.ContainsKey(ValheimRaftGUID))
            {
                Jotunn.Logger.LogInfo("Applying ValheimRAFT patches");
                Harmony.PatchAll(typeof(ModCompat.PatcherValheimRaft));
            }
        }
    }
}