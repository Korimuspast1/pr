using System;
using System.Collections.Generic;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class Abilities
	{
		private sealed class RushState
		{
			public Actor Self;

			public Actor Aimed;

			public float Damage;

			public float Until;

			public float Stun;

			public Vector2 Dir;

			public readonly HashSet<Actor> Hit = new HashSet<Actor>();

			public bool Run;

			public Vector2 Start;

			public float Distance;

			public float Duration;

			public float T0;
		}

		private static readonly Dictionary<string, Ability> ById;

		public static int Answered;

		public static readonly List<KeyValuePair<Ability, Actor>> CreatureUsed;

		private static readonly Dictionary<Actor, float> ReadyAt;

		private static readonly List<Actor> Near;

		private static readonly Ability Firebomb;

		private static readonly Ability BoneToss;

		private static readonly Ability RockThrow;

		private static readonly Ability Snowballs;

		private static readonly Ability RaiseDead;

		private static readonly Ability Fireball;

		private static readonly Ability Hellfire;

		private static readonly Ability PlasmaBolt;

		private static readonly Ability AcidSpit;

		private static readonly Ability Blessing;

		private static readonly Ability Remedy;

		private static readonly Ability Roots;

		private static readonly Ability FrostNova;

		private static readonly Ability FlameBurst;

		private static readonly Ability Blink;

		private static readonly Ability Spores;

		private static readonly Ability Rally;

		private static readonly Ability Grace;

		private static readonly Ability Bloodlust;

		private static readonly Ability GroundSlam;

		private static readonly Ability Howl;

		private static readonly Ability Roar;

		private static readonly Ability Charge;

		private static readonly Ability Pounce;

		private static readonly Ability Hop;

		private static readonly Ability Slide;

		private static readonly Ability Venom;

		private static readonly Ability ShellUp;

		private static readonly Ability Pinch;

		private static readonly Ability Feast;

		private static readonly Ability Rainbow;

		private static RushState PendingRush;

		private static float _npcTimer;

		private static readonly Dictionary<Actor, float> NpcReadyAt;

		public static int CreatureUses;

		internal static Actor Caster;

		public const string EarthId = "earthshaping";

		private static readonly Ability EarthRaise;

		private static readonly Ability EarthSink;

		private static readonly Ability EarthSplit;

		internal static int EarthMoved;

		public const string ChosenKey = "fp_spell";

		public const string LearnedKey = "fp_learned_spells";

		private static readonly string[] RaidSpells;

		public static int ChoiceVersion;

		private static readonly Dictionary<string, Ability> SpellAbilities;

		internal static readonly List<string> SpellsForTest;

		public static Ability For(Actor a)
		{
			string text = null;
			try
			{
				text = ((a != null && a.asset != null) ? a.asset.id : null);
			}
			catch
			{
			}
			if (text == null)
			{
				return null;
			}
			if (ById.TryGetValue(text, out var value))
			{
				return value;
			}
			if (text.StartsWith("zombie", StringComparison.Ordinal))
			{
				return Feast;
			}
			if (text.StartsWith("fire_elemental", StringComparison.Ordinal))
			{
				return FlameBurst;
			}
			if (text.StartsWith("civ_", StringComparison.Ordinal) && ById.TryGetValue(text.Substring(4), out value))
			{
				return value;
			}
			return null;
		}

		public static bool MovesBody(Ability ab)
		{
			if (ab != null)
			{
				if (ab.Motion != AbilityMotion.Leap && ab != Blink)
				{
					return ab.SpellId == "teleport";
				}
				return true;
			}
			return false;
		}

		public static float CooldownLeft(Actor a)
		{
			if (a == null || !ReadyAt.TryGetValue(a, out var value))
			{
				return 0f;
			}
			return Mathf.Max(0f, value - Time.unscaledTime);
		}

		public static void StartCooldown(Actor a, Ability ab)
		{
			if (a != null && ab != null)
			{
				if (ReadyAt.Count > 200)
				{
					ReadyAt.Clear();
				}
				ReadyAt[a] = Time.unscaledTime + ab.Cooldown;
			}
		}

		private static bool PaysMana(Actor a, Ability ab, out int pool)
		{
			int num = 0;
			int num2 = 0;
			try
			{
				num = a.getMaxMana();
				num2 = a.getMaxStamina();
			}
			catch
			{
			}
			bool flag = (ab.IsMagic ? (num > 0) : (num2 <= 0 && num > 0));
			pool = (flag ? num : num2);
			return flag;
		}

		public static int Cost(Actor a, Ability ab)
		{
			if (ab.Cost <= 0)
			{
				return 0;
			}
			PaysMana(a, ab, out var pool);
			if (pool <= 0)
			{
				return 0;
			}
			return Mathf.Clamp(Mathf.RoundToInt((float)pool * 0.7f), 1, ab.Cost);
		}

		public static bool CanPay(Actor a, Ability ab, out string why)
		{
			why = null;
			try
			{
				int num = Cost(a, ab);
				if (num <= 0)
				{
					return true;
				}
				if (PaysMana(a, ab, out var _))
				{
					if (a.hasEnoughMana(num))
					{
						return true;
					}
					why = "Not enough mana";
					return false;
				}
				if (a.hasEnoughStamina(num))
				{
					return true;
				}
				why = "Too tired";
				return false;
			}
			catch
			{
				return true;
			}
		}

		public static void Pay(Actor a, Ability ab)
		{
			try
			{
				int num = Cost(a, ab);
				if (num > 0)
				{
					if (PaysMana(a, ab, out var _))
					{
						a.spendMana(num);
					}
					else
					{
						a.spendStamina(num);
					}
				}
			}
			catch
			{
			}
		}

		public static void Refund(Actor a, Ability ab)
		{
			if (a == null || ab == null)
			{
				return;
			}
			ReadyAt.Remove(a);
			try
			{
				int num = Cost(a, ab);
				if (num > 0)
				{
					if (PaysMana(a, ab, out var _))
					{
						a.restoreMana(num);
					}
					else
					{
						a.restoreStamina(num);
					}
				}
			}
			catch
			{
			}
		}

		public static string CostName(Actor a, Ability ab)
		{
			if (!PaysMana(a, ab, out var _))
			{
				return "stamina";
			}
			return "mana";
		}

		static Abilities()
		{
			ById = new Dictionary<string, Ability>();
			CreatureUsed = new List<KeyValuePair<Ability, Actor>>();
			ReadyAt = new Dictionary<Actor, float>();
			Near = new List<Actor>();
			Firebomb = new Ability
			{
				Sound = "firebomb",
				Name = "Firebomb",
				Blurb = "Hold X to aim, let go to throw a lit bomb",
				Motion = AbilityMotion.Throw,
				Icon = AbilityIcon.Bomb,
				Cooldown = 3.5f,
				Cost = 8,
				Aimed = true,
				Range = 11f,
				Radius = 1.6f,
				Delay = 0.2f,
				Prop = "bomb_round",
				Effect = (AbilityUse u) => Throw(u, "firebomb")
			};
			BoneToss = new Ability
			{
				Npc = true,
				Sound = "bone",
				Name = "Bone Toss",
				Blurb = "Hold X to aim, let go to hurl one of your own bones",
				Motion = AbilityMotion.Throw,
				Icon = AbilityIcon.Bone,
				Cooldown = 1.6f,
				Cost = 4,
				Aimed = true,
				Range = 10f,
				Radius = 0.6f,
				Delay = 0.18f,
				Prop = "thrown_bone",
				Effect = (AbilityUse u) => Throw(u, "bone", needsTarget: true)
			};
			RockThrow = new Ability
			{
				Sound = "rock",
				Name = "Rock Throw",
				Blurb = "Hold X to aim, let go to throw a rock",
				Motion = AbilityMotion.Throw,
				Icon = AbilityIcon.Rock,
				Cooldown = 1.8f,
				Cost = 5,
				Aimed = true,
				Range = 9f,
				Radius = 0.6f,
				Delay = 0.18f,
				Prop = "thrown_rock",
				Effect = (AbilityUse u) => Throw(u, "rock", needsTarget: true)
			};
			Snowballs = new Ability
			{
				Sound = "snow",
				Name = "Snowball Volley",
				Blurb = "Hold X to aim, let go to fling three snowballs",
				Motion = AbilityMotion.Bite,
				Icon = AbilityIcon.Snow,
				Cooldown = 3f,
				Cost = 8,
				Aimed = true,
				Range = 9f,
				Radius = 1.2f,
				Delay = 0.1f,
				Effect = (AbilityUse u) => Volley(u, "snowball", 3, 0.9f)
			};
			RaiseDead = new Ability
			{
				Npc = true,
				Sound = "raise",
				Name = "Raise the Dead",
				Blurb = "Call up skeletons where you aim, to fight for you",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Skull,
				Cooldown = 18f,
				Cost = 30,
				Aimed = true,
				Range = 8f,
				Radius = 1.4f,
				Delay = 0.25f,
				Glow = new Color(0.5f, 1f, 0.55f),
				Effect = RaiseSkeletons
			};
			Fireball = new Ability
			{
				Sound = "fire",
				Name = "Fireball",
				Blurb = "Hold X to aim, let go to hurl a bursting ball of fire",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Fire,
				Cooldown = 4f,
				Cost = 15,
				Aimed = true,
				Range = 12f,
				Radius = 1.5f,
				Delay = 0.15f,
				Glow = new Color(1f, 0.55f, 0.15f),
				Effect = (AbilityUse u) => Throw(u, "fireball")
			};
			Hellfire = new Ability
			{
				Npc = true,
				Sound = "hellfire",
				Name = "Hellfire",
				Blurb = "Hold X to aim, let go to spit a ball of hellfire",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Fire,
				Cooldown = 3.5f,
				Cost = 12,
				Aimed = true,
				Range = 11f,
				Radius = 1.5f,
				Delay = 0.12f,
				Glow = new Color(1f, 0.35f, 0.1f),
				Effect = (AbilityUse u) => Throw(u, "fireball")
			};
			PlasmaBolt = new Ability
			{
				Npc = true,
				Sound = "plasma",
				Name = "Plasma Bolt",
				Blurb = "Hold X to aim, let go to fire a bolt of plasma",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Plasma,
				Cooldown = 3f,
				Cost = 10,
				Aimed = true,
				Range = 13f,
				Radius = 1.2f,
				Delay = 0.1f,
				Glow = new Color(0.4f, 1f, 0.9f),
				Effect = (AbilityUse u) => Throw(u, "plasma_ball")
			};
			AcidSpit = new Ability
			{
				Sound = "acid",
				Name = "Acid Spit",
				Blurb = "Hold X to aim, let go to spit a gob of acid",
				Motion = AbilityMotion.Bite,
				Icon = AbilityIcon.Acid,
				Cooldown = 3f,
				Cost = 8,
				Aimed = true,
				Range = 8f,
				Radius = 1.2f,
				Delay = 0.1f,
				Effect = (AbilityUse u) => Throw(u, "acid_ball")
			};
			Blessing = new Ability
			{
				Sound = "bless",
				Name = "Blessing",
				Blurb = "Heal and shield yourself and your allies around you",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Heal,
				Cooldown = 16f,
				Cost = 30,
				Range = 0f,
				Radius = 6f,
				Delay = 0.2f,
				Glow = new Color(1f, 0.95f, 0.6f),
				Effect = Bless
			};
			Remedy = new Ability
			{
				Sound = "remedy",
				Name = "Remedy",
				Blurb = "Cure plague and poison and heal everyone around you",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Heal,
				Cooldown = 14f,
				Cost = 20,
				Range = 0f,
				Radius = 4f,
				Delay = 0.2f,
				Glow = new Color(0.6f, 1f, 0.7f),
				Effect = Cure
			};
			Roots = new Ability
			{
				Npc = true,
				Sound = "roots",
				Name = "Entangling Roots",
				Blurb = "Roots grab every foe where you aim, and grass springs up",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Leaf,
				Cooldown = 14f,
				Cost = 25,
				Aimed = true,
				Range = 9f,
				Radius = 2.5f,
				Delay = 0.2f,
				Glow = new Color(0.55f, 1f, 0.35f),
				Effect = Entangle
			};
			FrostNova = new Ability
			{
				Npc = true,
				Sound = "frost",
				Name = "Frost Nova",
				Blurb = "Freeze every foe around you solid",
				Motion = AbilityMotion.Slam,
				Icon = AbilityIcon.Frost,
				Cooldown = 14f,
				Cost = 25,
				Range = 0f,
				Radius = 3.5f,
				Delay = 0.25f,
				Glow = new Color(0.6f, 0.9f, 1f),
				Magic = true,
				Effect = Freeze
			};
			FlameBurst = new Ability
			{
				Sound = "flameburst",
				Name = "Flame Burst",
				Blurb = "Burst into flame, setting everything around you alight",
				Motion = AbilityMotion.Slam,
				Icon = AbilityIcon.Fire,
				Cooldown = 10f,
				Cost = 15,
				Range = 0f,
				Radius = 2.5f,
				Delay = 0.2f,
				Glow = new Color(1f, 0.6f, 0.2f),
				Magic = true,
				Effect = Burst
			};
			Blink = new Ability
			{
				Sound = "blink",
				Name = "Blink",
				Blurb = "Vanish and reappear where you aim",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Blink,
				Cooldown = 6f,
				Cost = 10,
				Aimed = true,
				Range = 9f,
				Radius = 0.5f,
				Delay = 0.1f,
				Glow = new Color(0.7f, 0.85f, 1f),
				Effect = Teleport
			};
			Spores = new Ability
			{
				Sound = "spores",
				Name = "Spore Cloud",
				Blurb = "Release spores over the foes where you aim",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Spore,
				Cooldown = 12f,
				Cost = 15,
				Aimed = true,
				Range = 7f,
				Radius = 2f,
				Delay = 0.15f,
				Glow = new Color(0.85f, 0.7f, 1f),
				Effect = (AbilityUse u) => Drop(u, "mush_spores", 0, 1.5f)
			};
			Rally = new Ability
			{
				Sound = "rally",
				Name = "Rally",
				Blurb = "Your kin around you take heart and fall on your foe",
				Motion = AbilityMotion.Shout,
				Icon = AbilityIcon.Rally,
				Cooldown = 25f,
				Cost = 15,
				Range = 14f,
				Radius = 10f,
				Delay = 0.05f,
				Effect = RallyKin
			};
			Grace = new Ability
			{
				Sound = "grace",
				Name = "Nature's Grace",
				Blurb = "Heal yourself and your kin; quick feet for a while; grass springs up",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Leaf,
				Cooldown = 18f,
				Cost = 20,
				Range = 0f,
				Radius = 5f,
				Delay = 0.2f,
				Glow = new Color(0.6f, 1f, 0.6f),
				Effect = NaturesGrace
			};
			Bloodlust = new Ability
			{
				Sound = "warcry",
				Name = "Bloodlust",
				Blurb = "A war cry: rage fills you and your foes falter",
				Motion = AbilityMotion.Shout,
				Icon = AbilityIcon.Rage,
				Cooldown = 20f,
				Cost = 10,
				Range = 0f,
				Radius = 4f,
				Delay = 0.05f,
				Effect = WarCry
			};
			GroundSlam = new Ability
			{
				Sound = "slam",
				Name = "Ground Slam",
				Blurb = "Strike the ground: foes around you are thrown back and stunned",
				Motion = AbilityMotion.Slam,
				Icon = AbilityIcon.Slam,
				Cooldown = 10f,
				Cost = 15,
				Range = 0f,
				Radius = 2.6f,
				Delay = 0.28f,
				Effect = Slam
			};
			Howl = new Ability
			{
				Sound = "howl",
				Name = "Howl",
				Blurb = "Call the pack: your kind nearby join your hunt",
				Motion = AbilityMotion.Shout,
				Icon = AbilityIcon.Howl,
				Cooldown = 20f,
				Cost = 10,
				Range = 14f,
				Radius = 14f,
				Delay = 0.05f,
				Effect = CallPack
			};
			Roar = new Ability
			{
				Sound = "roar",
				Name = "Roar",
				Blurb = "A roar that stuns the foes in front of you",
				Motion = AbilityMotion.Shout,
				Icon = AbilityIcon.Roar,
				Cooldown = 12f,
				Cost = 15,
				Range = 0f,
				Radius = 4f,
				Delay = 0.05f,
				Effect = Frighten
			};
			Charge = new Ability
			{
				Sound = "charge",
				Name = "Charge",
				Blurb = "Charge headlong, bowling over whatever is in your way",
				Motion = AbilityMotion.Leap,
				Icon = AbilityIcon.Charge,
				Cooldown = 7f,
				Cost = 15,
				Range = 6f,
				Radius = 0.9f,
				Delay = 0.05f,
				Effect = (AbilityUse u) => Rush(u, 5f, 1f, 1.6f, 4.5f)
			};
			Pounce = new Ability
			{
				Sound = "pounce",
				Name = "Pounce",
				Blurb = "Leap onto the creature you aim at",
				Motion = AbilityMotion.Leap,
				Icon = AbilityIcon.Pounce,
				Cooldown = 5f,
				Cost = 10,
				Aimed = true,
				Range = 6f,
				Radius = 0.7f,
				Delay = 0.05f,
				Effect = Leap
			};
			Hop = new Ability
			{
				Sound = "flutter",
				Name = "Flutter",
				Blurb = "Flap up high and glide forward",
				Motion = AbilityMotion.Leap,
				Icon = AbilityIcon.Wing,
				Cooldown = 3f,
				Cost = 5,
				Range = 4f,
				Radius = 0.5f,
				Delay = 0.02f,
				Effect = (AbilityUse u) => Rush(u, 2.8f, 2.2f, 0f)
			};
			Slide = new Ability
			{
				Sound = "slide",
				Name = "Belly Slide",
				Blurb = "Throw yourself forward on your belly",
				Motion = AbilityMotion.Leap,
				Icon = AbilityIcon.Charge,
				Cooldown = 4f,
				Cost = 8,
				Range = 5f,
				Radius = 0.7f,
				Delay = 0.02f,
				Effect = (AbilityUse u) => Rush(u, 4.5f, 0.5f, 0.6f, 3.5f)
			};
			Venom = new Ability
			{
				Sound = "venom",
				Name = "Venom",
				Blurb = "Sink venom into the creature in front of you",
				Motion = AbilityMotion.Bite,
				Icon = AbilityIcon.Venom,
				Cooldown = 8f,
				Cost = 10,
				Range = 2.5f,
				Radius = 0.6f,
				Delay = 0.08f,
				Effect = Poison
			};
			ShellUp = new Ability
			{
				Sound = "shell",
				Name = "Shell",
				Blurb = "Pull into your shell: blows glance off for a while",
				Motion = AbilityMotion.Bite,
				Icon = AbilityIcon.Shield,
				Cooldown = 15f,
				Cost = 10,
				Range = 0f,
				Radius = 0.5f,
				Delay = 0.02f,
				Effect = (AbilityUse u) => Self(u, "shield", 6f)
			};
			Pinch = new Ability
			{
				Sound = "pincer",
				Name = "Pincer Grip",
				Blurb = "Grab the creature in front of you and hold it fast",
				Motion = AbilityMotion.Bite,
				Icon = AbilityIcon.Venom,
				Cooldown = 8f,
				Cost = 10,
				Range = 2f,
				Radius = 0.6f,
				Delay = 0.08f,
				Effect = Grip
			};
			Feast = new Ability
			{
				Sound = "rotbite",
				Name = "Rotten Bite",
				Blurb = "Bite the creature in front of you, healing from the wound",
				Motion = AbilityMotion.Bite,
				Icon = AbilityIcon.Venom,
				Cooldown = 8f,
				Cost = 10,
				Range = 2f,
				Radius = 0.6f,
				Delay = 0.08f,
				Effect = RottenBite
			};
			Rainbow = new Ability
			{
				Sound = "heal",
				Name = "Healing Light",
				Blurb = "Heal yourself and every creature around you",
				Motion = AbilityMotion.Bite,
				Icon = AbilityIcon.Heal,
				Cooldown = 15f,
				Cost = 20,
				Range = 0f,
				Radius = 5f,
				Delay = 0.1f,
				Glow = new Color(1f, 0.8f, 1f),
				Effect = Cure
			};
			NpcReadyAt = new Dictionary<Actor, float>();
			EarthRaise = new Ability
			{
				Sound = "slam",
				Name = "Earthshaping: raise",
				Blurb = "Raise a mound of earth where you aim",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Rock,
				Cooldown = 60f,
				Cost = 30,
				Magic = true,
				Aimed = true,
				Range = 12f,
				Radius = 2f,
				Delay = 0.45f,
				Glow = new Color(0.75f, 0.55f, 0.3f),
				Effect = (AbilityUse u) => Shape(u, 0)
			};
			EarthSink = new Ability
			{
				Sound = "slam",
				Name = "Earthshaping: sink",
				Blurb = "Sink the ground where you aim into a bowl",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Slam,
				Cooldown = 60f,
				Cost = 30,
				Magic = true,
				Aimed = true,
				Range = 12f,
				Radius = 2f,
				Delay = 0.45f,
				Glow = new Color(0.75f, 0.55f, 0.3f),
				Effect = (AbilityUse u) => Shape(u, 1)
			};
			EarthSplit = new Ability
			{
				Sound = "slam",
				Name = "Earthshaping: split",
				Blurb = "Split the ground into a trench across your way",
				Motion = AbilityMotion.Cast,
				Icon = AbilityIcon.Charge,
				Cooldown = 60f,
				Cost = 40,
				Magic = true,
				Aimed = true,
				Range = 12f,
				Radius = 4f,
				Delay = 0.45f,
				Glow = new Color(0.75f, 0.55f, 0.3f),
				Effect = (AbilityUse u) => Shape(u, 2)
			};
			RaidSpells = new string[12]
			{
				"cast_fire", "summon_lightning", "cast_shield", "cast_cure", "cast_curse", "teleport", "cast_silence", "summon_tornado", "cast_blood_rain", "spawn_skeleton",
				"spawn_vegetation", "cast_grass_seeds"
			};
			SpellAbilities = new Dictionary<string, Ability>();
			SpellsForTest = new List<string>();
			Add(Rally, "human");
			Add(Grace, "elf");
			Add(Bloodlust, "orc");
			Add(GroundSlam, "dwarf");
			Add(Firebomb, "bandit");
			Add(RaiseDead, "necromancer");
			Add(Fireball, "evil_mage", "fire_skull");
			Add(Blessing, "white_mage", "angle", "fairy");
			Add(Roots, "druid");
			Add(Remedy, "plague_doctor");
			Add(BoneToss, "skeleton");
			Add(Hellfire, "demon");
			Add(PlasmaBolt, "alien", "UFO");
			Add(FrostNova, "cold_one");
			Add(Snowballs, "snowman");
			Add(Blink, "ghost");
			Add(Spores, "mush_unit", "mush_animal");
			Add(AcidSpit, "acid_blob", "bioblob", "tumor_monster_unit", "tumor_monster_animal");
			Add(GroundSlam, "greg", "crystal_golem");
			Add(Howl, "wolf", "dog", "hyena", "fox");
			Add(Roar, "bear", "crocodile");
			Add(Charge, "rhino", "buffalo", "cow", "goat", "sheep", "alpaca", "capybara");
			Add(Rainbow, "unicorn");
			Add(Pounce, "cat", "rabbit", "frog", "raccoon", "rat");
			Add(RockThrow, "monkey");
			Add(Hop, "chicken", "ostrich", "bee", "fly", "butterfly", "grasshopper");
			Add(Slide, "penguin", "seal");
			Add(Venom, "snake", "scorpion", "sand_spider", "ant_black", "ant_green", "ant_blue", "ant_red", "beetle", "piranha");
			Add(ShellUp, "turtle", "armadillo", "lemon_snail");
			Add(Pinch, "crab");
		}

		private static void Add(Ability ab, params string[] ids)
		{
			foreach (string key in ids)
			{
				ById[key] = ab;
			}
		}

		private static bool Throw(AbilityUse u, string projectile, bool needsTarget = false)
		{
			Actor self = u.Self;
			BaseSimObject baseSimObject = u.Target;
			if (baseSimObject == null && needsTarget)
			{
				baseSimObject = NearestFoe(self, u.At, 1.6f);
			}
			Vector2 vector = baseSimObject?.current_position ?? u.At;
			Vector2 vector2 = self.current_position + u.Dir * Mathf.Min(0.75f, Mathf.Max(0.3f, (vector - self.current_position).magnitude * 0.4f));
			float pStartPosZ = Mathf.Clamp(Height(self) * 0.75f, 0.2f, 2.5f);
			FirstPersonShots.LaunchAbility(World.world.projectiles.spawn(self, baseSimObject, projectile, new Vector3(vector2.x, vector2.y, 0f), new Vector3(vector.x, vector.y, 0f), (baseSimObject != null) ? 0.3f : 0f, pStartPosZ), self, baseSimObject as Actor, vector);
			return true;
		}

		private static bool Volley(AbilityUse u, string projectile, int count, float spread)
		{
			Actor self = u.Self;
			Vector2 vector = new Vector2(0f - u.Dir.y, u.Dir.x);
			Vector2 vector2 = self.current_position + u.Dir * 0.3f;
			float pStartPosZ = Mathf.Clamp(Height(self) * 0.6f, 0.2f, 2f);
			for (int i = 0; i < count; i++)
			{
				float num = ((count > 1) ? ((float)i / (float)(count - 1) - 0.5f) : 0f);
				Vector2 vector3 = u.At + vector * (num * spread * 2f) + u.Dir * (UnityEngine.Random.value - 0.5f) * spread;
				Actor actor = NearestFoe(self, vector3, 1f) ?? ((i == count / 2) ? u.Target : null);
				FirstPersonShots.LaunchAbility(World.world.projectiles.spawn(self, actor, projectile, new Vector3(vector2.x, vector2.y, 0f), new Vector3(vector3.x, vector3.y, 0f), 0.2f, pStartPosZ), self, actor, vector3);
			}
			return true;
		}

		private static bool RaiseSkeletons(AbilityUse u)
		{
			int num = 0;
			for (int i = 0; i < 3; i++)
			{
				float f = (float)i * 2.1f + UnityEngine.Random.value;
				WorldTile worldTile = TileAt(u.At + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * ((i == 0) ? 0f : 0.9f));
				if (worldTile == null || worldTile.Type == null || worldTile.Type.liquid || worldTile.Type.block)
				{
					continue;
				}
				try
				{
					if (ActionLibrary.spawnSkeleton(u.Self, worldTile))
					{
						num++;
					}
				}
				catch (Exception ex)
				{
					Log.Error("raise dead: " + ex.Message);
				}
			}
			if (num == 0)
			{
				u.Toast?.Invoke("The dead won't rise there");
				return false;
			}
			u.Toast?.Invoke((num == 1) ? "A skeleton claws its way up to serve you" : (num + " skeletons claw their way up to serve you"));
			return true;
		}

		private static bool Bless(AbilityUse u)
		{
			int num = 0;
			foreach (Actor item in Around(u.Self.current_position, 6f))
			{
				if (item != u.Self && !Friend(u.Self, item))
				{
					continue;
				}
				try
				{
					item.restoreHealthPercent(0.35f);
					((BaseSimObject)item).addStatusEffect("shield", 8f, pColorEffect: true);
					if (item != u.Self)
					{
						num++;
					}
				}
				catch
				{
				}
			}
			Effect("fx_cast_top_blue", u.Self.current_position, 0.3f);
			u.Toast?.Invoke((num == 0) ? "Light wraps round you" : ("Light wraps round you and " + num + ((num == 1) ? " ally" : " allies")));
			return true;
		}

		private static bool Cure(AbilityUse u)
		{
			int num = 0;
			WorldTile worldTile = TileAt(u.Self.current_position);
			foreach (Actor item in Around(u.Self.current_position, 4f))
			{
				if (item != u.Self && Foe(u.Self, item))
				{
					continue;
				}
				try
				{
					item.restoreHealthPercent(0.3f);
					item.finishStatusEffect("poisoned");
					item.finishStatusEffect("cough");
					item.finishStatusEffect("ash_fever");
					if (item.hasTrait("plague"))
					{
						item.removeTrait("plague");
					}
					if (item != u.Self)
					{
						num++;
					}
				}
				catch
				{
				}
			}
			try
			{
				if (worldTile != null)
				{
					ActionLibrary.castCure(u.Self, u.Self, worldTile);
				}
			}
			catch
			{
			}
			u.Toast?.Invoke((num == 0) ? "You tend your own wounds" : ("You tend your wounds and " + num + " others'"));
			return true;
		}

		private static bool Entangle(AbilityUse u)
		{
			int num = 0;
			foreach (Actor item in Around(u.At, 2.5f))
			{
				if (item != u.Self && Foe(u.Self, item))
				{
					try
					{
						item.makeStunned(2.5f);
						((BaseSimObject)item).addStatusEffect("slowness", 8f, pColorEffect: true);
						Anger(item, u.Self);
						num++;
					}
					catch
					{
					}
				}
			}
			WorldTile worldTile = TileAt(u.At);
			try
			{
				if (worldTile != null)
				{
					ActionLibrary.castSpawnGrassSeeds(u.Self, null, worldTile);
				}
			}
			catch
			{
			}
			Effect("fx_cast_ground_green", u.At, 0.35f);
			u.Toast?.Invoke((num == 0) ? "Roots and grass burst from the ground" : ("Roots seize " + num + ((num == 1) ? " foe" : " foes")));
			return true;
		}

		private static bool Freeze(AbilityUse u)
		{
			int num = 0;
			foreach (Actor item in Around(u.Self.current_position, 3.5f))
			{
				if (item != u.Self && Foe(u.Self, item))
				{
					try
					{
						((BaseSimObject)item).addStatusEffect("frozen", 4f, pColorEffect: true);
						Anger(item, u.Self);
						num++;
					}
					catch
					{
					}
				}
			}
			try
			{
				ActionLibrary.coldAuraEffect(u.Self, TileAt(u.Self.current_position));
			}
			catch
			{
			}
			Effect("fx_cast_ground_blue", u.Self.current_position, 0.4f);
			u.Toast?.Invoke((num == 0) ? "Frost spreads around you" : (num + ((num == 1) ? " foe freezes solid" : " foes freeze solid")));
			return true;
		}

		private static bool Burst(AbilityUse u)
		{
			Vector2 current_position = u.Self.current_position;
			for (int i = 0; i < 6; i++)
			{
				float f = (float)i * MathF.PI / 3f + UnityEngine.Random.value * 0.4f;
				Vector2 vector = current_position + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 1.8f;
				World.world.projectiles.spawn(u.Self, null, "fireball", new Vector3(current_position.x, current_position.y, 0f), new Vector3(vector.x, vector.y, 0f), 0f, 0.5f);
			}
			return true;
		}

		private static bool Teleport(AbilityUse u)
		{
			WorldTile worldTile = TileAt(u.At);
			if (worldTile == null || worldTile.Type == null || Climbing.Stops(u.Self, worldTile) || (worldTile.Type.liquid && !((BaseSimObject)u.Self).isFlying()))
			{
				u.Toast?.Invoke("You can't appear there");
				return false;
			}
			try
			{
				ActionLibrary.teleportEffect(u.Self, TileAt(u.Self.current_position));
				u.Self.cancelAllBeh();
				u.Self.spawnOn(worldTile, 0f);
				ActionLibrary.teleportEffect(u.Self, worldTile);
			}
			catch (Exception ex)
			{
				Log.Error("blink: " + ex.Message);
				return false;
			}
			return true;
		}

		private static bool Drop(AbilityUse u, string drop, int count, float height)
		{
			WorldTile worldTile = TileAt(u.At);
			if (worldTile == null)
			{
				return false;
			}
			World.world.drop_manager.spawn(worldTile, drop, height, -1f, (u.Self.data != null) ? u.Self.data.id : (-1));
			for (int i = 0; i < count; i++)
			{
				WorldTile worldTile2 = TileAt(u.At + UnityEngine.Random.insideUnitCircle * 1.5f);
				if (worldTile2 != null)
				{
					World.world.drop_manager.spawn(worldTile2, drop, height, -1f, (u.Self.data != null) ? u.Self.data.id : (-1));
				}
			}
			return true;
		}

		private static bool RallyKin(AbilityUse u)
		{
			Actor self = u.Self;
			Actor actor = ((u.Target != null && Foe(self, u.Target)) ? u.Target : (self.has_attack_target ? (self.attack_target as Actor) : null));
			if (actor == null)
			{
				actor = NearestFoe(self, self.current_position, 10f);
			}
			int num = 0;
			foreach (Actor item in Around(self.current_position, 10f))
			{
				if (item == self || !Kin(self, item) || !item.isAdult())
				{
					continue;
				}
				try
				{
					((BaseSimObject)item).addStatusEffect("inspired", 15f, pColorEffect: true);
					if (actor != null && actor.isAlive() && ((BaseSimObject)item).canAttackTarget((BaseSimObject)actor, pCheckForFactions: true, pAttackBuildings: false))
					{
						item.addAggro(actor);
						item.startFightingWith(actor);
					}
					num++;
				}
				catch
				{
				}
			}
			try
			{
				((BaseSimObject)self).addStatusEffect("inspired", 15f, pColorEffect: true);
			}
			catch
			{
			}
			Answered = num;
			u.Toast?.Invoke((num == 0) ? "You cry out, but no one is near enough to hear" : ((actor != null) ? (num + " of your people take heart and charge with you!") : (num + " of your people take heart")));
			return true;
		}

		private static bool NaturesGrace(AbilityUse u)
		{
			int num = 0;
			foreach (Actor item in Around(u.Self.current_position, 5f))
			{
				if (item != u.Self && !Friend(u.Self, item))
				{
					continue;
				}
				try
				{
					item.restoreHealthPercent(0.25f);
					if (item != u.Self)
					{
						num++;
					}
				}
				catch
				{
				}
			}
			try
			{
				((BaseSimObject)u.Self).addStatusEffect("dodge", 1f, pColorEffect: true);
				((BaseSimObject)u.Self).addStatusEffect("motivated", 8f, pColorEffect: true);
				ActionLibrary.castSpawnGrassSeeds(u.Self, null, TileAt(u.Self.current_position));
			}
			catch
			{
			}
			u.Toast?.Invoke((num == 0) ? "You feel light on your feet" : ("You and " + num + " of your kin are healed"));
			return true;
		}

		private static bool WarCry(AbilityUse u)
		{
			Actor self = u.Self;
			try
			{
				((BaseSimObject)self).addStatusEffect("rage", 10f, pColorEffect: true);
				self.restoreStaminaPercent(0.5f);
			}
			catch
			{
			}
			int num = 0;
			foreach (Actor item in Around(self.current_position, 4f))
			{
				if (item != self && Foe(self, item))
				{
					try
					{
						item.makeStunned(0.9f);
						num++;
					}
					catch
					{
					}
				}
			}
			u.Toast?.Invoke((num == 0) ? "Rage fills you!" : ("Rage fills you, and " + num + ((num == 1) ? " foe falters" : " foes falter")));
			return true;
		}

		private static bool Slam(AbilityUse u)
		{
			Actor self = u.Self;
			Vector2 current_position = self.current_position;
			float pDamage = Damage(self) * 1.2f;
			int num = 0;
			foreach (Actor item in Around(current_position, 2.6f))
			{
				if (item == self || !Foe(self, item) || !FriendlyFire.MayHurt(self, item) || FriendlyFire.PassesOver(self, item, u.Target))
				{
					continue;
				}
				try
				{
					int health = item.getHealth();
					((BaseSimObject)item).getHit(pDamage, pFlash: true, AttackType.Weapon, (BaseSimObject)self, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
					Landed(self, item, health);
					if (item.isAlive())
					{
						Vector2 vector = item.current_position - current_position;
						if (vector.sqrMagnitude < 0.01f)
						{
							vector = u.Dir;
						}
						Push(item, vector.normalized, 2.2f, 0.4f, cancelJob: true);
						item.makeStunned(1.4f);
						num++;
					}
				}
				catch
				{
				}
			}
			try
			{
				EffectsLibrary.spawnExplosionWave(new Vector3(current_position.x, current_position.y, 0f), 2.6f, 1.2f);
			}
			catch
			{
			}
			Effect("fx_jump", current_position, 0.3f);
			return true;
		}

		private static bool CallPack(AbilityUse u)
		{
			Actor self = u.Self;
			Actor actor = u.Target ?? (self.has_attack_target ? (self.attack_target as Actor) : null) ?? NearestFoe(self, self.current_position, 12f);
			string text = ((self.asset != null) ? self.asset.id : null);
			int num = 0;
			foreach (Actor item in Around(self.current_position, 14f))
			{
				if (item == self || item.asset == null || (item.asset.id != text && !Kin(self, item)))
				{
					continue;
				}
				try
				{
					((BaseSimObject)item).addStatusEffect("motivated", 12f, pColorEffect: true);
					if (actor != null && actor != item && actor.isAlive() && ((BaseSimObject)item).canAttackTarget((BaseSimObject)actor, pCheckForFactions: false, pAttackBuildings: false))
					{
						item.addAggro(actor);
						item.startFightingWith(actor);
					}
					num++;
				}
				catch
				{
				}
			}
			Answered = num;
			u.Toast?.Invoke((num == 0) ? "You howl, but no pack answers" : ((actor != null) ? (num + " of your pack answer and run with you!") : (num + " of your pack answer the call")));
			return true;
		}

		private static bool Frighten(AbilityUse u)
		{
			Actor self = u.Self;
			int num = 0;
			foreach (Actor item in Around(self.current_position, 4f))
			{
				if (item == self || !Foe(self, item))
				{
					continue;
				}
				Vector2 vector = item.current_position - self.current_position;
				if (!(vector.sqrMagnitude > 0.25f) || !(Vector2.Dot(vector.normalized, u.Dir) < 0.2f))
				{
					try
					{
						item.makeStunned(1.8f);
						((BaseSimObject)item).addStatusEffect("surprised", 2f, pColorEffect: true);
						num++;
					}
					catch
					{
					}
				}
			}
			u.Toast?.Invoke((num == 0) ? "You roar!" : ("Your roar stuns " + num + ((num == 1) ? " foe" : " foes")));
			return true;
		}

		private static bool Rush(AbilityUse u, float force, float height, float hitScale, float run = 0f)
		{
			Actor self = u.Self;
			float unscaledTime = Time.unscaledTime;
			if (run > 0f || !CanBePushed(self) || WorldSync.Mirroring)
			{
				PendingRush = new RushState
				{
					Self = self,
					Aimed = u.Target,
					Damage = Damage(self) * hitScale,
					Until = unscaledTime + 0.7f,
					Dir = u.Dir,
					Run = true,
					Start = self.current_position,
					Distance = ((run > 0f) ? run : 2.5f),
					Duration = 0.45f,
					T0 = unscaledTime
				};
				return true;
			}
			try
			{
				Push(self, u.Dir, force, height, cancelJob: false);
			}
			catch (Exception ex)
			{
				Log.Error("rush: " + ex.Message);
				return false;
			}
			if (hitScale > 0f)
			{
				PendingRush = new RushState
				{
					Self = self,
					Aimed = u.Target,
					Damage = Damage(self) * hitScale,
					Until = unscaledTime + 0.6f,
					Dir = u.Dir
				};
			}
			return true;
		}

		private static bool CanBePushed(Actor a)
		{
			try
			{
				return a.asset != null && a.asset.can_be_moved_by_powers;
			}
			catch
			{
				return false;
			}
		}

		private static bool Leap(AbilityUse u)
		{
			Actor self = u.Self;
			Vector2 current_position = self.current_position;
			Vector2 dir = ((u.Target != null) ? u.Target.current_position : u.At) - current_position;
			float num = Mathf.Min(dir.magnitude, 6f);
			if (num < 0.2f)
			{
				dir = u.Dir;
			}
			dir.Normalize();
			float unscaledTime = Time.unscaledTime;
			if (!CanBePushed(self) || WorldSync.Mirroring)
			{
				PendingRush = new RushState
				{
					Self = self,
					Aimed = u.Target,
					Damage = Damage(self) * 1.5f,
					Until = unscaledTime + 0.8f,
					Dir = dir,
					Stun = 1f,
					Run = true,
					Start = current_position,
					Distance = Mathf.Max(1f, num - 0.4f),
					Duration = 0.35f,
					T0 = unscaledTime
				};
				return true;
			}
			try
			{
				Push(self, dir, 1.6f + num * 0.55f, 1.1f, cancelJob: false);
			}
			catch (Exception ex)
			{
				Log.Error("pounce: " + ex.Message);
				return false;
			}
			PendingRush = new RushState
			{
				Self = self,
				Aimed = u.Target,
				Damage = Damage(self) * 1.5f,
				Until = unscaledTime + 0.9f,
				Dir = dir,
				Stun = 1f
			};
			return true;
		}

		private static bool Poison(AbilityUse u)
		{
			Actor actor = Reach(u, 2.5f);
			if (actor == null)
			{
				u.Toast?.Invoke("Nothing in reach to bite");
				return false;
			}
			try
			{
				int health = actor.getHealth();
				((BaseSimObject)actor).getHit(Damage(u.Self), pFlash: true, AttackType.Weapon, (BaseSimObject)u.Self, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
				Landed(u.Self, actor, health);
				if (actor.isAlive())
				{
					((BaseSimObject)actor).addStatusEffect("poisoned", 10f, pColorEffect: true);
				}
			}
			catch
			{
			}
			return true;
		}

		private static bool Grip(AbilityUse u)
		{
			Actor actor = Reach(u, 2f);
			if (actor == null)
			{
				u.Toast?.Invoke("Nothing in reach to grab");
				return false;
			}
			try
			{
				int health = actor.getHealth();
				((BaseSimObject)actor).getHit(Damage(u.Self) * 0.8f, pFlash: true, AttackType.Weapon, (BaseSimObject)u.Self, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
				Landed(u.Self, actor, health);
				if (actor.isAlive())
				{
					actor.makeStunned(2.5f);
				}
			}
			catch
			{
			}
			return true;
		}

		private static bool RottenBite(AbilityUse u)
		{
			Actor actor = Reach(u, 2f);
			if (actor == null)
			{
				u.Toast?.Invoke("Nothing in reach to bite");
				return false;
			}
			try
			{
				float num = Damage(u.Self) * 1.3f;
				int health = actor.getHealth();
				((BaseSimObject)actor).getHit(num, pFlash: true, AttackType.Infection, (BaseSimObject)u.Self, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
				Landed(u.Self, actor, health);
				u.Self.restoreHealth(Mathf.Max(1, Mathf.RoundToInt(num * 0.6f)));
			}
			catch
			{
			}
			return true;
		}

		private static bool Self(AbilityUse u, string status, float seconds)
		{
			try
			{
				((BaseSimObject)u.Self).addStatusEffect(status, seconds, pColorEffect: true);
			}
			catch
			{
				return false;
			}
			return true;
		}

		public static void Update()
		{
			RushState pendingRush = PendingRush;
			if (pendingRush == null)
			{
				return;
			}
			if (Time.unscaledTime > pendingRush.Until || pendingRush.Self == null || !pendingRush.Self.isAlive())
			{
				PendingRush = null;
				return;
			}
			if (pendingRush.Run)
			{
				RunOn(pendingRush);
			}
			if (pendingRush.Damage <= 0f)
			{
				return;
			}
			Vector2 current_position = pendingRush.Self.current_position;
			float radius = ((Height(pendingRush.Self) > 0.8f) ? 0.8f : 1f);
			Actor caster = Caster;
			Caster = pendingRush.Self;
			List<Actor> list;
			try
			{
				list = Around(current_position, radius);
			}
			finally
			{
				Caster = caster;
			}
			foreach (Actor item in list)
			{
				if (item == pendingRush.Self || pendingRush.Hit.Contains(item) || !FriendlyFire.MayHurt(pendingRush.Self, item) || (!FriendlyFire.Allowed && Kin(pendingRush.Self, item)) || FriendlyFire.PassesOver(pendingRush.Self, item, pendingRush.Aimed))
				{
					continue;
				}
				pendingRush.Hit.Add(item);
				if (WorldSync.ForwardRushHit(pendingRush.Self, item, pendingRush.Damage, pendingRush.Dir, pendingRush.Stun))
				{
					continue;
				}
				try
				{
					int health = item.getHealth();
					((BaseSimObject)item).getHit(pendingRush.Damage, pFlash: true, AttackType.Weapon, (BaseSimObject)pendingRush.Self, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
					Landed(pendingRush.Self, item, health);
					if (item.isAlive())
					{
						Vector2 vector = new Vector2(0f - pendingRush.Dir.y, pendingRush.Dir.x);
						if (Vector2.Dot(item.current_position - current_position, vector) < 0f)
						{
							vector = -vector;
						}
						Push(item, (pendingRush.Dir + vector * 0.7f).normalized, 2.5f, 0.5f, cancelJob: true);
						if (pendingRush.Stun > 0f)
						{
							item.makeStunned(pendingRush.Stun);
						}
					}
				}
				catch
				{
				}
			}
		}

		private static void RunOn(RushState r)
		{
			float num = (Time.unscaledTime - r.T0) / Mathf.Max(0.05f, r.Duration);
			if (num > 1f)
			{
				r.Run = false;
				return;
			}
			float num2 = 1f - (1f - num) * (1f - num);
			Actor self = r.Self;
			Vector2 current_position = self.current_position;
			Vector2 move = r.Start + r.Dir * (r.Distance * num2) - current_position;
			if (!(move.sqrMagnitude < 1E-06f))
			{
				move = Colliders.Slide(current_position, move, 0.22f, move.magnitude + 0.05f);
				Vector2 vector = current_position + move;
				WorldTile worldTile = TileAt(vector);
				if (worldTile == null || worldTile.Type == null || Climbing.Stops(self, worldTile) || (worldTile.Type.liquid && !IsSwimmer(self)))
				{
					r.Run = false;
					return;
				}
				self.current_position = vector;
				self.dirty_current_tile = true;
			}
		}

		private static bool IsSwimmer(Actor a)
		{
			try
			{
				return a.asset != null && (a.asset.id == "penguin" || a.asset.id == "seal" || a.asset.id == "crocodile" || a.asset.id == "piranha");
			}
			catch
			{
				return false;
			}
		}

		public static void Reset()
		{
			PendingRush = null;
		}

		public static void UpdateCreatures(Actor you, float dt)
		{
			_npcTimer -= dt;
			if (_npcTimer > 0f || you == null)
			{
				return;
			}
			_npcTimer = 0.5f;
			float unscaledTime = Time.unscaledTime;
			try
			{
				if (World.world.isPaused())
				{
					return;
				}
			}
			catch
			{
				return;
			}
			if (NpcReadyAt.Count > 300)
			{
				NpcReadyAt.Clear();
			}
			int num = 0;
			foreach (Actor item in Around(you.current_position, 28f))
			{
				if (item == you || num >= 2)
				{
					continue;
				}
				Ability ability = For(item);
				if (ability == null || !ability.Npc || (NpcReadyAt.TryGetValue(item, out var value) && unscaledTime < value))
				{
					continue;
				}
				Actor actor = null;
				try
				{
					if (item.has_attack_target && item.attack_target != null && item.attack_target.isAlive())
					{
						actor = item.attack_target as Actor;
					}
				}
				catch
				{
				}
				if (actor == null)
				{
					continue;
				}
				Vector2 vector = actor.current_position - item.current_position;
				float num2 = ((ability.Range > 0f) ? ability.Range : ability.Radius);
				if (vector.sqrMagnitude > num2 * num2 || vector.sqrMagnitude < 0.25f)
				{
					continue;
				}
				NpcReadyAt[item] = unscaledTime + ability.Cooldown * 2.5f * (0.75f + UnityEngine.Random.value * 0.5f);
				if (UnityEngine.Random.value > 0.35f || !CanPay(item, ability, out var _))
				{
					continue;
				}
				AbilityUse arg = new AbilityUse
				{
					Self = item,
					At = actor.current_position,
					Target = actor,
					Dir = vector.normalized
				};
				Caster = item;
				bool flag;
				try
				{
					flag = ability.Effect(arg);
				}
				catch (Exception ex)
				{
					Log.Error("creature ability " + ability.Name + ": " + ex.Message);
					flag = false;
				}
				finally
				{
					Caster = null;
				}
				if (flag)
				{
					Pay(item, ability);
					try
					{
						item.punchTargetAnimation(new Vector3(actor.current_position.x, actor.current_position.y, 0f), pFlip: true, pReverse: false, 45f);
					}
					catch
					{
					}
					num++;
					CreatureUses++;
					if (CreatureUsed.Count < 8)
					{
						CreatureUsed.Add(new KeyValuePair<Ability, Actor>(ability, item));
					}
				}
			}
		}

		private static void Push(Actor a, Vector2 dir, float force, float height, bool cancelJob)
		{
			Vector2 current_position = a.current_position;
			a.calculateForce(current_position.x, current_position.y, current_position.x - dir.x, current_position.y - dir.y, force, height, cancelJob);
		}

		private static float Damage(Actor a)
		{
			try
			{
				return Mathf.Max(5f, ((BaseSimObject)a).stats["damage"]);
			}
			catch
			{
				return 10f;
			}
		}

		private static float Height(Actor a)
		{
			try
			{
				return (a.asset != null && a.asset.is_humanoid) ? 0.9f : 0.5f;
			}
			catch
			{
				return 0.6f;
			}
		}

		private static WorldTile TileAt(Vector2 p)
		{
			try
			{
				return World.world.GetTile((int)p.x, (int)p.y);
			}
			catch
			{
				return null;
			}
		}

		private static void Effect(string id, Vector2 at, float scale)
		{
			try
			{
				EffectsLibrary.spawnAt(id, new Vector3(at.x, at.y, 0f), scale);
			}
			catch
			{
			}
		}

		private static List<Actor> Around(Vector2 at, float radius)
		{
			Near.Clear();
			List<Actor> simpleList = World.world.units.getSimpleList();
			float num = radius * radius;
			bool active = RoomCombat.Active;
			bool flag = active && RoomCombat.In(Caster);
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor != null && actor.isAlive() && !((actor.current_position - at).sqrMagnitude > num) && (!active || RoomCombat.In(actor) == flag))
				{
					Near.Add(actor);
				}
			}
			return new List<Actor>(Near);
		}

		private static bool Foe(Actor self, Actor a)
		{
			try
			{
				if (a == self)
				{
					return false;
				}
				if (self.has_attack_target && self.attack_target == a)
				{
					return true;
				}
				if (a.has_attack_target && a.attack_target == self)
				{
					return true;
				}
				return self.areFoes(a);
			}
			catch
			{
				return false;
			}
		}

		private static bool Kin(Actor self, Actor a)
		{
			try
			{
				if (a == self || Foe(self, a))
				{
					return false;
				}
				if (self.kingdom != null && a.kingdom == self.kingdom && self.kingdom.isCiv())
				{
					return true;
				}
				return a.asset == self.asset;
			}
			catch
			{
				return false;
			}
		}

		private static bool Friend(Actor self, Actor a)
		{
			return Kin(self, a);
		}

		private static Actor NearestFoe(Actor self, Vector2 at, float radius)
		{
			Actor result = null;
			float num = radius * radius;
			foreach (Actor item in Around(at, radius))
			{
				if (Foe(self, item))
				{
					float sqrMagnitude = (item.current_position - at).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = item;
					}
				}
			}
			return result;
		}

		private static Actor Reach(AbilityUse u, float reach)
		{
			Vector2 current_position = u.Self.current_position;
			if (u.Target != null && u.Target.isAlive() && (u.Target.current_position - current_position).sqrMagnitude <= reach * reach && FriendlyFire.MayHurt(u.Self, u.Target))
			{
				return u.Target;
			}
			Actor result = null;
			float num = reach * reach;
			foreach (Actor item in Around(current_position, reach))
			{
				if (Foe(u.Self, item))
				{
					Vector2 vector = item.current_position - current_position;
					if ((!(vector.sqrMagnitude > 0.04f) || !(Vector2.Dot(vector.normalized, u.Dir) < 0.3f)) && vector.sqrMagnitude < num)
					{
						num = vector.sqrMagnitude;
						result = item;
					}
				}
			}
			return result;
		}

		private static void Landed(Actor by, Actor victim, int before)
		{
			try
			{
				if (by != null && by == Blocks.Host && victim != null)
				{
					CombatFeedback.Struck(victim, before);
				}
			}
			catch
			{
			}
		}

		private static void Anger(Actor a, Actor by)
		{
			if (by != null && ControllableUnit.isControllingUnit(by))
			{
				CombatFeedback.NoteProvoked(a, by);
			}
			try
			{
				a.addAggro(by);
			}
			catch
			{
			}
		}

		public static bool KnowsEarth(Actor a)
		{
			if (a != null)
			{
				return LearnedOf(a).Contains("earthshaping");
			}
			return false;
		}

		public static bool TeachEarth(Actor a)
		{
			if (a?.data == null || KnowsEarth(a))
			{
				return false;
			}
			List<string> list = LearnedOf(a);
			list.Add("earthshaping");
			try
			{
				a.data.set("fp_learned_spells", string.Join(",", list));
			}
			catch
			{
				return false;
			}
			ChoiceVersion++;
			return true;
		}

		private static void AddEarth(Actor a, List<Ability> list)
		{
			if (KnowsEarth(a))
			{
				list.Add(EarthRaise);
				list.Add(EarthSink);
				list.Add(EarthSplit);
			}
		}

		internal static bool CastEarthForTest(Actor self, Vector2 at, int how)
		{
			return Shape(new AbilityUse
			{
				Self = self,
				At = at,
				Dir = Vector2.right,
				Toast = ((WorldBoxMod.Instance != null) ? new Action<string>(WorldBoxMod.Instance.Toast) : null)
			}, how);
		}

		internal static int EarthCastingsForTest(Actor a)
		{
			return Known(a).FindAll((Ability ab) => ab == EarthRaise || ab == EarthSink || ab == EarthSplit).Count;
		}

		private static bool Shape(AbilityUse u, int how)
		{
			Actor self = u.Self;
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready)
			{
				return false;
			}
			Vector2 at = u.At;
			List<(WorldTile, float)> list = new List<(WorldTile, float)>();
			if (how < 2)
			{
				for (int i = -2; i <= 2; i++)
				{
					for (int j = -2; j <= 2; j++)
					{
						WorldTile tile = World.world.GetTile((int)at.x + j, (int)at.y + i);
						if (tile != null)
						{
							float magnitude = new Vector2((float)tile.x + 0.5f - at.x, (float)tile.y + 0.5f - at.y).magnitude;
							if (!(magnitude > 2.4f))
							{
								list.Add((tile, 1f - 0.67f * Mathf.Clamp01(magnitude / 2.4f)));
							}
						}
					}
				}
			}
			else
			{
				Vector2 vector = ((u.Dir.sqrMagnitude > 0.0001f) ? u.Dir.normalized : Vector2.up);
				Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
				HashSet<WorldTile> hashSet = new HashSet<WorldTile>();
				for (float num = -4f; num <= 4f; num += 0.5f)
				{
					for (float num2 = -0.5f; num2 <= 0.5f; num2 += 1f)
					{
						Vector2 vector3 = at + vector2 * num + vector * num2;
						WorldTile tile2 = World.world.GetTile((int)vector3.x, (int)vector3.y);
						if (tile2 != null && hashSet.Add(tile2))
						{
							list.Add((tile2, 1f));
						}
					}
				}
			}
			int num3 = 0;
			bool flag = Run.Current == null;
			foreach (var (worldTile, share) in list)
			{
				if (!flag && !Earthworks.MayDig(self, worldTile, out var _))
				{
					continue;
				}
				try
				{
					Building building = worldTile.building;
					if (building != null && building.isAlive() && building.asset != null && AssetStyle.IsSolid(building) && how != 0)
					{
						continue;
					}
				}
				catch
				{
				}
				if (Earthworks.Apply(worldTile, how == 0, share, capped: false).Changed)
				{
					num3++;
				}
			}
			EarthMoved = num3;
			try
			{
				EffectsLibrary.spawnExplosionWave(new Vector3(at.x, at.y, 0f), (how == 2) ? 4f : 2.4f, 1.2f);
			}
			catch
			{
			}
			Effect("fx_jump", at, 0.5f);
			Sounds.PlayAt("event:/SFX/NATURE/EarthQuake", at);
			if (num3 == 0)
			{
				u.Toast?.Invoke("The ground here won't answer you.");
			}
			return num3 > 0;
		}

		public static List<Ability> Known(Actor a)
		{
			List<Ability> list = new List<Ability>();
			if (a == null)
			{
				return list;
			}
			Ability ability = For(a);
			if (ability != null)
			{
				list.Add(ability);
			}
			foreach (SpellAsset item in GameSpellsOf(a))
			{
				Ability ability2 = ForSpell(item);
				if (ability2 != null && !list.Contains(ability2))
				{
					list.Add(ability2);
				}
			}
			AddEarth(a, list);
			return list;
		}

		private static IEnumerable<SpellAsset> GameSpellsOf(Actor a)
		{
			IReadOnlyList<SpellAsset> spells = null;
			try
			{
				spells = a._spells?.spells;
			}
			catch
			{
			}
			if (spells != null)
			{
				foreach (SpellAsset item in spells)
				{
					if (item != null && item.action != null)
					{
						yield return item;
					}
				}
			}
			foreach (string item2 in LearnedOf(a))
			{
				SpellAsset spellAsset = null;
				try
				{
					spellAsset = AssetManager.spells.get(item2);
				}
				catch
				{
				}
				if (spellAsset != null && spellAsset.action != null && (spells == null || !Contains(spells, spellAsset)))
				{
					yield return spellAsset;
				}
			}
			if (SpellsForTest.Count == 0)
			{
				yield break;
			}
			foreach (string item3 in SpellsForTest)
			{
				SpellAsset spellAsset2 = null;
				try
				{
					spellAsset2 = AssetManager.spells.get(item3);
				}
				catch
				{
				}
				if (spellAsset2 != null && spellAsset2.action != null && (spells == null || !Contains(spells, spellAsset2)))
				{
					yield return spellAsset2;
				}
			}
		}

		public static List<string> LearnedOf(Actor a)
		{
			List<string> list = new List<string>();
			string pResult = null;
			try
			{
				a?.data.get("fp_learned_spells", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return list;
			}
			string[] array = pResult.Split(',');
			foreach (string text in array)
			{
				if (text.Length > 0 && !list.Contains(text))
				{
					list.Add(text);
				}
			}
			return list;
		}

		private static void SaveLearned(Actor a, List<string> ids)
		{
			try
			{
				a.data.set("fp_learned_spells", string.Join(",", ids.ToArray()));
			}
			catch (Exception ex)
			{
				Log.Error("spells: learned: " + ex.Message);
			}
		}

		public static Ability LearnFromRaid(Actor a)
		{
			if (a == null)
			{
				return null;
			}
			List<string> list = LearnedOf(a);
			HashSet<string> hashSet = new HashSet<string>();
			foreach (SpellAsset item in GameSpellsOf(a))
			{
				hashSet.Add(item.id);
			}
			string[] raidSpells = RaidSpells;
			foreach (string text in raidSpells)
			{
				if (!hashSet.Contains(text))
				{
					SpellAsset spellAsset = null;
					try
					{
						spellAsset = AssetManager.spells.get(text);
					}
					catch
					{
					}
					if (spellAsset != null && spellAsset.action != null)
					{
						list.Add(text);
						SaveLearned(a, list);
						Ability ability = ForSpell(spellAsset);
						Log.Info($"spells: {WorldBoxMod.Describe(a)} learned {ability?.Name ?? text} from a raid ({list.Count} learned)");
						return ability;
					}
				}
			}
			return null;
		}

		public static void InheritLearned(string fromRaw, Actor to)
		{
			if (to == null || string.IsNullOrEmpty(fromRaw))
			{
				return;
			}
			List<string> list = LearnedOf(to);
			string[] array = fromRaw.Split(',');
			foreach (string text in array)
			{
				if (text.Length > 0 && !list.Contains(text))
				{
					list.Add(text);
				}
			}
			SaveLearned(to, list);
		}

		private static bool Contains(IReadOnlyList<SpellAsset> list, SpellAsset s)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] == s)
				{
					return true;
				}
			}
			return false;
		}

		internal static string KeyOf(Ability ab)
		{
			if (ab.SpellId == null)
			{
				return "own";
			}
			return "spell:" + ab.SpellId;
		}

		internal static Ability ByKey(Actor a, string key)
		{
			foreach (Ability item in Known(a))
			{
				if (KeyOf(item) == key)
				{
					return item;
				}
			}
			return null;
		}

		public static Ability Chosen(Actor a)
		{
			List<Ability> list = Known(a);
			if (list.Count == 0)
			{
				return null;
			}
			string pResult = null;
			try
			{
				a.data.get("fp_spell", out pResult, null);
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(pResult))
			{
				foreach (Ability item in list)
				{
					if (KeyOf(item) == pResult)
					{
						return item;
					}
				}
			}
			return list[0];
		}

		public static void Choose(Actor a, Ability ab)
		{
			if (a != null && ab != null)
			{
				try
				{
					a.data.set("fp_spell", KeyOf(ab));
				}
				catch
				{
				}
				ChoiceVersion++;
				Log.Info("spells: " + WorldBoxMod.Describe(a) + " put " + ab.Name + " on X");
			}
		}

		private static Ability ForSpell(SpellAsset s)
		{
			if (s == null || string.IsNullOrEmpty(s.id))
			{
				return null;
			}
			if (SpellAbilities.TryGetValue(s.id, out var value))
			{
				return value;
			}
			string id = s.id;
			Ability ability = new Ability
			{
				SpellId = id,
				Motion = AbilityMotion.Cast,
				Magic = true,
				Cost = Mathf.Max(1, s.cost_mana),
				Delay = 0.2f,
				Effect = (AbilityUse u) => CastGameSpell(u, id)
			};
			switch (s.cast_target)
			{
			case CastTarget.Himself:
				ability.Range = 0f;
				ability.Radius = 2f;
				break;
			case CastTarget.Friendly:
				ability.Aimed = true;
				ability.Range = 8f;
				ability.Radius = 1f;
				break;
			case CastTarget.Region:
				ability.Aimed = true;
				ability.Range = 9f;
				ability.Radius = 2f;
				break;
			default:
				ability.Aimed = true;
				ability.Range = 13f;
				ability.Radius = 1.5f;
				break;
			}
			switch (id)
			{
			case "teleport":
				Set(ability, "Teleport", "Vanish, and appear somewhere nearby", AbilityIcon.Blink, "blink", 10f, new Color(0.7f, 0.6f, 1f));
				break;
			case "summon_lightning":
				Set(ability, "Call Lightning", "Hold X to aim, let go to bring lightning down on a foe", AbilityIcon.Plasma, "plasma", 8f, new Color(0.7f, 0.85f, 1f));
				break;
			case "summon_tornado":
				Set(ability, "Summon Tornado", "Hold X to aim, let go to raise a tornado on your foes", AbilityIcon.Wing, "flutter", 20f, new Color(0.8f, 0.85f, 0.9f));
				break;
			case "cast_curse":
				Set(ability, "Curse", "Hold X to aim, let go to curse a foe", AbilityIcon.Skull, "raise", 8f, new Color(0.6f, 0.3f, 0.8f));
				break;
			case "cast_fire":
				Set(ability, "Set Ablaze", "Hold X to aim, let go to set a foe on fire", AbilityIcon.Fire, "fire", 5f, new Color(1f, 0.5f, 0.15f));
				break;
			case "cast_silence":
				Set(ability, "Silence", "Hold X to aim, let go to silence a foe's magic", AbilityIcon.Shield, "frost", 10f, new Color(0.6f, 0.8f, 1f));
				break;
			case "cast_blood_rain":
				Set(ability, "Blood Rain", "A red rain that heals those around you", AbilityIcon.Heal, "remedy", 15f, new Color(1f, 0.3f, 0.3f));
				break;
			case "cast_grass_seeds":
				Set(ability, "Sow Grass", "Hold X to aim, let go to make grass spring up", AbilityIcon.Leaf, "roots", 8f, new Color(0.55f, 1f, 0.35f));
				break;
			case "spawn_vegetation":
				Set(ability, "Grow Plants", "Hold X to aim, let go to make plants grow", AbilityIcon.Leaf, "roots", 8f, new Color(0.55f, 1f, 0.35f));
				break;
			case "spawn_skeleton":
				Set(ability, "Raise Skeleton", "A skeleton climbs out of the ground beside you", AbilityIcon.Bone, "raise", 14f, new Color(0.7f, 1f, 0.6f));
				break;
			case "cast_shield":
				Set(ability, "Shield", "Shield yourself from harm", AbilityIcon.Shield, "bless", 14f, new Color(1f, 0.95f, 0.6f));
				break;
			case "cast_cure":
				Set(ability, "Cure", "Hold X to aim, let go to cure a friend (or yourself) of what ails them", AbilityIcon.Heal, "heal", 10f, new Color(0.6f, 1f, 0.7f));
				break;
			default:
			{
				string text = id.Replace("cast_", "").Replace("summon_", "").Replace("spawn_", "")
					.Replace('_', ' ');
				Set(ability, char.ToUpperInvariant(text[0]) + text.Substring(1), "A spell the game taught you", AbilityIcon.Plasma, "plasma", 10f, new Color(0.7f, 0.8f, 1f));
				break;
			}
			}
			SpellAbilities[id] = ability;
			return ability;
		}

		private static Actor AimedFoe(AbilityUse u, float reach)
		{
			Vector2 current_position = u.Self.current_position;
			Vector2 rhs = ((u.Dir.sqrMagnitude > 1E-06f) ? u.Dir.normalized : Vector2.right);
			Actor actor = null;
			float num = 2.5f;
			foreach (Actor item in Around(current_position, reach))
			{
				if (!Foe(u.Self, item) || !FriendlyFire.MayHurt(u.Self, item))
				{
					continue;
				}
				Vector2 lhs = item.current_position - current_position;
				if (!(Vector2.Dot(lhs, rhs) <= 0.3f))
				{
					float num2 = Mathf.Abs(lhs.x * rhs.y - lhs.y * rhs.x);
					if (num2 < num)
					{
						num = num2;
						actor = item;
					}
				}
			}
			return actor ?? NearestFoe(u.Self, u.At, 3f);
		}

		private static void Set(Ability ab, string name, string blurb, AbilityIcon icon, string sound, float cooldown, Color glow)
		{
			ab.Name = name;
			ab.Blurb = blurb;
			ab.Icon = icon;
			ab.Sound = sound;
			ab.Cooldown = cooldown;
			ab.Glow = glow;
		}

		private static bool CastGameSpell(AbilityUse u, string id)
		{
			SpellAsset spellAsset = null;
			try
			{
				spellAsset = AssetManager.spells.get(id);
			}
			catch
			{
			}
			if (spellAsset == null || spellAsset.action == null)
			{
				return false;
			}
			WorldTile worldTile = TileAt(u.At);
			BaseSimObject baseSimObject;
			switch (spellAsset.cast_target)
			{
			case CastTarget.Himself:
				baseSimObject = u.Self;
				worldTile = u.Self.current_tile;
				break;
			case CastTarget.Friendly:
				worldTile = (baseSimObject = ((u.Target != null && u.Target.isAlive() && !Foe(u.Self, u.Target)) ? u.Target : u.Self)).current_tile;
				break;
			case CastTarget.Region:
				baseSimObject = u.Self;
				break;
			default:
			{
				Actor actor = ((u.Target != null && u.Target.isAlive() && Foe(u.Self, u.Target) && FriendlyFire.MayHurt(u.Self, u.Target)) ? u.Target : AimedFoe(u, 13f));
				if (actor == null)
				{
					u.Toast?.Invoke("No foe there to cast it on");
					return false;
				}
				baseSimObject = actor;
				worldTile = actor.current_tile;
				break;
			}
			}
			if (worldTile == null || baseSimObject == null)
			{
				return false;
			}
			int before = 0;
			Actor actor2 = ((baseSimObject is Actor actor3 && actor3 != u.Self && Foe(u.Self, actor3)) ? actor3 : null);
			try
			{
				if (actor2 != null)
				{
					before = actor2.getHealth();
				}
			}
			catch
			{
				actor2 = null;
			}
			spellAsset.action(u.Self, baseSimObject, worldTile);
			if (actor2 != null)
			{
				Landed(u.Self, actor2, before);
			}
			Effect("fx_cast_top_blue", u.Self.current_position, 0.3f);
			return true;
		}
	}
}
