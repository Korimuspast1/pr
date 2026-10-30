using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace FirstPerson.Net
{
	public static class SteamGlue
	{
		private static bool _resolving;

		private static readonly HashSet<string> Said = new HashSet<string>();

		private static ModuleBuilder _module;

		private static Type _hostType;

		private static Type _joinType;

		private static readonly Dictionary<object, SteamTransport> Owners = new Dictionary<object, SteamTransport>();

		internal static void FindSteamLibrary()
		{
			if (_resolving)
			{
				return;
			}
			_resolving = true;
			AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
			{
				string name = new AssemblyName(args.Name).Name;
				if (!name.StartsWith("Facepunch.Steamworks", StringComparison.Ordinal))
				{
					return (Assembly)null;
				}
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly assembly in assemblies)
				{
					string name2 = assembly.GetName().Name;
					if (name2 != name && name2.StartsWith("Facepunch.Steamworks", StringComparison.Ordinal))
					{
						return Found(name, assembly);
					}
				}
				try
				{
					string directoryName = Path.GetDirectoryName(typeof(UnityEngine.Object).Assembly.Location);
					if (!string.IsNullOrEmpty(directoryName))
					{
						string[] files = Directory.GetFiles(directoryName, "Facepunch.Steamworks*.dll");
						foreach (string text in files)
						{
							if (Path.GetFileNameWithoutExtension(text) != name)
							{
								return Found(name, Assembly.LoadFrom(text));
							}
						}
					}
				}
				catch (Exception ex)
				{
					Log.Info("NET no Steam library to load: " + ex.Message);
				}
				return (Assembly)null;
			};
		}

		private static Assembly Found(string asked, Assembly a)
		{
			if (Said.Add(asked))
			{
				Log.Info("NET Steam library " + asked + " answered by " + a.GetName().Name);
			}
			return a;
		}

		internal static SocketManager OpenHost(SteamTransport owner)
		{
			if (_hostType == null)
			{
				_hostType = Make(typeof(SocketManager), "HostSocket", "Host");
			}
			SocketManager socketManager = (SocketManager)Call(Generic(typeof(SteamNetworkingSockets), "CreateRelaySocket", 1).MakeGenericMethod(_hostType), 0);
			if (socketManager != null)
			{
				Owners[socketManager] = owner;
			}
			return socketManager;
		}

		internal static ConnectionManager Connect(SteamTransport owner, ulong host)
		{
			if (_joinType == null)
			{
				_joinType = Make(typeof(ConnectionManager), "JoinConnection", "Join");
			}
			MethodInfo methodInfo = Generic(typeof(SteamNetworkingSockets), "ConnectRelay", 2);
			SteamId steamId = host;
			ConnectionManager connectionManager = (ConnectionManager)Call(methodInfo.MakeGenericMethod(_joinType), steamId, 0);
			if (connectionManager != null)
			{
				Owners[connectionManager] = owner;
			}
			return connectionManager;
		}

		internal static void Forget(object manager)
		{
			if (manager != null)
			{
				Owners.Remove(manager);
			}
		}

		private static MethodInfo Generic(Type on, string name, int args)
		{
			MethodInfo[] methods = on.GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name == name && methodInfo.IsGenericMethodDefinition && methodInfo.GetParameters().Length == args)
				{
					return methodInfo;
				}
			}
			throw new MissingMethodException(on.Name, name);
		}

		private static object Call(MethodInfo m, params object[] args)
		{
			try
			{
				return m.Invoke(null, args);
			}
			catch (TargetInvocationException ex) when (ex.InnerException != null)
			{
				throw ex.InnerException;
			}
		}

		private static Type Make(Type parent, string name, string prefix)
		{
			if (_module == null)
			{
				_module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("WorldfallSteamGlue"), AssemblyBuilderAccess.Run).DefineDynamicModule("WorldfallSteamGlue");
			}
			TypeBuilder typeBuilder = _module.DefineType("WorldfallSteamGlue." + name, TypeAttributes.Public | TypeAttributes.Sealed, parent);
			typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
			string[] array = new string[3] { "OnConnected", "OnDisconnected", "OnMessage" };
			foreach (string text in array)
			{
				MethodInfo method = parent.GetMethod(text, BindingFlags.Instance | BindingFlags.Public);
				MethodInfo method2 = typeof(SteamGlue).GetMethod(prefix + text, BindingFlags.Static | BindingFlags.Public);
				Type[] array2 = Array.ConvertAll(method.GetParameters(), (ParameterInfo p) => p.ParameterType);
				MethodBuilder methodBuilder = typeBuilder.DefineMethod(text, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig, typeof(void), array2);
				ILGenerator iLGenerator = methodBuilder.GetILGenerator();
				if (text == "OnConnected")
				{
					Forward(iLGenerator, array2.Length, method);
				}
				Forward(iLGenerator, array2.Length, method2);
				if (text == "OnDisconnected")
				{
					Forward(iLGenerator, array2.Length, method);
				}
				iLGenerator.Emit(OpCodes.Ret);
				typeBuilder.DefineMethodOverride(methodBuilder, method);
			}
			return typeBuilder.CreateType();
		}

		private static void Forward(ILGenerator il, int args, MethodInfo to)
		{
			for (short num = 0; num <= args; num++)
			{
				il.Emit(OpCodes.Ldarg, num);
			}
			il.Emit(OpCodes.Call, to);
		}

		public static void HostOnConnected(SocketManager self, Connection c, ConnectionInfo info)
		{
			try
			{
				if (Owners.TryGetValue(self, out var value))
				{
					value.Up(c, info);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET steam connected: " + ex);
			}
		}

		public static void HostOnDisconnected(SocketManager self, Connection c, ConnectionInfo info)
		{
			try
			{
				if (Owners.TryGetValue(self, out var value))
				{
					value.Down(c, info);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET steam disconnected: " + ex);
			}
		}

		public static void HostOnMessage(SocketManager self, Connection c, NetIdentity id, IntPtr data, int size, long num, long time, int channel)
		{
			if (Owners.TryGetValue(self, out var value))
			{
				value.Message(c.Id, data, size);
			}
		}

		public static void JoinOnConnected(ConnectionManager self, ConnectionInfo info)
		{
			try
			{
				if (Owners.TryGetValue(self, out var value))
				{
					value.Up(self.Connection, info);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET steam connected: " + ex);
			}
		}

		public static void JoinOnDisconnected(ConnectionManager self, ConnectionInfo info)
		{
			try
			{
				if (Owners.TryGetValue(self, out var value))
				{
					value.Down(self.Connection, info);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET steam disconnected: " + ex);
			}
		}

		public static void JoinOnMessage(ConnectionManager self, IntPtr data, int size, long num, long time, int channel)
		{
			if (Owners.TryGetValue(self, out var value))
			{
				value.Message(self.Connection.Id, data, size);
			}
		}
	}
}
