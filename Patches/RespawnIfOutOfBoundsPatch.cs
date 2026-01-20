using HarmonyLib;

namespace VVVVVV.Patches;

[HarmonyPatch(typeof(HeroController), nameof(HeroController.Update10))]
internal static class RespawnIfOutOfBoundsPatch {

	private static void Postfix(HeroController __instance) {
		if (
			GameManager.SilentInstance is GameManager gm && gm.IsGameplayScene()
			&& V6Plugin.GravityIsFlipped
			&& __instance.transform.position.y > gm.sceneHeight + 20
		) {
			V6Plugin.QueueRespawnHero();
		}
	}

}
