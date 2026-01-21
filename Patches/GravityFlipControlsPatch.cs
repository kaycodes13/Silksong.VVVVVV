using HarmonyLib;

namespace VVVVVV.Patches;

[HarmonyPatch(typeof(HeroController))]
internal static class GravityFlipControlsPatch {

	[HarmonyPatch(nameof(HeroController.Awake))]
	[HarmonyPostfix]
	private static void FixFlippingOnReload() {
		V6Plugin.GravityIsFlipped = false;
	}

	[HarmonyPatch(nameof(HeroController.HeroJump), [typeof(bool)])]
	[HarmonyPostfix]
	private static void FlipOnJump(HeroController __instance) {
		V6Plugin.FlipGravity(__instance, jumpBoost: true);
		__instance.CancelHeroJump();
	}

	[HarmonyPatch(nameof(HeroController.HeroJumpNoEffect))]
	[HarmonyPostfix]
	private static void FlipOnBackflip(HeroController __instance) {
		V6Plugin.FlipGravity(__instance, jumpBoost: true);
		__instance.CancelHeroJump();
	}

	[HarmonyPatch(nameof(HeroController.DoDoubleJump))]
	[HarmonyPostfix]
	private static void FlipOnDoubleJump(HeroController __instance) {
		V6Plugin.FlipGravity(__instance);
		__instance.CancelHeroJump();
	}

	[HarmonyPatch(nameof(HeroController.HazardRespawn))]
	[HarmonyPrefix]
	private static void FlipOnHazardRespawn(HeroController __instance) {
		if (V6Plugin.GravityIsFlipped)
			V6Plugin.FlipGravity(__instance, force: true);
	}

	[HarmonyPatch(nameof(HeroController.Respawn))]
	[HarmonyPrefix]
	private static void FlipOnRespawn(HeroController __instance) {
		if (V6Plugin.GravityIsFlipped)
			V6Plugin.FlipGravity(__instance, force: true);
	}

}
