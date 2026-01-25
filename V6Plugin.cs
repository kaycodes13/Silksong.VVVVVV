using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VVVVVV;

[BepInAutoPlugin(id: "io.github.kaycodes13.vvvvvv")]
[BepInDependency("org.silksong-modding.fsmutil", "0.3.12")]
[BepInDependency("org.silksong-modding.modmenu", "0.2.0")]
[BepInDependency("org.silksong-modding.i18n")]
public partial class V6Plugin : BaseUnityPlugin, IModMenuCustomMenu {

	internal static V6Plugin Instance { get; private set; } = null!;

	public static bool GravityIsFlipped { get; internal set; } = false;
	public static bool FaydownFlipsGravity => Instance.faydownFlips?.Value ?? false;

	internal static ManualLogSource Log { get; private set; } = null!;

	private Harmony Harmony { get; } = new(Id);

	private ConfigEntry<bool>? faydownFlips;
	private ChoiceElement<bool>? faydownOption;

	private ConfigEntry<KeyCode>? respawnKey;
	private ChoiceElement<KeyCode>? respawnKeyOption;
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

	private const int FLIP_FRAME_LIMIT = 5;
	private const float RESPAWN_TIME_LIMIT = 5;

	private static int flipTimer = 0;
	private static float respawnTimer = 0;

	private void Awake() {
		Instance = this;
		Log = Logger;

		respawnKey = Config.Bind("", "RespawnKeybind", KeyCode.None);
		if (!bindableKeys.Contains(respawnKey.Value))
			respawnKey.Value = KeyCode.None;
		respawnKey.SettingChanged += RespawnKeyChanged;

		faydownFlips = Config.Bind("", "FlipdownCloak", false);
		faydownFlips.SettingChanged += FlipdownChanged;

		Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
		
		void RespawnKeyChanged(object sender, System.EventArgs e) {
			if (respawnKeyOption != null && respawnKeyOption.Value != respawnKey.Value)
				respawnKeyOption.Value = respawnKey.Value;
		}
		void FlipdownChanged(object sender, System.EventArgs e) {
			if (faydownOption != null && faydownOption.Value != faydownFlips.Value)
				faydownOption.Value = faydownFlips.Value;
		}
	}

	private void Start() {
		Harmony.PatchAll();
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
			&& respawnKey!.Value != KeyCode.None && Input.GetKeyDown(respawnKey!.Value)
		) {
			respawnTimer = RESPAWN_TIME_LIMIT;
			QueueRespawnHero();
		}

		if (respawnTimer > 0)
			respawnTimer -= Time.deltaTime;
		if (flipTimer > 0)
			flipTimer--;
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
			Value = respawnKey!.Value
		};
		respawnKeyOption.OnValueChanged += key => respawnKey.Value = key;

		faydownOption = new(
			"Flipdown Cloak",
			ChoiceModels.ForBool("Off", "On"),
			"Make the Faydown Cloak flip gravity instead of jumping."
		) {
			Value = faydownFlips!.Value
		};
		faydownOption.OnValueChanged += value => faydownFlips.Value = value;

		SimpleMenuScreen screen = new(Name);
		screen.AddRange([respawnBtn, respawnKeyOption, faydownOption]);

		return screen;
	}

	internal static void QueueRespawnHero() {
		if (GameManager.SilentInstance is not GameManager gm || gm.IsNonGameplayScene())
			return;

		var hc = HeroController.instance;
		SpriteRenderer screenFader = gm.cameraCtrl.fadeFSM.transform
			.Find("Screen Fader").GetComponent<SpriteRenderer>();

		gm.StartCoroutine(RespawnHero());

		IEnumerator RespawnHero() {
			if (gm.IsGamePaused()) {
				IEnumerator unpauseIterator = gm.PauseGameToggle(playSound: false);
				while (unpauseIterator.MoveNext())
					yield return unpauseIterator.Current;
			}

			yield return new WaitForEndOfFrame();

			hc.doingHazardRespawn = true;
			hc.SetState(ActorStates.no_input);
			hc.heroInPositionDelayed += ForceRemaskerUpdate;
			hc.StartInvulnerable(0.56f);
			gm.cameraCtrl.FadeOut(CameraFadeType.HERO_HAZARD_DEATH);
			while (screenFader.color.a < 1)
				yield return null;
			gm.HazardRespawn();
		}

		void ForceRemaskerUpdate(bool _) {
			Remasker[] remaskers = FindObjectsByType<Remasker>(FindObjectsSortMode.None);
			foreach (Remasker rem in remaskers) {
				bool active = rem.gameObject.activeSelf;
				rem.gameObject.SetActive(false);
				rem.gameObject.SetActive(active);
			}
			hc.heroInPositionDelayed -= ForceRemaskerUpdate;
		}
	}

	internal static void FlipGravity(HeroController hc, bool jumpBoost = false, bool force = false) {
		if (!hc || (flipTimer > 0 && !force))
			return;

		GravityIsFlipped = !GravityIsFlipped;
		flipTimer = FLIP_FRAME_LIMIT;

		hc.MAX_FALL_VELOCITY *= -1;
		hc.MAX_FALL_VELOCITY_WEIGHTED *= -1;
		hc.MAX_FALL_VELOCITY_DJUMP *= -1;
		hc.BOUNCE_VELOCITY *= -1;
		hc.FLOAT_SPEED *= -1;
		hc.JUMP_SPEED *= -1;
		hc.JUMP_SPEED_UPDRAFT_EXIT *= -1; // also used by balloon bounces, for some reason

		hc.WALLSLIDE_ACCEL *= -1;
		hc.WALLSLIDE_SHUTTLECOCK_VEL *= -1;
		hc.WALLCLING_DECEL *= -1;

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
