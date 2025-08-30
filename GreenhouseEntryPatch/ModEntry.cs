using HarmonyLib; // el diavolo nuevo
using StardewModdingAPI;
using StardewValley.Buildings;

namespace GreenhouseEntryPatch;

public class ModEntry : Mod
{
	public override void Entry(IModHelper helper) => new Harmony(this.Helper.ModRegistry.ModID).Patch(AccessTools.Method(typeof(GreenhouseBuilding), "CanDrawEntranceTiles"), new HarmonyMethod(this.GetType(), nameof(ModEntry.Greenhouse_CanDrawEntranceTiles_Prefix)));

	public static bool Greenhouse_CanDrawEntranceTiles_Prefix() => false;
}
