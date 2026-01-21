using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VVVVVV;

[BepInAutoPlugin(id: "io.github.kaycodes13.vvvvvv")]
[BepInDependency("org.silksong-modding.fsmutil", "0.3.5")]
[BepInDependency("org.silksong-modding.modmenu", "0.2.0")]
public partial class V6Plugin : BaseUnityPlugin, IModMenuCustomMenu {

	public static bool GravityIsFlipped { get; internal set; } = false;

	internal static ManualLogSource Log { get; private set; } = null!;

	private Harmony Harmony { get; } = new(Id);

	private ConfigEntry<KeyCode> RespawnKey { get; set; } = null!;
	ChoiceElement<KeyCode>? respawnKeyOption = null;
	private static readonly List<KeyCode> bindableKeys = [
		KeyCode.None,
		KeyCode.F3,
		KeyCode.F4,
		KeyCode.F5,
		KeyCode.F6,
		KeyCode.F7,
		KeyCode.F8,
		KeyCode.F9,
		KeyCode.F10,
		KeyCode.Backspace,
		KeyCode.Tab,
		KeyCode.Backslash,
		KeyCode.Slash,
		KeyCode.LeftAlt,
		KeyCode.RightAlt,
	];

	private const float FLIP_TIME_LIMIT = 0.1f;
	private const float RESPAWN_TIME_LIMIT = 5;

	private static float flipTimer = 0;
	private static float respawnTimer = 0;

	private void Awake() {
		Harmony.PatchAll();
		Log = Logger;

		RespawnKey = Config.Bind("", "RespawnKeybind", KeyCode.None);
		if (!bindableKeys.Contains(RespawnKey.Value))
			RespawnKey.Value = KeyCode.None;
		RespawnKey.SettingChanged += RespawnKeyChanged;

		Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
	}

	private void OnDestroy() {
		Harmony.UnpatchSelf();
		if (GravityIsFlipped) {
			FlipGravity(HeroController.instance, force: true);
			GravityIsFlipped = false;
		}
	}

	private void Update() {
		if (
			GameManager.SilentInstance is GameManager gm
			&& gm.IsGameplayScene() && !gm.IsGamePaused()
			&& !HeroController.instance.controlReqlinquished
			&& respawnTimer <= 0
			&& RespawnKey.Value != KeyCode.None && Input.GetKeyDown(RespawnKey.Value)
		) {
			respawnTimer = RESPAWN_TIME_LIMIT;
			QueueRespawnHero();
		}

		if (respawnTimer > 0)
			respawnTimer -= Time.deltaTime;
		if (flipTimer > 0)
			flipTimer -= Time.deltaTime;
	}

	public string ModMenuName() => Name;

	public AbstractMenuScreen BuildCustomMenu() {
		TextButton respawnBtn = new("Respawn Hornet") {
			OnSubmit = QueueRespawnHero
		};
		respawnKeyOption = new(
			"Respawn Key",
			bindableKeys,
			"Useful for getting unstuck from weird corners in the roof."
		) {
			Value = RespawnKey.Value
		};
		respawnKeyOption.OnValueChanged += SetKeybind;

		SimpleMenuScreen screen = new(Name);
		screen.AddRange([respawnBtn, respawnKeyOption]);

		return screen;

		void SetKeybind(KeyCode key) {
			RespawnKey.Value = key;
		}
	}

	private void RespawnKeyChanged(object sender, System.EventArgs e) {
		if (respawnKeyOption != null && respawnKeyOption.Value != RespawnKey.Value)
			respawnKeyOption.Value = RespawnKey.Value;
	}

	internal static void QueueRespawnHero() {
		if (GameManager.SilentInstance is not GameManager gm || gm.IsNonGameplayScene())
			return;

		gm.StartCoroutine(RespawnHero());

		IEnumerator RespawnHero() {
			if (gm.IsGamePaused()) {
				IEnumerator unpauseIterator = gm.PauseGameToggle(playSound: false);
				while (unpauseIterator.MoveNext())
					yield return unpauseIterator.Current;
			}

			yield return new WaitForEndOfFrame();

			HeroController.instance.doingHazardRespawn = true;
			HeroController.instance.SetState(ActorStates.no_input);
			HeroController.instance.heroInPositionDelayed += ForceRemaskerUpdate;
			gm.HazardRespawn();
		}

		void ForceRemaskerUpdate(bool _) {
			Remasker[] remaskers = FindObjectsByType<Remasker>(FindObjectsSortMode.None);
			foreach (Remasker rem in remaskers) {
				bool active = rem.gameObject.activeSelf;
				rem.gameObject.SetActive(false);
				rem.gameObject.SetActive(active);
			}
			HeroController.instance.heroInPositionDelayed -= ForceRemaskerUpdate;
		}
	}

	internal static void FlipGravity(HeroController hc, bool jumpBoost = false, bool force = false) {
		if (!hc || (flipTimer > 0 && !force))
			return;

		GravityIsFlipped = !GravityIsFlipped;
		flipTimer = FLIP_TIME_LIMIT;

		hc.MAX_FALL_VELOCITY *= -1;
		hc.MAX_FALL_VELOCITY_WEIGHTED *= -1;
		hc.MAX_FALL_VELOCITY_DJUMP *= -1;
		hc.BOUNCE_VELOCITY *= -1;
		hc.FLOAT_SPEED *= -1;
		hc.JUMP_SPEED *= -1;

		hc.DEFAULT_GRAVITY *= -1;
		hc.AIR_HANG_GRAVITY *= -1;
		hc.AIR_HANG_ACCEL *= -1;
		hc.rb2d.gravityScale *= -1;

		Vector3 scale = hc.transform.localScale;
		scale.y *= -1;
		hc.transform.localScale = scale;
		
		float yVel = jumpBoost ? -(hc.JUMP_SPEED / 2f) : 0;
		hc.StartCoroutine(ApplyBoostVelocity());

		IEnumerator ApplyBoostVelocity() {
			yield return null;
			hc.rb2d.linearVelocityY = yVel;
		}

		foreach(HeroController.ConfigGroup cfg in hc.configs) {
			bool flippedCharge = false;
			foreach(Transform attack in cfg.ActiveRoot.transform) {
				if (attack && attack.GetComponent<NailSlashTravel>() is NailSlashTravel nailTravel) {
					if (cfg.ChargeSlash == attack.gameObject)
						flippedCharge = true;
					hc.StartCoroutine(InvertNailTravel(nailTravel));
				}
			}
			if (!flippedCharge && cfg.ChargeSlash && cfg.ChargeSlash.GetComponent<NailSlashTravel>() is NailSlashTravel chargeTravel) {
				hc.StartCoroutine(InvertNailTravel(chargeTravel));
			}
		}

		IEnumerator InvertNailTravel(NailSlashTravel nailTravel) {
			while (nailTravel.travelRoutine != null)
				yield return null;

			Vector2 dist = nailTravel.travelDistance;
			dist = dist with { y = -dist.y };
			nailTravel.travelDistance = dist;

			nailTravel.groundedYOffset *= -1;

			if (nailTravel.maxYOffset != null)
				nailTravel.maxYOffset.Value *= -1;
		}
	}

	internal static void FlipHeroVelocity() {
		if (!HeroController.instance)
			return;
		Vector2 vel = HeroController.instance.rb2d.linearVelocity;
		vel = vel with { y = -vel.y };
		HeroController.instance.rb2d.linearVelocity = vel;
	}

}
