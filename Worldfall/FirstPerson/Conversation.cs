using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Conversation
	{
		public const float TypeRate = 55f;

		public bool Open;

		public Actor With;

		public Actor You;

		public string Name;

		public string Relation;

		public Voice Voice;

		public readonly List<string> Pages = new List<string>();

		public int Page;

		public float LineStart;

		public bool Romantic;

		public readonly List<Choice> Choices = new List<Choice>();

		private readonly List<Choice> _later = new List<Choice>();

		public int Selected;

		public int Sets;

		public TalkPanel Panel;

		public float OpenedAt;

		public int Said;

		private string _lastTopic;

		private int _topicsSaid;

		private readonly HashSet<string> _asked = new HashSet<string>();

		public Voices.Persona Persona;

		public Voices.Scene Scene;

		public Voices.Slots Slots;

		public Mind.Rec Memory;

		public int Beats;

		public string MoodWord;

		public string CardSuffix;

		public string Welcome;

		public bool EndAfter;

		public FamilyTree Tree;

		public Actor Marked;

		public string MarkedName;

		public Action<string> Toast;

		public Action<Actor> Hearts;

		public Func<Actor, float> DistanceTo;

		public bool AsIs;

		public int YoursUpTo;

		public string Stranger;

		public int NameAt;

		private float _thinkNext;

		public Action Ended;

		public string LastTopic
		{
			get
			{
				return _lastTopic;
			}
			set
			{
				_lastTopic = value;
				_topicsSaid++;
			}
		}

		public string Line
		{
			get
			{
				if (Page < 0 || Page >= Pages.Count)
				{
					return null;
				}
				return Pages[Page];
			}
		}

		public bool MorePages => Page + 1 < Pages.Count;

		public bool LineIsYours => Page < YoursUpTo;

		public string Shown
		{
			get
			{
				if (Stranger == null || Page >= NameAt)
				{
					return Name;
				}
				return Stranger;
			}
		}

		public float Age => Time.unscaledTime - LineStart;

		public bool Typed
		{
			get
			{
				if (Line != null)
				{
					return Age * 55f >= (float)Line.Length;
				}
				return true;
			}
		}

		public bool Thinking => Age < 0f;

		public void Think(float seconds)
		{
			_thinkNext = Mathf.Max(_thinkNext, seconds);
		}

		public void Start(Actor you, Actor npc, bool hello = true)
		{
			Open = true;
			You = you;
			With = npc;
			Panel = TalkPanel.None;
			EndAfter = false;
			Said = 0;
			_thinkNext = 0f;
			OpenedAt = Time.unscaledTime;
			bool flag = hello && !PeopleMet.Knows(you, npc);
			Name = Dialogue.NameOf(npc);
			Stranger = (flag ? PeopleMet.Looks(npc) : null);
			Welcome = null;
			try
			{
				Welcome = Romance.Meet(npc, you);
			}
			catch (Exception ex)
			{
				Log.Error("talk: romance meet: " + ex.Message);
			}
			Relation = Romance.WithStanding(Dialogue.RelationTo(npc, you), npc, you);
			Voice = Dialogue.VoiceOf(npc, you);
			Chronicle.Note(npc, you, 1);
			PeopleMet.Note(you, npc);
			_asked.Clear();
			Beats = 0;
			try
			{
				Memory = Mind.Talked(npc, you);
				Persona = Personas.Of(npc, you, Voice);
				Scene = Personas.SceneNow(you);
				Slots = Personas.SlotsFor(npc, you, Voice);
				MoodWord = Voices.MoodWord(in Persona);
				CardSuffix = Dialogue.CardSuffix(this);
			}
			catch (Exception ex2)
			{
				Log.Error("talk: persona: " + ex2.Message);
				Slots = null;
				MoodWord = (CardSuffix = null);
			}
			if (flag)
			{
				Dialogue.Introduce(this);
			}
			else
			{
				Dialogue.Greet(this);
			}
		}

		public void Hello(string yours, string theirs, bool theirsAsIs)
		{
			Pages.Clear();
			Pages.Add(Renames.Apply(yours));
			Pages.Add(theirsAsIs ? Renames.Apply(theirs) : Dialogue.Speak(Voice, Renames.Apply(theirs)));
			AsIs = theirsAsIs;
			YoursUpTo = 1;
			NameAt = 1;
			Page = 0;
			LineStart = Time.unscaledTime;
			_thinkNext = 0f;
			Romantic = false;
			Said += Pages.Count;
			Choices.Clear();
			ShowPending();
		}

		public void Update()
		{
			if (Open && LineIsYours && Typed && Line != null && Age > (float)Line.Length / 55f + 0.6f)
			{
				Next();
			}
		}

		public void End()
		{
			if (Ended != null)
			{
				Ended();
			}
			else
			{
				Close();
			}
		}

		public void Close()
		{
			if (Memory != null && With != null)
			{
				try
				{
					Mind.Save(With, Memory);
				}
				catch
				{
				}
			}
			Memory = null;
			Open = false;
			Panel = TalkPanel.None;
			YoursUpTo = 0;
			Stranger = null;
			Pages.Clear();
			Choices.Clear();
			_later.Clear();
			With = null;
			You = null;
		}

		public void Say(params string[] pages)
		{
			Introduced();
			Pages.Clear();
			AsIs = false;
			foreach (string text in pages)
			{
				if (!string.IsNullOrEmpty(text))
				{
					Pages.Add(Dialogue.Speak(Voice, Renames.Apply(text)));
				}
			}
			if (Pages.Count == 0)
			{
				Pages.Add(Dialogue.Speak(Voice, "..."));
			}
			Page = 0;
			LineStart = Time.unscaledTime + _thinkNext;
			_thinkNext = 0f;
			Romantic = false;
			Said += Pages.Count;
			Choices.Clear();
			ShowPending();
		}

		public void SayAsIs(string line)
		{
			Introduced();
			Pages.Clear();
			Pages.Add(Renames.Apply(line));
			AsIs = true;
			Page = 0;
			LineStart = Time.unscaledTime;
			Said++;
			Choices.Clear();
			ShowPending();
		}

		private void Introduced()
		{
			YoursUpTo = 0;
			Stranger = null;
		}

		public void Offer(List<Choice> choices)
		{
			_later.Clear();
			_later.AddRange(choices);
			foreach (Choice item in _later)
			{
				item.Label = Renames.Apply(item.Label);
			}
			Choices.Clear();
			ShowPending();
		}

		private void ShowPending()
		{
			Choices.Clear();
			if (MorePages)
			{
				Choices.Add(new Choice("Go on...", Next, ChoiceTone.Back));
			}
			else if (EndAfter)
			{
				Choices.Add(new Choice("Goodbye", End, ChoiceTone.Leave));
			}
			else
			{
				Choices.AddRange(_later);
			}
			Sets++;
			Selected = 0;
			while (Selected < Choices.Count - 1 && !Choices[Selected].Enabled)
			{
				Selected++;
			}
		}

		public void Next()
		{
			if (!Typed)
			{
				LineStart = Time.unscaledTime - 999f;
			}
			else if (MorePages)
			{
				Page++;
				LineStart = Time.unscaledTime;
				ShowPending();
			}
		}

		public void Pick(int index)
		{
			if (index < 0 || index >= Choices.Count)
			{
				return;
			}
			Choice choice = Choices[index];
			if (!choice.Enabled)
			{
				Toast?.Invoke(choice.Note ?? "You can't say that now.");
				return;
			}
			if (!Typed && choice.Label != "Go on...")
			{
				LineStart = Time.unscaledTime - 999f;
				return;
			}
			int topicsSaid = _topicsSaid;
			try
			{
				choice.Act?.Invoke();
			}
			catch (Exception ex)
			{
				Log.Error("talk: " + choice.Label + ": " + ex);
				Say("Hm? Where was I...");
				Offer(Dialogue.Topics(this));
				return;
			}
			if (_topicsSaid == topicsSaid || !Open || _lastTopic == null)
			{
				return;
			}
			bool again = !_asked.Add(_lastTopic);
			try
			{
				Dialogue.AfterTopic(this, _lastTopic, again);
			}
			catch (Exception ex2)
			{
				Log.Error("talk: after " + _lastTopic + ": " + ex2.Message);
			}
		}

		public void Move(int step, bool wrap = true)
		{
			if (Choices.Count == 0)
			{
				return;
			}
			int num = Selected;
			for (int i = 0; i < Choices.Count; i++)
			{
				num += step;
				if (num < 0 || num >= Choices.Count)
				{
					if (!wrap)
					{
						break;
					}
					num = (num + Choices.Count) % Choices.Count;
				}
				if (Choices[num].Enabled)
				{
					Selected = num;
					break;
				}
			}
		}
	}
}
