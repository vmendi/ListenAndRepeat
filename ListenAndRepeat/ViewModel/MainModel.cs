using System;
using System.Collections.Generic;
using SQLite;
using System.IO;
using System.Linq;
using ListenAndRepeat.Util;

namespace ListenAndRepeat.ViewModel
{
	public class MainModel
	{
		static public void RegisterServices()
		{
			ServiceContainer.Register<MainModel>();
			ServiceContainer.Register<SearchModel>();
			ServiceContainer.Register<PlaySoundModel>();
			ServiceContainer.Register<SuggestionModel>();
		}

		public EventHandler WordsListChanged;

		public MainModel()
		{
			// This forces the instantiation
			mDictionarySearcher = ServiceContainer.Resolve<SearchModel> ();
			mSuggestionModel = ServiceContainer.Resolve<SuggestionModel> ();
			mPlaySoundModel = ServiceContainer.Resolve<PlaySoundModel> ();

			mDictionarySearcher.SearchCompleted += OnSearchCompleted;

			var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			mDatabasePath = Path.Combine(documentsPath, "..", "Library", "db_sqlite-net.db");

			bool isFirstLoad = false;

			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
				// https://github.com/praeclarum/sqlite-net/wiki
				// In general, it will execute an automatic migration
				conn.CreateTable<WordModel>();
                conn.CreateTable<WaveModel>();

				InitialRefresh(conn);
				LoadNextWord(conn);
				RefreshWordsList(conn);

				isFirstLoad = conn.Table<WordModel>().Count() == 0;
			}

			if (isFirstLoad)
			{
				AddWord("Thanks");
				AddWord("For");
				AddWord("Downloading");
			}
		}
	
		public void RemoveWordAt(int idx)
		{
			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
                conn.RunInTransaction(() =>
                {
                    var wordID = mWordsList[idx].ID;
                    var theWaves = conn.Table<WaveModel>().Where(wave => wave.WordID == wordID);

                    foreach (var wave in theWaves)
                    {
                        conn.Delete(wave);
                        File.Delete(Path.Combine ("file://", GetSoundsDirectory(), wave.WaveFileName));
                    }

                    conn.Delete(mWordsList[idx]);
                });

				RefreshWordsList(conn);
			}
		}

		public void ReorderWords(int firstIdx, int secondIdx)
		{
			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
				conn.RunInTransaction(() =>
				{
					var firstID = mWordsList[firstIdx].ID;
					var secondID = mWordsList[secondIdx].ID;
					var firstWord = conn.Table<WordModel>().Where(w => w.ID == firstID).First();
					var secondWord = conn.Table<WordModel>().Where(w => w.ID == secondID).First();

					var swap = firstWord.IdxOrder;
					firstWord.IdxOrder = secondWord.IdxOrder;
					secondWord.IdxOrder = swap;

					conn.Update(firstWord);
					conn.Update(secondWord);
				});
				RefreshWordsList(conn);
			}
		}

		private void InitialRefresh(SQLiteConnection conn)
		{
			if (conn.Table<WordModel>().Count() != 0)
				mMaxIdxOrder = conn.Table<WordModel>().Max(w => w.IdxOrder);
			else
				mMaxIdxOrder = 0;

			// Words that need to be Searched/Downloaded
            var allWords = conn.Table<WordModel>().ToList();

			foreach (WordModel word in allWords)
            {
                if (NeedsSearch(word))
                    word.Status = WordStatus.NOT_STARTED;
            }

            conn.UpdateAll(allWords.ToList());
		}

		private bool NeedsSearch(WordModel word)
        {
			if (word.Status == WordStatus.QUERYING_AH || word.Status == WordStatus.NETWORK_ERROR)
				return true;

            List<WaveModel> theWaves = GetWavesForWord(word);

            foreach (var wave in theWaves)
            {
			    var localFile = Path.Combine (MainModel.GetSoundsDirectory (), wave.WaveFileName);
			    if (!File.Exists(localFile))
    				return true;
            }

			return false;
		}

        private List<WaveModel> GetWavesForWord(WordModel word)
        {
            using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
            {
                return conn.Table<WaveModel>().Where(w => w.WordID == word.ID).ToList();
            }
        }

		public void ActionOnWord(WordModel theWord)
		{
			if (theWord.Status == WordStatus.COMPLETE) 
			{
                mPlaySoundModel.PlaySound(theWord, GetWavesForWord(theWord));
			}
			else if (!mDictionarySearcher.IsSearching)
			{
				using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
				{
					theWord.Status = WordStatus.QUERYING_AH;
					conn.Update (theWord);
					mDictionarySearcher.Search (theWord.Word);
					RefreshWordsList (conn);
				}
			}
		}

		static public string GetSoundsDirectory()
		{
			var documentsPath =	Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			return Path.Combine(documentsPath, "..", "Library", "Sounds");
		}

		private void OnSearchCompleted(object sender, SearchCompletedEventArgs e)
		{
			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
                conn.RunInTransaction(() =>
                {
    				var theWord = FindWord(conn, e.Word);

    				// Did the user delete the word?
    				if (theWord != null)
    				{
    					if (e.IsGeneralError)
    					{
    						theWord.Status = WordStatus.GENERAL_ERROR;
    					}
    					else
    					if (e.IsNetworkError)
    					{
    						theWord.Status = WordStatus.NETWORK_ERROR;
    					}
    					else
    					if (!e.Found || e.Waves.Count == 0)
    					{
    						theWord.Status = WordStatus.NOT_FOUND;
    					}
    					else
    					{
                            theWord.Status = WordStatus.COMPLETE;
                            theWord.Pronunciation = e.Pronunciation;

                            foreach (var wave in e.Waves)
                            {
                                conn.Insert(new WaveModel() { WordID = theWord.ID, WaveFileName = wave.Item2 });
                            }
    					}

    					conn.Update (theWord);
    				}
                });

				LoadNextWord (conn);
				RefreshWordsList (conn);
			}
		}

		public List<WordModel> WordsList
		{
			get { return mWordsList; }
		}

		private void RefreshWordsList(SQLite.SQLiteConnection conn)
		{
			mWordsList = GetSortedWordList(conn);

			var method = WordsListChanged;
			if (method != null)
				method(this, EventArgs.Empty);
		}

		
		private void LoadNextWord(SQLite.SQLiteConnection conn)
		{
			if (!mDictionarySearcher.IsSearching)
			{
				WordModel nextWord = FindWordByNextNotStarted (conn);

				if (nextWord != null)
				{
					nextWord.Status = WordStatus.QUERYING_AH;
					conn.Update (nextWord);
					mDictionarySearcher.Search (nextWord.Word);
				}
			}
		}

		public void AddWord(string word)
		{
			word = Sanitize(word);

			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
				if (FindWord (conn, word) != null)
					return;

				var newWordModel = new WordModel();
				newWordModel.Word = word;
				newWordModel.Status = WordStatus.NOT_STARTED;
                newWordModel.Pronunciation = "";
				newWordModel.IdxOrder = mMaxIdxOrder++;

				conn.Insert(newWordModel);

				LoadNextWord (conn);
				RefreshWordsList(conn);
			}
		}

		private string Sanitize(string word)
		{
			word = word.Trim();

			// Capitalize
			return word.Substring(0, 1).ToUpper() + word.Substring(1);
		}

		private WordModel FindWordByNextNotStarted(SQLiteConnection conn)
		{
			return GetSortedWordList(conn).Where(w => w.Status == WordStatus.NOT_STARTED).FirstOrDefault();
		}

		private WordModel FindWord(SQLiteConnection conn, string word)
		{
			return conn.Table<WordModel>().ToList().Where(w => w.Word == word).FirstOrDefault();
		}

		private List<WordModel> GetSortedWordList(SQLiteConnection conn)
		{
			return conn.Table<WordModel>().OrderBy(word => word.IdxOrder).ToList();
		}


		string mDatabasePath;

		SearchModel mDictionarySearcher;
		SuggestionModel mSuggestionModel;
		PlaySoundModel mPlaySoundModel;

		List<WordModel> mWordsList;
		int mMaxIdxOrder;
	}

	public enum WordStatus 
	{
		NOT_STARTED,
		QUERYING_AH,
		COMPLETE,
		NOT_FOUND,
		NETWORK_ERROR,
		GENERAL_ERROR
	}

	[Serializable]
	public class WordModel
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		public int        IdxOrder { get; set; }
		public string     Word { get; set; }
        public string     Pronunciation { get; set; }
		public WordStatus Status { get; set; }
	}

    [Serializable]
    public class WaveModel
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        [Indexed]
        public int    WordID { get; set; }
        public string WaveFileName { get; set; }
    }
}

