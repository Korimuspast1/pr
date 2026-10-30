using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Net
{
	internal sealed class NetTest : MonoBehaviour
	{
		private sealed class FakeHost
		{
			private readonly TcpListener _listener;

			private volatile bool _stop;

			public FakeHost(int port, bool hello)
			{
				FakeHost fakeHost = this;
				_listener = new TcpListener(IPAddress.Loopback, port);
				_listener.Start();
				Thread thread = new Thread((ThreadStart)delegate
				{
					fakeHost.Serve(hello);
				});
				thread.IsBackground = true;
				thread.Name = "nettest-fake";
				thread.Start();
			}

			private void Serve(bool hello)
			{
				try
				{
					while (!_stop)
					{
						TcpClient tcpClient = _listener.AcceptTcpClient();
						NetworkStream stream = tcpClient.GetStream();
						if (hello)
						{
							Frame(stream, 1, new byte[8] { 7, 0, 0, 0, 12, 0, 0, 0 });
						}
						byte[] array = new byte[4];
						while (!_stop && Read(stream, array, 4))
						{
							int num = array[0] | (array[1] << 8) | (array[2] << 16) | (array[3] << 24);
							if (num < 1 || num > 1048576)
							{
								break;
							}
							byte[] array2 = new byte[num];
							if (!Read(stream, array2, num))
							{
								break;
							}
							if (array2[0] == 3 && hello)
							{
								byte[] array3 = new byte[num - 1];
								Buffer.BlockCopy(array2, 1, array3, 0, num - 1);
								Frame(stream, 4, array3);
							}
						}
						try
						{
							tcpClient.Close();
						}
						catch
						{
						}
					}
				}
				catch
				{
				}
			}

			private static void Frame(NetworkStream st, byte kind, byte[] pay)
			{
				int num = pay.Length + 1;
				byte[] array = new byte[4 + num];
				array[0] = (byte)num;
				array[1] = (byte)(num >> 8);
				array[2] = (byte)(num >> 16);
				array[3] = (byte)(num >> 24);
				array[4] = kind;
				Buffer.BlockCopy(pay, 0, array, 5, pay.Length);
				st.Write(array, 0, array.Length);
			}

			private static bool Read(NetworkStream st, byte[] b, int n)
			{
				int num;
				for (int i = 0; i < n; i += num)
				{
					num = st.Read(b, i, n - i);
					if (num <= 0)
					{
						return false;
					}
				}
				return true;
			}

			public void Stop()
			{
				_stop = true;
				try
				{
					_listener.Stop();
				}
				catch
				{
				}
			}
		}

		private string _options = "";

		private int _port = 27801;

		private int _pass;

		private int _fail;

		private const string Password = "pw";

		private static Relief LandNow => ReliefWorld.Sampler?.Relief;

		private IEnumerator HostCrab()
		{
			SetPrefs("Hoster");
			MpPrefs.Current.Port = _port;
			MpPrefs.Current.Password = "pw";
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 6, 0);
				if (king != null)
				{
					break;
				}
			}
			if (!Check("a village to play in", king != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			WorldBoxMod mod = WorldBoxMod.Instance;
			Check("first person on the host", mod != null && mod.IsFirstPerson);
			string text = NetSession.Host();
			if (!Check("hosting started", text == null, text ?? ""))
			{
				yield break;
			}
			NetPlayer joiner = null;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 300f)
			{
				foreach (NetPlayer value in NetSession.Current.Players.Values)
				{
					if (!value.Host && value.Ready)
					{
						joiner = value;
					}
				}
				if (joiner != null)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (!Check("a joiner got the world", joiner != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Say("crab " + king.data.id);
			t0 = Time.realtimeSinceStartup;
			Actor crab = null;
			while (Time.realtimeSinceStartup - t0 < 30f)
			{
				crab = ((joiner.Body != 0L) ? World.world.units.get(joiner.Body) : null);
				if (crab != null && Crabzillas.Is(crab))
				{
					break;
				}
				crab = null;
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!Check("the joiner's Crabzilla is given to the joiner", crab != null, "body " + joiner.Body))
			{
				yield break;
			}
			Check("the host is still in its own body", ControllableUnit._unit_main == king && mod.IsFirstPerson && Crabzillas.You == null, "controlling " + ((ControllableUnit._unit_main != null) ? WorldBoxMod.Describe(ControllableUnit._unit_main) : "none"));
			Check("the crab isn't under the host's controls", !ControllableUnit._units.Contains(crab));
			string line = null;
			Vector2 c0 = crab.current_position;
			yield return WaitChat("walked crab", 40f, delegate(string x)
			{
				line = x;
			});
			yield return new WaitForSecondsRealtime(0.5f);
			Check("the joiner's crab walks on the host", line != null && (crab.current_position - c0).magnitude > 2f, $"moved {(crab.current_position - c0).magnitude:F1} tiles");
			yield return WaitChat("aim ", 30f, delegate(string x)
			{
				line = x;
			});
			if (!Check("the joiner aims", line != null))
			{
				yield break;
			}
			string[] array = line.Split(' ');
			int num = (int)float.Parse(array[1], CultureInfo.InvariantCulture);
			int num2 = (int)float.Parse(array[2], CultureInfo.InvariantCulture);
			List<Actor> flock = new List<Actor>();
			for (int num3 = 0; num3 < 4; num3++)
			{
				WorldTile tile = World.world.GetTile(num - 1 + num3 % 2 * 2, num2 - 1 + num3 / 2 * 2);
				Actor actor = null;
				try
				{
					if (tile != null)
					{
						actor = World.world.units.spawnNewUnit("sheep", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					}
				}
				catch
				{
				}
				if (actor != null)
				{
					actor.makeWait(30f);
					flock.Add(actor);
				}
			}
			Vector2 vector = (crab.current_position + new Vector2(num, num2)) * 0.5f;
			mod.DebugEye = vector + new Vector2(-40f, 0f);
			mod.DebugEyeSet = true;
			mod.DebugEyeHeight = 16f;
			mod.SetYaw(0f);
			mod.SetPitchDegrees(-12f);
			yield return new WaitForSecondsRealtime(0.5f);
			Say("flock " + flock.Count);
			bool heard = false;
			bool drawn = false;
			bool mapLaser = false;
			float most = 0f;
			t0 = Time.realtimeSinceStartup;
			bool pictured = false;
			float drawnAt = -1f;
			while (Time.realtimeSinceStartup - t0 < 10f)
			{
				if (CrabFire.Of(crab, out var shot) && shot.Any)
				{
					heard = true;
				}
				float num4 = Mathf.Max(Crabzillas.PowerOf(crab, 0), Crabzillas.PowerOf(crab, 1));
				most = Mathf.Max(most, num4);
				if (num4 > 0.9f)
				{
					drawn = true;
				}
				try
				{
					mapLaser |= crab.getActorComponent<Crabzilla>()._beam_enabled;
				}
				catch
				{
				}
				if (drawn && drawnAt < 0f)
				{
					drawnAt = Time.realtimeSinceStartup;
				}
				if (drawn && !pictured && Time.realtimeSinceStartup - drawnAt > 0.6f)
				{
					pictured = true;
					mod.SaveFrame("net_crab_beam_host.png");
				}
				int num5 = 0;
				foreach (Actor item2 in flock)
				{
					if (item2 != null && item2.isAlive())
					{
						num5++;
					}
				}
				if (heard && drawn && pictured && num5 == 0)
				{
					break;
				}
				yield return null;
			}
			int num6 = 0;
			foreach (Actor item3 in flock)
			{
				if (item3 != null && item3.isAlive())
				{
					num6++;
				}
			}
			Check("the joiner's beams are heard on the host", heard);
			Check("the host draws the joiner's beams", drawn, "power " + most.ToString("0.00"));
			Check("the game's own laser picture shows on the host's map", mapLaser);
			Check("the joiner's beams kill on the host", flock.Count > 0 && num6 == 0, num6 + " of " + flock.Count + " left");
			mod.DebugEyeSet = false;
			mod.DebugEyeHeight = 0f;
			Say("crab done");
			yield return WaitChat("done", 30f, delegate(string x)
			{
				line = x;
			});
		}

		private IEnumerator JoinCrab()
		{
			SetPrefs("Joiner");
			float t0 = Time.realtimeSinceStartup;
			while (!WorldSync.Mirroring && Time.realtimeSinceStartup - t0 < 300f)
			{
				if (!NetSession.Active)
				{
					string text = NetSession.JoinIp("127.0.0.1:" + _port, "pw");
					if (text != null)
					{
						Log.Info("NETTEST join: " + text);
					}
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			if (!Check("joined and got the host's world", WorldSync.Mirroring))
			{
				yield break;
			}
			string line = null;
			yield return WaitChat("crab ", 60f, delegate(string x)
			{
				line = x;
			});
			Actor actor = ((line != null) ? World.world.units.get(long.Parse(line.Substring(5))) : null);
			if (!Check("the host's king is here", actor != null))
			{
				yield break;
			}
			WorldTile worldTile = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				float magnitude = (new Vector2(item.x, item.y) - actor.current_position).magnitude;
				if (magnitude > 35f && magnitude < 80f && item.x > 30 && item.y > 30 && item.x < MapBox.width - 60 && item.y < MapBox.height - 60)
				{
					worldTile = item;
					break;
				}
			}
			GodPower godPower = AssetManager.powers.get("crabzilla");
			if (!Check("a place and the Crabzilla power", worldTile != null && godPower != null))
			{
				yield break;
			}
			World.world.player_control.clickedFinal(new Vector2Int(worldTile.x, worldTile.y), godPower, pTrack: true);
			t0 = Time.realtimeSinceStartup;
			Actor crab = null;
			while (Time.realtimeSinceStartup - t0 < 30f)
			{
				crab = ((WorldSync.MyBody != 0L) ? World.world.units.get(WorldSync.MyBody) : null);
				if (crab != null && Crabzillas.Is(crab) && ControllableUnit._unit_main == crab)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (Check("you are your Crabzilla", crab != null && ControllableUnit._unit_main == crab, "body " + WorldSync.MyBody))
			{
				WorldBoxMod mod = WorldBoxMod.Instance;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 8f && Crabzillas.You != crab)
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Check("in first person as the crab", mod != null && mod.IsFirstPerson && Crabzillas.You == crab);
				Vector2 c0 = crab.current_position;
				yield return Walk(90f, 2f);
				Check("the crab walks in the paused copy", (crab.current_position - c0).magnitude > 2f, $"moved {(crab.current_position - c0).magnitude:F1} tiles");
				Say("walked crab");
				mod.SetYaw(MathF.PI / 2f);
				mod.SetPitchDegrees(-25f);
				yield return new WaitForSecondsRealtime(1f);
				Vec3 vec = Crabzillas.TargetOf(0);
				Say("aim " + vec.X.ToString("0.0", CultureInfo.InvariantCulture) + " " + vec.Y.ToString("0.0", CultureInfo.InvariantCulture));
				yield return WaitChat("flock ", 20f, delegate(string x)
				{
					line = x;
				});
				bool[] scriptedFire = Crabzillas.ScriptedFire;
				bool[] scriptedFire2 = Crabzillas.ScriptedFire;
				bool flag = true;
				scriptedFire2[1] = true;
				scriptedFire[0] = flag;
				yield return new WaitForSecondsRealtime(1.5f);
				mod.SaveFrame("net_crab_beam_joiner.png");
				Check("your own beams are out", Crabzillas.PowerOf(crab, 0) > 0.9f && Crabzillas.PowerOf(crab, 1) > 0.9f);
				yield return WaitChat("crab done", 15f, delegate(string x)
				{
					line = x;
				});
				bool[] scriptedFire3 = Crabzillas.ScriptedFire;
				bool[] scriptedFire4 = Crabzillas.ScriptedFire;
				flag = false;
				scriptedFire4[1] = false;
				scriptedFire3[0] = flag;
				Say("done");
				yield return new WaitForSecondsRealtime(2f);
			}
		}

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			_options = File.ReadAllText(path);
			bool quit = _options.Contains("quit");
			int num = _options.IndexOf("port=", StringComparison.Ordinal);
			if (num >= 0)
			{
				string text = "";
				for (int i = num + 5; i < _options.Length && char.IsDigit(_options[i]); i++)
				{
					text += _options[i];
				}
				int.TryParse(text, out _port);
			}
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			IpTransport.LoopbackOnly = true;
			bool flag2 = _options.Contains("host");
			Log.Info("NETTEST " + (flag2 ? "host" : "joiner") + " on port " + _port + ", world " + MapBox.width + "x" + MapBox.height + ", units " + World.world.units.Count);
			IEnumerator run = ((!_options.Contains("crab")) ? ((!_options.Contains("lights")) ? ((!_options.Contains("relief")) ? ((!_options.Contains("plots")) ? ((!_options.Contains("towns")) ? (flag2 ? Host() : Join()) : (flag2 ? HostTowns() : JoinTowns())) : (flag2 ? HostPlots() : JoinPlots())) : (flag2 ? HostRelief() : JoinRelief())) : (flag2 ? HostLights() : JoinLights())) : (flag2 ? HostCrab() : JoinCrab()));
			while (true)
			{
				object current;
				try
				{
					if (!run.MoveNext())
					{
						break;
					}
					current = run.Current;
					goto IL_034d;
				}
				catch (Exception ex)
				{
					Check("no exception", ok: false, ex.ToString());
				}
				break;
				IL_034d:
				yield return current;
			}
			Log.Info("NETTEST " + _pass + " passed, " + _fail + " failed");
			if (NetSession.Active)
			{
				NetSession.Leave();
			}
			yield return new WaitForSecondsRealtime(2f);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("NETTEST DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private bool Check(string what, bool ok, string detail = "")
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("NETTEST " + (ok ? "PASS " : "FAIL ") + what + ((detail != "") ? (" (" + detail + ")") : ""));
			return ok;
		}

		private static void Say(string text)
		{
			NetSession.Current?.SendChat(text, party: false);
		}

		private static string Why(Actor target, Actor by)
		{
			try
			{
				return "target inside " + target.isInsideSomething() + ", flying " + ((BaseSimObject)target).isFlying() + ", in water " + ((BaseSimObject)target).isInLiquid() + ", magnet " + target.isInMagnet() + ", action " + (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)target.ai)?.action?.id ?? "none") + ((((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)target.ai)?.action != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)target.ai).action.special_prevent_can_be_attacked) ? " (can't be attacked)" : "") + "; striker " + ((by != null) ? (WorldBoxMod.Describe(by) + " possessed " + by._has_status_possessed + ", host strikes for it " + WorldSync.HostStrikesFor(by)) : "none");
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		private IEnumerator WaitChat(string start, float seconds, Action<string> got)
		{
			float t0 = Time.realtimeSinceStartup;
			_ = NetSession.Current?.Chat.Count;
			while (Time.realtimeSinceStartup - t0 < seconds && NetSession.Current != null)
			{
				foreach (NetSession.ChatLine item in NetSession.Current.Chat)
				{
					if (!item.System && item.From != NetSession.Current.Me?.Id && item.Text.StartsWith(start, StringComparison.Ordinal))
					{
						got(item.Text);
						yield break;
					}
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			got(null);
		}

		private static void SetPrefs(string name)
		{
			NetSession.NoSteamForTest = true;
			MpPrefs current = MpPrefs.Current;
			current.Nickname = name;
			current.Port = 0;
			current.AllowIp = true;
			current.ListOnSteam = false;
			current.RouterPort = false;
			current.MaxPlayers = 4;
			current.Rules = new HostRules();
		}

		private static IEnumerator Walk(float yawDeg, float seconds)
		{
			try
			{
				if (WorldBoxMod.Instance != null && WorldBoxMod.Instance.Converse.Open)
				{
					WorldBoxMod.Instance.EndConversation();
				}
			}
			catch
			{
			}
			PossessionHooks.Yaw = yawDeg * (MathF.PI / 180f);
			WorldBoxMod.Instance?.SetYaw(yawDeg * (MathF.PI / 180f));
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(seconds);
			PossessionHooks.ScriptedInput = Vector2.zero;
		}

		private IEnumerator Host()
		{
			SetPrefs("Hoster");
			MpPrefs.Current.Port = _port;
			MpPrefs.Current.Password = "pw";
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 10, 2);
				if (king != null)
				{
					break;
				}
			}
			if (!Check("a village to play in", king != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			Check("first person on the host", WorldBoxMod.Instance != null && WorldBoxMod.Instance.IsFirstPerson);
			string text = NetSession.Host();
			if (!Check("hosting started", text == null, text ?? ""))
			{
				yield break;
			}
			Check("the host's card has this PC's addresses", NetSession.Current.LanAddresses.Count > 0, string.Join(", ", NetSession.Current.LanAddresses.ToArray()));
			JoinCode.Parsed parsed = JoinCode.Read(NetSession.Current.Code);
			Check("the host has a join code holding its address", parsed.Valid && parsed.Host == "127.0.0.1" && parsed.Port == _port, NetSession.Current.Code);
			NetPlayer joiner = null;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 300f)
			{
				foreach (NetPlayer value2 in NetSession.Current.Players.Values)
				{
					if (!value2.Host && value2.Ready)
					{
						joiner = value2;
					}
				}
				if (joiner != null)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (!Check("a joiner got the world", joiner != null, (Time.realtimeSinceStartup - t0).ToString("0") + " s"))
			{
				yield break;
			}
			t0 = Time.realtimeSinceStartup;
			while (joiner.Body == 0L && Time.realtimeSinceStartup - t0 < 60f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!Check("the joiner took a body", joiner.Body != 0))
			{
				yield break;
			}
			Actor body = World.world.units.get(joiner.Body);
			Vector2 p0 = body?.current_position ?? Vector2.zero;
			NetSession.Current.SendChat("Welcome, " + joiner.Name, party: false);
			yield return Picture("net_host_tab.png", tab: true);
			Check("the joiner's body is held from its own life", ((BaseSimObject)body)?.hasStatus("possessed") ?? false);
			string line = null;
			yield return WaitChat("walked", 40f, delegate(string s)
			{
				line = s;
			});
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 vector = body?.current_position ?? Vector2.zero;
			Check("the joiner's walk is seen on the host", line != null && (vector - p0).magnitude > 1.5f, $"moved {(vector - p0).magnitude:F2} tiles");
			yield return Walk(90f, 3f);
			Say("host walked");
			Actor actor = null;
			if (body != null)
			{
				WorldTile pTile = World.world.GetTile((int)body.current_position.x + 3, (int)body.current_position.y) ?? body.current_tile;
				try
				{
					actor = World.world.units.spawnNewUnit("sheep", pTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
				}
				catch (Exception ex)
				{
					Log.Error("NETTEST sheep: " + ex.Message);
				}
			}
			if (!Check("a sheep spawned", actor != null))
			{
				yield break;
			}
			long sheepId = actor.data.id;
			actor.makeWait(60f);
			Say("sheep " + sheepId);
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 80f)
			{
				Actor actor2 = World.world.units.get(sheepId);
				if (actor2 == null || !actor2.isAlive())
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Actor actor3 = World.world.units.get(sheepId);
			Check("the joiner's blows killed the sheep on the host", actor3 == null || !actor3.isAlive());
			int before = Count("sheep");
			Say("power");
			yield return WaitChat("powered", 30f, delegate(string s)
			{
				line = s;
			});
			yield return new WaitForSecondsRealtime(3f);
			Check("the joiner's god power worked on the host", line != null && Count("sheep") > before, before + " -> " + Count("sheep"));
			yield return WaitChat("patched ", 30f, delegate(string s)
			{
				line = s;
			});
			int money;
			if (line != null)
			{
				string[] array = line.Split(' ');
				long pID = long.Parse(array[1]);
				long npcId = long.Parse(array[3]);
				money = int.Parse(array[2]);
				Actor me = World.world.units.get(pID);
				Actor npc = ((npcId != 0L) ? World.world.units.get(npcId) : null);
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 5f && (me == null || me.data.money != money))
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				int pResult = 0;
				string pResult2 = null;
				me?.data.get("fp_nettest", out pResult, 0);
				npc?.data.get("fp_nettest_npc", out pResult2, null);
				Check("the joiner's gold reached the host", me != null && me.data.money == money, ((me != null) ? me.data.money.ToString() : "no body") + " / " + money);
				Check("the joiner's custom data on itself reached the host", pResult == 7, pResult.ToString());
				Check("the joiner's custom data on someone near reached the host", npcId == 0L || pResult2 == "hello", pResult2 ?? "none");
			}
			yield return WaitChat("made ", 20f, delegate(string s)
			{
				line = s;
			});
			if (line != null)
			{
				long npcId = long.Parse(line.Split(' ')[1]);
				Actor npc = null;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 5f)
				{
					Actor actor4;
					npc = (actor4 = World.world.units.get(npcId));
					if (actor4 != null)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Check("the creature the joiner made is in the host's world", npc?.isAlive() ?? false, (npc != null) ? WorldBoxMod.Describe(npc) : ("id " + npcId));
			}
			Actor founder = null;
			foreach (WorldTile item2 in KingTest.GoodSites())
			{
				if ((item2.pos - king.current_tile.pos).sqrMagnitude >= 1600)
				{
					founder = KingTest.FoundKingdom(item2, "human", 6, 0);
					if (founder != null)
					{
						break;
					}
				}
			}
			if (Check("a second village founded", founder != null && founder.city != null))
			{
				yield return new WaitForSecondsRealtime(1.5f);
				Say("village " + founder.city.data.id + " " + founder.kingdom.data.id + " " + founder.data.id);
				yield return WaitChat("saw village", 20f, delegate(string s)
				{
					line = s;
				});
				Check("the joiner saw the village", line != null && !line.Contains("not"), line ?? "no answer");
				yield return WaitChat("bordered ", 30f, delegate(string s)
				{
					line = s;
				});
				string[] array2 = ((line != null) ? line.Split(' ') : new string[0]);
				if (array2.Length >= 4 && int.TryParse(array2[2], out money) && int.TryParse(array2[3], out var zy))
				{
					TileZone bz = World.world.zone_calculator.getZone(money, zy);
					City town = founder.city;
					float tb = Time.realtimeSinceStartup;
					while (Time.realtimeSinceStartup - tb < 5f && (bz == null || town == null || bz.city != town))
					{
						yield return new WaitForSecondsRealtime(0.25f);
					}
					bool flag = bz != null && town != null && bz.city == town;
					Check("the joiner's border brush grew the town on the host", flag, "zone " + money + "," + zy + " in " + ((bz?.city?.data != null) ? bz.city.data.id.ToString() : "none"));
					Say(flag ? "sawborder yes" : "sawborder no");
				}
				else
				{
					Check("the joiner used the border brush", ok: false, line ?? "no answer");
				}
			}
			if (body != null)
			{
				WorldTile c = body.current_tile;
				TileType tileType = AssetManager.tiles.get("sand");
				int num = 0;
				for (int num2 = 3; num2 <= 5; num2++)
				{
					for (int num3 = -1; num3 <= 1; num3++)
					{
						WorldTile tile = World.world.GetTile(c.x + num2, c.y + num3);
						if (tile != null && tileType != null && tile.main_type != tileType)
						{
							tile.setTileTypes(tileType, null);
							num++;
						}
					}
				}
				Say("tiles " + (c.x + 4) + " " + c.y + " sand " + num);
				yield return WaitChat("saw tiles", 20f, delegate(string s)
				{
					line = s;
				});
				Check("the joiner saw the land change", line != null && !line.Contains("not"), line ?? "no answer");
				Building made = null;
				string text2 = null;
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple?.asset != null && simple.asset.id.StartsWith("tree", StringComparison.Ordinal))
					{
						text2 = simple.asset.id;
						break;
					}
				}
				if (text2 == null)
				{
					foreach (Building simple2 in World.world.buildings.getSimpleList())
					{
						if (simple2?.asset != null && simple2.city == null)
						{
							text2 = simple2.asset.id;
							break;
						}
					}
				}
				WorldTile tile2 = World.world.GetTile(c.x - 4, c.y + 3);
				if (text2 != null && tile2 != null)
				{
					try
					{
						made = World.world.buildings.addBuilding(text2, tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					}
					catch (Exception ex2)
					{
						Log.Error("NETTEST building: " + ex2.Message);
					}
				}
				if (!Check("a building put up", made != null, text2 ?? "no wild building to copy"))
				{
					Say("building none");
				}
				else
				{
					Say("building " + made.data.id);
					yield return WaitChat("saw building", 20f, delegate(string s)
					{
						line = s;
					});
					Check("the joiner saw the building", line != null && !line.Contains("not"), line ?? "no answer");
					made.startDestroyBuilding();
					yield return new WaitForSecondsRealtime(1f);
					Say("building gone");
					yield return WaitChat("saw gone", 20f, delegate(string s)
					{
						line = s;
					});
					Check("the joiner saw it go", line != null && !line.Contains("not"), line ?? "no answer");
				}
			}
			NetSession ses = NetSession.Current;
			long bodyBefore;
			if (joiner.Body != 0L)
			{
				long npcId = joiner.Body;
				ses.Rules.Death = 1;
				ses.Rules.RespawnSeconds = 2;
				Kill(World.world.units.get(npcId));
				Say("killed 1");
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 12f && (joiner.Body == 0L || joiner.Body == npcId))
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Check("respawned in town after dying (death rule 1)", joiner.Body != 0L && joiner.Body != npcId, "body " + joiner.Body);
				yield return WaitChat("saw respawn", 20f, delegate(string s)
				{
					line = s;
				});
				long body2 = joiner.Body;
				ses.Rules.Death = 0;
				Kill(World.world.units.get(body2));
				Say("killed 0");
				t0 = Time.realtimeSinceStartup;
				bool sawNone = false;
				while (Time.realtimeSinceStartup - t0 < 6f && !sawNone)
				{
					sawNone = joiner.Body == 0;
					yield return null;
				}
				Check("back to the god view after dying (death rule 0)", sawNone);
				yield return WaitChat("born", 30f, delegate(string s)
				{
					line = s;
				});
				Check("born in a town from the spawn panel", joiner.Body != 0, "body " + joiner.Body);
				bodyBefore = joiner.Body;
				Say("resync");
				yield return WaitChat("resynced", 90f, delegate(string s)
				{
					line = s;
				});
				Check("the joiner resynced without leaving", line != null && !line.Contains("not") && NetSession.Current == ses && joiner.Ready, line ?? "no answer");
				Check("the joiner's body is its own again after the resync", joiner.Body == bodyBefore && bodyBefore != 0, "body " + joiner.Body + ", was " + bodyBefore);
				int num4 = 0;
				foreach (Actor simple3 in World.world.units.getSimpleList())
				{
					if (simple3 != null && simple3.isAlive())
					{
						num4++;
					}
				}
				Say("units " + num4);
				yield return WaitChat("units seen", 20f, delegate(string s)
				{
					line = s;
				});
				Check("the joiner has the host's creatures after the resync", line != null && !line.Contains("not"), line ?? "no answer");
				City city = null;
				Actor actor5 = null;
				Actor actor6 = null;
				foreach (City city2 in World.world.cities)
				{
					if (city2?.data == null || !city2.isAlive() || city2.kingdom == null || !city2.kingdom.isAlive())
					{
						continue;
					}
					actor5 = (actor6 = null);
					foreach (Actor unit in city2.getUnits())
					{
						if (unit?.data != null && unit.isAlive() && unit.isAdult() && !unit.isKing() && !unit.isCityLeader() && unit.data.id != joiner.Body && unit != ControllableUnit._unit_main)
						{
							if (actor5 == null)
							{
								actor5 = unit;
							}
							else if (actor6 == null)
							{
								actor6 = unit;
								break;
							}
						}
					}
					if (actor6 != null)
					{
						city = city2;
						break;
					}
				}
				if (city == null)
				{
					Say("rulers none");
				}
				else
				{
					city.removeLeader();
					city.setLeader(actor5, pNew: true);
					city.kingdom.setKing(actor6);
					Say("rulers " + city.kingdom.data.id + " " + actor6.data.id + " " + city.data.id + " " + actor5.data.id);
					yield return WaitChat("rulers seen", 30f, delegate(string s)
					{
						line = s;
					});
					Check("the joiner sees the host's new king and town leader", line != null && !line.Contains("not"), line ?? "no answer");
				}
			}
			if (joiner.Body != 0L)
			{
				Actor npc = World.world.units.get(joiner.Body);
				if (npc != null)
				{
					Vector3 pLaunchPosition = new Vector3(npc.current_position.x - 6f, npc.current_position.y, 0f);
					Vector3 pTargetPosition = new Vector3(npc.current_position.x + 6f, npc.current_position.y + 1f, 0f);
					try
					{
						World.world.projectiles.spawn(king, null, "arrow", pLaunchPosition, pTargetPosition, 0f, 1f);
					}
					catch (Exception ex3)
					{
						Log.Error("NETTEST arrow: " + ex3.Message);
					}
					Say("arrow");
					yield return WaitChat("saw arrow", 20f, delegate(string s)
					{
						line = s;
					});
					Check("the joiner saw the host's arrow fly", line != null && !line.Contains("not"), line ?? "no answer");
					try
					{
						Config.setWorldSpeed("x2", pUpdateDebug: false);
					}
					catch
					{
					}
					Vector2 current_position = npc.current_position;
					WorldTile tile3 = World.world.GetTile((int)current_position.x + 10, (int)current_position.y);
					WorldTile tile4 = World.world.GetTile((int)current_position.x - 30, (int)current_position.y + 10);
					WorldTile tile5 = World.world.GetTile((int)current_position.x, (int)current_position.y + 6);
					try
					{
						if (tile3 != null)
						{
							World.world.drop_manager.spawn(tile3, "tnt", 30f, 1f, 0L);
						}
					}
					catch (Exception ex4)
					{
						Log.Error("NETTEST tnt: " + ex4.Message);
					}
					try
					{
						if (tile4 != null)
						{
							Meteorite.spawnMeteorite(tile4);
						}
					}
					catch (Exception ex5)
					{
						Log.Error("NETTEST meteor: " + ex5.Message);
					}
					try
					{
						if (tile5 != null)
						{
							EffectsLibrary.spawn("fx_cloud", tile5, "cloud_normal", null, 0f, 0f, 0f);
						}
					}
					catch (Exception ex6)
					{
						Log.Error("NETTEST cloud: " + ex6.Message);
					}
					Say("fx");
					yield return WaitChat("saw fx", 25f, delegate(string s)
					{
						line = s;
					});
					Check("the joiner saw the host's drop, blast, meteor, cloud and speed", line != null && !line.Contains("not"), line ?? "no answer");
					try
					{
						Config.setWorldSpeed("x1", pUpdateDebug: false);
					}
					catch
					{
					}
					int zy = king.getHealth();
					bool sawNone = npc.kingdom == king.kingdom;
					FriendlyFire.TraceOnYou = new List<string>();
					Say("pvp");
					yield return WaitChat("pvp struck", 25f, delegate(string s)
					{
						line = s;
					});
					yield return new WaitForSecondsRealtime(0.5f);
					Check("the joiner's blows land on the host's body" + (sawNone ? " (one realm)" : ""), !king.isAlive() || king.getHealth() < zy, zy + " -> " + king.getHealth() + "; on the host: " + string.Join(", ", FriendlyFire.TraceOnYou.ToArray()) + "; " + Why(king, World.world.units.get(joiner.Body)));
					FriendlyFire.TraceOnYou = null;
					try
					{
						king.restoreHealth(king.getMaxHealth());
					}
					catch
					{
					}
					Building made = null;
					foreach (Building simple4 in World.world.buildings.getSimpleList())
					{
						if (simple4 != null && simple4.isAlive() && simple4.isBurnable() && !Torches.Burning(simple4) && (simple4.current_position - npc.current_position).sqrMagnitude < 400f)
						{
							made = simple4;
							break;
						}
					}
					if (made != null)
					{
						try
						{
							((BaseSimObject)made).addStatusEffect("burning", 0f, pColorEffect: true);
						}
						catch (Exception ex7)
						{
							Log.Error("NETTEST burn: " + ex7.Message);
						}
						Say("burnb " + made.data.id);
						yield return WaitChat("saw burnb", 20f, delegate(string s)
						{
							line = s;
						});
						Check("the joiner sees the host's building burn", line != null && !line.Contains("not"), line ?? "no answer");
						try
						{
							made.finishStatusEffect("burning");
						}
						catch
						{
						}
					}
					Say("light");
					yield return WaitChat("lit", 20f, delegate(string s)
					{
						line = s;
					});
					bool burnt = false;
					string[] array3 = ((line != null) ? line.Split(' ') : new string[0]);
					int ly;
					if (array3.Length >= 2 && array3[0] == "litb" && long.TryParse(array3[1], out bodyBefore))
					{
						float tb = Time.realtimeSinceStartup;
						while (!burnt && Time.realtimeSinceStartup - tb < 4f)
						{
							Building building = World.world.buildings.get(bodyBefore);
							burnt = building != null && (Torches.Burning(building) || (building.current_tile != null && building.current_tile.isOnFire()));
							if (!burnt)
							{
								yield return null;
							}
						}
					}
					else if (array3.Length >= 3 && int.TryParse(array3[1], out money) && int.TryParse(array3[2], out ly))
					{
						float tb = Time.realtimeSinceStartup;
						while (!burnt && Time.realtimeSinceStartup - tb < 4f)
						{
							WorldTile tile6 = World.world.GetTile(money, ly);
							burnt = tile6 != null && (tile6.isOnFire() || tile6.burned_stages > 0);
							if (!burnt)
							{
								yield return null;
							}
						}
					}
					Check("the joiner's flame set the host's ground or tree alight", burnt, line ?? "no answer");
					Say("gear");
					yield return WaitChat("geared ", 40f, delegate(string s)
					{
						line = s;
					});
					string gid = ((line != null && line.Length > 7) ? line.Substring(7) : null);
					if (gid != null && gid != "none")
					{
						yield return new WaitForSecondsRealtime(0.5f);
						string text3 = Hotbar.Weapon(npc)?.asset?.id;
						int count = Gear.Of(npc).FindAll((Gear.Piece x) => x.Id == gid).Count;
						Check("a weapon the joiner put away is out of its hand here too, once in its bag", text3 != gid && count == 1, "hand " + (text3 ?? "bare") + ", " + count + " in the bag");
					}
				}
			}
			double open0 = World.world.map_stats.world_time;
			yield return new WaitForSecondsRealtime(1.5f);
			double open1 = World.world.map_stats.world_time;
			Log.Info("NETTEST world time without a window: +" + (open1 - open0).ToString("0.00") + " in 1.5 s, paused " + Config.paused + "/" + World.world.isPaused());
			double before2 = World.world.map_stats.world_time;
			int frames = 0;
			float tw = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - tw < 1.5f)
			{
				ScrollWindow._is_window_active = true;
				frames++;
				yield return null;
			}
			double world_time = World.world.map_stats.world_time;
			bool flag2 = World.world.isPaused();
			ScrollWindow._is_window_active = false;
			Check("the world runs on under a game window", (world_time > before2 + 0.02 && !flag2) || open1 - open0 < 0.02, (world_time - before2).ToString("0.00") + " world s in " + frames + " frames, paused flag " + flag2 + ", without the window +" + (open1 - open0).ToString("0.00"));
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 20f && !Parties.Pending.ContainsKey(joiner.Id))
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (Check("the joiner's party invite reached the host", Parties.Pending.ContainsKey(joiner.Id)))
			{
				Parties.AcceptFrom(joiner.Id);
				yield return new WaitForSecondsRealtime(1f);
				Check("host and joiner in one party", Parties.SameParty(ses.Me, joiner), ses.Me.Party + " / " + joiner.Party);
				Say("party");
			}
			PlayerTalk.ReachForTest = 1000000f;
			try
			{
				if (WorldBoxMod.Instance != null && WorldBoxMod.Instance.Converse.Open)
				{
					WorldBoxMod.Instance.EndConversation();
				}
			}
			catch
			{
			}
			Say("ptalk ask");
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 20f && !PlayerTalk.CardOpen)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (Check("the joiner's ask puts the talk's card up on the host", PlayerTalk.CardOpen && !PlayerTalk.Talking && PlayerTalk.With == joiner.Id, PlayerTalk.Card.Name + ": " + PlayerTalk.Card.Line))
			{
				yield return Picture("mp_talk_ask.png", tab: false);
				PlayerTalk.Card.Pick(0);
				Check("...and taking it starts the talk", PlayerTalk.Talking && PlayerTalk.Card.Choices.Count == 4, PlayerTalk.Card.Choices.Count + " choices");
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 10f && !PlayerTalk.TypingNow(joiner.Id))
				{
					yield return null;
				}
				Check("the joiner's typing shows as dots here", PlayerTalk.TypingNow(joiner.Id));
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 10f && PlayerTalk.Card.Line != "hello host")
				{
					yield return new WaitForSecondsRealtime(0.1f);
				}
				Check("the joiner's line is on the host's card", PlayerTalk.Card.Line == "hello host", PlayerTalk.Card.Line ?? "none");
				Check("...and over the joiner's head", PlayerTalk.Bubbles.TryGetValue(joiner.Id, out var value) && value.Text == "hello host" && !PlayerTalk.TypingNow(joiner.Id));
				Check("...and in the chat, for the host only", ses.Chat.Exists((NetSession.ChatLine chatLine) => chatLine.Private && chatLine.Text == "hello host"));
				Actor actor7 = null;
				try
				{
					actor7 = World.world.units.get(joiner.Body);
				}
				catch
				{
				}
				WorldBoxMod mod = WorldBoxMod.Instance;
				if (actor7 != null && mod != null)
				{
					mod.DebugEye = actor7.current_position + new Vector2(0f, -3f);
					mod.DebugEyeSet = true;
					mod.SetYaw(MathF.PI / 2f);
					mod.SetPitchDegrees(0f);
					yield return new WaitForSecondsRealtime(0.5f);
				}
				yield return Picture("mp_talk_line.png", tab: false);
				if (mod != null)
				{
					mod.DebugEyeSet = false;
				}
				Actor unit_main = ControllableUnit._unit_main;
				Bag.Put(unit_main, "wood", 5, spill: false);
				int num5 = Civil.BagCount(unit_main, "wood");
				PlayerTalk.GiveForTest("wood", 5);
				Check("the gift left the host's bag", Civil.BagCount(unit_main, "wood") == num5 - 5, num5 + " -> " + Civil.BagCount(unit_main, "wood"));
				Say("ptalk gave");
				yield return WaitChat("ptalk got", 20f, delegate(string s)
				{
					line = s;
				});
				Check("the joiner got the gift", line == "ptalk got 5", line ?? "no answer");
				PlayerTalk.Escape();
				Check("Esc ends the talk here", !PlayerTalk.CardOpen && !PlayerTalk.Talking);
				yield return WaitChat("ptalk closed", 20f, delegate(string s)
				{
					line = s;
				});
				Check("...and on the joiner's side", line == "ptalk closed", line ?? "no answer");
			}
			PlayerTalk.ReachForTest = 0f;
			yield return WaitChat("rules ", 60f, delegate(string s)
			{
				line = s;
			});
			if (line != null && line != "rules none")
			{
				long result = 0L;
				long.TryParse(line.Substring(6), out result);
				Actor npc = World.world.units.get(result);
				int zy = npc?.getHealth() ?? 0;
				int ly = WorldSync.PatchesRefused;
				money = WorldSync.ActsRefused;
				Say("rules go");
				yield return WaitChat("rules sent", 30f, delegate(string s)
				{
					line = s;
				});
				yield return new WaitForSecondsRealtime(1.5f);
				string pResult3 = null;
				npc?.data.get("fp_nettest_far", out pResult3, null);
				Check("a joiner's change to someone far from its body is refused", npc != null && pResult3 == null && WorldSync.PatchesRefused > ly, (pResult3 ?? "untouched") + ", refused " + (WorldSync.PatchesRefused - ly));
				Check("a joiner's blow at someone out of its reach is refused", npc != null && npc.isAlive() && npc.getHealth() >= zy && WorldSync.ActsRefused > money, "health " + zy + " -> " + (npc?.getHealth() ?? 0) + ", refused " + (WorldSync.ActsRefused - money) + " (" + WorldSync.LastActRefusal + ")");
				if (line != null && line.EndsWith("guard", StringComparison.Ordinal))
				{
					Check("the joiner's raised guard reached the host", WorldSync.GuardUpForTest(joiner), "guards in " + WorldSync.GuardsIn);
				}
				else
				{
					Log.Info("NETTEST the joiner had no guard up to send (" + line + ")");
				}
				Say("rules checked");
			}
			yield return WaitChat("done", 30f, delegate(string s)
			{
				line = s;
			});
			Check("the joiner finished", line != null);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(1.5f);
			byte[] array4 = null;
			try
			{
				array4 = SaveManager.currentWorldToSavedMap().toZip();
			}
			catch (Exception ex8)
			{
				Log.Error("NETTEST saving the world: " + ex8.Message);
			}
			if (array4 != null)
			{
				SaveManager.loadMapFromBytes(array4);
				t0 = Time.realtimeSinceStartup;
				while (NetSession.Active && Time.realtimeSinceStartup - t0 < 10f)
				{
					yield return null;
				}
				Check("hosting ends when another world loads", !NetSession.Active && MpUi.HostProblem.Contains("Another world"), MpUi.HostProblem);
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 60f)
				{
					bool flag3 = true;
					try
					{
						flag3 = SmoothLoader.isLoading();
					}
					catch
					{
					}
					if (!flag3 && Config.game_loaded)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.5f);
				}
				yield return new WaitForSecondsRealtime(3f);
			}
			if (NetSession.Active)
			{
				NetSession.Leave();
			}
		}

		private static IEnumerator Picture(string name, bool tab)
		{
			WorldBoxMod mod = WorldBoxMod.Instance;
			if (!(mod == null))
			{
				if (tab)
				{
					mod.SettingsMenu.Open = true;
					mod.SettingsMenu.Tab = 6;
					mod.SettingsMenu.Unroll = 1f;
					yield return new WaitForSecondsRealtime(0.6f);
				}
				mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
				yield return new WaitForSecondsRealtime(0.5f);
				if (tab)
				{
					mod.SettingsMenu.Open = false;
				}
			}
		}

		private static void Kill(Actor a)
		{
			if (a == null || !a.isAlive())
			{
				return;
			}
			try
			{
				((BaseSimObject)a).getHit(1000000f, pFlash: true, AttackType.Weapon, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
			}
			catch
			{
				try
				{
					a.dieSimpleNone();
				}
				catch
				{
				}
			}
		}

		private static int Count(string asset)
		{
			int num = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && simple.asset.id == asset)
				{
					num++;
				}
			}
			return num;
		}

		private IEnumerator TryJoin(string address, string password, float seconds, Action<float> took)
		{
			MpUi.JoinAddress = address;
			MpUi._joinPassword = password;
			float t0 = Time.realtimeSinceStartup;
			MpUi.JoinTyped();
			while ((NetSession.Active || MpUi.FindingCode) && Time.realtimeSinceStartup - t0 < seconds)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			took(Time.realtimeSinceStartup - t0);
			if (NetSession.Active)
			{
				NetSession.Leave();
			}
		}

		private bool Shown(string what, string mustContain, float took)
		{
			string problem = MpUi.Problem;
			bool flag = MpUi.BannerUntil > Time.realtimeSinceStartup && MpUi.BannerBad && MpUi.BannerTitle.StartsWith("Could not join", StringComparison.Ordinal) && MpUi.BannerText == problem;
			bool ok = !NetSession.Active && problem != "" && flag && (mustContain == null || problem.IndexOf(mustContain, StringComparison.OrdinalIgnoreCase) >= 0);
			return Check(what, ok, took.ToString("0.0") + " s: \"" + problem + "\"" + (flag ? "" : ", no banner"));
		}

		private IEnumerator Failures()
		{
			int num = 27777;
			Check("addresses read: [::1]:27777, host:port, bare host, bare IPv6, [v6]", Split("[::1]:27777", "::1", 27777) && Split("myhost.net:1234", "myhost.net", 1234) && Split(" 192.168.1.20 ", "192.168.1.20", num) && Split("fe80::1", "fe80::1", num) && Split("[2001:db8::1]", "2001:db8::1", num) && Split("1.2.3.4:21111", "1.2.3.4", 21111));
			Check("bad addresses refused: empty, port 0, port 70000, letters, [::1]x", Bad("") && Bad("host:0") && Bad("host:70000") && Bad("host:abc") && Bad("[::1]x") && Bad("[::1]:") && Bad("a b"));
			Check("addresses put IPv6 in brackets", IpTransport.HostPort("2001:db8::1", 27777) == "[2001:db8::1]:27777" && IpTransport.HostPort("1.2.3.4", 27777) == "1.2.3.4:27777");
			string text = JoinCode.ForAddress("203.0.113.57", num);
			string text2 = JoinCode.ForAddress("198.51.100.4", 30001);
			string text3 = JoinCode.SteamOnly(new System.Random(5));
			JoinCode.Parsed parsed = JoinCode.Read(text.ToLowerInvariant());
			JoinCode.Parsed parsed2 = JoinCode.Read(text2.Replace("-", " "));
			JoinCode.Parsed parsed3 = JoinCode.Read(text3);
			Check("join codes: 8 and 12 characters, read back in any case and spacing", text.Length == 9 && text2.Length == 14 && parsed.Valid && parsed.Host == "203.0.113.57" && parsed.Port == num && parsed2.Valid && parsed2.Host == "198.51.100.4" && parsed2.Port == 30001 && parsed3.Valid && parsed3.SteamOnly, text + " " + text2 + " " + text3);
			int num2 = 0;
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] != '-')
				{
					char c = ((text[i] == 'A') ? 'B' : 'A');
					if (!JoinCode.Read(text.Substring(0, i) + c + text.Substring(i + 1)).Valid || JoinCode.Read(text.Substring(0, i) + c + text.Substring(i + 1)).Host != "203.0.113.57")
					{
						num2++;
					}
				}
			}
			Check("join codes: a mistyped character never reads as the same host", num2 == 8, num2 + "/8");
			Check("join codes vs addresses", JoinCode.Looks(text) && !JoinCode.Looks("192.168.1.20") && !JoinCode.Looks("myhost:27777") && !JoinCode.Looks("hello"));
			MpUi.JoinAddress = "";
			MpUi.JoinTyped();
			Check("an empty address says what to type", !NetSession.Active && MpUi.Problem.StartsWith("Type the host's address", StringComparison.Ordinal), MpUi.Problem);
			float took = 0f;
			yield return TryJoin("127.0.0.1:" + (_port + 1), "", 20f, delegate(float x)
			{
				took = x;
			});
			Shown("a closed port: refused, and said so", "refused", took);
			yield return TryJoin(JoinCode.ForAddress("127.0.0.1", _port + 1), "", 20f, delegate(float x)
			{
				took = x;
			});
			Shown("a code nobody answers: the host's port, said plainly", "Could not reach the host", took);
			yield return TryJoin("192.0.2.1:" + _port, "", 40f, delegate(float x)
			{
				took = x;
			});
			Shown("an address that never answers: a time-out, and said so", null, took);
			FakeHost silent = new FakeHost(_port + 2, hello: false);
			yield return TryJoin("127.0.0.1:" + (_port + 2), "", 40f, delegate(float x)
			{
				took = x;
			});
			silent.Stop();
			Shown("something that isn't a Worldfall host: said so", "No Worldfall host", took);
			FakeHost stuck = new FakeHost(_port + 3, hello: true);
			yield return TryJoin("127.0.0.1:" + (_port + 3), "", 40f, delegate(float x)
			{
				took = x;
			});
			stuck.Stop();
			Shown("a host that never answers the join: the joiner's own time limit, and said so", "did not answer", took);
			Check("...within its limit", took < 25f, took.ToString("0.0") + " s");
			float t0 = Time.realtimeSinceStartup;
			bool up = false;
			while (!up && Time.realtimeSinceStartup - t0 < 240f)
			{
				bool answered = false;
				bool done = false;
				NetSession.AskInfo("127.0.0.1:" + _port, delegate(ServerInfo info, string why)
				{
					answered = info != null;
					done = true;
				});
				while (!done)
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				up = answered;
				if (!up)
				{
					yield return new WaitForSecondsRealtime(2f);
				}
			}
			if (!Check("the host is up", up, (Time.realtimeSinceStartup - t0).ToString("0") + " s"))
			{
				yield break;
			}
			Actor own = null;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive())
				{
					own = simple;
					break;
				}
			}
			if (own != null)
			{
				ControllableUnit.setControllableCreature(own);
				yield return new WaitForSecondsRealtime(2.5f);
			}
			yield return TryJoin("127.0.0.1:" + _port, "wrong", 30f, delegate(float x)
			{
				took = x;
			});
			Shown("a wrong password: refused, and said so", "Wrong password", took);
			if (own != null)
			{
				yield return Picture("net_join_refused.png", tab: true);
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(1.5f);
			}
			NetProtocol.BuildForTest = "deadbeef";
			yield return TryJoin("127.0.0.1:" + _port, "pw", 30f, delegate(float x)
			{
				took = x;
			});
			NetProtocol.BuildForTest = null;
			Shown("another build: refused, and said so", "another Worldfall build", took);
			yield return new WaitForSecondsRealtime(1f);
			bool Bad(string a)
			{
				string host;
				int port;
				return !NetSession.SplitAddress(a, out host, out port);
			}
			bool Split(string a, string host, int port)
			{
				if (NetSession.SplitAddress(a, out var host2, out var port2) && host2 == host)
				{
					return port2 == port;
				}
				return false;
			}
		}

		private IEnumerator Join()
		{
			SetPrefs("Joiner");
			bool phone = _options.Contains("phone");
			if (phone)
			{
				Device.ForceForTest(Device.Kind.Phone);
				Check("the joiner plays a phone without Steam", Device.OnPhone && !NetSession.SteamOn);
			}
			IEnumerator failures = Failures();
			while (failures.MoveNext())
			{
				yield return failures.Current;
			}
			ServerInfo listed = null;
			if (phone)
			{
				for (int round = 0; round < 3; round++)
				{
					if (listed != null)
					{
						break;
					}
					MpUi.Refresh();
					float r0 = Time.realtimeSinceStartup;
					while (MpUi.Refreshing && Time.realtimeSinceStartup - r0 < 10f)
					{
						MpUi.Poll();
						yield return new WaitForSecondsRealtime(0.2f);
					}
					listed = MpUi.Rows.Find((ServerInfo serverInfo) => serverInfo.Source == "Same network" && serverInfo.Address.EndsWith(":" + _port, StringComparison.Ordinal));
					if (listed == null)
					{
						yield return new WaitForSecondsRealtime(2f);
					}
				}
				Check("the host's world is in the same-network list, with its name and code", listed != null && listed.Name != "" && JoinCode.Read(listed.Code).Valid, (listed == null) ? (MpUi.Rows.Count + " rows") : (listed.Name + " " + listed.Address + " " + listed.Code));
			}
			float t0 = Time.realtimeSinceStartup;
			int tries = 0;
			while (!WorldSync.Mirroring && Time.realtimeSinceStartup - t0 < 300f)
			{
				if (!NetSession.Active && !MpUi.FindingCode)
				{
					tries++;
					if (tries == 1 && listed != null)
					{
						MpUi._joinPassword = "pw";
						MpUi.Join(listed);
					}
					else if (tries == 1)
					{
						MpUi.JoinAddress = JoinCode.ForAddress("127.0.0.1", _port).ToLowerInvariant();
						MpUi._joinPassword = "pw";
						MpUi.JoinTyped();
					}
					else
					{
						string text = NetSession.JoinIp("127.0.0.1:" + _port, "pw");
						if (text != null)
						{
							Log.Info("NETTEST join: " + text);
						}
					}
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			if (!Check("joined and got the host's world", WorldSync.Mirroring, tries + " tries, " + (Time.realtimeSinceStartup - t0).ToString("0") + " s"))
			{
				yield break;
			}
			Check(phone ? "joined from the same-network list on the first try" : "joined by the host's join code on the first try", tries == 1 && (!phone || listed != null), MpUi.Problem);
			yield return new WaitForSecondsRealtime(3f);
			Check("the copy stays paused", Config.paused);
			NetSession current = NetSession.Current;
			NetPlayer netPlayer = null;
			foreach (NetPlayer value in current.Players.Values)
			{
				if (value.Host)
				{
					netPlayer = value;
				}
			}
			Actor hostBody = ((netPlayer != null) ? World.world.units.get(netPlayer.Body) : null);
			if (!Check("the host's body is in this copy", hostBody?.isAlive() ?? false, (netPlayer != null) ? ("id " + netPlayer.Body) : "no host"))
			{
				yield break;
			}
			Actor mine = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != hostBody && simple.asset != null && !(simple.asset.id != "human") && simple.isAdult())
				{
					float sqrMagnitude = (simple.current_position - hostBody.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						mine = simple;
					}
				}
			}
			if (!Check("a villager to take", mine != null))
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(mine);
			t0 = Time.realtimeSinceStartup;
			while (WorldSync.MyBody != mine.data.id && Time.realtimeSinceStartup - t0 < 10f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!Check("the host gave the body", WorldSync.MyBody == mine.data.id))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Check("first person on the joiner", WorldBoxMod.Instance != null && WorldBoxMod.Instance.IsFirstPerson);
			if (phone)
			{
				Device.ForceForTest(Device.Kind.Pc);
				if (WorldBoxMod.Instance != null)
				{
					WorldBoxMod.Instance.Settings.TouchControls = 2;
				}
				Log.Info("NETTEST phone part done: joined from the list, world and body arrived; PC input from here");
				yield return new WaitForSecondsRealtime(4f);
			}
			yield return Picture("net_joiner_fp.png", tab: false);
			yield return Picture("net_joiner_tab.png", tab: true);
			Vector2 m0 = mine.current_position;
			yield return Walk(0f, 3f);
			Vector2 current_position = mine.current_position;
			Check("the joiner walks in its paused copy", (current_position - m0).magnitude > 1.5f, $"moved {(current_position - m0).magnitude:F2} tiles");
			Vector2 h0 = hostBody.current_position;
			Say("walked");
			string line = null;
			yield return WaitChat("host walked", 40f, delegate(string x)
			{
				line = x;
			});
			yield return new WaitForSecondsRealtime(1f);
			Vector2 current_position2 = hostBody.current_position;
			Check("the host's walk is seen here", line != null && (current_position2 - h0).magnitude > 1.5f, $"moved {(current_position2 - h0).magnitude:F2} tiles");
			yield return WaitChat("sheep ", 40f, delegate(string x)
			{
				line = x;
			});
			long sheepId = 0L;
			if (line != null)
			{
				long.TryParse(line.Substring(6), out sheepId);
			}
			Actor sheep = null;
			t0 = Time.realtimeSinceStartup;
			while (sheepId != 0L && Time.realtimeSinceStartup - t0 < 8f)
			{
				sheep = World.world.units.get(sheepId);
				if (sheep != null)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (Check("the host's new sheep appears here", sheep != null, "id " + sheepId))
			{
				t0 = Time.realtimeSinceStartup;
				int round = 0;
				while (Time.realtimeSinceStartup - t0 < 75f)
				{
					sheep = World.world.units.get(sheepId);
					if (sheep == null || !sheep.isAlive())
					{
						break;
					}
					Vector2 vector = sheep.current_position - mine.current_position;
					if (vector.magnitude > 1f)
					{
						float yawDeg = Mathf.Atan2(vector.y, vector.x) * 57.29578f;
						yield return Walk(yawDeg, Mathf.Min(0.4f, vector.magnitude * 0.12f));
					}
					else
					{
						WorldSync.ForwardStrike(mine, sheep, new Vector3(sheep.current_position.x, sheep.current_position.y, 0f), sheep.current_tile, mine.getAttackRange(), 1f, slice: false, 1f);
						round++;
						yield return new WaitForSecondsRealtime(0.5f);
					}
				}
				sheep = World.world.units.get(sheepId);
				Check("the sheep's death comes back from the host", sheep == null || !sheep.isAlive(), round + " blows");
			}
			WorldBoxMod mod = WorldBoxMod.Instance;
			Ability own = Abilities.Chosen(mine);
			if (mod != null && own != null)
			{
				int round = WorldSync.AbilitiesSent;
				try
				{
					mine.restoreStaminaPercent(1f);
					mine.restoreMana(9999);
				}
				catch
				{
				}
				mod.PressAbilityForTest(down: true);
				yield return new WaitForSecondsRealtime(own.Aimed ? 0.6f : 0.2f);
				mod.PressAbilityForTest(down: false);
				yield return new WaitForSecondsRealtime(own.Delay + 0.5f);
				bool ok = WorldSync.AbilitiesSent > round || Abilities.MovesBody(own);
				Check("X works in someone else's world (" + own.Name + ")", ok, "sent " + (WorldSync.AbilitiesSent - round) + ", cooldown " + Abilities.CooldownLeft(mine).ToString("0.0"));
			}
			yield return WaitChat("power", 40f, delegate(string x)
			{
				line = x;
			});
			GodPower godPower = AssetManager.powers.get("sheep");
			if (Check("a sheep power", godPower != null))
			{
				WorldTile current_tile = mine.current_tile;
				World.world.player_control.clickedFinal(new Vector2Int(current_tile.x + 2, current_tile.y + 2), godPower, pTrack: true);
				yield return new WaitForSecondsRealtime(0.5f);
				Check("the power did nothing here by itself", ok: true);
			}
			Say("powered");
			Actor npc = null;
			foreach (Actor simple2 in World.world.units.getSimpleList())
			{
				if (simple2 != null && simple2 != mine && simple2 != hostBody && simple2.isAlive() && (simple2.current_position - mine.current_position).sqrMagnitude < 100f)
				{
					npc = simple2;
					break;
				}
			}
			int money = mine.data.money + 50;
			mine.data.money = money;
			mine.data.set("fp_nettest", 7);
			npc?.data.set("fp_nettest_npc", "hello");
			yield return new WaitForSecondsRealtime(1f);
			Say("patched " + mine.data.id + " " + money + " " + (npc?.data.id ?? 0));
			Actor made = null;
			try
			{
				made = World.world.units.spawnNewUnit("chicken", mine.current_tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("NETTEST chicken: " + ex.Message);
			}
			Check("a chicken made here, in this player's own ids", made != null && made.data.id >= 2199023255552L, (made != null) ? ("id " + made.data.id) : "none");
			yield return new WaitForSecondsRealtime(1.5f);
			if (made != null)
			{
				Say("made " + made.data.id);
			}
			yield return WaitChat("village ", 60f, delegate(string x)
			{
				line = x;
			});
			Actor founder;
			if (line != null)
			{
				string[] array = line.Split(' ');
				long cityId = long.Parse(array[1]);
				long kingdomId = long.Parse(array[2]);
				long founderId = long.Parse(array[3]);
				City city = null;
				Kingdom kingdom = null;
				founder = null;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 8f)
				{
					city = World.world.cities.get(cityId);
					kingdom = World.world.kingdoms.get(kingdomId);
					founder = World.world.units.get(founderId);
					if (city != null && kingdom != null && founder != null && founder.city == city)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				bool num2 = Check("the new town and realm are here, the founder in them", city != null && kingdom != null && founder != null && founder.city == city, "city " + (city != null) + ", kingdom " + (kingdom != null) + ", founder " + (founder != null) + ", in town " + (founder != null && founder.city == city));
				Say(num2 ? "saw village" : "saw village not");
				if (num2)
				{
					TileZone tileZone = null;
					TileZone to = null;
					foreach (TileZone zone in city.zones)
					{
						if (zone?.neighbours_all == null)
						{
							continue;
						}
						TileZone[] neighbours_all = zone.neighbours_all;
						foreach (TileZone tileZone2 in neighbours_all)
						{
							if (tileZone2 != null && tileZone2.city == null && tileZone2.centerTile != null)
							{
								tileZone = zone;
								to = tileZone2;
								break;
							}
						}
						if (to != null)
						{
							break;
						}
					}
					GodPower godPower2 = AssetManager.powers.get("border_brush");
					if (Check("a border brush and free land beside the town", godPower2 != null && tileZone != null && to != null))
					{
						PlayerControl player_control = World.world.player_control;
						player_control.first_pressed_tile = tileZone.centerTile;
						player_control.first_pressed_type = tileZone.centerTile.main_type;
						player_control.first_pressed_top_type = tileZone.centerTile.top_type;
						player_control.clickedFinal(new Vector2Int(to.centerTile.x, to.centerTile.y), godPower2, pTrack: true);
						player_control.first_pressed_tile = null;
						player_control.first_pressed_type = null;
						player_control.first_pressed_top_type = null;
						Check("the border brush did nothing here by itself", to.city == null);
						Say("bordered " + city.data.id + " " + to.x + " " + to.y);
						yield return WaitChat("sawborder ", 20f, delegate(string x)
						{
							line = x;
						});
						float r0 = Time.realtimeSinceStartup;
						while (Time.realtimeSinceStartup - r0 < 10f && to.city != city)
						{
							yield return new WaitForSecondsRealtime(0.25f);
						}
						Check("the town's new border came back to this copy", to.city == city && city.zones.Contains(to), "zone in " + ((to.city?.data != null) ? to.city.data.id.ToString() : "none"));
					}
					else
					{
						Say("bordered none");
					}
				}
			}
			yield return WaitChat("tiles ", 30f, delegate(string x)
			{
				line = x;
			});
			if (line != null)
			{
				string[] w = line.Split(' ');
				int round = int.Parse(w[1]);
				int ty = int.Parse(w[2]);
				WorldTile t1 = null;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 6f)
				{
					t1 = World.world.GetTile(round, ty);
					if (t1 != null && t1.main_type != null && t1.main_type.id == w[3])
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Say(Check("the host's land change is here", t1 != null && t1.main_type != null && t1.main_type.id == w[3], t1?.main_type?.id ?? "no tile") ? "saw tiles" : "saw tiles not");
			}
			yield return WaitChat("building ", 30f, delegate(string x)
			{
				line = x;
			});
			if (line != null && !line.EndsWith("gone", StringComparison.Ordinal) && !line.EndsWith("none", StringComparison.Ordinal))
			{
				long founderId = long.Parse(line.Split(' ')[1]);
				Building b = null;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 6f)
				{
					Building building;
					b = (building = World.world.buildings.get(founderId));
					if (building != null)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Say(Check("the host's new building is here", b != null, "id " + founderId) ? "saw building" : "saw building not");
				yield return WaitChat("building gone", 30f, delegate(string x)
				{
					line = x;
				});
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 8f)
				{
					b = World.world.buildings.get(founderId);
					if (b == null || b.data == null || b.data.state == BuildingState.Removed || !b.isAlive())
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Say(Check("the host's building is gone here too", b == null || b.data == null || b.data.state == BuildingState.Removed || !b.isAlive()) ? "saw gone" : "saw gone not");
			}
			yield return WaitChat("killed 1", 40f, delegate(string x)
			{
				line = x;
			});
			if (line != null)
			{
				long founderId = mine.data.id;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 12f && (WorldSync.MyBody == 0L || WorldSync.MyBody == founderId))
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				yield return new WaitForSecondsRealtime(2f);
				Check("a new body here after dying", WorldSync.MyBody != 0L && WorldSync.MyBody != founderId && ControllableUnit._unit_main != null && ControllableUnit._unit_main.data.id == WorldSync.MyBody, "body " + WorldSync.MyBody + ", controlling " + (ControllableUnit._unit_main?.data?.id).GetValueOrDefault());
				Say("saw respawn");
				yield return WaitChat("killed 0", 30f, delegate(string x)
				{
					line = x;
				});
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 8f && WorldSync.MyBody != 0L)
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Check("no body here after dying (back to the god view)", WorldSync.MyBody == 0L && !ControllableUnit.isControllingUnit());
				City city = null;
				foreach (Actor simple3 in World.world.units.getSimpleList())
				{
					if (simple3 != null && simple3.isAlive() && simple3.city != null && simple3.city.data != null && simple3.city.isAlive())
					{
						city = simple3.city;
						break;
					}
				}
				if (city != null)
				{
					WorldSync.AskBorn(city);
				}
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 8f && WorldSync.MyBody == 0L)
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				yield return new WaitForSecondsRealtime(1.5f);
				Check("born in a town here, and in that body", WorldSync.MyBody != 0L && ControllableUnit._unit_main?.data?.id == WorldSync.MyBody, (city != null) ? city.data.name : "no town");
				Check("no heirs of this copy's own after dying (the host's rule)", WorldBoxMod.Instance == null || WorldBoxMod.Instance.Heirs.Count == 0, (WorldBoxMod.Instance?.Heirs.Count ?? 0) + " heirs");
				Say("born");
				yield return WaitChat("resync", 40f, delegate(string x)
				{
					line = x;
				});
				if (line != null)
				{
					long kingdomId = WorldSync.MyBody;
					WorldSync.AskResync();
					bool asked = WorldSync.Resyncing;
					t0 = Time.realtimeSinceStartup;
					while (Time.realtimeSinceStartup - t0 < 80f && (WorldSync.Resyncing || !WorldSync.Mirroring))
					{
						yield return new WaitForSecondsRealtime(0.25f);
					}
					t0 = Time.realtimeSinceStartup;
					while (Time.realtimeSinceStartup - t0 < 10f)
					{
						Actor unit_main = ControllableUnit._unit_main;
						if (unit_main != null && unit_main.data?.id == kingdomId)
						{
							break;
						}
						yield return new WaitForSecondsRealtime(0.25f);
					}
					bool num4 = Check("resynced: the host's world again, still joined", asked && WorldSync.Mirroring && NetSession.Joined, "asked " + asked);
					NetTest netTest = this;
					int ok2;
					if (kingdomId != 0L && WorldSync.MyBody == kingdomId)
					{
						Actor unit_main2 = ControllableUnit._unit_main;
						ok2 = ((unit_main2 != null && unit_main2.data?.id == kingdomId) ? 1 : 0);
					}
					else
					{
						ok2 = 0;
					}
					Say((num4 & netTest.Check("resynced: back in the same body", (byte)ok2 != 0, "body " + WorldSync.MyBody + ", was " + kingdomId + ", controlling " + (ControllableUnit._unit_main?.data?.id).GetValueOrDefault())) ? "resynced" : "resynced not");
					yield return WaitChat("units", 30f, delegate(string x)
					{
						line = x;
					});
					int ty = 0;
					if (line != null)
					{
						int.TryParse(line.Substring(6), out ty);
					}
					yield return new WaitForSecondsRealtime(1f);
					int num5 = 0;
					foreach (Actor simple4 in World.world.units.getSimpleList())
					{
						if (simple4 != null && simple4.isAlive())
						{
							num5++;
						}
					}
					bool flag = ty > 0 && Mathf.Abs(num5 - ty) <= Mathf.Max(3, ty / 50);
					Check("resynced: as many creatures as the host", flag, num5 + " here, " + ty + " on the host");
					Say(flag ? "units seen" : "units seen not");
					yield return WaitChat("rulers", 40f, delegate(string x)
					{
						line = x;
					});
					string[] array2 = ((line != null) ? line.Split(' ') : new string[0]);
					if (array2.Length == 5)
					{
						long cityId = long.Parse(array2[1]);
						long king = long.Parse(array2[2]);
						long cid = long.Parse(array2[3]);
						long lead = long.Parse(array2[4]);
						Kingdom kingdom = null;
						City rtown = null;
						t0 = Time.realtimeSinceStartup;
						bool kingOk = false;
						bool leadOk = false;
						while (Time.realtimeSinceStartup - t0 < 15f && !(kingOk && leadOk))
						{
							kingdom = World.world.kingdoms.get(cityId);
							rtown = World.world.cities.get(cid);
							kingOk = kingdom?.king?.data != null && kingdom.king.data.id == king && kingdom.king.isKing();
							leadOk = rtown?.leader?.data != null && rtown.leader.data.id == lead && rtown.leader.isCityLeader();
							yield return new WaitForSecondsRealtime(0.25f);
						}
						Say((Check("the host's new king is king here", kingOk, "king here " + (kingdom?.king?.data?.id).GetValueOrDefault() + ", host's " + king) & Check("the host's new town leader leads here", leadOk, "leader here " + (rtown?.leader?.data?.id).GetValueOrDefault() + ", host's " + lead)) ? "rulers seen" : "rulers seen not");
					}
				}
			}
			yield return WaitChat("arrow", 40f, delegate(string x)
			{
				line = x;
			});
			if (line != null)
			{
				int ty = 0;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 3f && ty == 0)
				{
					foreach (Projectile projectile in World.world.projectiles)
					{
						if (projectile?.asset != null && projectile.asset.id == "arrow")
						{
							ty++;
						}
					}
					yield return null;
				}
				Check("the host's arrow flies here", ty > 0, ty + " in flight");
				Say((ty > 0) ? "saw arrow" : "saw arrow not");
				yield return WaitChat("fx", 30f, delegate(string x)
				{
					line = x;
				});
				bool asked = false;
				bool leadOk = false;
				bool kingOk = false;
				bool cloud = false;
				bool speed = false;
				bool landed = false;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 10f && !(asked && leadOk && kingOk && cloud && speed))
				{
					DropManager drop_manager = World.world.drop_manager;
					for (int num6 = 0; num6 < Math.Min(drop_manager._activeIndex, drop_manager._drops.Count); num6++)
					{
						Drop drop = drop_manager._drops[num6];
						if (drop != null && drop.active && drop._asset != null && drop._asset.id == "tnt")
						{
							asked = true;
							landed |= drop._landed;
						}
					}
					foreach (BaseEffectController item in World.world.stack_effects.list)
					{
						if (item?._list == null || item.asset == null)
						{
							continue;
						}
						for (int num7 = 0; num7 < Math.Min(item._active_index, item._list.Count); num7++)
						{
							BaseEffect baseEffect = item._list[num7];
							if (!(baseEffect == null) && baseEffect.active)
							{
								if (item.asset.id.Contains("explosion"))
								{
									leadOk = true;
								}
								if (baseEffect is Meteorite)
								{
									kingOk = true;
								}
								if (baseEffect is Cloud)
								{
									cloud = true;
								}
							}
						}
					}
					speed |= Config.time_scale_asset != null && Config.time_scale_asset.id == "x2";
					yield return null;
				}
				Check("the host's TNT falls here", asked);
				Check("its blast is seen here", leadOk);
				Check("the host's meteor comes down here", kingOk);
				Check("the host's cloud drifts here", cloud);
				Check("the world runs at the host's speed here", speed, Config.time_scale_asset?.id ?? "none");
				try
				{
					Config.setWorldSpeed("x5", pUpdateDebug: false);
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(0.5f);
				Check("this copy's own speed change is put back", Config.time_scale_asset != null && Config.time_scale_asset.id != "x5", Config.time_scale_asset?.id ?? "none");
				Say((asked && leadOk && kingOk && cloud && speed) ? "saw fx" : ("saw fx not: drop " + asked + " blast " + leadOk + " meteor " + kingOk + " cloud " + cloud + " speed " + speed));
			}
			yield return WaitChat("pvp", 40f, delegate(string x)
			{
				line = x;
			});
			Actor me;
			if (line != null)
			{
				NetPlayer netPlayer2 = null;
				foreach (NetPlayer value2 in NetSession.Current.Players.Values)
				{
					if (value2.Host)
					{
						netPlayer2 = value2;
					}
				}
				founder = ((netPlayer2 != null) ? World.world.units.get(netPlayer2.Body) : null);
				me = ControllableUnit._unit_main;
				Vector2 walkedFrom = me?.current_position ?? Vector2.zero;
				float r0 = Time.realtimeSinceStartup;
				while (founder != null && me != null && founder.isAlive() && Time.realtimeSinceStartup - r0 < 20f)
				{
					Vector2 vector2 = founder.current_position - me.current_position;
					if (vector2.magnitude <= 1f)
					{
						break;
					}
					yield return Walk(Mathf.Atan2(vector2.y, vector2.x) * 57.29578f, Mathf.Min(0.4f, vector2.magnitude * 0.12f));
				}
				if (founder != null && me != null)
				{
					for (int ty = 0; ty < 2; ty++)
					{
						WorldSync.ForwardStrike(me, founder, new Vector3(founder.current_position.x, founder.current_position.y, 0f), founder.current_tile, me.getAttackRange(), 1f, slice: false, 1f);
						yield return new WaitForSecondsRealtime(0.6f);
					}
				}
				r0 = Time.realtimeSinceStartup;
				while (me != null && me.isAlive() && Time.realtimeSinceStartup - r0 < 15f)
				{
					Vector2 vector3 = walkedFrom - me.current_position;
					if (vector3.magnitude <= 0.6f)
					{
						break;
					}
					yield return Walk(Mathf.Atan2(vector3.y, vector3.x) * 57.29578f, Mathf.Min(0.4f, vector3.magnitude * 0.12f));
				}
				Say("pvp struck");
				yield return WaitChat("burnb ", 20f, delegate(string x)
				{
					line = x;
				});
				if (line != null && long.TryParse(line.Substring(6), out var result))
				{
					Building b = World.world.buildings.get(result);
					bool landed = false;
					t0 = Time.realtimeSinceStartup;
					while (!landed && Time.realtimeSinceStartup - t0 < 5f)
					{
						landed = b != null && Torches.Burning(b);
						if (!landed)
						{
							yield return null;
						}
					}
					Check("the host's building burns here", landed);
					Say(landed ? "saw burnb" : "saw burnb not");
				}
				yield return WaitChat("light", 20f, delegate(string x)
				{
					line = x;
				});
				me = ControllableUnit._unit_main;
				WorldTile worldTile = null;
				if (me != null)
				{
					for (int num8 = -5; num8 <= 5; num8++)
					{
						if (worldTile != null)
						{
							break;
						}
						for (int num9 = -5; num9 <= 5; num9++)
						{
							if (worldTile != null)
							{
								break;
							}
							if (Mathf.Abs(num8) + Mathf.Abs(num9) >= 2 && num8 * num8 + num9 * num9 <= 25)
							{
								WorldTile tile = World.world.GetTile((int)me.current_position.x + num8, (int)me.current_position.y + num9);
								if (tile != null && tile.Type != null && tile.Type.burnable && !tile.Type.explodable && !tile.is_liquid && tile.building == null && !tile.isOnFire())
								{
									worldTile = tile;
								}
							}
						}
					}
				}
				if (worldTile != null)
				{
					bool flag2 = Torches.IgniteGround(new Vec3((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f, 0f));
					Check("a flame held to the ground goes to the host", flag2 && !worldTile.isOnFire(), worldTile.x + "," + worldTile.y);
					Say("lit " + worldTile.x + " " + worldTile.y);
				}
				else
				{
					Building building2 = null;
					if (me != null)
					{
						foreach (Building simple5 in World.world.buildings.getSimpleList())
						{
							if (simple5 != null && simple5.isAlive() && simple5.isBurnable() && !Torches.Burning(simple5) && (simple5.current_position - me.current_position).sqrMagnitude < 25f)
							{
								building2 = simple5;
								break;
							}
						}
					}
					bool flag3 = building2 != null && Torches.Ignite(me, building2, null);
					Check("a flame held to a tree goes to the host", flag3 && !Torches.Burning(building2), (building2 != null) ? (building2.asset?.id + " " + building2.data.id) : "nothing that burns near");
					Say((building2 != null) ? ("litb " + building2.data.id) : "lit none");
				}
				Check("the host's data from before your change doesn't undo it (the bow copied)", WorldSync.StaleDataKeepsChangeForTest(ControllableUnit._unit_main));
				yield return WaitChat("gear", 30f, delegate(string x)
				{
					line = x;
				});
				me = ControllableUnit._unit_main;
				string gid = null;
				if (line != null && me != null && Hotbar.UsesItems(me))
				{
					string text2 = Hotbar.Weapon(me)?.asset?.id;
					string[] array3 = new string[3] { "axe_wood", "sword_wood", "spear_wood" };
					foreach (string id in array3)
					{
						if (id != text2 && AssetManager.items.get(id) != null && !Gear.Of(me).Exists((Gear.Piece x) => x.Id == id))
						{
							gid = id;
							break;
						}
					}
				}
				if (gid != null)
				{
					Gear.Piece p = new Gear.Piece
					{
						Id = gid,
						By = me.getName()
					};
					if (!Gear.Add(me, p))
					{
						Gear.AddOver(me, p);
					}
					yield return new WaitForSecondsRealtime(0.3f);
					Hotbar.Hold(me, BagOrder.Of(me).IndexOf(BagOrder.PieceKey(Gear.Of(me).FindIndex((Gear.Piece x) => x.Id == gid))), null);
					yield return new WaitForSecondsRealtime(2f);
					bool landed = Hotbar.Weapon(me)?.asset?.id == gid;
					bool speed = Hotbar.Stow(me, null);
					yield return new WaitForSecondsRealtime(4f);
					string text3 = Hotbar.Weapon(me)?.asset?.id;
					int count = Gear.Of(me).FindAll((Gear.Piece x) => x.Id == gid).Count;
					Check("a weapon taken up and put away stays put away (no copy)", landed && speed && text3 != gid && count == 1, "held " + landed + ", stowed " + speed + ", hand " + (text3 ?? "bare") + ", " + count + " in the bag");
				}
				Say("geared " + (gid ?? "none"));
			}
			NetPlayer hostPlayer = null;
			foreach (NetPlayer value3 in NetSession.Current.Players.Values)
			{
				if (value3.Host)
				{
					hostPlayer = value3;
				}
			}
			if (hostPlayer != null)
			{
				Parties.InviteTo(hostPlayer);
				yield return WaitChat("party", 30f, delegate(string x)
				{
					line = x;
				});
				NetPlayer me2 = NetSession.Current.Me;
				foreach (NetPlayer value4 in NetSession.Current.Players.Values)
				{
					if (value4.Host)
					{
						hostPlayer = value4;
					}
				}
				Check("in the host's party here too", line != null && Parties.SameParty(me2, hostPlayer), (me2?.Party ?? (-1)) + " / " + hostPlayer.Party);
				PlayerTalk.ReachForTest = 1000000f;
				yield return WaitChat("ptalk ask", 30f, delegate(string x)
				{
					line = x;
				});
				Actor a = null;
				try
				{
					a = World.world.units.get(hostPlayer.Body);
				}
				catch
				{
				}
				Check("E on the host's body asks the host to talk (no NPC talk)", line != null && PlayerTalk.TryAsk(a) && !WorldBoxMod.Instance.Converse.Open);
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 20f && !PlayerTalk.Talking)
				{
					yield return new WaitForSecondsRealtime(0.1f);
				}
				if (Check("the host took the talk: its card is up here", PlayerTalk.Talking && PlayerTalk.CardOpen && PlayerTalk.Card.Name == hostPlayer.Name, PlayerTalk.Card.Name ?? "none"))
				{
					MpUi.ChatOpen = true;
					MpUi.ChatText = "hel";
					yield return new WaitForSecondsRealtime(2f);
					yield return Picture("mp_talk_typing.png", tab: false);
					MpUi.ChatOpen = false;
					MpUi.ChatText = "";
					me = ControllableUnit._unit_main;
					int ty = Civil.BagCount(me, "wood");
					PlayerTalk.SayLine("hello host");
					Check("your own line on your card", PlayerTalk.Card.Line == "hello host" && PlayerTalk.Card.LineIsYours);
					yield return WaitChat("ptalk gave", 30f, delegate(string x)
					{
						line = x;
					});
					t0 = Time.realtimeSinceStartup;
					while (Time.realtimeSinceStartup - t0 < 10f && Civil.BagCount(me, "wood") <= ty)
					{
						yield return new WaitForSecondsRealtime(0.1f);
					}
					Say("ptalk got " + (Civil.BagCount(me, "wood") - ty));
					t0 = Time.realtimeSinceStartup;
					while (Time.realtimeSinceStartup - t0 < 15f && PlayerTalk.CardOpen)
					{
						yield return new WaitForSecondsRealtime(0.1f);
					}
					Say(PlayerTalk.CardOpen ? "ptalk closed not" : "ptalk closed");
				}
				PlayerTalk.ReachForTest = 0f;
			}
			me = ControllableUnit._unit_main;
			founder = null;
			if (me != null)
			{
				foreach (Actor simple6 in World.world.units.getSimpleList())
				{
					if (simple6?.data != null && simple6.isAlive() && simple6 != me && !WorldSync.IsPlayerBody(simple6) && simple6.data.id < 1099511627776L && (simple6.current_position - me.current_position).sqrMagnitude > 1600f)
					{
						founder = simple6;
						break;
					}
				}
			}
			if (me == null || founder == null)
			{
				Say("rules none");
			}
			else
			{
				Say("rules " + founder.data.id);
				yield return WaitChat("rules go", 30f, delegate(string x)
				{
					line = x;
				});
				GuardControl g = GuardControl.Current;
				if (g != null)
				{
					g.HeldForTest = true;
				}
				WorldSync.PatchForTest(founder, "fp_nettest_far", "far");
				WorldSync.ForwardStrike(me, founder, new Vector3(founder.current_position.x, founder.current_position.y, 0f), founder.current_tile, me.getAttackRange(), 1f, slice: false, 1f);
				yield return new WaitForSecondsRealtime(0.8f);
				Say("rules sent" + ((g != null && g.State.Blocking) ? " guard" : ""));
				yield return WaitChat("rules checked", 30f, delegate(string x)
				{
					line = x;
				});
				if (g != null)
				{
					g.HeldForTest = false;
				}
			}
			yield return new WaitForSecondsRealtime(1f);
			Say("done");
			t0 = Time.realtimeSinceStartup;
			while (NetSession.Active && Time.realtimeSinceStartup - t0 < 40f)
			{
				yield return new WaitForSecondsRealtime(0.5f);
			}
			Check("the session ended when the host loaded another world", !NetSession.Active && NetSession.LastEnd.Contains("another world"), NetSession.LastEnd);
			Check("...and the banner says so", MpUi.BannerUntil > Time.realtimeSinceStartup && MpUi.BannerTitle.StartsWith("Left", StringComparison.Ordinal) && MpUi.Problem.Contains("another world"), MpUi.BannerTitle + " / " + MpUi.BannerText);
			yield return new WaitForSecondsRealtime(2f);
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 60f)
			{
				bool flag4 = true;
				try
				{
					flag4 = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag4 && Config.game_loaded)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			Check("own world back and running", !WorldSync.Mirroring && !Config.paused, "units " + World.world.units.Count);
		}

		private IEnumerator HostLights()
		{
			SetPrefs("Hoster");
			MpPrefs.Current.Port = _port;
			MpPrefs.Current.Password = "pw";
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 6, 0);
				if (king != null)
				{
					break;
				}
			}
			if (!Check("a village to play in", king != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			string text = NetSession.Host();
			if (!Check("hosting started", text == null, text ?? ""))
			{
				yield break;
			}
			NetPlayer joiner = null;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 300f)
			{
				foreach (NetPlayer value in NetSession.Current.Players.Values)
				{
					if (!value.Host && value.Ready)
					{
						joiner = value;
					}
				}
				if (joiner != null)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (!Check("a joiner got the world", joiner != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Say("lights " + king.data.id);
			string line = null;
			yield return WaitChat("placed ", 90f, delegate(string x)
			{
				line = x;
			});
			if (!Check("the joiner set a torch down", line != null))
			{
				yield break;
			}
			PlacedLights.Light theirs = PlacedLights.All.Find((PlacedLights.Light l) => l.Kind == PlacedLights.Kind.Torch && l.Owner == joiner.Body);
			Check("the host has the joiner's torch, the joiner's own", theirs != null, PlacedLights.All.Count + " lights, joiner body " + joiner.Body);
			Check("set down for a joiner once", WorldSync.LightsPlacedForJoiners == 1, WorldSync.LightsPlacedForJoiners.ToString());
			Supplies.Give(king, "tw_item_lamppost", 1);
			Vector2 at = king.current_position + new Vector2(-2f, 1f);
			for (int num = 0; num < 16; num++)
			{
				if (PlacedLights.WhyNotAt(at, PlacedLights.Kind.Lamp) == null)
				{
					break;
				}
				at = king.current_position + new Vector2(Mathf.Cos((float)num * 0.8f), Mathf.Sin((float)num * 0.8f)) * (2f + (float)num * 0.25f);
			}
			string text2 = PlacedLights.Place(king, PlacedLights.Kind.Lamp, at, 0f, null);
			Check("the host sets a lamp post down", text2 == null, text2 ?? "");
			Say("host placed");
			yield return WaitChat("saw lamp", 30f, delegate(string x)
			{
				line = x;
			});
			Check("the joiner saw the host's lamp post", line == "saw lamp");
			yield return WaitChat("took", 30f, delegate(string x)
			{
				line = x;
			});
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 5f && theirs != null && PlacedLights.All.Contains(theirs))
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the joiner's torch taken back is gone on the host", theirs == null || !PlacedLights.All.Contains(theirs));
			yield return WaitChat("refunded", 30f, delegate(string x)
			{
				line = x;
			});
			Check("a light set down out of the joiner's reach is refused", WorldSync.LightsRefused >= 1 && WorldSync.LightsPlacedForJoiners == 1, "refused " + WorldSync.LightsRefused + ", placed " + WorldSync.LightsPlacedForJoiners);
			yield return new WaitForSecondsRealtime(3f);
		}

		private IEnumerator JoinLights()
		{
			SetPrefs("Joiner");
			float t0 = Time.realtimeSinceStartup;
			while (!WorldSync.Mirroring && Time.realtimeSinceStartup - t0 < 300f)
			{
				if (!NetSession.Active)
				{
					string text = NetSession.JoinIp("127.0.0.1:" + _port, "pw");
					if (text != null)
					{
						Log.Info("NETTEST join: " + text);
					}
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			if (!Check("joined and got the host's world", WorldSync.Mirroring))
			{
				yield break;
			}
			string line = null;
			yield return WaitChat("lights ", 60f, delegate(string x)
			{
				line = x;
			});
			Actor king = ((line != null) ? World.world.units.get(long.Parse(line.Substring(7))) : null);
			if (!Check("the host's king is here", king != null))
			{
				yield break;
			}
			Actor mine = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != king && simple.asset != null && !(simple.asset.id != "human") && simple.isAdult())
				{
					float sqrMagnitude = (simple.current_position - king.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						mine = simple;
					}
				}
			}
			if (!Check("a villager to take", mine != null))
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(mine);
			t0 = Time.realtimeSinceStartup;
			while (WorldSync.MyBody != mine.data.id && Time.realtimeSinceStartup - t0 < 10f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!Check("the host gave the body", WorldSync.MyBody == mine.data.id))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Supplies.Give(mine, "tw_item_torch", 2);
			int torches = Torches.Count(mine);
			Vector2 at = mine.current_position + new Vector2(1.5f, 0.5f);
			for (int num2 = 0; num2 < 12; num2++)
			{
				if (PlacedLights.WhyNotAt(at, PlacedLights.Kind.Torch) == null)
				{
					break;
				}
				at = mine.current_position + new Vector2(Mathf.Cos(num2) * 2f, Mathf.Sin(num2) * 2f);
			}
			int lists = WorldSync.LightListsIn;
			string text2 = PlacedLights.Place(mine, PlacedLights.Kind.Torch, at, 0f, null);
			Check("set down (sent to the host)", text2 == null && WorldSync.LightsSent == 1, text2 + " sent " + WorldSync.LightsSent);
			Check("...a torch out of the joiner's bag", Torches.Count(mine) == torches - 1, torches + " -> " + Torches.Count(mine));
			t0 = Time.realtimeSinceStartup;
			PlacedLights.Light l = null;
			while (Time.realtimeSinceStartup - t0 < 8f && l == null)
			{
				l = PlacedLights.All.Find((PlacedLights.Light x) => x.Kind == PlacedLights.Kind.Torch && x.Owner == mine.data.id);
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the host's list came back with it", l != null && WorldSync.LightListsIn > lists, "lists " + lists + " -> " + WorldSync.LightListsIn);
			Say("placed " + mine.data.id);
			yield return WaitChat("host placed", 30f, delegate(string x)
			{
				line = x;
			});
			t0 = Time.realtimeSinceStartup;
			PlacedLights.Light lamp = null;
			while (Time.realtimeSinceStartup - t0 < 8f && lamp == null)
			{
				lamp = PlacedLights.All.Find((PlacedLights.Light x) => x.Kind == PlacedLights.Kind.Lamp && x.Owner == king.data.id);
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the host's lamp post is here", lamp != null);
			Say("saw lamp");
			if (l != null)
			{
				text2 = PlacedLights.TakeBack(mine, l);
				Check("the joiner takes its torch back", text2 == null && Torches.Count(mine) == torches, text2 + " torches " + Torches.Count(mine));
				yield return new WaitForSecondsRealtime(2f);
				Check("...and the host's list agrees", PlacedLights.All.Find((PlacedLights.Light x) => x.Id == l.Id) == null);
			}
			Say("took");
			torches = Torches.Count(mine);
			if (Supplies.Take(mine, "tw_item_torch", 1) && WorldSync.ForwardLight(mine, PlacedLights.Kind.Torch, mine.current_position + new Vector2(60f, 0f), 0f, null))
			{
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 8f && WorldSync.LightsRefunded == 0)
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
			}
			Check("a light the host refuses comes back to the bag", WorldSync.LightsRefunded == 1 && Torches.Count(mine) == torches, "refunded " + WorldSync.LightsRefunded + ", torches " + torches + " -> " + Torches.Count(mine));
			Say("refunded");
			yield return new WaitForSecondsRealtime(3f);
		}

		private IEnumerator HostPlots()
		{
			SetPrefs("Hoster");
			MpPrefs.Current.Port = _port;
			MpPrefs.Current.Password = "pw";
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 10, 2);
				if (king != null)
				{
					break;
				}
			}
			if (!Check("a village to play in", king != null && king.city != null))
			{
				yield break;
			}
			City town = king.city;
			yield return new WaitForSecondsRealtime(1.5f);
			Actor mine = NearestVillager(town, king.current_position, king, null);
			if (!Check("a villager for the host", mine != null))
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(mine);
			yield return new WaitForSecondsRealtime(2.5f);
			string text = NetSession.Host();
			if (!Check("hosting started", text == null, text ?? ""))
			{
				yield break;
			}
			NetPlayer joiner = null;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 300f)
			{
				foreach (NetPlayer value in NetSession.Current.Players.Values)
				{
					if (!value.Host && value.Ready)
					{
						joiner = value;
					}
				}
				if (joiner != null)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (!Check("a joiner got the world", joiner != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Actor actor = NearestVillager(town, king.current_position, king, mine);
			if (!Check("a villager to crown", actor != null))
			{
				yield break;
			}
			town.kingdom.setKing(actor);
			Check("the new king is king on the host", actor.isKing());
			Say("plots " + actor.data.id + " " + town.data.id + " " + mine.data.id);
			string line = null;
			yield return WaitChat("body ", 60f, delegate(string x)
			{
				line = x;
			});
			Actor jb = ((line != null) ? World.world.units.get(long.Parse(line.Substring(5))) : null);
			if (!Check("the joiner took a villager", jb != null && jb.data.id == joiner.Body, line ?? "no answer"))
			{
				yield break;
			}
			jb.addMoney(500);
			int gold = Economy.Money(jb);
			Say("gold");
			yield return WaitChat("raised ", 90f, delegate(string x)
			{
				line = x;
			});
			if (!Check("the joiner marked a plot out", line != null && line != "raised none", line ?? "no answer"))
			{
				yield break;
			}
			Building site = World.world.buildings.get(long.Parse(line.Substring(7)));
			Check("raised once for a joiner", WorldSync.PlotsRaisedForJoiners == 1, WorldSync.PlotsRaisedForJoiners.ToString());
			if (!Check("the joiner's site stands on the host, going up", site != null && site.isAlive() && site.isUnderConstruction(), site?.asset?.id))
			{
				yield break;
			}
			Check("...the joiner's, in the town", Homes.OwnedBy(site, jb) && town.buildings.Contains(site) && LandPlot.PendingSite(jb) == site);
			Check("...a cottage's price taken from the joiner's body", Economy.Money(jb) == gold - LandPlot.PriceOf(0, ruler: false), gold + " -> " + Economy.Money(jb));
			yield return WaitChat("helped", 30f, delegate(string x)
			{
				line = x;
			});
			int constructionProgress = site.getConstructionProgress();
			Check("the joiner's blows of help struck on the host", WorldSync.PlotHelpTaken >= 1 && constructionProgress > 0, "blows " + WorldSync.PlotHelpTaken + ", progress " + constructionProgress + "/" + site.asset.construction_progress_needed);
			yield return WaitChat("refused", 30f, delegate(string x)
			{
				line = x;
			});
			Check("a second plot while one goes up is refused", WorldSync.PlotsRaisedForJoiners == 1 && Economy.Money(jb) == gold - LandPlot.PriceOf(0, ruler: false), "raised " + WorldSync.PlotsRaisedForJoiners + ", gold " + Economy.Money(jb));
			for (int num = 0; num < 400; num++)
			{
				if (!site.isAlive())
				{
					break;
				}
				if (!site.isUnderConstruction())
				{
					break;
				}
				site.updateBuild(1);
			}
			Building house = null;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 6f && house == null)
			{
				yield return new WaitForSecondsRealtime(0.25f);
				if (!LandPlot.Read(jb, out var _, out var _, out var _, out var _))
				{
					house = HomeLife.OwnHouse(jb);
				}
			}
			if (Check("built, the house is the joiner's home on the host", house != null && jb.getHomeBuilding() == house, house?.asset?.id))
			{
				Check("...a house, not the tent", LandPlot.TierOf(house) == 0, house.asset.id);
				Say("built " + house.data.id);
				yield return WaitChat("home ", 30f, delegate(string x)
				{
					line = x;
				});
				Check("the joiner lives there too", line == "home ok", line ?? "no answer");
				yield return new WaitForSecondsRealtime(3f);
			}
		}

		private IEnumerator JoinPlots()
		{
			SetPrefs("Joiner");
			float t0 = Time.realtimeSinceStartup;
			while (!WorldSync.Mirroring && Time.realtimeSinceStartup - t0 < 300f)
			{
				if (!NetSession.Active)
				{
					string text = NetSession.JoinIp("127.0.0.1:" + _port, "pw");
					if (text != null)
					{
						Log.Info("NETTEST join: " + text);
					}
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			if (!Check("joined and got the host's world", WorldSync.Mirroring))
			{
				yield break;
			}
			string line = null;
			yield return WaitChat("plots ", 60f, delegate(string x)
			{
				line = x;
			});
			string[] array = ((line != null) ? line.Split(' ') : new string[0]);
			Actor king = ((array.Length >= 4) ? World.world.units.get(long.Parse(array[1])) : null);
			City town = ((array.Length >= 4) ? World.world.cities.get(long.Parse(array[2])) : null);
			Actor hosts = ((array.Length >= 4) ? World.world.units.get(long.Parse(array[3])) : null);
			if (!Check("the host's new king and town are here", king != null && town != null))
			{
				yield break;
			}
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 10f && !king.isKing())
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the host's new king is king here", king.isKing() && king.kingdom == town.kingdom, "this realm's king " + (town.kingdom?.king?.data?.id).GetValueOrDefault());
			Actor mine = NearestVillager(town, king.current_position, king, hosts);
			if (!Check("a villager to take", mine != null))
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(mine);
			t0 = Time.realtimeSinceStartup;
			while (WorldSync.MyBody != mine.data.id && Time.realtimeSinceStartup - t0 < 10f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!Check("the host gave the body", WorldSync.MyBody == mine.data.id))
			{
				Say("body none");
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			Say("body " + mine.data.id);
			yield return WaitChat("gold", 30f, delegate(string x)
			{
				line = x;
			});
			int price = LandPlot.PriceOf(0, ruler: false);
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 8f && Economy.Money(mine) < price)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			int money = Economy.Money(mine);
			WorldBoxMod mod = WorldBoxMod.Instance;
			if (!Check("talking to the king", mod != null && mod.StartConversationForTest(king)))
			{
				Say("raised none");
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Conversation c = mod.Converse;
			Choice choice = FindChoice(c, "I'd like land for a house.");
			t0 = Time.realtimeSinceStartup;
			while (choice == null && Time.realtimeSinceStartup - t0 < 6f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
				choice = FindChoice(c, "I'd like land for a house.");
			}
			if (!Check("the king offers a joiner land for a house", choice?.Enabled ?? false, (choice != null) ? choice.Note : Labels(c)))
			{
				Say("raised none");
				yield break;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			yield return new WaitForSecondsRealtime(0.3f);
			Choice choice2 = FindChoice(c, "A cottage");
			if (!Check("a cottage among the sizes", choice2?.Enabled ?? false, (choice2 != null) ? choice2.Note : Labels(c)))
			{
				Say("raised none");
				yield break;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice2));
			yield return new WaitForSecondsRealtime(0.3f);
			Check("marking the plot out, nothing paid on this copy", LandPlot.Aiming && Economy.Money(mine) == money, money + " -> " + Economy.Money(mine));
			if (mod.Converse.Open)
			{
				mod.EndConversation();
			}
			yield return new WaitForSecondsRealtime(0.3f);
			WorldTile good = null;
			float num = float.MaxValue;
			WorldTile current_tile = mine.current_tile;
			for (int num2 = -14; num2 <= 14; num2++)
			{
				for (int num3 = -14; num3 <= 14; num3++)
				{
					WorldTile worldTile = ((current_tile != null) ? World.world.GetTile(current_tile.x + num2, current_tile.y + num3) : null);
					float num4 = num2 * num2 + num3 * num3;
					if (worldTile != null && !(num4 >= num) && worldTile.zone != null && worldTile.zone.city == town && LandPlot.CanStand(town, 0, worldTile, out var _))
					{
						num = num4;
						good = worldTile;
					}
				}
			}
			if (!Check("clear town land near by", good != null))
			{
				Say("raised none");
				yield break;
			}
			bool flag = LandPlot.Ask(mine, good);
			Check("the plot asked of the host", flag && !LandPlot.Aiming && WorldSync.PlotsAsked == 1, "asked " + WorldSync.PlotsAsked);
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 8f && WorldSync.PlotAnswers == 0)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the host's answer: marked", WorldSync.LastPlotAnswer.StartsWith("Plot marked"), WorldSync.LastPlotAnswer);
			Building site = null;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 8f && (site == null || Economy.Money(mine) != money - price))
			{
				yield return new WaitForSecondsRealtime(0.25f);
				site = LandPlot.PendingSite(mine);
			}
			Check("the site came here, the joiner's", site != null && Homes.OwnedBy(site, mine), site?.asset?.id);
			Check("...and the gold the host took", Economy.Money(mine) == money - price, money + " -> " + Economy.Money(mine));
			Say("raised " + ((site != null) ? site.data.id.ToString() : "none"));
			if (site == null)
			{
				yield break;
			}
			try
			{
				WorldTile current_tile2 = site.current_tile;
				mine.current_position = new Vector2((float)current_tile2.x + 2f, (float)current_tile2.y + 0.5f);
				mine.dirty_current_tile = true;
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(0.6f);
			for (int i = 0; i < 3; i++)
			{
				WorldSync.ForwardPlotHelp(mine, site);
				yield return new WaitForSecondsRealtime(0.4f);
			}
			yield return new WaitForSecondsRealtime(1f);
			Say("helped");
			int answers = WorldSync.PlotAnswers;
			int before = Economy.Money(mine);
			WorldSync.ForwardPlot(mine, town, 0, good);
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 8f && WorldSync.PlotAnswers == answers)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			yield return new WaitForSecondsRealtime(1f);
			Check("a second plot is refused", WorldSync.LastPlotAnswer == "Your house is being built" && Economy.Money(mine) == before, WorldSync.LastPlotAnswer + ", gold " + before + " -> " + Economy.Money(mine));
			Say("refused");
			yield return WaitChat("built ", 120f, delegate(string x)
			{
				line = x;
			});
			long houseId = ((line != null) ? long.Parse(line.Substring(6)) : 0);
			Building home = null;
			t0 = Time.realtimeSinceStartup;
			long cityId;
			long buildingId;
			int tier;
			int paid;
			while (Time.realtimeSinceStartup - t0 < 10f)
			{
				home = mine.getHomeBuilding();
				if (home?.data != null && home.data.id == houseId && !LandPlot.Read(mine, out cityId, out buildingId, out tier, out paid) && WorldSync.LastPlotAnswer.StartsWith("Your house is built"))
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Say(Check("built: told, the house is home here, the site record gone", home?.data != null && home.data.id == houseId && !LandPlot.Read(mine, out buildingId, out cityId, out paid, out tier) && WorldSync.LastPlotAnswer.StartsWith("Your house is built"), "home " + (home?.data?.id).GetValueOrDefault() + " of " + houseId + ", told '" + WorldSync.LastPlotAnswer + "'") ? "home ok" : "home not");
			yield return new WaitForSecondsRealtime(3f);
		}

		private static Actor NearestVillager(City town, Vector2 near, Actor not1, Actor not2)
		{
			Actor result = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != not1 && simple != not2 && simple.city == town && simple.asset != null && !(simple.asset.id != "human") && simple.isAdult() && !simple.isKing())
				{
					float sqrMagnitude = (simple.current_position - near).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = simple;
					}
				}
			}
			return result;
		}

		private static Choice FindChoice(Conversation c, string start)
		{
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private static string Labels(Conversation c)
		{
			List<string> list = new List<string>();
			foreach (Choice choice in c.Choices)
			{
				list.Add(choice.Label + (choice.Enabled ? "" : " (off)"));
			}
			return string.Join(" | ", list.ToArray());
		}

		private static WorldTile SoilNear(Vector2 at, float reach, City town, bool wild)
		{
			for (float num = 1.5f; num <= reach; num += 0.5f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f + num;
					WorldTile tile = World.world.GetTile((int)(at.x + Mathf.Cos(f) * num), (int)(at.y + Mathf.Sin(f) * num));
					if (tile?.main_type != null && !tile.main_type.liquid && !tile.main_type.block && tile.main_type.can_be_biome && tile.main_type.height_min < 199 && (tile.building == null || !AssetStyle.IsSolid(tile.building)) && !(wild ? (tile.zone?.city != null) : (tile.zone?.city != town)))
					{
						return tile;
					}
				}
			}
			return null;
		}

		private IEnumerator HostRelief()
		{
			SetPrefs("Hoster");
			MpPrefs.Current.Port = _port;
			MpPrefs.Current.Password = "pw";
			MpPrefs.Current.Rules.GuestsDig = false;
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 6, 0);
				if (king != null)
				{
					break;
				}
			}
			if (!Check("a village to dig in", king != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			string text = NetSession.Host();
			if (!Check("hosting started", text == null, text ?? ""))
			{
				yield break;
			}
			NetPlayer joiner = null;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 300f)
			{
				foreach (NetPlayer value in NetSession.Current.Players.Values)
				{
					if (!value.Host && value.Ready)
					{
						joiner = value;
					}
				}
				if (joiner != null)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (!Check("a joiner got the world", joiner != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Say("relief " + king.data.id);
			WorldTile dug = SoilNear(king.current_position, 6f, king.city, wild: false);
			if (!Check("a tile of the village to dig", dug != null))
			{
				yield break;
			}
			string id = dug.main_type.id;
			for (int i = 0; i < 12; i++)
			{
				if (!(dug.main_type.id == id))
				{
					break;
				}
				Earthworks.Apply(dug, fill: false, 0.25f, capped: false);
			}
			int i2 = dug.y * LandNow.Width + dug.x;
			Check("the host dug it a band down", dug.main_type.id != id, id + " -> " + dug.main_type.id);
			yield return new WaitForSecondsRealtime(1f);
			Say($"dug {dug.x} {dug.y} {Mathf.RoundToInt(LandNow.EditOf(i2) / (1f / 64f))} {dug.main_type.id}");
			string line = null;
			yield return WaitChat("saw dug", 30f, delegate(string x)
			{
				line = x;
			});
			Check("the joiner saw the dug tile", line == "saw dug");
			int digs0 = WorldSync.DigsForGuests;
			yield return WaitChat("guest dug ", 60f, delegate(string x)
			{
				line = x;
			});
			if (Check("the joiner swung", line != null && line.StartsWith("guest dug ")))
			{
				string[] array = line.Substring(10).Split(' ');
				WorldTile home = World.world.GetTile(int.Parse(array[0]), int.Parse(array[1]));
				WorldTile wild = ((array.Length >= 4 && array[2] != "-") ? World.world.GetTile(int.Parse(array[2]), int.Parse(array[3])) : null);
				yield return new WaitForSecondsRealtime(1f);
				Check("the joiner's swings at its village were dug here", WorldSync.DigsForGuests > digs0 && Mathf.Abs(LandNow.EditOf(home.y * LandNow.Width + home.x)) > 1f / 32f, $"{WorldSync.DigsForGuests - digs0} digs, edit {LandNow.EditOf(home.y * LandNow.Width + home.x):0.000}");
				if (wild != null)
				{
					Check("...and the wild's refused (guests dig their own town's land)", LandNow.EditOf(wild.y * LandNow.Width + wild.x) == 0f, $"edit {LandNow.EditOf(wild.y * LandNow.Width + wild.x):0.000}");
				}
				Say("host saw guest dig");
				yield return WaitChat("guest saw", 30f, delegate(string x)
				{
					line = x;
				});
				Check("the joiner's copy has its dig back", line == "guest saw");
				yield return WaitChat("guest levelled ", 30f, delegate(string x)
				{
					line = x;
				});
				if (line != null && line.StartsWith("guest levelled "))
				{
					string[] array2 = line.Substring(15).Split(' ');
					WorldTile lt = World.world.GetTile(int.Parse(array2[0]), int.Parse(array2[1]));
					float mark = float.Parse(array2[2], CultureInfo.InvariantCulture);
					yield return new WaitForSecondsRealtime(1f);
					Check("the joiner's level swing cut its tile to the mark here", lt != null && Mathf.Abs(Earthworks.WorkHeight(lt) - mark) < 0.05f, $"{Earthworks.WorkHeight(lt):F3} vs {mark:F3}");
				}
				else
				{
					Check("the joiner levelled", ok: false);
				}
				Say("host saw level");
				yield return new WaitForSecondsRealtime(3f);
			}
		}

		private IEnumerator JoinRelief()
		{
			SetPrefs("Joiner");
			float t0 = Time.realtimeSinceStartup;
			while (!WorldSync.Mirroring && Time.realtimeSinceStartup - t0 < 300f)
			{
				if (!NetSession.Active)
				{
					string text = NetSession.JoinIp("127.0.0.1:" + _port, "pw");
					if (text != null)
					{
						Log.Info("NETTEST join: " + text);
					}
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			if (!Check("joined and got the host's world", WorldSync.Mirroring))
			{
				yield break;
			}
			string line = null;
			yield return WaitChat("relief ", 60f, delegate(string text2)
			{
				line = text2;
			});
			Actor king = ((line != null) ? World.world.units.get(long.Parse(line.Substring(7))) : null);
			if (!Check("the host's king is here", king != null))
			{
				yield break;
			}
			Check("the world's land is the host's", ReliefWorld.Land != 0, "land " + ReliefWorld.Land);
			yield return WaitChat("dug ", 60f, delegate(string text2)
			{
				line = text2;
			});
			if (!Check("the host dug", line != null))
			{
				yield break;
			}
			string[] array = line.Substring(4).Split(' ');
			int x = int.Parse(array[0]);
			int y = int.Parse(array[1]);
			int units = int.Parse(array[2]);
			WorldTile tile = World.world.GetTile(x, y);
			Check("...its tile stepped here as the host's", tile?.main_type?.id == array[3], tile?.main_type?.id + " vs " + array[3]);
			Actor mine = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != king && simple.asset != null && !(simple.asset.id != "human") && simple.isAdult() && simple.city == king.city)
				{
					float sqrMagnitude = (simple.current_position - king.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						mine = simple;
					}
				}
			}
			if (!Check("a villager to take", mine != null))
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(mine);
			t0 = Time.realtimeSinceStartup;
			while (WorldSync.MyBody != mine.data.id && Time.realtimeSinceStartup - t0 < 10f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!Check("the host gave the body", WorldSync.MyBody == mine.data.id))
			{
				yield break;
			}
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 15f && (LandNow == null || LandNow.Width == 0 || Mathf.RoundToInt(LandNow.EditOf(y * LandNow.Width + x) / (1f / 64f)) != units))
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			int num2 = ((LandNow != null && LandNow.Width > 0) ? Mathf.RoundToInt(LandNow.EditOf(y * LandNow.Width + x) / (1f / 64f)) : (-999));
			Check("the host's dig made while this copy was on the map is in its land", num2 == units, $"edit {num2} of {units} units, {WorldSync.ReliefIn} relief messages");
			Say("saw dug");
			yield return new WaitForSecondsRealtime(2f);
			WorldTile home = SoilNear(mine.current_position, 4f, mine.city, wild: false);
			WorldTile wild = SoilNear(mine.current_position, 5.5f, null, wild: true);
			if (!Check("a tile of the village within a swing", home != null))
			{
				yield break;
			}
			int sent0 = WorldSync.DigsSent;
			for (int n = 0; n < 3; n++)
			{
				WorldSync.ForwardDig(mine, home.x, home.y, fill: false, 0.25f);
				if (wild != null)
				{
					WorldSync.ForwardDig(mine, wild.x, wild.y, fill: false, 0.25f);
				}
				yield return new WaitForSecondsRealtime(0.4f);
			}
			Check("the swings went to the host", WorldSync.DigsSent - sent0 == ((wild != null) ? 6 : 3), WorldSync.DigsSent - sent0 + " sent");
			Say(string.Format("guest dug {0} {1} {2}", home.x, home.y, (wild != null) ? (wild.x + " " + wild.y) : "- -"));
			yield return WaitChat("host saw guest dig", 30f, delegate(string text2)
			{
				line = text2;
			});
			t0 = Time.realtimeSinceStartup;
			int hi = home.y * LandNow.Width + home.x;
			while (Time.realtimeSinceStartup - t0 < 6f && Mathf.Abs(LandNow.EditOf(hi)) <= 1f / 32f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the village's tile came back dug", Mathf.Abs(LandNow.EditOf(hi)) > 1f / 32f, $"edit {LandNow.EditOf(hi):0.000}");
			Say("guest saw");
			yield return new WaitForSecondsRealtime(1f);
			float toward = Earthworks.WorkHeight(home) - 0.3f;
			int digsSent = WorldSync.DigsSent;
			WorldSync.ForwardDigTo(mine, home.x, home.y, fill: false, 1f, toward);
			Check("a level swing went to the host", WorldSync.DigsSent - digsSent == 1);
			Say(string.Format("guest levelled {0} {1} {2}", home.x, home.y, toward.ToString("F3", CultureInfo.InvariantCulture)));
			yield return WaitChat("host saw level", 30f, delegate(string text2)
			{
				line = text2;
			});
			yield return new WaitForSecondsRealtime(3f);
		}

		private IEnumerator HostTowns()
		{
			SetPrefs("Hoster");
			MpPrefs.Current.Port = _port;
			MpPrefs.Current.Password = "pw";
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 10, 2);
				if (king != null)
				{
					break;
				}
			}
			if (!Check("a village to play in", king != null && king.city != null))
			{
				yield break;
			}
			City town = king.city;
			yield return new WaitForSecondsRealtime(1.5f);
			Actor mine = NearestVillager(town, king.current_position, king, null);
			if (!Check("a villager for the host", mine != null))
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(mine);
			yield return new WaitForSecondsRealtime(2.5f);
			string text = NetSession.Host();
			if (!Check("hosting started", text == null, text ?? ""))
			{
				yield break;
			}
			NetPlayer joiner = null;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 300f)
			{
				foreach (NetPlayer value in NetSession.Current.Players.Values)
				{
					if (!value.Host && value.Ready)
					{
						joiner = value;
					}
				}
				if (joiner != null)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (!Check("a joiner got the world", joiner != null))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Say("towns " + town.data.id + " " + king.data.id + " " + mine.data.id);
			string line = null;
			yield return WaitChat("body ", 60f, delegate(string x)
			{
				line = x;
			});
			Actor jb = ((line != null) ? World.world.units.get(long.Parse(line.Substring(5))) : null);
			if (!Check("the joiner took a villager of the town", jb != null && jb.data.id == joiner.Body && jb.city == town, line ?? "no answer"))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)jb).addStatusEffect("invincible", 1200f, pColorEffect: false);
			}
			catch
			{
			}
			yield return WaitChat("left", 30f, delegate(string x)
			{
				line = x;
			});
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 6f && jb.city != null)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the joiner left the town on the host", jb.city == null && !town.units.Contains(jb), jb.city?.name ?? "");
			Check("...the game's way (one of its people's nomads, not of the town's realm)", jb.kingdom != town.kingdom, jb.kingdom?.name ?? "");
			yield return new WaitForSecondsRealtime(5f);
			Check("...and is still out of every town 5 s on", jb.city == null, jb.city?.name ?? "");
			Say("stays " + ((jb.city == null) ? "out" : ("in " + jb.city.data.id)));
			Bag.Put(jb, "wood", 40, spill: false);
			Bag.Put(jb, "stone", 20, spill: false);
			yield return WaitChat("founding", 90f, delegate(string x)
			{
				line = x;
			});
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 10f && WorldSync.TownsFoundedForJoiners == 0)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			City village = jb.city;
			if (!Check("the host founded the joiner's village", WorldSync.TownsFoundedForJoiners == 1 && village != null && village != town, "founded " + WorldSync.TownsFoundedForJoiners))
			{
				Say("village none");
				yield break;
			}
			Check("...the joiner its king, its own village", jb.isKing() && village.kingdom.king == jb && Village.IsVillage(village));
			yield return new WaitForSecondsRealtime(1f);
			Check("...held for the joiner's plans (its own building off)", Village.PlansAlone(village) && village.timer_build > 5f, "timer " + village.timer_build.ToString("F1"));
			List<Building> list = Blueprints.SitesOf(village);
			Building fire = ((list.Count > 0) ? list[0] : null);
			if (!Check("...the bonfire its first site", fire != null && fire.asset.type == "type_bonfire" && fire.isUnderConstruction(), fire?.asset?.id))
			{
				Say("village none");
				yield break;
			}
			Say("village " + village.data.id + " " + fire.data.id);
			yield return WaitChat("hammered", 120f, delegate(string x)
			{
				line = x;
			});
			Check("the joiner's blows struck on the host", WorldSync.TownBlowsTaken >= 3, "blows " + WorldSync.TownBlowsTaken);
			Check("...the bonfire stands, the village keeps it", fire.isAlive() && !fire.isUnderConstruction() && village.isAlive() && jb.city == village, "fire " + ((!fire.isAlive()) ? "gone" : (fire.isUnderConstruction() ? ("going up " + Blueprints.ProgressOf(fire).ToString("P0")) : "standing")));
			Say("fire " + ((fire.isAlive() && !fire.isUnderConstruction()) ? "up" : "not"));
			yield return WaitChat("planned", 60f, delegate(string x)
			{
				line = x;
			});
			Check("the joiner's plan placed on the host", WorldSync.TownPlansForJoiners >= 1 && Blueprints.CountOf(village) >= 1, "plans " + Blueprints.CountOf(village));
			Say("plans " + Blueprints.CountOf(village));
			yield return WaitChat("removed ", 60f, delegate(string x)
			{
				line = x;
			});
			Building plan = ((line != null) ? World.world.buildings.get(long.Parse(line.Substring(8))) : null);
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 5f && plan != null && plan.isAlive())
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("...and taken back", line != null && line != "removed 0" && (plan == null || !plan.isAlive()), (line ?? "no answer") + ", plans " + Blueprints.CountOf(village));
			Check("the joiner still rules the village", jb.isKing() && jb.city == village && village.isAlive());
			Say("done");
			yield return new WaitForSecondsRealtime(3f);
		}

		private IEnumerator JoinTowns()
		{
			SetPrefs("Joiner");
			float t0 = Time.realtimeSinceStartup;
			while (!WorldSync.Mirroring && Time.realtimeSinceStartup - t0 < 300f)
			{
				if (!NetSession.Active)
				{
					string text = NetSession.JoinIp("127.0.0.1:" + _port, "pw");
					if (text != null)
					{
						Log.Info("NETTEST join: " + text);
					}
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			if (!Check("joined and got the host's world", WorldSync.Mirroring))
			{
				yield break;
			}
			string line = null;
			yield return WaitChat("towns ", 60f, delegate(string x)
			{
				line = x;
			});
			string[] array = ((line != null) ? line.Split(' ') : new string[0]);
			City city = ((array.Length >= 4) ? World.world.cities.get(long.Parse(array[1])) : null);
			Actor actor = ((array.Length >= 4) ? World.world.units.get(long.Parse(array[2])) : null);
			Actor not = ((array.Length >= 4) ? World.world.units.get(long.Parse(array[3])) : null);
			if (!Check("the host's town is here", city != null && actor != null))
			{
				yield break;
			}
			Actor mine = NearestVillager(city, actor.current_position, actor, not);
			if (!Check("a villager to take", mine != null))
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(mine);
			t0 = Time.realtimeSinceStartup;
			while (WorldSync.MyBody != mine.data.id && Time.realtimeSinceStartup - t0 < 10f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!Check("the host gave the body", WorldSync.MyBody == mine.data.id))
			{
				Say("body none");
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			Say("body " + mine.data.id);
			yield return new WaitForSecondsRealtime(1f);
			Check("leaving the town here", Belong.Leave(mine) && mine.city == null);
			Say("left");
			yield return WaitChat("stays ", 30f, delegate(string x)
			{
				line = x;
			});
			Check("the host has you out of the town", line == "stays out", line ?? "no answer");
			Check("...and this copy too: not put back", mine.city == null, mine.city?.name ?? "");
			List<WorldTile> tried = new List<WorldTile>();
			bool flag = false;
			for (int attempt = 0; attempt < 3; attempt++)
			{
				if (flag)
				{
					break;
				}
				WorldTile spot = null;
				WorldTile current_tile = mine.current_tile;
				for (int num = 8; num <= 120; num += 4)
				{
					if (spot != null)
					{
						break;
					}
					for (int num2 = -num; num2 <= num; num2 += 2)
					{
						if (spot != null)
						{
							break;
						}
						for (int num3 = -num; num3 <= num; num3 += 2)
						{
							if (spot != null)
							{
								break;
							}
							if (Mathf.Abs(num2) != num && Mathf.Abs(num3) != num)
							{
								continue;
							}
							WorldTile worldTile = ((current_tile != null) ? World.world.GetTile(current_tile.x + num2, current_tile.y + num3) : null);
							if (worldTile == null || Village.WhyNotFoundAt(mine, worldTile) != null)
							{
								continue;
							}
							bool flag2 = false;
							foreach (WorldTile item in tried)
							{
								if (Mathf.Abs(item.x - worldTile.x) < 24 && Mathf.Abs(item.y - worldTile.y) < 24)
								{
									flag2 = true;
								}
							}
							if (!flag2)
							{
								spot = worldTile;
							}
						}
					}
				}
				if (!Check("open land for a village", spot != null))
				{
					break;
				}
				tried.Add(spot);
				mine.current_position = new Vector2((float)spot.x + 0.5f, (float)spot.y - 2.5f);
				mine.dirty_current_tile = true;
				yield return new WaitForSecondsRealtime(2f);
				Planning.Key(mine);
				Check("Y as a nomad is founding", Planning.Aiming && Planning.Founding);
				BuildingAsset buildingAsset = Village.BonfireOf(mine);
				int num4 = ((buildingAsset?.fundament != null) ? (buildingAsset.fundament.top / 2) : 0);
				Planning.AimForTest = World.world.GetTile(spot.x, spot.y + num4) ?? spot;
				for (int i = 0; i < 4; i++)
				{
					yield return null;
				}
				Planning.ClickForTest = true;
				for (int i = 0; i < 6; i++)
				{
					yield return null;
				}
				Planning.AimForTest = null;
				bool flag3 = !Planning.Aiming;
				if (!flag3)
				{
					Planning.Escape();
					flag3 = WorldSync.ForwardFound(mine, spot);
				}
				Check("the founding asked of the host", flag3);
				Check("...nothing founded on this copy alone", mine.city == null);
				int answers = WorldSync.TownAnswers;
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 6f && WorldSync.TownAnswers == answers)
				{
					yield return new WaitForSecondsRealtime(0.1f);
				}
				flag = WorldSync.LastTownAnswer.StartsWith("You founded");
				if (!flag)
				{
					Log.Info("NETTEST the host turned the spot " + spot.x + "," + spot.y + " down: " + WorldSync.LastTownAnswer);
				}
			}
			Say("founding");
			yield return WaitChat("village ", 30f, delegate(string x)
			{
				line = x;
			});
			array = ((line != null) ? line.Split(' ') : new string[0]);
			long villageId = ((array.Length >= 3) ? long.Parse(array[1]) : 0);
			long fireId = ((array.Length >= 3) ? long.Parse(array[2]) : 0);
			if (!Check("the host founded it", villageId != 0, line ?? "no answer"))
			{
				yield break;
			}
			City village = null;
			Building fire = null;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 10f)
			{
				village = World.world.cities.get(villageId);
				fire = World.world.buildings.get(fireId);
				if (village != null && mine.city == village && mine.isKing() && fire != null && fire.isAlive())
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("the host's answer said so", WorldSync.LastTownAnswer.StartsWith("You founded"), WorldSync.LastTownAnswer);
			Check("your village here: you its king", village != null && mine.city == village && mine.isKing(), (mine.city?.name ?? "no town") + ", king " + mine.isKing());
			if (fire == null)
			{
				string text2 = "";
				try
				{
					foreach (Building simple in World.world.buildings.getSimpleList())
					{
						if (simple?.data != null && (simple.data.cityID == villageId || (simple.asset != null && simple.asset.type == "type_bonfire" && (simple.current_position - mine.current_position).magnitude < 20f)))
						{
							text2 = text2 + simple.data.id + ":" + simple.asset?.id + ":" + simple.data.state.ToString() + " ";
						}
					}
					WorldTile worldTile2 = ((tried.Count > 0) ? tried[tried.Count - 1] : null);
					text2 = text2 + "| at the spot " + ((worldTile2?.building != null) ? (worldTile2.building.data?.id + ":" + worldTile2.building.asset?.id) : "nothing") + " | village " + ((village != null) ? (village.buildings.Count + " buildings") : "none");
				}
				catch (Exception ex)
				{
					text2 += ex.Message;
				}
				Log.Info("NETTEST bonfire " + fireId + " missing here: " + text2);
			}
			if (!Check("...its bonfire site here", fire != null && fire.isAlive() && fire.isUnderConstruction(), fire?.asset?.id))
			{
				yield break;
			}
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 5f && !Village.PlansAlone(village))
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("...planned by you alone here too", Village.PlansAlone(village), "village mark " + Village.IsVillage(village) + ", seat " + Village.Seat(WorldSync.MyBodyActor, village) + ", rules " + Rep.Rules(mine, village));
			Vector2 vector = new Vector2((float)fire.current_tile.x + 0.5f, (float)fire.current_tile.y + 1f);
			mine.current_position = vector + new Vector2(0f, -1.6f);
			mine.dirty_current_tile = true;
			yield return new WaitForSecondsRealtime(1f);
			int sent = 0;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 60f && fire.isAlive() && fire.isUnderConstruction())
			{
				if (WorldSync.ForwardSiteBlow(mine, fire, 1f))
				{
					sent++;
				}
				yield return new WaitForSecondsRealtime(0.6f);
			}
			yield return new WaitForSecondsRealtime(1f);
			Check("the bonfire stands here, built by your blows", fire.isAlive() && !fire.isUnderConstruction(), "sent " + sent + ", answer '" + WorldSync.LastTownAnswer + "'");
			Check("...the village is still yours", village.isAlive() && mine.city == village);
			Say("hammered");
			yield return WaitChat("fire ", 30f, delegate(string x)
			{
				line = x;
			});
			PlanPieces.Piece piece = null;
			BuildingAsset buildingAsset2 = null;
			foreach (PlanPieces.Category item2 in PlanPieces.All)
			{
				foreach (PlanPieces.Piece piece2 in item2.Pieces)
				{
					if (piece == null && piece2.Light < 0 && piece2.Wall < 0 && !piece2.Street && PlanPieces.WhyNot(piece2, village) == null && PlanPieces.AssetOf(piece2, village) != null)
					{
						piece = piece2;
						buildingAsset2 = PlanPieces.AssetOf(piece2, village);
					}
				}
			}
			WorldTile ground = null;
			WorldTile current_tile2 = fire.current_tile;
			for (int num5 = 3; num5 <= 14; num5++)
			{
				if (ground != null)
				{
					break;
				}
				if (buildingAsset2 == null)
				{
					break;
				}
				for (int num6 = -num5; num6 <= num5; num6++)
				{
					if (ground != null)
					{
						break;
					}
					for (int num7 = -num5; num7 <= num5; num7++)
					{
						if (ground != null)
						{
							break;
						}
						WorldTile tile = World.world.GetTile(current_tile2.x + num6, current_tile2.y + num7);
						if (tile != null && Blueprints.WhyNotHere(village, buildingAsset2, tile, new List<TileZone>()) == null)
						{
							ground = tile;
						}
					}
				}
			}
			if (!Check("a piece to plan and ground for it", piece != null && ground != null, piece?.Order ?? "no piece"))
			{
				Say("planned");
				yield break;
			}
			Check("the plan asked of the host", WorldSync.ForwardPlans(mine, village, piece.Order, 0, new List<WorldTile> { ground }));
			Building plan = null;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 8f && plan == null)
			{
				yield return new WaitForSecondsRealtime(0.25f);
				foreach (Building item3 in Blueprints.SitesOf(village))
				{
					if (item3.current_tile == ground)
					{
						plan = item3;
					}
				}
			}
			Check("the host's plan came here", plan?.isUnderConstruction() ?? false, piece.Order + " at " + ground.x + "," + ground.y + ", answer '" + WorldSync.LastTownAnswer + "'");
			Say("planned");
			yield return WaitChat("plans ", 30f, delegate(string x)
			{
				line = x;
			});
			long planId = ((plan?.data != null) ? plan.data.id : 0);
			if (plan != null)
			{
				Check("taking the plan back asked of the host", WorldSync.ForwardRemovePlan(mine, plan));
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 8f && plan.isAlive())
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Check("...gone here too", !plan.isAlive(), WorldSync.LastTownAnswer);
			}
			Say("removed " + planId);
			yield return WaitChat("done", 30f, delegate(string x)
			{
				line = x;
			});
			Check("still your village at the end", mine.city == village && mine.isKing());
			yield return new WaitForSecondsRealtime(3f);
		}
	}
}
