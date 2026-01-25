using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TeamCherry.Localization;

namespace VVVVVV.Patches;

[HarmonyPatch]
internal static class FaydownUIPatch {

	internal const string LANG_SHEET = $"Mods.{V6Plugin.Id}";
	private static string[]? langKeys;

	[HarmonyPatch(typeof(Language), nameof(Language.Get), [typeof(string), typeof(string)])]
	[HarmonyPrefix]
	private static void ReplaceFaydownStrings(ref string key, ref string sheetTitle) {
		langKeys ??= [.. Language.GetKeys(LANG_SHEET)];

		if (key == "PROMPT_DJ") {
			// this key should be left alone when faydown is normal
			if (V6Plugin.FaydownFlipsGravity)
				sheetTitle = LANG_SHEET;
		}
		else if (key == "INV_DESC_DRESS_DJ") {
			sheetTitle = LANG_SHEET;
			key += V6Plugin.FaydownFlipsGravity ? "_ON" : "_OFF";
		}
		else if (langKeys.Contains(key))
			sheetTitle = LANG_SHEET;
	}

	[HarmonyPatch(typeof(CollectableItemStates), nameof(CollectableItemStates.GetDescription))]
	[HarmonyPrefix]
	private static void AddExtraDescription(CollectableItemStates __instance) {
		if (__instance.name != "Dresses")
			return;

		int index = __instance.GetCurrentStateIndex();

		if (
			__instance.states[index].DescriptionExtra == default(LocalisedString)
			&& __instance.states[index].Test.TestGroups.SelectMany(y => y.Tests)
				.Any(z => z.FieldName == nameof(PlayerData.hasDoubleJump))
		) {
			__instance.states[index] = __instance.states[index] with {
				DescriptionExtra = new(LANG_SHEET, "INV_DESC_DRESS_DJ_EXTRA")
			};
		}
	}

}
