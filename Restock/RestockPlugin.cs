using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Restock
{
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    public class RestockPlugin : BaseUnityPlugin
    {
        private const string MyGUID = "com.equinox.Restock";
        private const string PluginName = "Restock";
        private const string VersionString = "2.0.0";

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log = new ManualLogSource(PluginName);

        public static string restockRadiusKey = "Restock Radius";
        public static ConfigEntry<int> restockRadius;

        private Bounds scanZone;
        private int buildablesMask = 2097512;

        private void Awake() {
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loading...");
            Harmony.PatchAll();

            restockRadius = Config.Bind<int>("General", restockRadiusKey, 5, new ConfigDescription("The radius around the player to scan for chests", new AcceptableValueRange<int>(0, 10)));

            // ToDo: Apply Patches

            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loaded.");
            Log = Logger;
        }

        private void FixedUpdate() {
            if (Player.instance == null) return;
            int radius = restockRadius.Value;
            scanZone = new Bounds {
                center = Player.instance.transform.position,
                extents = new Vector3(radius, 1, radius)
            };

            Collider[] colliders = Physics.OverlapBox(scanZone.center, scanZone.extents, Quaternion.identity, buildablesMask);
            foreach(Collider collider in colliders) {
                GenericMachineInstanceRef machine = FHG_Utils.FindMachineRef(collider.gameObject);
                if (!machine.IsValid()) continue;
                if (machine.typeIndex != MachineTypeEnum.Chest) continue;

                ChestInstance chest = machine.Get<ChestInstance>();
                Inventory inventory = chest.GetCommonInfo().inventories[0];
                List<ResourceStack> nonEmptyStacks = inventory.myStacks.Where(stack => stack.info != null).Distinct().ToList();
                foreach (ResourceStack stack in nonEmptyStacks) {
                    if (!Player.instance.inventory.HasAnyOfResource(stack.info)) continue;

                    int maxStack = stack.maxStack;
                    int curerntStack = Player.instance.inventory.GetResourceCount(stack.info);
                    if (curerntStack >= maxStack) continue;

                    int numPlayerNeeds = maxStack - curerntStack;
                    int toSend = numPlayerNeeds > stack.count ? stack.count - 1 : numPlayerNeeds;
                    if (toSend == 0) continue;

                    int resID = stack.info.uniqueId;
                    if(!Player.instance.inventory.CanAddResources(resID, toSend)) {
                        Debug.Log($"Can't add resources to player inventory");
                        continue;
                    }

                    inventory.TryRemoveResources(stack.info.uniqueId, toSend);
                    Player.instance.inventory.AddResources(resID, toSend);
                }
            }
        }
    }
}
