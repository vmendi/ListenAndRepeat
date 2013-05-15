using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace ListenAndRepeat
{
	public class SuggestionModel
	{
		public event EventHandler SuggestionChanged;

		public SuggestionModel()
		{
			new Thread (LoadSuggestions).Start ();
		}

		private void LoadSuggestions()
		{
			mSuggestions = File.ReadAllLines("3esl.txt");
		}

		public List<string> Suggestion
		{
			get { 
				lock (mCurrentSuggestionSync) {
					return mCurrentSuggestion; 
				}
			}
		}

		public void NewSuggestion(string target)
		{
			if (mSuggestions == null)
				return;

			lock (mSearchInProgressSync) 
			{
				// Is there another search in progress?
				if (mSearchThread != null)
					mSearchThread.Abort ();

				mSearchThread = new Thread (SearchThread);
				mSearchThread.Start (target);
			}
		}

		private void SearchThread(object target)
		{
			int index = Array.BinarySearch (mSuggestions, (string)target);

			if (index < 0)
				index = ~index;

			// We select words from index to index + X
			int startIdx = index;

			if (startIdx >= mSuggestions.Length - NUM_SUGGESTIONS - 1)
				startIdx = mSuggestions.Length - NUM_SUGGESTIONS - 1;

			lock (mCurrentSuggestionSync) 
			{
				mCurrentSuggestion = new List<string> (mSuggestions.Skip(startIdx).Take(NUM_SUGGESTIONS));
			}

			lock (mSearchInProgressSync) 
			{
				mSearchThread = null;
			}

			var method = SuggestionChanged;
			if (method != null)
				SuggestionChanged(this, EventArgs.Empty);
		}

		private const int NUM_SUGGESTIONS = 20;

		string[] mSuggestions;
		List<string> mCurrentSuggestion = new List<string>();
		private readonly object mCurrentSuggestionSync = new object();

		Thread mSearchThread;
		private readonly object mSearchInProgressSync = new object();
	}
}

