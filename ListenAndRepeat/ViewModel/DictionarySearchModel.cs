using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using MonoTouch.Foundation;

namespace ListenAndRepeat.ViewModel
{
	public class DictionarySearchModel
	{
		public event EventHandler<SearchCompletedEventArgs> SearchCompleted;

		public void Search(string word)
		{
			lock (mSearchInProgressSync)
			{
				Console.WriteLine ("Starting " + word);

				// Is there another search in progress?
				if (mCurrentThread != null) 
					mCurrentThread.Abort ();

				mCurrentResult = new SearchCompletedEventArgs(word);
				mCurrentThread = new Thread(SearchThread);
				mCurrentThread.Start();
			}
		}

		public bool IsSearching
		{
			get 
			{
				lock (mSearchInProgressSync) {
					return mCurrentThread != null; 
				}
			}
		}

		private void SearchThread() 
		{
			try
			{
				var theSearchURL = String.Format("http://ahdictionary.com/word/search.html?q={0}", mCurrentResult.Word);

				Console.WriteLine ("Step 1 " + mCurrentResult.Word);
				var htmlText = mWebClient.DownloadString(new Uri(theSearchURL));
				Console.WriteLine ("Step 2 " + mCurrentResult.Word);
								
				if (!htmlText.Contains("No word definition found"))
				{
					mCurrentResult.Found = true;
					Console.WriteLine ("Step 3 " + mCurrentResult.Word);
					
					ParseWavFiles(htmlText);
					DownloadWavFiles();
				}
			} 
			catch (WebException e) {
				Console.WriteLine (e.ToString());
				mCurrentResult.IsNetworkError = true;
			}
			catch (Exception e) {
				Console.WriteLine (e.ToString());
				mCurrentResult.IsGeneralError = true;
			}

			Console.WriteLine ("Step 10 " + mCurrentResult.Word);
			var currentResult = mCurrentResult;

			lock (mSearchInProgressSync)
			{
				mCurrentThread = null;
			}

			var method = SearchCompleted;
			if (method != null)
				method(this, currentResult);
		}

		private void DownloadWavFiles()
		{
			Directory.CreateDirectory(MainModel.GetSoundsDirectory());

			foreach (var wave in mCurrentResult.Waves)
			{
				Console.WriteLine ("Step 4 " + wave.Item1);
				var theDataBytes = mWebClient.DownloadData("http://ahdictionary.com" + wave.Item1);
				Console.WriteLine ("Step 5 " + wave.Item1);

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
				mCurrentResult.Waves.Add(Tuple.Create(match.Groups[0].Value, match.Groups[1].Value + ".wav"));
				match = match.NextMatch();
			}
		}

		private Thread mCurrentThread;
		private readonly object mSearchInProgressSync = new object();
		private SearchCompletedEventArgs mCurrentResult;
		private MyWebClient mWebClient = new MyWebClient();

		private class MyWebClient : WebClient
		{
			public int Timeout { get; set; }

			public MyWebClient() : this(3000) { }
			public MyWebClient(int timeout) { this.Timeout = timeout; }

			protected override WebRequest GetWebRequest(Uri address)
			{
				var request = base.GetWebRequest(address);
				if (request != null)
					request.Timeout = this.Timeout;
				return request;
			}
		}
	}
	
	public class SearchCompletedEventArgs : EventArgs
	{
		public string Word { get { return mWord; } }
		public List<Tuple<string, string>> Waves { get { return mWaves; } }

		public bool Found { get; set; }
		public bool IsGeneralError { get; set; }
		public bool IsNetworkError { get; set; }
		
		public SearchCompletedEventArgs(string word)
		{
			mWord = word;
			mWaves = new List<Tuple<string, string>>();
		}
		
		private string mWord;
		private List<Tuple<string, string>> mWaves;
	}
}

