using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using MonoTouch.Foundation;
using System.Linq;
using System.Xml.Linq;

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
                SearchMerrianWebster();
                DownloadWavFiles();
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

        private void SearchMerrianWebster()
        {
            var theSearchURL = String.Format("http://www.dictionaryapi.com/api/v1/references/learners/xml/{0}?key=3715e89c-54db-4672-b628-0b011efd18a2", mCurrentResult.Word.ToLower());
            var htmlText = DownloadTextFromUrl(theSearchURL);

            var doc = XDocument.Parse(htmlText);
            var entries = doc.Descendants("entry");

            foreach (var entry in entries)
            {
                ExtractMerrianWebsterEntry(entry, "hw");

                // Maybe it's an uro (Theoretical => Theoretically)
                foreach (var uro in entry.Elements("uro"))
                {
                    ExtractMerrianWebsterEntry(uro, "ure");
                }
            }

            // It's possible that an entry has the same wave as another (wind)
            mCurrentResult.Waves = mCurrentResult.Waves.Distinct().ToList();
        }

        private void ExtractMerrianWebsterEntry(XElement entry, string childElementName)
        {
            if (entry.Element(childElementName) == null)
                return;

            var entryText = entry.Element(childElementName).Value.Replace("*", "").ToLower();

            if (entryText == mCurrentResult.Word.ToLower())
            {
                mCurrentResult.Found |= ExtractMerrianWebsterSounds(entry);

                if (mCurrentResult.Found && entry.Element("pr") != null)
                {
                    if (mCurrentResult.Pronunciation == null)
                        mCurrentResult.Pronunciation = entry.Element("pr").Value;
                    else
                        mCurrentResult.Pronunciation += ", " + entry.Element("pr").Value;
                }
            }
        }

        bool ExtractMerrianWebsterSounds(XElement entry)
        {
            if (entry.Element("sound") == null)
                return false;

            var sounds = entry.Element("sound").Descendants("wav");

            foreach (var sound in sounds)
            {
                var theWavName = sound.Value;
                var subDir = mCurrentResult.Word[0].ToString().ToLower();

                if (theWavName.StartsWith("gix"))
                    subDir = "gix";
                else if (theWavName.StartsWith("gg"))
                    subDir = "gg";

                var baseURL = "http://media.merriam-webster.com/soundc11/" + subDir + "/";
                mCurrentResult.Waves.Add(Tuple.Create(baseURL + theWavName, theWavName));
            }

            return sounds.Count() != 0;
        }

        string DownloadTextFromUrl(string theSearchURL)
        {
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
            return htmlText;
        }

		private void DownloadWavFiles()
		{
			Directory.CreateDirectory(MainModel.GetSoundsDirectory());

			foreach (var wave in mCurrentResult.Waves)
			{
                Console.WriteLine("Downloading " + wave);

				byte[] theDataBytes = null;

				mWebRequest = (HttpWebRequest)HttpWebRequest.Create (new Uri(wave.Item1));
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

        
        private void SearchAmericanHeritage()
        {
            var theSearchURL = String.Format("http://ahdictionary.com/word/search.html?q={0}", mCurrentResult.Word);
            var htmlText = DownloadTextFromUrl(theSearchURL);

            if (!htmlText.Contains("No word definition found"))
            {
                mCurrentResult.Found = true;

                ParseWavFilesAmericanHeritage(htmlText);
                ParsePronunciationAmericanHeritage(htmlText);
            }
        }

        /*
        private void AddWavesAmericanHeritage()
        {
            var theSearchURL = String.Format("http://ahdictionary.com/word/search.html?q={0}", mCurrentResult.Word);
            var htmlText = DownloadTextFromUrl(theSearchURL);

            if (!htmlText.Contains("No word definition found"))
            {
                // We are only adding new waves, this method assumes another dictionary is the main one.
                // Therefore we make sure that what we found matches with the exact word we are looking 
                // up (not a derivate).

                ParseWavFilesAmericanHeritage(htmlText);
            }
        }
        */

		private void ParseWavFilesAmericanHeritage(string html)
		{
			// /application/resources/wavs/T0172200.wav
			Match match = Regex.Match(html, @"/application/resources/wavs/(.*)\.wav", RegexOptions.IgnoreCase);

			while (match != null && match.Success)
			{
                mCurrentResult.Waves.Add(Tuple.Create("http://ahdictionary.com" + match.Groups[0].Value, match.Groups[1].Value + ".wav"));
				match = match.NextMatch();
			}
		}

		private void ParsePronunciationAmericanHeritage(string html)
		{
			Match match = Regex.Match(html, " \\((.+?)\\)", RegexOptions.IgnoreCase);

			if (match != null) 
			{
				mCurrentResult.Pronunciation = Regex.Replace(match.Groups[1].Value, "<(.+?)>", "").Replace("", "'").Replace("", "ʊ").Replace("", "u:");
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
		public string Pronunciation { get; set; }
        public List<Tuple<string, string>> Waves { get; set; }

		public bool Found { get; set; }
		public bool IsGeneralError { get; set; }
		public bool IsNetworkError { get; set; }
		
		public SearchCompletedEventArgs(string word)
		{
			mWord = word;
			Waves = new List<Tuple<string, string>>();
		}
		
		private string mWord;
	}
}

