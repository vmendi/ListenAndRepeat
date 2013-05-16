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
				// Is there another search in progress?
				if (mWaitCallback != null) 
					throw new Exception ("Search in progress");

				mCurrentResult = new SearchCompletedEventArgs(word);

				mWaitCallback = new WaitCallback (SearchThread);
				ThreadPool.QueueUserWorkItem (mWaitCallback);
			}
		}

		public bool IsSearching
		{
			get 
			{
				lock (mSearchInProgressSync) {
					return mWaitCallback != null; 
				}
			}
		}

		private void SearchThread(object state) 
		{
			try
			{
				var theSearchURL = String.Format("http://ahdictionary.com/word/search.html?q={0}", mCurrentResult.Word);
				string htmlText = null;

				mWebRequest = (HttpWebRequest)HttpWebRequest.Create(new Uri(theSearchURL));
				mWebRequest.Timeout = 10000;

				using (var webResponse = mWebRequest.GetResponse())
				{
					using (var responseStream = webResponse.GetResponseStream())
					{
						responseStream.ReadTimeout = 10000;
						using (var streamReader = new StreamReader(responseStream))
						{
							htmlText = streamReader.ReadToEnd();
						}
					}
				}

				if (!htmlText.Contains("No word definition found"))
				{
					mCurrentResult.Found = true;
					
					ParseWavFiles(htmlText);
					DownloadWavFiles();
				}
			} 
			catch (WebException exc) {
				Console.WriteLine (exc.ToString());
				mCurrentResult.IsNetworkError = true;
			}
			catch (Exception exc) {
				Console.WriteLine (exc.ToString());
				mCurrentResult.IsGeneralError = true;
			}

			var currentResult = mCurrentResult;

			lock (mSearchInProgressSync)
			{
				mWaitCallback = null;
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
				byte[] theDataBytes = null;

				mWebRequest = (HttpWebRequest)HttpWebRequest.Create (new Uri("http://ahdictionary.com" + wave.Item1));
				mWebRequest.Timeout = 10000;

				using (var webResponse = mWebRequest.GetResponse())
				{
					using (var responseStream = webResponse.GetResponseStream())
					{
						responseStream.ReadTimeout = 10000;
						using (var binaryReader = new BinaryReader(responseStream)) 
						{
							theDataBytes = binaryReader.ReadBytes((int)responseStream.Length);
						}
					}
				}

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

		private WaitCallback mWaitCallback;
		private readonly object mSearchInProgressSync = new object();
		private SearchCompletedEventArgs mCurrentResult;
		private HttpWebRequest mWebRequest;
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

