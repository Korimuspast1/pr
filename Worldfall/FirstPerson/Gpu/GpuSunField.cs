using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuSunField : IDisposable
	{
		private sealed class Slot
		{
			public Texture2D Tex;

			public int Version;

			public Vec3 Light;
		}

		private readonly Slot[] _slots = new Slot[3]
		{
			new Slot(),
			new Slot(),
			new Slot()
		};

		private int _w;

		private int _h;

		private int _shown;

		private int _whole;

		private Task _job;

		private int _jobVersion;

		private Vec3 _jobLight;

		private ushort[] _jobOut;

		private ushort[] _spare;

		private bool _failLogged;

		private bool _timesLogged;

		private const int BandFrom = 1048576;

		private const int BandBytes = 2097152;

		private Slot _band;

		private ushort[] _bandData;

		private int _bandVersion;

		private int _bandRow;

		private int _bandRows;

		private Vec3 _bandLight;

		private Texture2D _staging;

		private int _easing;

		private float _easeAt;

		private float _easeOver;

		private const float EaseSeconds = 1f;

		private const float EaseLeast = 0.25f;

		internal double ConvertMs;

		internal double UploadMs;

		internal int Uploads;

		internal int ShownVersion;

		internal int EasedFromVersion;

		internal int Waits;

		internal float ShownBlend;

		private static int _supported = -1;

		private int _keptFrame = -1;

		private static bool Supported
		{
			get
			{
				if (_supported < 0)
				{
					_supported = (SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf) ? 1 : 0);
				}
				return _supported == 1;
			}
		}

		public bool Bind(CommandBuffer cmd, TerrainCache land, bool natural, bool outdoors)
		{
			if (!natural)
			{
				Release();
			}
			SunShadowField sunShadowField = ((natural && outdoors) ? Newest(land) : null);
			ShownVersion = (EasedFromVersion = 0);
			ShownBlend = 1f;
			if (sunShadowField == null)
			{
				if (natural && outdoors && land != null && land.SunField == null)
				{
					_shown = 0;
				}
				Off(cmd);
				return false;
			}
			SunShadowField sunShadowField2 = Before(land, sunShadowField);
			if (_keptFrame != Time.frameCount || sunShadowField.Width != _w || sunShadowField.Height != _h)
			{
				Keep(sunShadowField, sunShadowField2);
			}
			Slot slot = Find(sunShadowField.Version);
			Slot slot2 = ((sunShadowField2 != null) ? Find(sunShadowField2.Version) : null);
			bool flag = _band != null && sunShadowField2 != null && _bandVersion == sunShadowField2.Version;
			Slot slot3 = slot;
			if (slot3 == null)
			{
				Slot slot4 = ((_shown != 0) ? Find(_shown) : null);
				if (slot4 != null && !flag && (slot4.Light.X != sunShadowField.Light.X || slot4.Light.Y != sunShadowField.Light.Y || slot4.Light.Z != sunShadowField.Light.Z))
				{
					slot4 = null;
				}
				slot3 = ((slot4 != null && (flag || sunShadowField2 == null || _shown > sunShadowField2.Version)) ? slot4 : slot2);
				if (slot3 == null && flag)
				{
					SendBands(int.MaxValue);
					slot3 = (slot2 = Find(sunShadowField2.Version));
				}
				if (slot3 == null)
				{
					slot3 = (slot = Waited(sunShadowField, sunShadowField2));
				}
			}
			if (slot3 == null)
			{
				Off(cmd);
				return false;
			}
			_shown = slot3.Version;
			float num = land.SunFieldBlend;
			if (slot3 == slot && sunShadowField2 != null && slot2 == null && num < 1f)
			{
				_whole = sunShadowField.Version;
			}
			if (slot3 == slot && slot2 != null && slot2 != slot3 && _whole != sunShadowField.Version && _easing != sunShadowField.Version)
			{
				_easing = sunShadowField.Version;
				_easeAt = Time.unscaledTime;
				_easeOver = Mathf.Max((1f - Mathf.Clamp01(num)) * 1f, 0.25f);
			}
			if (_easing == sunShadowField.Version)
			{
				num = Mathf.Clamp01((Time.unscaledTime - _easeAt) / _easeOver);
			}
			bool flag2 = slot2 != null && slot2 != slot3 && num < 1f && _whole != sunShadowField.Version;
			cmd.SetGlobalTexture("_FP_SunField", slot3.Tex);
			cmd.SetGlobalTexture("_FP_SunFieldPrev", flag2 ? slot2.Tex : slot3.Tex);
			cmd.SetGlobalVector("_FP_SunFieldQ", new Vector4(1f / (float)_w, 1f / (float)_h, flag2 ? Mathf.Clamp01(num) : 1f, 1f));
			cmd.SetGlobalVector("_FP_SunSoft", new Vector4(0.35f, 0.035f, 0.15f, 0f));
			ShownVersion = slot3.Version;
			EasedFromVersion = (flag2 ? slot2.Version : 0);
			ShownBlend = (flag2 ? Mathf.Clamp01(num) : 1f);
			return true;
		}

		public void KeepUp(TerrainCache land, bool natural)
		{
			SunShadowField sunShadowField = (natural ? Newest(land) : null);
			if (sunShadowField != null)
			{
				Keep(sunShadowField, Before(land, sunShadowField));
				_keptFrame = Time.frameCount;
			}
		}

		private static SunShadowField Newest(TerrainCache land)
		{
			SunShadowField sunShadowField = ((land != null && land.SoftLandShadows && land.CastShadows) ? land.SunField : null);
			if (sunShadowField == null || sunShadowField.Width <= 0 || sunShadowField.Height <= 0 || sunShadowField.Width != land.Width || sunShadowField.Height != land.Height || !Supported)
			{
				return null;
			}
			return sunShadowField;
		}

		private static SunShadowField Before(TerrainCache land, SunShadowField now)
		{
			SunShadowField sunFieldPrev = land.SunFieldPrev;
			if (sunFieldPrev == null || sunFieldPrev.Width != now.Width || sunFieldPrev.Height != now.Height)
			{
				return null;
			}
			return sunFieldPrev;
		}

		private void Keep(SunShadowField now, SunShadowField prev)
		{
			if (now.Width != _w || now.Height != _h)
			{
				Release();
				_w = now.Width;
				_h = now.Height;
			}
			TakeFinished(now, prev, all: false);
			Pump(now, prev);
			if (_job == null && _band == null)
			{
				if (Find(now.Version) == null)
				{
					Start(now);
				}
				else if (prev != null && Find(prev.Version) == null && _whole != now.Version)
				{
					Start(prev);
				}
			}
		}

		private Slot Waited(SunShadowField now, SunShadowField prev)
		{
			Waits++;
			if (_band != null)
			{
				if (_bandVersion == now.Version)
				{
					SendBands(int.MaxValue);
				}
				else
				{
					DropBand();
				}
				Slot slot = Find(now.Version);
				if (slot != null)
				{
					return slot;
				}
			}
			for (int i = 0; i < 2; i++)
			{
				if (_job == null)
				{
					Start(now);
				}
				if (_job == null)
				{
					return null;
				}
				try
				{
					_job.Wait();
				}
				catch (AggregateException)
				{
				}
				TakeFinished(now, prev, all: true);
				Slot slot2 = Find(now.Version);
				if (slot2 != null)
				{
					return slot2;
				}
			}
			return null;
		}

		private static void Off(CommandBuffer cmd)
		{
			cmd.SetGlobalTexture("_FP_SunField", Texture2D.whiteTexture);
			cmd.SetGlobalTexture("_FP_SunFieldPrev", Texture2D.whiteTexture);
			cmd.SetGlobalVector("_FP_SunFieldQ", Vector4.zero);
			cmd.SetGlobalVector("_FP_SunSoft", Vector4.zero);
		}

		private Slot Find(int version)
		{
			Slot[] slots = _slots;
			foreach (Slot slot in slots)
			{
				if (slot.Version == version && slot.Tex != null)
				{
					return slot;
				}
			}
			return null;
		}

		private void Start(SunShadowField field)
		{
			int n = field.Width * field.Height;
			float[] sl = field.SLand;
			float[] dl = field.DLand;
			float[] sa = field.SAll;
			float[] da = field.DAll;
			if (sl.Length >= n && dl.Length >= n && sa.Length >= n && da.Length >= n)
			{
				ushort[] o = ((_spare != null && _spare.Length == n * 4) ? _spare : new ushort[n * 4]);
				_spare = null;
				_jobOut = o;
				_jobVersion = field.Version;
				_jobLight = field.Light;
				_job = Task.Run(delegate
				{
					long timestamp = Stopwatch.GetTimestamp();
					Halves(sl, dl, sa, da, n, o);
					ConvertMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
				});
			}
		}

		private void TakeFinished(SunShadowField now, SunShadowField prev, bool all)
		{
			if (_job == null || !_job.IsCompleted)
			{
				return;
			}
			Task job = _job;
			ushort[] jobOut = _jobOut;
			int jobVersion = _jobVersion;
			Vec3 jobLight = _jobLight;
			_job = null;
			_jobOut = null;
			_spare = jobOut;
			if (job.IsFaulted || job.IsCanceled)
			{
				if (!_failLogged)
				{
					_failLogged = true;
					Log.Error("sun field: half floats failed: " + ((job.Exception != null) ? job.Exception.GetBaseException().Message : "cancelled"));
				}
			}
			else
			{
				if ((jobVersion != now.Version && (prev == null || jobVersion != prev.Version)) || jobOut == null || jobOut.Length != _w * _h * 4 || Find(jobVersion) != null)
				{
					return;
				}
				Slot slot = null;
				Slot[] slots = _slots;
				foreach (Slot slot2 in slots)
				{
					if (slot2.Version != now.Version && (prev == null || slot2.Version != prev.Version) && (slot2.Version != _shown || _shown == 0) && (slot == null || slot2.Tex == null || slot2.Version == 0))
					{
						slot = slot2;
					}
				}
				if (slot == null)
				{
					return;
				}
				long timestamp = Stopwatch.GetTimestamp();
				bool flag = Banded();
				if (slot.Tex == null || slot.Tex.width != _w || slot.Tex.height != _h || slot.Tex.isReadable == flag)
				{
					if (slot.Tex != null)
					{
						UnityEngine.Object.Destroy(slot.Tex);
					}
					slot.Tex = new Texture2D(_w, _h, TextureFormat.RGBAHalf, mipChain: false, linear: true)
					{
						filterMode = FilterMode.Bilinear,
						wrapMode = TextureWrapMode.Clamp,
						anisoLevel = 0,
						name = "FirstPersonSunField"
					};
					if (flag)
					{
						slot.Tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
					}
				}
				if (flag)
				{
					slot.Version = 0;
					_band = slot;
					_bandData = jobOut;
					_spare = null;
					_bandVersion = jobVersion;
					_bandLight = jobLight;
					_bandRow = 0;
					SendBands((!all) ? 1 : int.MaxValue);
				}
				else
				{
					slot.Tex.SetPixelData(jobOut, 0);
					slot.Tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
					UploadMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
					Sent(slot, jobVersion, jobLight, "");
				}
			}
		}

		private void Sent(Slot into, int version, Vec3 light, string how)
		{
			into.Version = version;
			into.Light = light;
			Uploads++;
			if (!_timesLogged && Uploads >= 3)
			{
				_timesLogged = true;
				Log.Info($"sun field: {_w}x{_h} on the card: half floats {ConvertMs:F1} ms (worker), sent up in {UploadMs:F1} ms{how}");
			}
		}

		private bool Banded()
		{
			if (_w * _h < 1048576 || (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
			{
				return false;
			}
			int num = 64 * Math.Max(1, 2097152 / (_w * 8) / 64);
			if (_h % num != 0)
			{
				return false;
			}
			_bandRows = num;
			return true;
		}

		private void Pump(SunShadowField now, SunShadowField prev)
		{
			if (_band != null)
			{
				if (_bandVersion != now.Version && (prev == null || _bandVersion != prev.Version))
				{
					DropBand();
				}
				else
				{
					SendBands(1);
				}
			}
		}

		private void SendBands(int bands)
		{
			int bandRows = _bandRows;
			if (_staging == null || _staging.width != _w || _staging.height != bandRows)
			{
				if (_staging != null)
				{
					UnityEngine.Object.Destroy(_staging);
				}
				_staging = new Texture2D(_w, bandRows, TextureFormat.RGBAHalf, mipChain: false, linear: true)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					anisoLevel = 0,
					name = "FirstPersonSunFieldBand"
				};
			}
			long timestamp = Stopwatch.GetTimestamp();
			int num = 0;
			while (num < bands && _bandRow < _h)
			{
				_staging.SetPixelData(_bandData, 0, _bandRow * _w * 4);
				_staging.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				Graphics.CopyTexture(_staging, 0, 0, 0, 0, _w, bandRows, _band.Tex, 0, 0, 0, _bandRow);
				num++;
				_bandRow += bandRows;
			}
			UploadMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			if (_bandRow >= _h)
			{
				Slot band = _band;
				_spare = _bandData;
				_band = null;
				_bandData = null;
				Sent(band, _bandVersion, _bandLight, $" (the last frame's bands, {_bandRows} rows each)");
			}
		}

		private void DropBand()
		{
			if (_band != null)
			{
				_spare = _bandData;
				_band = null;
				_bandData = null;
			}
		}

		private unsafe static void Halves(float[] s0, float[] s1, float[] s2, float[] s3, int n, ushort[] into)
		{
			fixed (float* ptr = s0)
			{
				fixed (float* ptr2 = s1)
				{
					fixed (float* ptr3 = s2)
					{
						fixed (float* ptr4 = s3)
						{
							fixed (ushort* ptr5 = into)
							{
								uint* ptr6 = (uint*)ptr;
								uint* ptr7 = (uint*)ptr2;
								uint* ptr8 = (uint*)ptr3;
								uint* ptr9 = (uint*)ptr4;
								ushort* ptr10 = ptr5;
								int num = 0;
								while (num < n)
								{
									*ptr10 = Half(ptr6[num]);
									ptr10[1] = Half(ptr7[num]);
									ptr10[2] = Half(ptr8[num]);
									ptr10[3] = Half(ptr9[num]);
									num++;
									ptr10 += 4;
								}
							}
						}
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ushort Half(uint f)
		{
			uint num = (f >> 16) & 0x8000;
			uint num2 = f & 0x7FFFFFFF;
			if (num2 >= 1199566848)
			{
				return (ushort)(num | 0x7BFF);
			}
			if (num2 < 947912704)
			{
				return (ushort)num;
			}
			num2 += 4095 + ((num2 >> 13) & 1);
			return (ushort)(num | (num2 - 939524096 >> 13));
		}

		private void Release()
		{
			Slot[] slots = _slots;
			foreach (Slot slot in slots)
			{
				if (slot.Tex != null)
				{
					UnityEngine.Object.Destroy(slot.Tex);
				}
				slot.Tex = null;
				slot.Version = 0;
			}
			DropBand();
			if (_staging != null)
			{
				UnityEngine.Object.Destroy(_staging);
			}
			_staging = null;
			_shown = 0;
			_whole = 0;
			_easing = 0;
			_w = (_h = 0);
		}

		public void Dispose()
		{
			Release();
			_spare = null;
		}
	}
}
