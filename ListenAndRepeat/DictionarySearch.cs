using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using MonoTouch.Foundation;

namespace ListenAndRepeat
{
	public delegate void OnSearchCompletedDelegate(DictionaryWord newWord);
	
	public class DictionarySearch
	{
		static OnSearchCompletedDelegate OnSearchCompleted;
		
		public DictionarySearch(OnSearchCompletedDelegate onCompleted)
		{
			DictionarySearch.OnSearchCompleted = onCompleted;
		}
		
		public void Search(string word)
		{
			mCurrentWord = null;

			new Thread(() =>
			{
				try
				{
					string theSearchURL = String.Format("http://ahdictionary.com/word/search.html?q={0}", word);
					string htmlText = new WebClient().DownloadString(new Uri(theSearchURL));
						
					if (!htmlText.Contains("No word definition found"))
					{
						mCurrentWord = new DictionaryWord(word);

						ParseWavFiles(htmlText);
						DownloadWavFiles();

						OnSearchCompleted(mCurrentWord);
					}
					else
					{
						OnSearchCompleted(null);
					}
				} 
				catch (Exception) {
					OnSearchCompleted(null);
				}
			}).Start();
		}

		private void CreateSoundsDirectory()
		{
			var documentsPath =	Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			var soundsPath = Path.Combine(documentsPath, "..", "Library", "Sounds");
			Directory.CreateDirectory(soundsPath);
		}

		private void DownloadWavFiles()
		{
			CreateSoundsDirectory();

			foreach (var wave in mCurrentWord.Waves)
			{
				var theDataBytes = new WebClient().DownloadData("http://ahdictionary.com" + wave.Item1);

				// The Library/Cache/ folder might be cleaned out, so it's better to store the files in our
				// own folder and tell the OS to not backup them
				string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				string finalPath = Path.Combine(documentsPath, "..", "Library", "Sounds", wave.Item2);
				File.WriteAllBytes(finalPath, theDataBytes);
				NSFileManager.SetSkipBackupAttribute(finalPath, true);
			}
		}

		private void ParseWavFiles(string text)
		{
			// /application/resources/wavs/T0172200.wav
			Match match = Regex.Match(text, @"/application/resources/wavs/(.*)\.wav", RegexOptions.IgnoreCase);

			while (match != null && match.Success)
			{
				mCurrentWord.Waves.Add(Tuple.Create(match.Groups[0].Value, match.Groups[1].Value + ".wav"));
				match = match.NextMatch();
			}
		}

		private DictionaryWord mCurrentWord;
	}

	public class DictionaryWord
	{
		public string Word { get { return mWord; } }
		public List<Tuple<string, string>> Waves { get { return mWaves; } }
		
		public DictionaryWord(string word)
		{
			mWord = word;
			mWaves = new List<Tuple<string, string>>();
		}
		
		private string mWord;
		private List<Tuple<string, string>> mWaves;
	}
}

