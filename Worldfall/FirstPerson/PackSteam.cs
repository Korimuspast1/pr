using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Steamworks.Ugc;
using UnityEngine;
using UnityEngine.Networking;

namespace FirstPerson
{
	internal static class PackSteam
	{
		internal sealed class Entry
		{
			public ulong Id;

			public string Title;

			public string Author;

			public string Description;

			public string PreviewUrl;

			public uint Up;

			public uint Down;

			public float Score;

			public string[] Tags = new string[0];

			public DateTime Updated;

			public long Size;

			public Texture2D Preview;

			public bool PreviewAsked;

			public List<string> Kinds = new List<string>();

			public ulong Owner;

			public bool OwnerAsked;
		}

		private static readonly Regex BbCode = new Regex("\\[/?[a-zA-Z0-9*]+(=[^\\]]*)?\\]");

		internal static string AnyWithTag;

		public static readonly string[] Sorts = new string[5] { "Popular", "Newest", "Top rated", "Most subscribed", "Updated" };

		private static bool? _ok;

		private static float _nextOk;

		private static readonly Dictionary<ulong, Task<bool>> Pending = new Dictionary<ulong, Task<bool>>();

		private static readonly HashSet<ulong> Downloading = new HashSet<ulong>();

		private static readonly Dictionary<ulong, float> Progress = new Dictionary<ulong, float>();

		private static float _nextPoll;

		public static bool NewlyInstalled;

		public static readonly HashSet<ulong> TurnOnWhenIn = new HashSet<ulong>();

		public static readonly List<ulong> Arrived = new List<ulong>();

		private static Task<ResultPage?> _query;

		private static int _queryPage;

		public static readonly List<Entry> Results = new List<Entry>();

		public static int Total;

		public static int Page = 1;

		public static string Status = "";

		private static readonly Dictionary<ulong, Texture2D> PreviewCache = new Dictionary<ulong, Texture2D>();

		private static readonly LinkedList<ulong> PreviewOrder = new LinkedList<ulong>();

		private const int MaxPreviews = 150;

		private static readonly List<KeyValuePair<Entry, UnityWebRequest>> Loading = new List<KeyValuePair<Entry, UnityWebRequest>>();

		public static bool Available
		{
			get
			{
				if (_ok.HasValue && Time.unscaledTime < _nextOk)
				{
					return _ok.Value;
				}
				_nextOk = Time.unscaledTime + 3f;
				try
				{
					_ok = Valid();
				}
				catch
				{
					_ok = false;
				}
				return _ok.Value;
			}
		}

		public static bool Querying => _query != null;

		private static object Internal => typeof(SteamUGC).GetProperty("Internal", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);

		public static string PlainText(string bb)
		{
			string text = BbCode.Replace(bb ?? "", "").Replace("\r", " ").Replace("\n", " ");
			while (text.Contains("  "))
			{
				text = text.Replace("  ", " ");
			}
			return text.Trim();
		}

		public static string AuthorOf(Entry e)
		{
			if (e.Owner == 0L)
			{
				return e.Author ?? "";
			}
			try
			{
				Friend friend = new Friend(e.Owner);
				string name = friend.Name;
				if (!string.IsNullOrEmpty(name) && name != "[unknown]")
				{
					return e.Author = name;
				}
				if (!e.OwnerAsked)
				{
					e.OwnerAsked = true;
					friend.RequestInfoAsync();
				}
			}
			catch
			{
			}
			if (e.Author == null || !(e.Author != "[unknown]"))
			{
				return "";
			}
			return e.Author;
		}

		private static bool Valid()
		{
			if (SteamClient.IsValid)
			{
				return SteamClient.IsLoggedOn;
			}
			return false;
		}

		public static HashSet<ulong> SubscribedOrNull()
		{
			try
			{
				return Available ? Subscribed() : null;
			}
			catch (Exception ex)
			{
				Log.Info("packs: Steam's subscriptions unknown (" + ex.Message + ")");
				return null;
			}
		}

		private static HashSet<ulong> Subscribed()
		{
			object obj = Internal;
			uint num = (uint)Method("GetNumSubscribedItems").Invoke(obj, null);
			PublishedFileId[] array = new PublishedFileId[num];
			if (num != 0)
			{
				Method("GetSubscribedItems").Invoke(obj, new object[2] { array, num });
			}
			HashSet<ulong> hashSet = new HashSet<ulong>();
			PublishedFileId[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				PublishedFileId publishedFileId = array2[i];
				hashSet.Add(publishedFileId.Value);
			}
			return hashSet;
		}

		public static Dictionary<ulong, string> InstalledFolders()
		{
			Dictionary<ulong, string> dictionary = new Dictionary<ulong, string>();
			try
			{
				if (!Available)
				{
					return dictionary;
				}
				foreach (ulong item2 in Subscribed())
				{
					Steamworks.Ugc.Item item = new Steamworks.Ugc.Item(item2);
					if (item.IsInstalled && !string.IsNullOrEmpty(item.Directory))
					{
						dictionary[item2] = item.Directory;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Info("packs: Steam's installed items unknown (" + ex.Message + ")");
			}
			return dictionary;
		}

		public static bool IsSubscribed(ulong id)
		{
			try
			{
				return Available && new Steamworks.Ugc.Item(id).IsSubscribed;
			}
			catch
			{
				return false;
			}
		}

		public static bool IsInstalled(ulong id)
		{
			try
			{
				return Available && new Steamworks.Ugc.Item(id).IsInstalled;
			}
			catch
			{
				return false;
			}
		}

		public static float DownloadOf(ulong id)
		{
			if (!Progress.TryGetValue(id, out var value))
			{
				if (!Pending.ContainsKey(id))
				{
					return -1f;
				}
				return 0f;
			}
			return value;
		}

		public static string Subscribe(ulong id)
		{
			try
			{
				if (!Available)
				{
					return "Steam is not running";
				}
				Pending[id] = new Steamworks.Ugc.Item(id).Subscribe();
				Progress[id] = 0f;
				TurnOnWhenIn.Add(id);
				return null;
			}
			catch (Exception ex)
			{
				return "Could not subscribe. " + ex.Message;
			}
		}

		public static string Unsubscribe(ulong id)
		{
			try
			{
				if (!Available)
				{
					return "Steam is not running";
				}
				new Steamworks.Ugc.Item(id).Unsubscribe();
				TurnOnWhenIn.Remove(id);
				Progress.Remove(id);
				Downloading.Remove(id);
				return null;
			}
			catch (Exception ex)
			{
				return "Could not unsubscribe. " + ex.Message;
			}
		}

		public static void Vote(ulong id, bool up)
		{
			try
			{
				if (Available)
				{
					new Steamworks.Ugc.Item(id).Vote(up);
				}
			}
			catch (Exception ex)
			{
				Log.Info("packs: vote failed (" + ex.Message + ")");
			}
		}

		public static void Tick()
		{
			TickQuery();
			TickPreviews();
			PackPublish.Tick();
			if ((Pending.Count == 0 && Downloading.Count == 0) || Time.unscaledTime < _nextPoll)
			{
				return;
			}
			_nextPoll = Time.unscaledTime + 0.5f;
			try
			{
				List<ulong> list = new List<ulong>();
				foreach (KeyValuePair<ulong, Task<bool>> item2 in Pending)
				{
					if (item2.Value.IsCompleted)
					{
						list.Add(item2.Key);
						if (!item2.Value.IsFaulted && item2.Value.Result)
						{
							new Steamworks.Ugc.Item(item2.Key).Download(highPriority: true);
							Downloading.Add(item2.Key);
						}
						else
						{
							Progress.Remove(item2.Key);
							Log.Info("packs: subscribing to " + item2.Key + " failed");
						}
					}
				}
				foreach (ulong item3 in list)
				{
					Pending.Remove(item3);
				}
				List<ulong> list2 = new List<ulong>();
				foreach (ulong item4 in Downloading)
				{
					Steamworks.Ugc.Item item = new Steamworks.Ugc.Item(item4);
					if (item.IsInstalled && !item.IsDownloading && !item.IsDownloadPending && !item.NeedsUpdate)
					{
						list2.Add(item4);
					}
					else
					{
						Progress[item4] = Mathf.Clamp01(item.DownloadAmount);
					}
				}
				foreach (ulong item5 in list2)
				{
					Downloading.Remove(item5);
					Progress.Remove(item5);
					NewlyInstalled = true;
					Arrived.Add(item5);
					Log.Info("packs: Workshop item " + item5 + " is in");
				}
			}
			catch (Exception ex)
			{
				Log.Info("packs: download check failed (" + ex.Message + ")");
			}
		}

		private static void KeepPreview(ulong id, Texture2D tex)
		{
			PreviewCache[id] = tex;
			PreviewOrder.Remove(id);
			PreviewOrder.AddLast(id);
			LinkedListNode<ulong> linkedListNode = PreviewOrder.First;
			while (linkedListNode != null && PreviewCache.Count > 150)
			{
				LinkedListNode<ulong> next = linkedListNode.Next;
				ulong old = linkedListNode.Value;
				if (!Results.Exists((Entry r) => r.Id == old))
				{
					if (PreviewCache.TryGetValue(old, out var value) && value != null)
					{
						UnityEngine.Object.Destroy(value);
					}
					PreviewCache.Remove(old);
					PreviewOrder.Remove(linkedListNode);
				}
				linkedListNode = next;
			}
		}

		public static void Query(string search, string kind, int sort, int page)
		{
			try
			{
				if (!Available)
				{
					Status = "Steam is not running";
					return;
				}
				Query items = Steamworks.Ugc.Query.Items;
				items = ((AnyWithTag != null) ? items.WithTag(AnyWithTag) : items.AddRequiredKeyValueTag("worldfall", "pack"));
				if (!string.IsNullOrEmpty(kind))
				{
					items = items.AddRequiredKeyValueTag("wf_kind", kind);
				}
				bool flag = !string.IsNullOrEmpty(search);
				if (flag)
				{
					items = items.WhereSearchText(search);
				}
				switch (sort)
				{
				case 1:
					items = items.RankedByPublicationDate();
					break;
				case 2:
					items = items.RankedByVote();
					break;
				case 3:
					items = items.RankedByTotalUniqueSubscriptions();
					break;
				case 4:
					items = items.SortByUpdateDate();
					break;
				default:
					items = (flag ? items.RankedByTextSearch() : items.RankedByTrend());
					break;
				}
				items = items.WithLongDescription(b: true);
				_queryPage = Math.Max(1, page);
				_query = items.GetPageAsync(_queryPage);
				Status = "Searching the Workshop";
			}
			catch (Exception ex)
			{
				Status = "The Workshop can't be searched. " + ex.Message;
				_query = null;
			}
		}

		private static void TickQuery()
		{
			if (_query == null || !_query.IsCompleted)
			{
				return;
			}
			Task<ResultPage?> query = _query;
			_query = null;
			try
			{
				if (query.IsFaulted || !query.Result.HasValue)
				{
					Status = "No answer from the Workshop";
					return;
				}
				ResultPage value = query.Result.Value;
				try
				{
					Results.Clear();
					Total = value.TotalCount;
					Page = _queryPage;
					foreach (Steamworks.Ugc.Item entry2 in value.Entries)
					{
						Entry entry = new Entry
						{
							Id = entry2.Id.Value,
							Title = (entry2.Title ?? ""),
							Author = SafeName(entry2),
							Description = (entry2.Description ?? ""),
							PreviewUrl = entry2.PreviewImageUrl,
							Up = entry2.VotesUp,
							Down = entry2.VotesDown,
							Score = entry2.Score,
							Tags = (entry2.Tags ?? new string[0]),
							Updated = entry2.Updated,
							Size = entry2.SizeBytes,
							Owner = OwnerId(entry2)
						};
						string[] tags = entry.Tags;
						foreach (string text in tags)
						{
							if (text.StartsWith("wf:", StringComparison.OrdinalIgnoreCase))
							{
								entry.Kinds.Add(text.Substring(3).ToLowerInvariant());
							}
						}
						if (PreviewCache.TryGetValue(entry.Id, out var value2))
						{
							entry.Preview = value2;
							entry.PreviewAsked = true;
							PreviewOrder.Remove(entry.Id);
							PreviewOrder.AddLast(entry.Id);
						}
						Results.Add(entry);
					}
					Status = ((Total == 0) ? "No packs found" : (Total + " packs"));
					Log.Info("packs: Workshop query page " + Page + ": " + Results.Count + " of " + Total + " (" + string.Join(", ", Results.ConvertAll((Entry r) => r.Id + " " + r.Title).ToArray()) + ")");
				}
				finally
				{
					((IDisposable)value/*cast due to .constrained prefix*/).Dispose();
				}
			}
			catch (Exception ex)
			{
				Status = "The Workshop's answer can't be read. " + ex.Message;
			}
		}

		private static ulong OwnerId(Steamworks.Ugc.Item item)
		{
			try
			{
				return item.Owner.Id.Value;
			}
			catch
			{
				return 0uL;
			}
		}

		private static string SafeName(Steamworks.Ugc.Item item)
		{
			try
			{
				return item.Owner.Name ?? "";
			}
			catch
			{
				return "";
			}
		}

		public static void WantPreview(Entry e)
		{
			if (e.PreviewAsked || string.IsNullOrEmpty(e.PreviewUrl) || Loading.Count >= 4)
			{
				return;
			}
			if (!e.PreviewUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				e.PreviewAsked = true;
				return;
			}
			e.PreviewAsked = true;
			try
			{
				UnityWebRequest unityWebRequest = UnityWebRequest.Get(e.PreviewUrl);
				unityWebRequest.timeout = 20;
				unityWebRequest.SendWebRequest();
				Loading.Add(new KeyValuePair<Entry, UnityWebRequest>(e, unityWebRequest));
			}
			catch
			{
			}
		}

		private static void TickPreviews()
		{
			for (int num = Loading.Count - 1; num >= 0; num--)
			{
				UnityWebRequest value = Loading[num].Value;
				if (value.isDone)
				{
					Entry key = Loading[num].Key;
					Loading.RemoveAt(num);
					try
					{
						byte[] array = ((value.result == UnityWebRequest.Result.Success) ? value.downloadHandler.data : null);
						if (array != null && array.Length != 0 && array.Length < 4194304)
						{
							Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
							{
								name = "WorkshopPreview" + key.Id,
								filterMode = FilterMode.Bilinear
							};
							if (texture2D.LoadImage(array))
							{
								key.Preview = texture2D;
								KeepPreview(key.Id, texture2D);
							}
							else
							{
								UnityEngine.Object.Destroy(texture2D);
							}
						}
					}
					catch
					{
					}
					finally
					{
						value.Dispose();
					}
				}
			}
		}

		private static MethodInfo Method(string name)
		{
			return Internal.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		internal static void AddDependency(ulong item, ulong required)
		{
			Method("AddDependency").Invoke(Internal, new object[2]
			{
				(PublishedFileId)item,
				(PublishedFileId)required
			});
		}
	}
}
