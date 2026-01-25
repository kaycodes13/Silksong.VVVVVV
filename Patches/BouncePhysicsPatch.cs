using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static VVVVVV.Utils.ILUtil;

namespace VVVVVV.Patches;

[HarmonyPatch]
internal static class BouncePhysicsPatch {

	[HarmonyPatch(typeof(BounceShared), nameof(BounceShared.BouncePull))]
	[HarmonyPrefix]
	private static void InvertBouncePullPos(Transform transform, ref Vector2 heroBouncePos) {
		if (!V6Plugin.GravityIsFlipped)
			return;

		float yOffset = Mathf.Abs(heroBouncePos.y - transform.position.y);

		float newBounceY = (heroBouncePos.y > transform.position.y)
			? transform.position.y - yOffset
			: transform.position.y + yOffset;

		heroBouncePos = heroBouncePos with { y = newBounceY };
	}

	#pragma warning disable HARMONIZE001 // A single unambiguous target could not be resolved
	// Harmonize, it's just an enumerator, please chill
	[HarmonyPatch(typeof(BounceShared), nameof(BounceShared.BouncePull), MethodType.Enumerator)]
	[HarmonyTranspiler]
	private static IEnumerable<CodeInstruction> InvertBouncePullPos(IEnumerable<CodeInstruction> instructions) {
		string fromPosName = "", toPosName = "";
		return new CodeMatcher(instructions)

			// find fromPos and toPos
			.Start()
			.MatchEndForward([
				new(Ldfld),
				new(x => Callvirt(x, "get_position")),
				new (x => Stfld(x, out fromPosName)),
			])
			.MatchEndForward([
				new(OpCodes.Newobj),
				new(x => Stfld(x, out toPosName)),
			])

			// if (<toPos>5__4.y < <fromPos>5__3.y && !flag)
			.MatchEndForward([
				new(x => x.opcode == OpCodes.Bge_Un || x.opcode == OpCodes.Bge_Un_S)
			])
			.MatchEndBackwards([
				new(x => x.opcode == OpCodes.Ldflda && x.operand is FieldInfo fi && fi.Name == fromPosName),
				new(x => Ldfld(x, "y"))
			])
			.Advance(1)
			.Insert(InvertFloatIfFlipped())
			.MatchEndBackwards([
				new(x => x.opcode == OpCodes.Ldflda && x.operand is FieldInfo fi && fi.Name == toPosName),
				new(x => Ldfld(x, "y"))
			])
			.Advance(1)
			.Insert(InvertFloatIfFlipped())

			.InstructionEnumeration();
	}
	#pragma warning restore HARMONIZE001



	[HarmonyPatch(typeof(HitRigidbody2D), nameof(HitRigidbody2D.Hit))]
	[HarmonyPrefix]
	private static void InvertHitRb2d(ref HitInstance damageInstance)
		=> UnflipHitDirection(ref damageInstance);


	[HarmonyPatch(typeof(BounceBalloon), nameof(BounceBalloon.Hit))]
	[HarmonyPrefix]
	private static void InvertBounceBalloon(ref HitInstance damageInstance)
		=> UnflipHitDirection(ref damageInstance);

	[HarmonyPatch(typeof(BounceBalloon), nameof(BounceBalloon.RaiseMovement))]
	[HarmonyPostfix]
	private static void InvertBounceBalloon2() {
		if (V6Plugin.GravityIsFlipped)
			V6Plugin.FlipHeroVelocity();
	}


	[HarmonyPatch(typeof(BouncePod), nameof(BouncePod.Hit))]
	[HarmonyPrefix]
	private static void InvertBouncePod(ref HitInstance damageInstance)
		=> UnflipHitDirection(ref damageInstance);

	[HarmonyPatch(typeof(BouncePod), nameof(BouncePod.Hit))]
	[HarmonyTranspiler]
	private static IEnumerable<CodeInstruction> InvertBouncePod(IEnumerable<CodeInstruction> instructions) {
		int heroIndex = -1;
		return new CodeMatcher(instructions)
			// find a ref to the hero instance
			.Start()
			.MatchEndForward([
				new(x => Call(x, typeof(HeroController), "get_instance")),
				new(x => Stloc(x, out heroIndex)),
			])

			// if (instance.transform.position.y < base.transform.position.y + num ...)
			.Start()
			.MatchEndForward([
				new(x => Ldloc(x, heroIndex)),
				new(x => Callvirt(x, "get_transform")),
				new(x => Callvirt(x, "get_position")),
				new(x => Ldfld(x, "y")),
			])
			.Advance(1)
			.Insert(InvertFloatIfFlipped())
			.MatchEndForward([
				new(OpCodes.Add),
			])
			.Advance(1)
			.Insert(InvertFloatIfFlipped())

			.InstructionEnumeration();
	}


	// this was responsible for reaper down slash breaking all the time
	[HarmonyPatch(typeof(NailSlashRecoil), nameof(NailSlashRecoil.GetActualHitDirection))]
	[HarmonyPostfix]
	private static void InvertNSRecoil(ref float __result) {
		int cardinal = DirectionUtils.GetCardinalDirection(__result);
		if (V6Plugin.GravityIsFlipped && (cardinal == DirectionUtils.Up || cardinal == DirectionUtils.Down))
			__result += 180;
	}


	// flipped in the first place to damage enemies properly, but that breaks bouncing
	private static void UnflipHitDirection(ref HitInstance damageInstance) {
		const float UP_ANGLE = 90, DOWN_ANGLE = 270;
		if (!V6Plugin.GravityIsFlipped || !damageInstance.IsNailDamage)
			return;

		if (damageInstance.Direction == UP_ANGLE)
			damageInstance = damageInstance with { Direction = DOWN_ANGLE };
		else if (damageInstance.Direction == DOWN_ANGLE)
			damageInstance = damageInstance with { Direction = UP_ANGLE };
	}

}
