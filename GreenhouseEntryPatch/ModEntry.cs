using HarmonyLib; // el diavolo nuevo
using StardewModdingAPI;
using StardewValley.Buildings;

namespace GreenhouseEntryPatch;

public class ModEntry : Mod
{
	public override void Entry(IModHelper helper) => new Harmony(this.Helper.ModRegistry.ModID).Patch(original: AccessTools.Method(typeof(GreenhouseBuilding), "CanDrawEntranceTiles"), postfix: new HarmonyMethod(this.GetType(), nameof(ModEntry.Greenhouse_CanDrawEntranceTiles_Postfix)));

	public static void Greenhouse_CanDrawEntranceTiles_Postfix(ref bool __result) => __result = false;
}
