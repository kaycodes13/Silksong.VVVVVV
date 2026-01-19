using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Silksong.FsmUtil;
using System;
using System.Linq;
using UnityEngine;
using Sv2dComparison = HutongGames.PlayMaker.Actions.SetVelocity2dConditional.ComparisonType;

namespace VVVVVV.Utils;
internal static class FsmFlipUtil {

	internal const string FLIP_BOOL_NAME = $"{V6Plugin.Id} Is Flipped";

	/// <summary>
	/// Performs an FSM edit which causes all <paramref name="checkStates"/> to check the gravity status, and if gravity is flipped, flip all hero-targeting y motion in all the actions in the <paramref name="affectedStates"/>.
	/// </summary>
	/// <remarks>
	/// The edit will ONLY apply to actions which existed at the time this function was called.
	/// </remarks>
	internal static void DoGravityFlipEdit(
		this PlayMakerFSM fsm, HeroController hc,
		FsmState[] checkStates, FsmState[]? affectedStates = null,
		Action? otherEdits = null
	) {
		affectedStates ??= fsm.FsmStates;
		FsmStateAction[] affectedActions = [.. affectedStates.SelectMany(x => x.Actions)];

		FsmBool isFlipped = fsm.GetBoolVariable(FLIP_BOOL_NAME);

		foreach(var state in checkStates)
			state.InsertLambdaMethod(0, FlipState);

		void FlipState(Action finished) {
			if (isFlipped.Value != V6Plugin.GravityIsFlipped) {
				isFlipped.Value = V6Plugin.GravityIsFlipped;
				affectedActions.FlipHeroMotion(hc);
				otherEdits?.Invoke();
			}
			finished();
		}
	}

	/// <summary>
	/// Simultaneously flips all hero-targeting y movements performed by the actions
	/// and prunes actions which don't affect hero's y movement out of the input list.
	/// </summary>
	internal static void FlipHeroMotion(this FsmStateAction[] actions, HeroController hc) {
		actions = [.. actions.Where(x => x != null).Where(x => x.FlipHeroMotion(hc))];
	}

	/// <summary>
	/// Flips all hero-targeting y movements performed by the action.
	/// </summary>
	/// <returns>True if the action is of a type which can affect hero y motion.</returns>
	private static bool FlipHeroMotion<T>(this T action, HeroController hc) where T : FsmStateAction {
		GameObject hero = hc.gameObject;
		switch (action) {
			case SetVelocity2d ac:
				if (ac.gameObject.GetSafe(ac) == hero)
					ac.y.Value *= -1;
				return true;
			case SetVelocityByScale ac:
				if (ac.gameObject.GetSafe(ac) == hero)
					ac.ySpeed.Value *= -1;
				return true;
			case AddForce2d ac:
				if (ac.gameObject.GetSafe(ac) == hero)
					ac.y.Value *= -1;
				return true;
			case Translate ac:
				if (ac.gameObject.GetSafe(ac) == hero)
					ac.y.Value *= -1;
				return true;
			case ClampVelocity2D ac:
				if (ac.gameObject.GetSafe(ac) == hero) {
					ac.yMax.Value *= -1;
					ac.yMin.Value *= -1;
					(ac.yMin, ac.yMax) = (ac.yMax, ac.yMin);
				}
				return true;
			case AccelerateToY ac:
				if (ac.gameObject.GetSafe(ac) == hero)
					ac.targetSpeed.Value *= -1;
				return true;
			case SetGravity2dScale ac:
				if (ac.gameObject.GetSafe(ac) == hero)
					ac.gravityScale.Value *= -1;
				return true;
			case SetGravity2dScaleV2 ac:
				if (ac.gameObject.GetSafe(ac) == hero)
					ac.gravityScale.Value *= -1;
				return true;
			case SetVelocity2dConditional ac:
				if (ac.gameObject.GetSafe(ac) == hero) {
					ac.y.Value *= -1;
					ac.yCondition.value.Value *= -1;
					switch (ac.yCondition.comparisonType) {
						case Sv2dComparison.GreaterThan:
							ac.yCondition.comparisonType = Sv2dComparison.LessThan;
							break;
						case Sv2dComparison.LessThan:
							ac.yCondition.comparisonType = Sv2dComparison.GreaterThan;
							break;
						case Sv2dComparison.GreaterThanOrEqualTo:
							ac.yCondition.comparisonType = Sv2dComparison.LessThanOrEqualTo;
							break;
						case Sv2dComparison.LessThanOrEqualTo:
							ac.yCondition.comparisonType = Sv2dComparison.GreaterThanOrEqualTo;
							break;
					}
				}
				return true;
		}
		return false;
	}

}
