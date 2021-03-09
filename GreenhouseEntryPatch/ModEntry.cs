using Harmony; // el diavolo
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace GreenhouseEntryPatch
{
	public interface IGenericModConfigMenuAPI
	{
		void RegisterModConfig(IManifest mod, Action revertToDefault, Action saveToFile);
		void RegisterSimpleOption(IManifest mod, string optionName, string optionDesc, Func<bool> optionGet, Action<bool> optionSet);
		void RegisterLabel(IManifest mod, string labelName, string labelDesc);
	}

	public class Config
	{
		public bool HideGreenhouseTiles { get; set; } = true;
		public bool HideGreenhouseShadow { get; set; } = false;
		public bool GreenhouseSoftShadow { get; set; } = false;
		public bool HideAllOtherShadows { get; set; } = false;
		public bool HideBarnShadow { get; set; } = false;
		public bool HideCoopShadow { get; set; } = false;
		public bool HideShedShadow { get; set; } = false;
		public bool HideWellShadow { get; set; } = false;
		public bool HideMillShadow { get; set; } = false;
		public bool HideSiloShadow { get; set; } = false;
		public bool HideStableShadow { get; set; } = false;
		public bool HideFishPondShadow { get; set; } = false;
		public bool HideShippingBinShadow { get; set; } = false;
		public bool HideSlimeHutchShadow { get; set; } = false;
		public bool HideCabinShadow { get; set; } = false;
		public bool HideObeliskShadow { get; set; } = false;
		public bool HideGoldClockShadow { get; set; } = false;
		public bool HideJunimoHutShadow { get; set; } = false;
	}

	public class AssetManager : IAssetEditor
	{
		public bool CanEdit<T>(IAssetInfo asset)
		{
			return asset.AssetName.StartsWith("Buildings") && !asset.AssetName.EndsWith("_PaintMask") && !asset.AssetName.EndsWith("Greenhouse");
		}

		public void Edit<T>(IAssetData asset)
		{
			// Force baked-in shadows for any buildings to be fully transparent if specified in config
			Color shadowColour = new Color(18, 0, 11, 89);
			string buildingName = asset.AssetName.Split('\\').Last().Split(' ').Last();
			PropertyInfo[] properties = ModEntry.Instance.Config.GetType().GetProperties();
			PropertyInfo property = properties.FirstOrDefault(p => p.Name.Contains(buildingName));
			if (ModEntry.Instance.Config.HideAllOtherShadows || (property != null && (bool)property.GetValue(ModEntry.Instance.Config)))
			{
				Texture2D sprite = asset.AsImage().Data;
				Color[] pixels = new Color[sprite.Width * sprite.Height];
				sprite.GetData(pixels);
				for (int i = 0; i < pixels.Length; ++i)
				{
					if (pixels[i] == shadowColour)
					{
						pixels[i].A = 0;
					}
				}
				sprite.SetData(pixels);
				asset.ReplaceWith(sprite);
			}
		}
	}

	public class ModEntry : Mod
	{
		internal static ModEntry Instance;
		internal Config Config;
		internal ITranslationHelper i18n => Helper.Translation;

		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<Config>();
			Helper.Content.AssetEditors.Add(new AssetManager());
			Helper.Events.GameLoop.GameLaunched += this.GameLoopOnGameLaunched;
			this.ApplyPatches();
		}

		private void GameLoopOnGameLaunched(object sender, GameLaunchedEventArgs e)
		{
			this.RegisterGenericModConfigMenuPage();
		}

		private void ApplyPatches()
		{
			HarmonyInstance harmony = HarmonyInstance.Create(Helper.ModRegistry.ModID);
			// Draw or hide shadows on select buildings
			harmony.Patch(
				original: AccessTools.Method(typeof(Building), "drawShadow"),
				prefix: new HarmonyMethod(this.GetType(), nameof(Building_DrawShadow_Prefix)));
			// Draw or hide entrance tiles on greenhouse
			harmony.Patch(
				original: AccessTools.Method(typeof(GreenhouseBuilding), "CanDrawEntranceTiles"),
				prefix: new HarmonyMethod(this.GetType(), nameof(Greenhouse_CanDrawEntranceTiles_Prefix)));
			// Draw or hide shadow on greenhouse
			harmony.Patch(
				original: AccessTools.Method(typeof(GreenhouseBuilding), "drawShadow"),
				prefix: new HarmonyMethod(this.GetType(), nameof(Greenhouse_DrawShadow_Prefix)));
			// Draw generic shadow on greenhouse
			harmony.Patch(
				original: AccessTools.Method(typeof(GreenhouseBuilding), "drawShadow"),
				prefix: new HarmonyMethod(this.GetType(), nameof(Greenhouse_DrawGenericShadow_Prefix)));
		}

		private void RegisterGenericModConfigMenuPage()
		{
			IGenericModConfigMenuAPI api = Helper.ModRegistry.GetApi<IGenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
			if (api == null)
				return;

			api.RegisterModConfig(ModManifest,
				revertToDefault: () => Config = new Config(),
				saveToFile: () =>
				{
					// Apply changes to config
					Helper.WriteConfig(Config);

					// Reload building sprite assets to reflect which buildings should have shadows embedded in their sprites
					IEnumerable<string> keys = Game1.content.Load<Dictionary<string, string>>(@"Data/Blueprints").Where(pair => pair.Value.Split('/')[0] != "animal").Select(pair => pair.Key);
					foreach (string key in keys)
					{
						Helper.Content.InvalidateCache("Buildings/" + key);
					}
				});

			// Populate config with all (assumed boolean) config values
			List<string> menu = Config.GetType().GetProperties().Select(p => p.Name).ToList();

			// Add labels between options manually
			menu.Insert(4, "SpecificBuildingsOptions");
			menu.Insert(3, "OtherBuildingsOptions");
			menu.Insert(0, "GreenhouseOptions");
			foreach (string entry in menu)
			{
				string key = entry.ToLower();
				Translation name, description;
				PropertyInfo property = Config.GetType().GetProperty(entry);
				if (property != null)
				{
					// Real properties
					name = i18n.Get("config." + key + ".name");
					description = i18n.Get("config." + key + ".description");
					api.RegisterSimpleOption(ModManifest,
						optionName: name.HasValue() ? name : property.Name,
						optionDesc: description.HasValue() ? description : null,
						optionGet: () => (bool)property.GetValue(Config),
						optionSet: (bool value) => property.SetValue(Config, value));
				}
				else
				{
					// Labels
					name = i18n.Get("config." + key + ".label");
					api.RegisterLabel(ModManifest,
						labelName: name,
						labelDesc: null);
				}
			}
		}

		public static bool Building_DrawShadow_Prefix(Building __instance)
		{
			if (Instance.Config.HideAllOtherShadows)
				return false;
			PropertyInfo property = Instance.Config.GetType().GetProperties().FirstOrDefault(p => p.Name.Contains(__instance.buildingType.Value.Split(' ').Last()));
			return property == null || !(bool)property.GetValue(Instance.Config);
		}

		public static bool Greenhouse_CanDrawEntranceTiles_Prefix()
		{
			return !Instance.Config.HideGreenhouseTiles;
		}

		public static bool Greenhouse_DrawShadow_Prefix()
		{
			return !Instance.Config.HideGreenhouseShadow;
		}

		public static bool Greenhouse_DrawGenericShadow_Prefix(GreenhouseBuilding __instance, SpriteBatch b, int localX = -1, int localY = -1)
		{
			if (!Instance.Config.HideGreenhouseShadow && Instance.Config.GreenhouseSoftShadow)
			{
				const int entryTilesWide = 3;
				float alpha = Instance.Helper.Reflection.GetField<NetFloat>(__instance, "alpha").GetValue();
				Vector2 basePosition = (localX == -1)
					? Game1.GlobalToLocal(new Vector2(__instance.tileX.Value * Game1.tileSize, (__instance.tileY.Value + __instance.tilesHigh.Value) * Game1.tileSize))
					: new Vector2(localX, localY + (__instance.getSourceRectForMenu().Height * 4));
				Vector2 topPosition = Game1.GlobalToLocal(new Vector2(__instance.tileX.Value * Game1.tileSize, __instance.tileY.Value * Game1.tileSize));
				Color colour = Color.White * ((localX == -1) ? alpha : 1f);

				// Draw shadow underneath greenhouse (visible at the sides of the vanilla sprite and may be visible in custom sprites)
				b.Draw(
					texture: Game1.mouseCursors,
					destinationRectangle: new Rectangle(
						(int)topPosition.X, (int)topPosition.Y,
						__instance.tilesWide.Value * Game1.tileSize, __instance.tilesHigh.Value * Game1.tileSize),
					sourceRectangle: new Rectangle(Building.leftShadow.X, Building.leftShadow.Y, 1, 1),
					color: colour,
					rotation: 0f, origin: Vector2.Zero, SpriteEffects.None, layerDepth: 1E-05f);
				// Shadow start
				b.Draw(
					texture: Game1.mouseCursors,
					position: basePosition,
					sourceRectangle: Building.leftShadow,
					color: colour,
					rotation: 0f, origin: Vector2.Zero, scale: Game1.pixelZoom, SpriteEffects.None, layerDepth: 1E-05f);
				for (int x = 1; x < __instance.tilesWide.Value - 1; x++)
				{
					// Avoid drawing over entry tiles if enabled
					if (!Instance.Config.HideGreenhouseTiles
						&& x > (__instance.tilesWide.Value - entryTilesWide) / 2
						&& x < __instance.tilesWide.Value - ((__instance.tilesWide.Value - entryTilesWide) / 2))
						continue;
					// Shadow middle
					b.Draw(
						texture: Game1.mouseCursors,
						position: basePosition + new Vector2(x * 64, 0f),
						sourceRectangle: Building.middleShadow,
						color: colour,
					rotation: 0f, origin: Vector2.Zero, scale: Game1.pixelZoom, SpriteEffects.None, layerDepth: 1E-05f);
				}
				// Shadow end
				b.Draw(
					texture: Game1.mouseCursors,
					position: basePosition + new Vector2((__instance.tilesWide.Value - 1) * 64, 0f),
					sourceRectangle: Building.rightShadow,
					color: colour,
					rotation: 0f, origin: Vector2.Zero, scale: Game1.pixelZoom, SpriteEffects.None, layerDepth: 1E-05f);
				
				return false;
			}

			return true;
		}
	}
}
