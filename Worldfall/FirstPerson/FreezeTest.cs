using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Towns;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FirstPerson
{
	internal sealed class FreezeTest : CivilTest
	{
		private readonly List<Actor> _town = new List<Actor>();

		private float _ownStep;

		private GameObject _fieldRoot;

		private static bool _throw;

		private readonly List<double> _worldAt = new List<double>();

		private readonly List<int> _gcAt = new List<int>();

		private readonly List<float[]> _parts = new List<float[]>();

		private int _pick;

		protected override string Prefix => "FZ";

		protected override IEnumerator Body()
		{
			CrownOffer.AutoAnswer = true;
			Mod.Settings.GodView = false;
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (!GameClock.Active)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			_ownStep = Time.fixedDeltaTime;
			Actor king = Village("human", 90, 20);
			if (!Check("a town of 100 and more", king != null && Founded.Count >= 100, $"{Founded.Count} people"))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item.isAdult() && item != king)
				{
					_town.Add(item);
				}
			}
			yield return CivilTest.Wait(2f);
			if (Options.Contains("keysfree"))
			{
				yield return Part("the keyboard's holds, every way out", Keys());
				yield break;
			}
			if (Options.Contains("fzonly=cadence"))
			{
				yield return Part("walking's cadence", Cadence());
				yield break;
			}
			if (Options.Contains("fzonly=far"))
			{
				yield return Part("far creatures, budgeted or not", Far(king));
				yield break;
			}
			if (!Options.Contains("fzonly=ways"))
			{
				yield return Part("the frame broken off", Cut());
			}
			if (!Options.Contains("fzonly=cut"))
			{
				yield return Part("a body left every way", Ways());
			}
		}

		private void FocusGameField()
		{
			if (_fieldRoot == null)
			{
				_fieldRoot = new GameObject("FZ test canvas");
				_fieldRoot.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
				GameObject gameObject = new GameObject("FZ test field");
				gameObject.transform.SetParent(_fieldRoot.transform, worldPositionStays: false);
				gameObject.AddComponent<Image>();
				GameObject obj = new GameObject("Text");
				obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
				Text text = obj.AddComponent<Text>();
				try
				{
					text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
				}
				catch
				{
				}
				gameObject.AddComponent<InputField>().textComponent = text;
			}
			InputField componentInChildren = _fieldRoot.GetComponentInChildren<InputField>();
			EventSystem.current.SetSelectedGameObject(componentInChildren.gameObject);
			componentInChildren.ActivateInputField();
		}

		private static bool FieldFocused()
		{
			EventSystem current = EventSystem.current;
			GameObject gameObject = ((current != null) ? current.currentSelectedGameObject : null);
			InputField inputField = ((gameObject != null) ? gameObject.GetComponent<InputField>() : null);
			if (inputField != null)
			{
				return inputField.isFocused;
			}
			return false;
		}

		private IEnumerator Esc()
		{
			if (!FieldFocused())
			{
				HotkeyLibrary.cancel.just_pressed_action?.Invoke(HotkeyLibrary.cancel);
			}
			PlayInput.TestPress(KeyCode.Escape);
			yield return null;
			yield return null;
			PlayInput.TestRelease(KeyCode.Escape);
			yield return null;
		}

		private IEnumerator Keys()
		{
			Actor child = null;
			List<KeyValuePair<string, Func<Actor, bool>>> list = new List<KeyValuePair<string, Func<Actor, bool>>>
			{
				new KeyValuePair<string, Func<Actor, bool>>("a talk", delegate(Actor actor2)
				{
					Actor actor = null;
					foreach (Actor item in _town)
					{
						if (item != actor2 && item.isAlive() && item.isAdult())
						{
							actor = item;
							break;
						}
					}
					if (actor == null)
					{
						return false;
					}
					CivilTest.StandBefore(actor, actor2, 1.4f);
					return Mod.StartConversationForTest(actor);
				}),
				new KeyValuePair<string, Func<Actor, bool>>("a board with none to show", delegate
				{
					Law.OpenBoard = null;
					Law.BoardOpen = true;
					return true;
				}),
				new KeyValuePair<string, Func<Actor, bool>>("the name box", delegate(Actor actor)
				{
					child = CivilTest.SpawnNear("human", actor.current_position, 1f, adult: false);
					if (child == null)
					{
						return false;
					}
					Births.Naming = child;
					Births.NameText = "Rowan";
					return true;
				}),
				new KeyValuePair<string, Func<Actor, bool>>("a text box of the game's", delegate
				{
					FocusGameField();
					return true;
				}),
				new KeyValuePair<string, Func<Actor, bool>>("E held back by a wheel", delegate
				{
					PlayInput.HeldBack = KeyCode.E;
					return true;
				})
			};
			List<KeyValuePair<string, Func<Actor, IEnumerator>>> exits = new List<KeyValuePair<string, Func<Actor, IEnumerator>>>
			{
				new KeyValuePair<string, Func<Actor, IEnumerator>>("Esc", (Actor actor) => Esc()),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("Esc twice", (Actor actor) => Twice()),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("nothing (3 s)", (Actor actor) => Nothing()),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("another body", (Actor actor) => Possess(Pick(), 1f)),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("G, then back in", (Actor you2) => OutAndIn(you2, delegate
				{
					Mod.LetGoForPadTest();
				})),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("V, then back in", (Actor you2) => VAndBack(you2)),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("death, then another body", (Actor you2) => DeathAndOn(you2))
			};
			int stuck = 0;
			int cases = 0;
			foreach (KeyValuePair<string, Func<Actor, bool>> hold in list)
			{
				foreach (KeyValuePair<string, Func<Actor, IEnumerator>> exit in exits)
				{
					string how = hold.Key + ", " + exit.Key;
					Actor you = Pick();
					yield return Possess(you, 1f);
					if (!InControl(you))
					{
						Note(how + ": not taken");
						continue;
					}
					yield return CivilTest.Wait(0.3f);
					if (!hold.Value(you))
					{
						Note(how + ": couldn't be made");
						continue;
					}
					yield return CivilTest.Wait(0.4f);
					string holder = Holders();
					bool mutedThen = Mod.IsTypingNow || PlayInput.HeldBack != KeyCode.None;
					yield return exit.Value(you);
					if (!Mod.IsFirstPerson || Mod.DeathTimer >= 0f)
					{
						for (float w = 0f; w < 16f; w += 0.2f)
						{
							if (!(Mod.DeathTimer >= 0f))
							{
								break;
							}
							yield return CivilTest.Wait(0.2f);
						}
						try
						{
							if (!Mod.IsFirstPerson && ControllableUnit.isControllingUnit())
							{
								ControllableUnit.clear(pCallKill: false);
							}
						}
						catch
						{
						}
						yield return CivilTest.Wait(0.5f);
						yield return Possess(Pick(), 1f);
					}
					yield return CivilTest.Wait(2.6f);
					Mod.SetSettingsMenu(open: false);
					Actor now = Mod.Host;
					bool drawnTalk = Mod.Converse.Open && Mod.IsFirstPerson;
					Vector2 p0 = now?.current_position ?? Vector2.zero;
					PlayInput.TestPress(KeyCode.W);
					yield return CivilTest.Wait(0.8f);
					PlayInput.TestRelease(KeyCode.W);
					yield return null;
					float num = ((now != null) ? (now.current_position - p0).magnitude : 0f);
					bool flag = !Mod.IsTypingNow && !Mod.GameKeysMuted && PlayInput.HeldBack == KeyCode.None && !FieldFocused();
					string detail = $"held at the start: {mutedThen} ({holder}); now {Holders()}; walked {num:F2} tiles; fp {Mod.IsFirstPerson}";
					cases++;
					if (drawnTalk)
					{
						Check(how + ": a talk on screen keeps the keys for itself", Mod.Converse.Open && !flag, detail);
					}
					else
					{
						bool flag2 = flag && num > 0.25f;
						if (!flag2)
						{
							stuck++;
						}
						Check(how + ": the keys are free and W walks", flag2, detail);
					}
					if (Mod.Converse.Open)
					{
						Mod.EndConversation();
					}
					Law.BoardOpen = false;
					if (Births.Naming != null)
					{
						Births.FinishNaming(Mod.Host, keep: true);
					}
					Births.Naming = null;
					if (child != null && child.isAlive())
					{
						CivilTest.Kill(child);
					}
					child = null;
					PlayInput.HeldBack = KeyCode.None;
					Mod.SetSettingsMenu(open: false);
					try
					{
						EventSystem.current.SetSelectedGameObject(null);
					}
					catch
					{
					}
					if (Mod.IsFirstPerson)
					{
						Mod.LetGoForPadTest();
						yield return CivilTest.Wait(1.2f);
					}
					try
					{
						if (ControllableUnit.isControllingUnit())
						{
							ControllableUnit.clear(pCallKill: false);
						}
					}
					catch
					{
					}
					yield return CivilTest.Wait(0.3f);
				}
			}
			Note($"keys left stuck in {stuck} of {cases} cases");
			if (_fieldRoot != null)
			{
				UnityEngine.Object.Destroy(_fieldRoot);
			}
		}

		private string Holders()
		{
			List<string> list = new List<string>();
			if (Mod.Converse.Open)
			{
				list.Add("talk");
			}
			if (Law.BoardOpen)
			{
				list.Add("board");
			}
			if (Births.Naming != null)
			{
				list.Add("naming");
			}
			if (Hub.AnyWindowOpen)
			{
				list.Add("towns");
			}
			if (HomeCard.Open)
			{
				list.Add("home card");
			}
			if (FieldFocused())
			{
				list.Add("game text box");
			}
			if (PlayInput.HeldBack != KeyCode.None)
			{
				list.Add("held back " + PlayInput.HeldBack);
			}
			if (Mod.IsTypingNow)
			{
				list.Add("typing");
			}
			if (Mod.GameKeysMuted)
			{
				list.Add("game keys muted");
			}
			if (Mod.SettingsMenu.Open)
			{
				list.Add("settings");
			}
			if (PossessionHooks.Frozen)
			{
				list.Add("body held still");
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray());
			}
			return "nothing";
		}

		private IEnumerator Twice()
		{
			yield return Esc();
			yield return CivilTest.Wait(0.3f);
			yield return Esc();
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator Nothing()
		{
			yield return CivilTest.Wait(0.4f);
		}

		private IEnumerator OutAndIn(Actor you, Action leave)
		{
			leave();
			for (float t = 0f; t < 6f; t += 0.1f)
			{
				if (!Mod.IsFirstPerson)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			yield return CivilTest.Wait(0.4f);
			yield return Possess(you.isAlive() ? you : Pick(), 1f);
		}

		private IEnumerator VAndBack(Actor you)
		{
			yield return Key(KeyCode.V);
			yield return CivilTest.Wait(1f);
			yield return Key(KeyCode.V);
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator DeathAndOn(Actor you)
		{
			CivilTest.Kill(you);
			for (float t = 0f; t < 16f; t += 0.2f)
			{
				if (!Mod.IsFirstPerson)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(0.4f);
		}

		private static void ThrowBeforeStep(AutoTesterBot self, float elapsed)
		{
			if (_throw)
			{
				throw new InvalidOperationException("FZ test: thrown in the game's frame before its world's step");
			}
		}

		private static void ThrowAfterStep(NameplateManager self)
		{
			if (_throw)
			{
				throw new InvalidOperationException("FZ test: thrown in the game's frame after its world's step");
			}
		}

		private IEnumerator Cut()
		{
			var array = new[]
			{
				new
				{
					Name = "before the world's step",
					Game = typeof(AutoTesterBot).GetMethod("update", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(float) }, null),
					Ours = typeof(FreezeTest).GetMethod("ThrowBeforeStep", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				},
				new
				{
					Name = "after the world's step",
					Game = typeof(NameplateManager).GetMethod("update", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null),
					Ours = typeof(FreezeTest).GetMethod("ThrowAfterStep", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				}
			};
			var array2 = array;
			foreach (var c in array2)
			{
				bool[] array3 = new bool[2] { true, false };
				foreach (bool fp in array3)
				{
					string how = c.Name + (fp ? ", in first person" : ", on the map");
					Actor you = Pick();
					if (fp)
					{
						yield return Possess(you);
						if (!Check(how + ": in a body", InControl(you)))
						{
							continue;
						}
					}
					Detour.Handle h;
					try
					{
						h = Detour.Apply(c.Game, c.Ours);
					}
					catch (Exception ex)
					{
						Check(how + ": the throw put in", ok: false, ex.Message);
						continue;
					}
					int broken = FrameGuard.Broken;
					_throw = true;
					yield return Measure(how, 3f, fp);
					_throw = false;
					try
					{
						Detour.Undo(h);
					}
					catch (Exception ex2)
					{
						Log.Error("FZ undo: " + ex2.Message);
					}
					Note($"{how}: frames broken off {FrameGuard.Broken - broken}");
					if (fp)
					{
						Mod.LetGoForPadTest();
						yield return CivilTest.Wait(2f);
					}
					yield return CivilTest.Wait(0.5f);
				}
			}
		}

		private IEnumerator Ways()
		{
			int n = 20;
			string[] array = Options.Split(' ', '\n', '\r');
			foreach (string text in array)
			{
				if (text.StartsWith("fzn=") && int.TryParse(text.Substring(4), out var result))
				{
					n = Mathf.Max(1, result);
				}
			}
			List<KeyValuePair<string, Func<Actor, IEnumerator>>> list = new List<KeyValuePair<string, Func<Actor, IEnumerator>>>
			{
				new KeyValuePair<string, Func<Actor, IEnumerator>>("the mod's G", (Actor actor) => Do(delegate
				{
					Mod.LetGoForPadTest();
				})),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("the game's own button", (Actor actor) => Do(delegate
				{
					ControllableUnit.clear();
				})),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("the settings' Leave body", (Actor actor) => FromSettings()),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("V", (Actor actor) => Key(KeyCode.V)),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("death", (Actor a) => Do(delegate
				{
					CivilTest.Kill(a);
				})),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("a talk, then G", (Actor you2) => TalkThen(you2, delegate
				{
					Mod.LetGoForPadTest();
				})),
				new KeyValuePair<string, Func<Actor, IEnumerator>>("the rise out", (Actor actor) => Rise())
			};
			Dictionary<string, int> fails = new Dictionary<string, int>();
			foreach (KeyValuePair<string, Func<Actor, IEnumerator>> way in list)
			{
				fails[way.Key] = 0;
				for (int i2 = 0; i2 < n; i2++)
				{
					Actor you = Pick();
					if (you == null)
					{
						Check("someone left to take", ok: false);
						yield break;
					}
					yield return Possess(you, 1f);
					if (!InControl(you))
					{
						Note($"{way.Key} #{i2 + 1}: not taken ({WorldBoxMod.Describe(you)})");
						continue;
					}
					yield return CivilTest.Wait(0.6f);
					yield return way.Value(you);
					yield return After(way.Key, i2 + 1, fails);
					if (Mod.IsFirstPerson && !Mod.DivingOutForTest)
					{
						Mod.LetGoForPadTest();
						yield return CivilTest.Wait(1.5f);
					}
					try
					{
						if (ControllableUnit.isControllingUnit())
						{
							ControllableUnit.clear(pCallKill: false);
						}
					}
					catch
					{
					}
					yield return CivilTest.Wait(0.3f);
				}
				Check($"{way.Key}: the world went on after all {n}", fails[way.Key] == 0, $"{fails[way.Key]} of {n} stood still");
			}
		}

		private IEnumerator Do(Action a)
		{
			a();
			yield return null;
		}

		private IEnumerator Key(KeyCode k)
		{
			PlayInput.TestPress(k);
			yield return null;
			yield return null;
			PlayInput.TestRelease(k);
			yield return null;
		}

		private IEnumerator FromSettings()
		{
			Mod.SetSettingsMenu(open: true);
			yield return CivilTest.Wait(0.3f);
			Mod.AskLeaveBody();
			yield return null;
		}

		private IEnumerator TalkThen(Actor you, Action leave)
		{
			Actor npc = null;
			foreach (Actor item in _town)
			{
				if (item != you && item.isAlive())
				{
					npc = item;
					break;
				}
			}
			if (npc != null)
			{
				CivilTest.StandBefore(npc, you, 1.4f);
				yield return CivilTest.Wait(0.3f);
				Mod.StartConversationForTest(npc);
				yield return CivilTest.Wait(0.6f);
			}
			leave();
			yield return null;
		}

		private IEnumerator Rise()
		{
			Mod.NoDive = false;
			Mod.LetGoForPadTest();
			yield return null;
			for (float t = 0f; t < 10f; t += 0.1f)
			{
				if (!Mod.DivingOutForTest)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Mod.NoDive = true;
		}

		private IEnumerator After(string way, int i, Dictionary<string, int> fails)
		{
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 16f && ((Mod.IsFirstPerson && Mod.DeathTimer >= 0f) || Mod.DivingOutForTest))
			{
				yield return null;
			}
			yield return CivilTest.Wait(0.4f);
			int failedBefore = Fail;
			yield return Measure($"{way} #{i}", 2f, fp: false, quiet: true);
			if (Fail > failedBefore)
			{
				fails[way]++;
			}
		}

		private IEnumerator Far(Actor king)
		{
			WorldTile current_tile = king.current_tile;
			List<Actor> far = new List<Actor>();
			Actor actor = Village("human", 30, 0, current_tile, 90f);
			if (!Check("far: a second town 90 tiles off", actor != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive())
				{
					far.Add(item);
				}
			}
			Actor you = Pick();
			yield return Possess(you);
			if (!Check("far: in a body in the first town", InControl(you)))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)you).addStatusEffect("invincible", 300f, pColorEffect: false);
			}
			catch
			{
			}
			int was = Mod.Settings.FarCreatures;
			int[] array = new int[4] { 1, 0, 1, 0 };
			foreach (int setting in array)
			{
				Mod.Settings.FarCreatures = setting;
				yield return CivilTest.Wait(2f);
				Dictionary<Actor, Vector2> at = new Dictionary<Actor, Vector2>();
				foreach (Actor item2 in far)
				{
					if (item2 != null && item2.isAlive())
					{
						at[item2] = item2.current_position;
					}
				}
				double w0 = World.world.getCurWorldTime();
				yield return CivilTest.Wait(20f);
				int num = 0;
				int num2 = 0;
				float num3 = 0f;
				foreach (KeyValuePair<Actor, Vector2> item3 in at)
				{
					if (item3.Key != null && item3.Key.isAlive())
					{
						float magnitude = (item3.Key.current_position - item3.Value).magnitude;
						num3 += magnitude;
						if (magnitude > 0.3f)
						{
							num++;
						}
						else
						{
							num2++;
						}
					}
				}
				float num4 = 0f;
				try
				{
					num4 = (far[0].current_position - you.current_position).magnitude;
				}
				catch
				{
				}
				Check("far creatures " + ((setting == 1) ? "budgeted" : "every step") + ": the far town lives", num >= Mathf.Max(1, at.Count / 3), $"{num} moved, {num2} stood, {num3 / (float)Mathf.Max(1, num + num2):F1} tiles each, world time +{World.world.getCurWorldTime() - w0:F1}, {num4:F0} tiles off");
			}
			Mod.Settings.FarCreatures = was;
		}

		private IEnumerator Cadence()
		{
			Actor you = Pick();
			yield return Possess(you);
			if (!Check("cadence: in a body", InControl(you)))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)you).addStatusEffect("invincible", 60f, pColorEffect: false);
			}
			catch
			{
			}
			WorldTile worldTile = CivilTest.Stage();
			if (worldTile != null)
			{
				you.current_position = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
				Note($"cadence: walking from {worldTile.x},{worldTile.y}");
			}
			yield return CivilTest.Wait(1.5f);
			List<float> t = new List<float>();
			List<float> dt = new List<float>();
			List<float> eye = new List<float>();
			List<float> body = new List<float>();
			List<float> ms = new List<float>();
			Vector3 e0 = Mod.EyeForTest;
			Vector2 b0 = you.current_position;
			PlayInput.TestPress(KeyCode.W);
			for (float s = 0f; s < 24f; s += Time.unscaledDeltaTime)
			{
				if (!you.isAlive())
				{
					break;
				}
				yield return null;
				float num = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
				Vector3 eyeForTest = Mod.EyeForTest;
				Vector2 current_position = you.current_position;
				t.Add(Time.realtimeSinceStartup);
				dt.Add(num);
				ms.Add(Mod.FrameMs);
				_worldAt.Add(World.world.getCurWorldTime());
				_gcAt.Add(GC.CollectionCount(0));
				_parts.Add(new float[3]
				{
					(float)Hitch.SumUpdates,
					(float)Hitch.SumLate,
					(float)Hitch.SumAfter
				});
				eye.Add(new Vector2(eyeForTest.x - e0.x, eyeForTest.y - e0.y).magnitude / num);
				body.Add((current_position - b0).magnitude / num);
				e0 = eyeForTest;
				b0 = current_position;
			}
			PlayInput.TestRelease(KeyCode.W);
			if (!Check("cadence: frames to read", t.Count > 100, t.Count + " frames"))
			{
				yield break;
			}
			Rhythm("the eye's speed", t, eye, dt);
			Rhythm("the body's speed", t, body, dt);
			float num2 = Median(dt);
			List<float> list = new List<float>();
			for (int i = 0; i < t.Count; i++)
			{
				if (dt[i] > num2 * 1.5f)
				{
					list.Add(t[i]);
				}
			}
			Note($"cadence: {t.Count} frames, median {num2 * 1000f:F1} ms, {list.Count} long (R at 1 Hz {Fold(list):F2}), mod frame median {Median(ms):F1} ms");
			List<float> list2 = new List<float>();
			for (int j = 1; j < _gcAt.Count; j++)
			{
				if (_gcAt[j] != _gcAt[j - 1])
				{
					list2.Add(t[j]);
				}
			}
			List<string> list3 = new List<string>();
			for (int k = 1; k < list2.Count && k < 12; k++)
			{
				list3.Add((list2[k] - list2[k - 1]).ToString("F2"));
			}
			Note(string.Format("cadence: {0} collections in {1:F1} s (R at 1 Hz {2:F2}), gaps {3} s", list2.Count, t[t.Count - 1] - t[0], Fold(list2), string.Join(" ", list3.ToArray())));
			for (int l = 1; l < t.Count; l++)
			{
				if (dt[l] > num2 * 1.4f)
				{
					Note($"cadence:   slow frame at {t[l]:F3}: {dt[l] * 1000f:F1} ms{PartsOf(l)}");
				}
			}
		}

		private string PartsOf(int i)
		{
			if (i < 1 || i >= _parts.Count)
			{
				return "";
			}
			float[] array = _parts[i - 1];
			float[] array2 = _parts[i];
			bool flag = _gcAt[i] != _gcAt[i - 1] || (i + 1 < _gcAt.Count && _gcAt[i + 1] != _gcAt[i]);
			return string.Format(", parts {0:F1}/{1:F1}/{2:F1} ms{3}", array2[0] - array[0], array2[1] - array[1], array2[2] - array[2], flag ? ", a collection" : "");
		}

		private void Rhythm(string what, List<float> t, List<float> v, List<float> dt)
		{
			float num = Median(v);
			List<float> list = new List<float>();
			for (int i = 2; i < v.Count; i++)
			{
				if (Mathf.Abs(v[i] - num) > num * 0.33f)
				{
					list.Add(t[i]);
				}
			}
			float num2 = Power(t, v, dt, num, 1f);
			float num3 = Power(t, v, dt, num, 0.7f);
			float num4 = Power(t, v, dt, num, 1.4f);
			Note($"cadence: {what}: median {num:F2} tiles/s, {list.Count} of {v.Count} frames off by a third (R at 1 Hz {Fold(list):F2}); power at 1 Hz {num2:F3}, 0.7 Hz {num3:F3}, 1.4 Hz {num4:F3}");
			int num5 = 0;
			for (int j = 2; j < v.Count; j++)
			{
				if (num5 >= 16)
				{
					break;
				}
				if (Mathf.Abs(v[j] - num) > num * 0.33f)
				{
					num5++;
					float num6 = t[j] - Mathf.Floor(t[j]);
					float num7 = ((_worldAt.Count > j) ? ((float)(_worldAt[j] - Math.Floor(_worldAt[j]))) : (-1f));
					Note($"cadence:   {what} jerk at {t[j]:F3} (phase {num6:F2}, world clock phase {num7:F2}): {v[j - 1]:F2} -> {v[j]:F2} -> {((j + 1 < v.Count) ? v[j + 1] : 0f):F2} tiles/s, frame {dt[j] * 1000f:F1} ms{PartsOf(j)}");
				}
			}
		}

		private static float Fold(List<float> times)
		{
			if (times.Count < 3)
			{
				return 0f;
			}
			double num = 0.0;
			double num2 = 0.0;
			foreach (float time in times)
			{
				num += Math.Cos(Math.PI * 2.0 * (double)time);
				num2 += Math.Sin(Math.PI * 2.0 * (double)time);
			}
			return (float)(Math.Sqrt(num * num + num2 * num2) / (double)times.Count);
		}

		private static float Power(List<float> t, List<float> v, List<float> dt, float mean, float hz)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			for (int i = 0; i < t.Count; i++)
			{
				double num4 = v[i] - mean;
				double num5 = dt[i];
				num += num4 * num5 * Math.Cos(Math.PI * 2.0 * (double)hz * (double)t[i]);
				num2 += num4 * num5 * Math.Sin(Math.PI * 2.0 * (double)hz * (double)t[i]);
				num3 += num5;
			}
			if (!(num3 > 0.0))
			{
				return 0f;
			}
			return (float)(Math.Sqrt(num * num + num2 * num2) / num3 / (double)Mathf.Max(0.001f, mean));
		}

		private static float Median(List<float> v)
		{
			List<float> list = new List<float>(v);
			list.Sort();
			if (list.Count <= 0)
			{
				return 0f;
			}
			return list[list.Count / 2];
		}

		private IEnumerator Measure(string how, float seconds, bool fp, bool quiet = false)
		{
			Dictionary<Actor, Vector2> at = new Dictionary<Actor, Vector2>();
			foreach (Actor item in _town)
			{
				if (item != null && item.isAlive() && at.Count < 40)
				{
					at[item] = item.current_position;
				}
			}
			double w0 = World.world.getCurWorldTime();
			int f0 = Time.frameCount;
			float hungerLow = float.MaxValue;
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				hungerLow = Mathf.Min(hungerLow, World.world.timer_nutrition_decay);
				yield return null;
			}
			double curWorldTime = World.world.getCurWorldTime();
			int num = Time.frameCount - f0;
			int num2 = 0;
			foreach (KeyValuePair<Actor, Vector2> item2 in at)
			{
				if (item2.Key != null && item2.Key.isAlive() && (item2.Key.current_position - item2.Value).sqrMagnitude > 0.04f)
				{
					num2++;
				}
			}
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			try
			{
				flag = Config.paused || World.world.isPaused();
				flag2 = ScrollWindow.isWindowActive();
				flag3 = Config.time_scale_asset.render_skip && !Mod.IsFirstPerson;
			}
			catch
			{
			}
			string text = $"world time +{curWorldTime - w0:F2} in {num} frames, {num2} of {at.Count} walked, paused {flag}, window {flag2}, render skip {flag3}, step {Time.fixedDeltaTime:F4} (own {_ownStep:F4}), fp {Mod.IsFirstPerson}";
			bool flag4 = curWorldTime - w0 > 0.004 * (double)num && num2 >= Mathf.Min(3, at.Count) && !flag && !flag3;
			if (!fp && !Mod.IsFirstPerson)
			{
				flag4 &= Mathf.Abs(Time.fixedDeltaTime - _ownStep) < 0.0001f;
			}
			if (!quiet || !flag4)
			{
				Check(how + ": the world goes on", flag4, text);
			}
			else
			{
				Pass++;
			}
			if (quiet && flag4)
			{
				Note(how + ": ok (" + text + ")");
			}
			if (hungerLow < -0.5f)
			{
				Check(how + ": the hunger clock wound up", ok: false, $"lowest {hungerLow:F2}");
			}
		}

		private Actor Pick()
		{
			for (int i = 0; i < _town.Count; i++)
			{
				Actor actor = _town[_pick++ % _town.Count];
				if (actor != null && actor.isAlive() && actor.isAdult())
				{
					return actor;
				}
			}
			return null;
		}
	}
}
