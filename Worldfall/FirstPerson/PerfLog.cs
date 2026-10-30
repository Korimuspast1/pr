using FirstPerson.Core;

namespace FirstPerson
{
	internal sealed class PerfLog
	{
		private double _frame;

		private double _collect;

		private double _render;

		private double _upload;

		private double _terrain;

		private double _objects;

		private double _post;

		private double _models;

		private double _sampler;

		private double _features;

		private double _late;

		private double _gui;

		private double _wait;

		private double _land;

		private double _landRead;

		private double _landLight;

		private long _landTiles;

		private long _landFull;

		private int _beside;

		private double _prep;

		private double _shadowMap;

		private double _bin;

		private double _raster;

		private double _modelShadows;

		private double _lights;

		private double _overlay;

		private double _finish;

		private double _weather;

		private double _tris;

		private double _hidden;

		private int _rebuilds0 = -1;

		private int _frames;

		private float _since;

		public void AddWorld(double samplerMs, double featuresMs)
		{
			_sampler += samplerMs;
			_features += featuresMs;
		}

		public void AddLand((double read, double light, long tiles, long full) t)
		{
			_landRead += t.read;
			_landLight += t.light;
			_landTiles += t.tiles;
			_landFull += t.full;
		}

		public void AddLate(double ms)
		{
			_late += ms;
		}

		public void AddGui(double ms)
		{
			_gui += ms;
		}

		public void Add(float frameSeconds, double collectMs, double renderMs, double uploadMs, FrameRenderer r)
		{
			Add(frameSeconds, collectMs, renderMs, uploadMs, r, beside: false, 0.0, 0.0);
		}

		public void AddBeside(float frameSeconds, double collectMs, double waitMs, double landMs, double drawMs, double uploadMs, FrameRenderer r)
		{
			Add(frameSeconds, collectMs, drawMs, uploadMs, r, beside: true, waitMs, landMs);
		}

		private void Add(float frameSeconds, double collectMs, double renderMs, double uploadMs, FrameRenderer r, bool beside, double waitMs, double landMs)
		{
			_frame += (double)frameSeconds * 1000.0;
			_collect += collectMs;
			_render += renderMs;
			_upload += uploadMs;
			if (beside)
			{
				_beside++;
				_wait += waitMs;
				_land += landMs;
			}
			_terrain += r.TerrainMs;
			_objects += r.ObjectsMs;
			_post += r.PostMs;
			_models += r.MeshMs;
			_prep += r.MeshPrepMs;
			_shadowMap += r.ShadowMs;
			_bin += r.MeshBinMs;
			_raster += r.MeshRasterMs - r.MeshBinMs;
			_modelShadows += r.ModelShadowMs;
			_lights += r.LightsMs;
			_overlay += r.OverlayMs;
			_finish += r.FinishMs;
			_weather += r.WeatherMs;
			_tris += r.MeshTriangles;
			_hidden += r.MeshesHidden;
			if (_rebuilds0 < 0)
			{
				_rebuilds0 = r.ShadowRebuilds;
			}
			_frames++;
			_since += frameSeconds;
			if (!(_since < 10f))
			{
				double num = 1.0 / (double)_frames;
				string text = $"drawing {_render * num:F1} (terrain {_terrain * num:F1}, sprites {_objects * num:F1}, models {_models * num:F1}, " + $"light and finish {(_post - _models) * num:F1})";
				if (_beside == 0)
				{
					Log.Info($"perf: {1000.0 / (_frame * num):F0} fps (frame {_frame * num:F1} ms); first person {(_collect + _render + _upload) * num:F1} ms of it: " + $"scene {_collect * num:F1}, {text}, upload {_upload * num:F1}; {r.Width}x{r.Height}");
					double num2 = _late - _sampler - _features - _collect - _render - _upload;
					Log.Info($"perf: rest of the frame: terrain sampler {_sampler * num:F1}, features {_features * num:F1}, other mod work {num2 * num:F1}, " + $"HUD {_gui * num:F1}; game and Unity {(_frame - _late - _gui) * num:F1} ms");
				}
				else
				{
					Log.Info($"perf: {1000.0 / (_frame * num):F0} fps (frame {_frame * num:F1} ms); first person drawn beside the game ({_beside} of {_frames} frames): " + $"on the game's thread scene {_collect * num:F1}, waiting for the drawing {_wait * num:F1}, upload {_upload * num:F1}; " + $"beside it land {_land * num:F1}, {text}; {r.Width}x{r.Height}");
					double num3 = _render * (double)(_frames - _beside) / (double)_frames;
					double num4 = _late - _sampler - _features - _collect - _wait - _upload - num3;
					Log.Info($"perf: rest of the frame: terrain sampler {_sampler * num:F1} (beside: land {_land * num:F1}), features {_features * num:F1}, " + $"other mod work {num4 * num:F1}, HUD {_gui * num:F1}; game and Unity {(_frame - _late - _gui) * num:F1} ms");
				}
				Log.Info(string.Format("perf: land: reading the game's tiles {0:F2} ms ({1:F0} tiles a frame{2}), relighting {3:F2} ms", _landRead * num, (double)_landTiles * num, WorldSampler.Quick ? $", {(double)_landFull * num:F0} of them changed and read in full" : "", _landLight * num));
				Log.Info($"perf: models: prep {_prep * num:F1}, shadow map {_shadowMap * num:F1} ({r.ShadowRebuilds - _rebuilds0} rebuilds), binning {_bin * num:F1}, " + $"raster {_raster * num:F1}, their shadows {_modelShadows * num:F1}, {_tris * num:F0} triangles ({_hidden * num:F0} models hidden behind the land skipped); light and finish: lights {_lights * num:F1}, " + $"hands {_overlay * num:F1}, weather {_weather * num:F1}, palette {_finish * num:F1}");
				Reset();
			}
		}

		public void Reset()
		{
			_frame = (_collect = (_render = (_upload = (_terrain = (_objects = (_post = (_models = 0.0)))))));
			_sampler = (_features = (_late = (_gui = 0.0)));
			_wait = (_land = 0.0);
			_landRead = (_landLight = 0.0);
			_landTiles = (_landFull = 0L);
			_beside = 0;
			_prep = (_shadowMap = (_bin = (_raster = (_modelShadows = (_lights = (_overlay = (_finish = (_weather = (_tris = (_hidden = 0.0))))))))));
			_rebuilds0 = -1;
			_frames = 0;
			_since = 0f;
		}
	}
}
