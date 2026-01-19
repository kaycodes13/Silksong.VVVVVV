using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Silksong.FsmUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VVVVVV.Utils;

namespace VVVVVV.Patches;

[HarmonyPatch]
internal static class HeroFsmsPatch {

	private static bool
		didDownAttackEdit = false,
		didSprintEdit = false;


	[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetupNewPlayerData))]
	[HarmonyPrefix]
	private static void ResetOnNewGame() => ResetEditedState();


	[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetupExistingPlayerData))]
	[HarmonyPrefix]
	private static void ResetOnLoadGame() => ResetEditedState();


	/*
	High priority prefixes because I'm only aiming to affect vanilla game content.
	
	The sheer amount of things you can edit in an fsm, and the fact that someone can
	manually add support for this mod in their mod, makes it impossible for me to cover
	every single possibility here.
	*/

	[HarmonyPatch(typeof(HeroController), nameof(HeroController.Start))]
	[HarmonyPrefix]
	[HarmonyPriority(Priority.First)]
	private static void HeroStart(HeroController __instance) {
		if (!didDownAttackEdit)
			EditDownAttacks(__instance);
		if (!didSprintEdit)
			EditSprint(__instance);
	}


	[HarmonyPatch(typeof(PlayMakerFSM), nameof(PlayMakerFSM.Start))]
	[HarmonyPrefix]
	[HarmonyPriority(Priority.First)]
	private static void PFSMStart(PlayMakerFSM __instance) {
		HeroController? hc = __instance.GetComponent<HeroController>();
		if (!hc) return;

		if (!didDownAttackEdit && ReferenceEquals(__instance, hc.crestAttacksFSM))
			EditDownAttacks(hc);
		else if (!didSprintEdit && ReferenceEquals(__instance, hc.sprintFSM))
			EditSprint(hc);
	}

	private static void ResetEditedState() {
		didDownAttackEdit = didSprintEdit = false;
	}


	private static void EditDownAttacks(HeroController hc) {
		didDownAttackEdit = true;

		GameObject hero = hc.gameObject;
		PlayMakerFSM fsm = hc.crestAttacksFSM;
		if (!fsm.Fsm.preprocessed)
			fsm.Preprocess();

		string[] varNames = [
			"Rpr DownSlash",
			"Witch Downslash",
			"Followup Slash",
			"SpinSlash",
			"SpinSlashRage",
			"Shaman Downslash",
			"Toolmaster Downslash",
			"Toolmaster Downslash Charged",
		];

		FsmGameObject[] downAttacks = [..
			varNames.Select(fsm.FindGameObjectVariable)
					.Where(x => x != null).Cast<FsmGameObject>()
		];

		FsmState idleState = fsm.GetState("Idle")!;

		fsm.DoGravityFlipEdit(
			hc,
			checkStates: [.. idleState.Transitions.Select(x => x.ToFsmState)],
			otherEdits: FlipKnockbackDirection
		);

		void FlipKnockbackDirection() {
			foreach(GameObject attack in downAttacks.Select(x => x.Value)) {
				DamageEnemies damager = attack.GetComponent<DamageEnemies>();
				if (!damager)
					continue;
				int direction = DirectionUtils.GetCardinalDirection(damager.direction);
				if (
					(V6Plugin.GravityIsFlipped && direction == DirectionUtils.Down)
					|| (!V6Plugin.GravityIsFlipped && direction == DirectionUtils.Up)
				) {
					damager.FlipDirection();
				}
			}

		}
	}

	private static void EditSprint(HeroController hc) {
		didSprintEdit = true;

		GameObject hero = hc.gameObject;
		PlayMakerFSM fsm = hc.sprintFSM;
		if (!fsm.Fsm.preprocessed)
			fsm.Preprocess();
		string pirouetteAnimName = "Shuttlecock";
		FsmBool
			isFlipped = fsm.GetBoolVariable(FsmFlipUtil.FLIP_BOOL_NAME),
			startedFromJump = fsm.GetBoolVariable($"{V6Plugin.Id} Jumped To Get Airborne"),
			shouldPirouette = fsm.GetBoolVariable($"{V6Plugin.Id} Should Pirouette");
		FsmString
			sprintAirAnim = fsm.FindStringVariable("Sprint Air Anim")!;

		FsmState
			airSprintL = fsm.GetState("Air Sprint L")!,
			airSprintR = fsm.GetState("Air Sprint R")!,
			airSprintLoop = fsm.GetState("Air Sprint Loop")!,
			jumpAntic = fsm.GetState("Jump Antic")!,
			dashedState = fsm.GetState("Dashed")!;

		dashedState.InsertAction(0, new SetBoolValue {
			boolVariable = startedFromJump,
			boolValue = false
		});
		jumpAntic.InsertAction(0, new SetBoolValue {
			boolVariable = startedFromJump,
			boolValue = true
		});

		foreach (var state in new FsmState[] { airSprintL, airSprintR }) {
			int index = Array.FindIndex(
				state.Actions,
				x => x is Tk2dPlayAnimationWithEvents t2d
					&& t2d.clipName.UsesVariable
					&& t2d.clipName.Name == sprintAirAnim.Name
			);
			state.InsertActions(index,
				new BoolAllTrue {
					boolVariables = [isFlipped, startedFromJump],
					storeResult = shouldPirouette,
				},
				new ConvertBoolToString {
					boolVariable = shouldPirouette,
					stringVariable = sprintAirAnim,

					falseString = sprintAirAnim,
					trueString = pirouetteAnimName
				}
			);
		}

		int loopIndex = Array.FindIndex(
			airSprintLoop.Actions,
			x => x is Tk2dPlayAnimation t2d
				&& t2d.clipName.Value == "Sprint Air Loop"
		);
		airSprintLoop.InsertAction(1 + loopIndex, new tk2dPlayAnimationConditional {
			Target = new FsmOwnerDefault(),
			AnimName = pirouetteAnimName,
			Condition = shouldPirouette
		});

		fsm.DoGravityFlipEdit(
			hc,
			checkStates: [
				fsm.GetState("Start Sprint")!,
				airSprintL,
				airSprintR,
				fsm.GetState("Start Attack")!,
			],
			affectedStates: [
				fsm.GetState("Start Sprint")!,
				fsm.GetState("Bump Up")!,
				fsm.GetState("Bump Up 2")!,
				fsm.GetState("Witch Lash Start")!,
				fsm.GetState("Reaper End")!,
				fsm.GetState("Wanderer Recoil")!,
				fsm.GetState("RecoilStab Dash")!,
				fsm.GetState("Warrior Leap")!,
				fsm.GetState("Warrior Slash")!,
				fsm.GetState("Shaman Leap")!,
				fsm.GetState("Shaman Slash")!,
			],
			otherEdits: ReaperCurveFloat
		);

		void ReaperCurveFloat() {
			CurveFloat curveFloat = fsm.GetState("Reaper Upper")!.GetFirstActionOfType<CurveFloat>()!;
			AnimationCurve anim = curveFloat.animCurve.curve;
			List<Keyframe> newKeys = [];
			foreach (Keyframe key in anim.keys)
				newKeys.Add(new(key.time, -key.value, key.inTangent, key.outTangent, key.inWeight, key.outWeight));
			anim.SetKeys([.. newKeys]);
		}
	}

}
