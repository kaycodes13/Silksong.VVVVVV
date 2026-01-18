using HarmonyLib;

namespace VVVVVV.Patches;

[HarmonyPatch(typeof(HeroController), nameof(HeroController.FinishedEnteringScene))]
internal static class OpeningSequencePatch {

	private static void Prefix(HeroController __instance, ref bool __state) {
		__state = __instance.isEnteringFirstLevel;
	}

	private static void Postfix(HeroController __instance, ref bool __state) {
		if (__state)
			__instance.SetHazardRespawn(
				__instance.transform.position,
				__instance.cState.facingRight
			);
	}

}
