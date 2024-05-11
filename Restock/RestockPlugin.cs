using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EquinoxsModUtils;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using UnityEngine;

namespace Restock
{
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    public class RestockPlugin : BaseUnityPlugin
    {
        private const string MyGUID = "com.equinox.Restock";
        private const string PluginName = "Restock";
        private const string VersionString = "3.0.0";

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log = new ManualLogSource(PluginName);

        public static string restockRadiusKey = "Restock Radius";
        public static ConfigEntry<int> restockRadius;

        internal static Dictionary<string, ConfigEntry<int>> stacksDictionary = new Dictionary<string, ConfigEntry<int>>();

        private Bounds scanZone;
        private int buildablesMask = 2097512;

        // Unity Functions

        private void Awake() {
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loading...");
            Harmony.PatchAll();

            restockRadius = Config.Bind<int>("General", restockRadiusKey, 5, new ConfigDescription("The radius around the player to scan for chests", new AcceptableValueRange<int>(0, 10)));

            foreach (string name in ResourceNames.SafeResources) {
                bool isBuilding = IsItemBuilding(name);
                string category = isBuilding ? "Buildings" : "Items";
                int defaultValue = isBuilding ? 1 : 0;
                stacksDictionary.Add(name, Config.Bind(category, name, defaultValue, new ConfigDescription($"The number of stacks of {name} to restock up to", new AcceptableValueRange<int>(0, int.MaxValue))));
            }

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
                List<ResourceStack> nonEmptyStacks = inventory.myStacks.Where(stack => !stack.isEmpty).Distinct().ToList();
                foreach (ResourceStack stack in nonEmptyStacks) {
                    int maxStack = stack.maxStack;
                    int amountInInventory = Player.instance.inventory.GetResourceCount(stack.info);
                    int desiredAmount = 0;
                    if (stacksDictionary.ContainsKey(stack.info.displayName)) {
                        desiredAmount = stacksDictionary[stack.info.displayName].Value * maxStack;
                    }

                    if (amountInInventory >= desiredAmount || desiredAmount == 0) continue;

                    int numPlayerNeeds = desiredAmount - amountInInventory;
                    int toSend = numPlayerNeeds > stack.count ? stack.count - 1 : numPlayerNeeds;
                    if (toSend == 0) continue;

                    int resID = stack.info.uniqueId;
                    if(!Player.instance.inventory.CanAddResources(resID, toSend)) continue;

                    inventory.TryRemoveResources(stack.info.uniqueId, toSend);
                    Player.instance.inventory.AddResources(resID, toSend);
                }
            }
        }

        // Private Functions

        private bool IsItemBuilding(string name) {
            int index = ResourceNames.SafeResources.IndexOf(name);
            int bioBrickIndex = ResourceNames.SafeResources.IndexOf(ResourceNames.Biobrick);
            int powerFloorIndex = ResourceNames.SafeResources.IndexOf(ResourceNames.PowerFloor);
            int cornerIndex = ResourceNames.SafeResources.IndexOf(ResourceNames.SectionalCorner2x2);

            if (index < bioBrickIndex) return true;
            if (index >= powerFloorIndex && index <= cornerIndex) return true;
            return false;
        }
    }
}
