using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Core;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal static class GodView
	{
		public static bool Active;

		public static bool MouseValid;

		public static Vector2 MousePoint;

		public static Vector2 LandPoint;

		public static float LandHeight;

		public static Vector2Int MouseTile;

		public static Actor MouseCreature;

		public static bool FlatValid;

		public static Vector2 FlatPoint;

		public static ViewState View;

		public static int Width;

		public static int Height;

		public static float[] Depth;

		public static TerrainCache Terrain;

		public static SightCut Cut;

		public static float FlatZ;

		public static Vector2 ViewAt;

		private static int _resolved = -1;

		private static Vector2 _resolvedAt;

		private static float _resolvedTurn;

		public static bool LaserOn;

		public static Vec3 LaserFrom;

		public static Vec3 LaserDir;

		public static Vector2 LaserAt;

		public static float LaserTurn;

		public static bool Flying;

		public static Vec3 FlyFrom;

		public static Vec3 FlyDir;

		public static bool InHeadset;

		private static Vector2 _laserFlatFrom;

		private static Vector2 _laserPointFrom;

		private static bool _laserFlatSet;

		private static bool _laserPointSet;

		private static bool _laserPressOnLand;

		private static bool _laserPressWas;

		public static float Turn;

		public static bool ArrowsTurn;

		public static bool KeysHooked;

		public static ViewState Shown;

		public static Vector2? TestMouse;

		public static bool TestCursor;

		public static float? TestWheel;

		public static Vector2? TestTurn;

		public static bool TestPanKey;

		private static bool _installed;

		private static string _error;

		private static readonly List<Actor> RayCandidates = new List<Actor>();

		private static bool _creatureErrorLogged;

		internal static bool StaleDragForTest;

		public static bool Installed => _installed;

		public static void Resolve()
		{
			if (_resolved == Time.frameCount)
			{
				return;
			}
			VrMouse.Sample();
			_resolved = Time.frameCount;
			MouseValid = (FlatValid = false);
			MouseCreature = null;
			int num;
			if (LaserOn && VrMouse.Driving)
			{
				ResolveLaser();
				if (!VrMouse.Now.Left && !VrMouse.Now.Right)
				{
					num = (VrMouse.Now.Middle ? 1 : 0);
					if (num == 0)
					{
						_laserPressOnLand = false;
						goto IL_0082;
					}
				}
				else
				{
					num = 1;
				}
				if (!_laserPressWas)
				{
					_laserPressOnLand = MouseValid;
				}
				goto IL_0082;
			}
			_laserPressOnLand = (_laserPressWas = false);
			if (Flying)
			{
				ResolveFly();
				return;
			}
			_resolvedAt = ViewAt;
			_resolvedTurn = View.Yaw - MathF.PI / 2f;
			if (InHeadset || Terrain == null || Width <= 0 || Height <= 0 || Screen.width <= 0 || Screen.height <= 0)
			{
				return;
			}
			Vector2 vector = TestMouse ?? ((Vector2)Input.mousePosition);
			float num2 = vector.x / (float)Screen.width * (float)Width;
			float num3 = ((float)Screen.height - vector.y) / (float)Screen.height * (float)Height;
			if (num2 < 0f || num3 < 0f || num2 >= (float)Width || num3 >= (float)Height)
			{
				return;
			}
			Vec3 vec = GodCamera.Ray(View, Width, Height, num2, num3);
			if (vec.Z < -0.0001f)
			{
				float num4 = (View.Z - FlatZ) / (0f - vec.Z);
				FlatPoint = new Vector2(View.X + vec.X * num4, View.Y + vec.Y * num4);
				FlatValid = true;
			}
			if (!GodCamera.GroundPoint(Terrain, View, Width, Height, num2, num3, Cut, out var point))
			{
				return;
			}
			int num5 = Mathf.FloorToInt(point.X);
			int num6 = Mathf.FloorToInt(point.Y);
			if (num5 < 0 || num6 < 0 || num5 >= MapBox.width || num6 >= MapBox.height)
			{
				return;
			}
			LandPoint = new Vector2(point.X, point.Y);
			LandHeight = point.Z;
			MouseTile = new Vector2Int(num5, num6);
			MousePoint = LandPoint;
			MouseValid = true;
			try
			{
				MouseCreature = CreatureOnRay(new Vec3(View.X, View.Y, View.Z), padPerTile: 3f * Mathf.Tan(View.Fov * 0.5f) / ((float)Screen.width * 0.5f), to: point, nudge: out var nudge);
				if (MouseCreature != null)
				{
					MousePoint = MouseCreature.current_position + nudge;
				}
				return;
			}
			catch (Exception ex)
			{
				MouseCreature = null;
				if (!_creatureErrorLogged)
				{
					Log.Error("god view: creature under the mouse: " + ex);
				}
				_creatureErrorLogged = true;
				return;
			}
			IL_0082:
			_laserPressWas = (byte)num != 0;
		}

		private static void ResolveLaser()
		{
			_resolvedAt = LaserAt;
			_resolvedTurn = LaserTurn;
			if (Terrain == null)
			{
				return;
			}
			Vec3 laserFrom = LaserFrom;
			Vec3 laserDir = LaserDir;
			float num = ((laserDir.Z < -0.0001f) ? ((laserFrom.Z - FlatZ) / (0f - laserDir.Z)) : (-1f));
			float num2 = Mathf.Sqrt(laserDir.X * laserDir.X + laserDir.Y * laserDir.Y + laserDir.Z * laserDir.Z);
			if (num > 0f && num * num2 < 1500f)
			{
				FlatPoint = new Vector2(laserFrom.X + laserDir.X * num, laserFrom.Y + laserDir.Y * num);
				FlatValid = true;
				_laserFlatFrom = FlatPoint - LaserAt;
				_laserFlatSet = true;
			}
			else if (_laserFlatSet)
			{
				FlatPoint = LaserAt + _laserFlatFrom;
				FlatValid = true;
			}
			if (!GodCamera.GroundAlong(Terrain, laserFrom, laserDir, out var point))
			{
				return;
			}
			int num3 = Mathf.FloorToInt(point.X);
			int num4 = Mathf.FloorToInt(point.Y);
			if (num3 < 0 || num4 < 0 || num3 >= MapBox.width || num4 >= MapBox.height)
			{
				return;
			}
			LandPoint = new Vector2(point.X, point.Y);
			LandHeight = point.Z;
			MouseTile = new Vector2Int(num3, num4);
			MousePoint = LandPoint;
			MouseValid = true;
			try
			{
				MouseCreature = CreatureOnRay(laserFrom, point, 0.005f, out var nudge);
				if (MouseCreature != null)
				{
					MousePoint = MouseCreature.current_position + nudge;
				}
			}
			catch (Exception ex)
			{
				MouseCreature = null;
				if (!_creatureErrorLogged)
				{
					Log.Error("god view: creature on the laser: " + ex);
				}
				_creatureErrorLogged = true;
			}
			_laserPointFrom = MousePoint - LaserAt;
			_laserPointSet = true;
		}

		private static void ResolveFly()
		{
			if (Terrain == null || !GodCamera.GroundAlong(Terrain, FlyFrom, FlyDir, out var point))
			{
				return;
			}
			int num = Mathf.FloorToInt(point.X);
			int num2 = Mathf.FloorToInt(point.Y);
			if (num < 0 || num2 < 0 || num >= MapBox.width || num2 >= MapBox.height)
			{
				return;
			}
			LandPoint = new Vector2(point.X, point.Y);
			LandHeight = point.Z;
			MouseTile = new Vector2Int(num, num2);
			MousePoint = LandPoint;
			MouseValid = true;
			try
			{
				float padPerTile = 3f * Mathf.Tan(View.Fov * 0.5f) / Mathf.Max(1f, (float)Screen.width * 0.5f);
				MouseCreature = CreatureOnRay(FlyFrom, point, padPerTile, out var nudge);
				if (MouseCreature != null)
				{
					MousePoint = MouseCreature.current_position + nudge;
				}
			}
			catch (Exception ex)
			{
				MouseCreature = null;
				if (!_creatureErrorLogged)
				{
					Log.Error("free flight: creature under the crosshair: " + ex);
				}
				_creatureErrorLogged = true;
			}
		}

		public static Actor CreatureOnRay(Vec3 from, Vec3 to, float padPerTile, out Vector2 nudge)
		{
			nudge = Vector2.zero;
			Hitboxes.Near(from, to, 6f, null, RayCandidates);
			HashSet<Actor> hashSet = ((World.world.magnet != null) ? World.world.magnet._magnet_units : null);
			Actor actor = null;
			float num = 1f;
			Hitbox hitbox = default(Hitbox);
			for (int i = 0; i < RayCandidates.Count; i++)
			{
				Actor actor2 = RayCandidates[i];
				if ((hashSet == null || !hashSet.Contains(actor2)) && Hitboxes.Get(actor2, out var box))
				{
					float num2 = box.Centre.X - from.X;
					float num3 = box.Centre.Y - from.Y;
					float num4 = box.Centre.Z - from.Z;
					float pad = padPerTile * Mathf.Sqrt(num2 * num2 + num3 * num3 + num4 * num4);
					if (box.Segment(from, to, pad, out var t) && t < num)
					{
						num = t;
						actor = actor2;
						hitbox = box;
					}
				}
			}
			RayCandidates.Clear();
			if (actor == null)
			{
				return null;
			}
			Vector2 vector = new Vector2(from.X + (to.X - from.X) * num - hitbox.Centre.X, from.Y + (to.Y - from.Y) * num - hitbox.Centre.Y);
			nudge = Vector2.ClampMagnitude(vector, 0.33f);
			if (actor.is_inside_building && Doorstep.Drawn(actor, out var at))
			{
				nudge += at - actor.current_position;
			}
			return actor;
		}

		public static void Install()
		{
			if (_installed || _error != null)
			{
				return;
			}
			Type typeFromHandle = typeof(GodView);
			if (!Detour.Begin())
			{
				_error = "skipped after a crash on this platform";
				return;
			}
			try
			{
				Detour.Apply(typeof(PlayerControl).GetMethod("getMousePos", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeFromHandle.GetMethod("MousePos", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(typeof(PixelDetector).GetMethod("GetSpritePixelColorUnderMousePointer", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeFromHandle.GetMethod("TileUnderMouse", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
			catch (Exception ex)
			{
				_error = ex.GetType().Name + ": " + ex.Message;
				Log.Error("god view: could not hook the mouse, powers land where the flat map would put them: " + ex);
				Detour.End();
				return;
			}
			try
			{
				Detour.Apply(typeof(MoveCamera).GetMethod("getMousePos", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeFromHandle.GetMethod("DragPos", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}
			catch (Exception ex2)
			{
				Log.Error("god view: could not take over map dragging: " + ex2.Message);
			}
			try
			{
				Detour.Apply(Game(typeof(MoveCamera), "move", 376), typeFromHandle.GetMethod("Move", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				KeysHooked = true;
			}
			catch (Exception ex3)
			{
				Log.Error("god view: could not take over the camera keys, the arrow keys pan and don't turn the view: " + ex3.Message);
			}
			try
			{
				Detour.Apply(Game(typeof(NameplateText), "transformPosition", 22), typeFromHandle.GetMethod("NameplateAt", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(Game(typeof(NameplateManager), "findObjectForTooltip", 354), typeFromHandle.GetMethod("PlateUnderMouse", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}
			catch (Exception ex4)
			{
				Log.Error("god view: name plates stay where the flat map has them: " + ex4.Message);
			}
			Detour.End();
			Log.Info("god view mouse hooks installed");
		}

		private static Vector2 MousePos(PlayerControl self)
		{
			if (Active)
			{
				Resolve();
			}
			if (Active && MouseValid)
			{
				return MousePoint;
			}
			if (Active && LaserOn && VrMouse.Driving)
			{
				if (!_laserPressOnLand || !_laserPointSet)
				{
					return new Vector2(-10000f, -10000f);
				}
				return LaserAt + _laserPointFrom;
			}
			Vector2 vector = Input.mousePosition;
			return World.world.camera.ScreenToWorldPoint(vector);
		}

		private static bool TileUnderMouse(MonoBehaviour mono, ref Vector2Int tile)
		{
			tile = new Vector2Int(-1, -1);
			if (Active)
			{
				Resolve();
				if (!MouseValid)
				{
					return false;
				}
				tile = MouseTile;
				return true;
			}
			Vector2 vector = Camera.main.ScreenToViewportPoint(Input.mousePosition);
			if (vector.x <= 0f || vector.x >= 1f || vector.y <= 0f || !(vector.y < 1f))
			{
				return false;
			}
			Ray ray;
			try
			{
				ray = Camera.main.ViewportPointToRay(vector);
			}
			catch
			{
				return false;
			}
			return PixelDetector.IntersectsSprite(mono, ray, out tile);
		}

		private static Vector3 DragPos(MoveCamera self)
		{
			if (!InputHelpers.mouseSupported)
			{
				return Vector3.one;
			}
			if (Active && Flying)
			{
				return Vector3.zero;
			}
			if (Active)
			{
				Resolve();
			}
			if (Active && FlatValid)
			{
				if (StaleDragForTest)
				{
					return FlatPoint;
				}
				Vector2 vector = self.transform.position;
				Vector2 vector2 = FlatPoint - _resolvedAt;
				float f = DivePath.Wrap(Turn - _resolvedTurn);
				if (Mathf.Abs(f) > 1E-06f)
				{
					float num = Mathf.Cos(f);
					float num2 = Mathf.Sin(f);
					vector2 = new Vector2(vector2.x * num - vector2.y * num2, vector2.x * num2 + vector2.y * num);
				}
				return vector + vector2;
			}
			return World.world.getMousePos();
		}

		private static void Move(HotkeyAsset asset)
		{
			MoveCamera instance = MoveCamera.instance;
			if (!(instance == null) && asset != null && (!Active || !Flying))
			{
				string text = asset.id ?? "";
				bool flag = text.StartsWith("fast_", StringComparison.Ordinal);
				float num = Time.deltaTime * 55f * (flag ? 2.5f : 1f) * instance._target_zoom * instance.camera_move_speed;
				float num2 = 0f;
				float num3 = 0f;
				switch (text)
				{
				case "up":
				case "fast_up":
					num3 = num;
					break;
				case "down":
				case "fast_down":
					num3 = 0f - num;
					break;
				case "right":
				case "fast_right":
					num2 = num;
					break;
				case "left":
				case "fast_left":
					num2 = 0f - num;
					break;
				}
				if (ArrowsTurn && !HeldByOtherKeys(asset))
				{
					num2 = (num3 = 0f);
				}
				else if (Turn != 0f)
				{
					float num4 = Mathf.Cos(Turn);
					float num5 = Mathf.Sin(Turn);
					float num6 = num2 * num4 - num3 * num5;
					float num7 = num2 * num5 + num3 * num4;
					num2 = num6;
					num3 = num7;
				}
				instance._move_velocity.x += num2;
				instance._move_velocity.y += num3;
				instance.clampVelocity();
				instance._mouse_controls_used_last = false;
			}
		}

		private static bool HeldByOtherKeys(HotkeyAsset asset)
		{
			if (!TestPanKey && !OtherKey(asset.default_key_1) && !OtherKey(asset.default_key_2))
			{
				return OtherKey(asset.default_key_3);
			}
			return true;
		}

		private static bool OtherKey(KeyCode k)
		{
			if (k != KeyCode.None && k != KeyCode.UpArrow && k != KeyCode.DownArrow && k != KeyCode.LeftArrow && k != KeyCode.RightArrow)
			{
				return Input.GetKey(k);
			}
			return false;
		}

		private static Vector2 NameplateAt(NameplateText self, Vector3 place)
		{
			if (Active && (InHeadset || Flying))
			{
				return new Vector2(-10000f, -10000f);
			}
			if (Active && Shown.Fov > 0f && Terrain != null && Screen.width > 0)
			{
				float z = Mathf.Max(Terrain.SampleHeight(place.x, place.y), 0f);
				if (GodTurn.Project(Shown, Screen.width, Screen.height, new Vec3(place.x, place.y, z), out var column, out var row))
				{
					return new Vector2(column, (float)Screen.height - row);
				}
				return new Vector2(-10000f, -10000f);
			}
			return World.world.camera.WorldToScreenPoint(place);
		}

		private static NanoObject PlateUnderMouse(NameplateManager self)
		{
			self.cursor_over_text = null;
			if (World.world.isBusyWithUI() || ControllableUnit.isControllingUnit())
			{
				return null;
			}
			bool mouseSupported = InputHelpers.mouseSupported;
			if (!mouseSupported && !self.checkTouch(out Vector2 _))
			{
				return null;
			}
			Vector2 point = ((!Active) ? ((Vector2)World.world.camera.WorldToScreenPoint(World.world.getMousePos())) : (TestMouse ?? ((Vector2)Input.mousePosition)));
			NameplateText nameplateText = null;
			float num = float.MaxValue;
			for (int i = 0; i < self._active.Count; i++)
			{
				NameplateText nameplateText2 = self._active[i];
				if (nameplateText2.isShowing())
				{
					Vector2 lastScreenPosition = nameplateText2.getLastScreenPosition();
					float num2 = Toolbox.SquaredDist(lastScreenPosition.x, lastScreenPosition.y, point.x, point.y);
					if (nameplateText2.map_text_rect_click.Contains(point) && (!(nameplateText != null) || (!(num2 > num) && !(num2 > 625f))))
					{
						nameplateText = nameplateText2;
						num = num2;
					}
				}
			}
			if (nameplateText == null)
			{
				return null;
			}
			NanoObject nano_object = nameplateText.nano_object;
			NanoObject result = (Input.mousePresent ? nano_object : null);
			self.cursor_over_text = nameplateText;
			nameplateText.forceScale(nameplateText.transform.localScale * 1.1f);
			IMetaObject metaObject = nano_object as IMetaObject;
			if (metaObject != null && mouseSupported)
			{
				metaObject.setCursorOver();
			}
			return result;
		}

		private static MethodInfo Game(Type type, string name, int ilBytes)
		{
			MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
			if (num != ilBytes)
			{
				throw new InvalidOperationException("the game's " + type.Name + "." + name + " has changed (" + num + " bytes of IL, not " + ilBytes + ")");
			}
			return method;
		}

		internal static Vector3 DragPointForTest(MoveCamera camera)
		{
			return DragPos(camera);
		}
	}
}
