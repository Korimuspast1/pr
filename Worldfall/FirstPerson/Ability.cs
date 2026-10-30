using System;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Ability
	{
		public string Name;

		public string Blurb;

		public AbilityMotion Motion;

		public AbilityIcon Icon;

		public float Cooldown = 6f;

		public int Cost = 10;

		public bool? Magic;

		public bool Npc;

		public bool Aimed;

		public float Range = 8f;

		public float Radius = 1f;

		public float Delay = 0.15f;

		public string Prop;

		public Color Glow = Color.white;

		public string Sound;

		public Func<AbilityUse, bool> Effect;

		public string SpellId;

		public bool IsMagic => Magic ?? (Motion == AbilityMotion.Cast);
	}
}
