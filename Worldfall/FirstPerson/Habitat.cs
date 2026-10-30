using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Habitat
	{
		private struct Life
		{
			public float Birds;

			public float Insects;

			public float Frogs;
		}

		public float Birds;

		public float Insects;

		public float Frogs;

		public readonly List<Vector3> Perches = new List<Vector3>(128);

		public readonly List<Vector3> Meadow = new List<Vector3>(64);

		public readonly List<Vector3> Marsh = new List<Vector3>(64);

		private const float Range = 26f;

		private const int Step = 3;

		private float _timer;

		private Vector2 _at = new Vector2(float.MinValue, float.MinValue);

		private readonly Dictionary<TileTypeBase, Life> _life = new Dictionary<TileTypeBase, Life>();

		public void Update(Vector2 pos, SceneCollector scene, TerrainCache cache, float dt)
		{
			_timer -= dt;
			if (_timer > 0f && (pos - _at).sqrMagnitude < 36f)
			{
				return;
			}
			_timer = 2f;
			_at = pos;
			try
			{
				Survey(pos, scene, cache);
			}
			catch (Exception ex)
			{
				Birds = (Insects = (Frogs = 0f));
				Log.Error("habitat survey: " + ex.Message);
				_timer = 30f;
			}
		}

		private void Survey(Vector2 pos, SceneCollector scene, TerrainCache cache)
		{
			MapBox world = World.world;
			if (world == null)
			{
				return;
			}
			scene.Perches(pos, 30f, Perches);
			Meadow.Clear();
			Marsh.Clear();
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			float num5 = 0f;
			float num6 = 0f;
			int num7 = (int)pos.x;
			int num8 = (int)pos.y;
			for (int i = -26; (float)i <= 26f; i += 3)
			{
				for (int j = -26; (float)j <= 26f; j += 3)
				{
					if ((float)(j * j + i * i) > 676f)
					{
						continue;
					}
					int num9 = num7 + j;
					int num10 = num8 + i;
					if (num9 < 0 || num10 < 0 || num9 >= cache.Width || num10 >= cache.Height)
					{
						continue;
					}
					int num11 = num10 * cache.Width + num9;
					num++;
					byte num12 = cache.Flags[num11];
					if ((num12 & 4) != 0)
					{
						num4++;
					}
					if ((num12 & 3) != 0)
					{
						continue;
					}
					WorldTile tile = world.GetTile(num9, num10);
					if (tile != null)
					{
						Life life = LifeOf(tile);
						num2++;
						num5 += life.Birds;
						num6 += life.Insects;
						float z = cache.Ground[num11];
						if (life.Frogs > 0f)
						{
							num3++;
							Marsh.Add(new Vector3((float)num9 + 0.5f, (float)num10 + 0.5f, z));
						}
						if (life.Birds >= 0.6f && j * j + i * i > 16)
						{
							Meadow.Add(new Vector3((float)num9 + 0.5f, (float)num10 + 0.5f, z));
						}
					}
				}
			}
			if (num2 == 0)
			{
				Birds = (Insects = (Frogs = 0f));
				return;
			}
			float num13 = Mathf.Sqrt((float)num2 / (float)num);
			float num14 = Mathf.Clamp01((float)Perches.Count / 12f);
			Birds = num5 / (float)num2 * (0.3f + 0.7f * num14) * num13;
			Insects = num6 / (float)num2 * num13;
			Frogs = Mathf.Clamp01((float)num3 / (float)num2 * 2.5f);
			if (num4 > 0)
			{
				float num15 = Mathf.Clamp01(1f - (float)num4 / 6f);
				Birds *= num15;
				Insects *= num15;
				Frogs *= num15;
			}
		}

		private Life LifeOf(WorldTile tile)
		{
			TileTypeBase tileTypeBase = (TileTypeBase)(((object)tile.top_type) ?? ((object)tile.main_type));
			if (tileTypeBase == null)
			{
				return default(Life);
			}
			if (_life.TryGetValue(tileTypeBase, out var value))
			{
				return value;
			}
			value = Classify(tileTypeBase.id ?? "");
			_life[tileTypeBase] = value;
			return value;
		}

		private static Life Classify(string id)
		{
			string text = id;
			if (text.EndsWith("_low", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - 4);
			}
			else if (text.EndsWith("_high", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - 5);
			}
			switch (text)
			{
			case "grass":
				return L(1f, 1f);
			case "jungle":
				return L(1f, 1f, 0.25f);
			case "birch":
			case "maple":
				return L(1f, 0.8f);
			case "flower":
			case "clover":
				return L(0.95f, 1f);
			case "lemon":
			case "enchanted":
				return L(0.9f, 0.8f);
			case "savanna":
				return L(0.7f, 1f);
			case "swamp":
				return L(0.6f, 1f, 1f);
			case "candy":
			case "garlic":
			case "pumpkin":
				return L(0.6f, 0.6f);
			case "mushroom":
				return L(0.5f, 0.6f, 0.3f);
			case "celestial":
				return L(0.5f, 0.4f);
			case "rocklands":
			case "crystal":
				return L(0.25f, 0.25f);
			case "desert":
				return L(0.12f, 0.45f);
			case "frozen":
			case "snow":
			case "permafrost":
				return L(0.12f, 0f);
			case "hills":
			case "sand":
			case "soil":
				return L(0.35f, 0.4f);
			case "mountains":
				return L(0.1f, 0.05f);
			case "tumor":
			case "summit":
			case "corrupted":
			case "wasteland":
			case "cybertile":
			case "paradox":
			case "biomass":
			case "infernal":
			case "singularity":
				return default(Life);
			default:
				if (!id.Contains("field"))
				{
					return L(0.4f, 0.4f);
				}
				return L(0.6f, 0.8f);
			}
		}

		private static Life L(float birds, float insects, float frogs = 0f)
		{
			return new Life
			{
				Birds = birds,
				Insects = insects,
				Frogs = frogs
			};
		}
	}
}
