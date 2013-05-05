using System;
using System.Collections.Generic;
using SQLite;
using System.IO;
using System.Linq;

namespace ListenAndRepeat
{
	public class ListenAndRepeatModel
	{
		public ListenAndRepeatModel()
		{
			var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			mDatabasePath = Path.Combine(documentsPath, "..", "Library", "db_sqlite-net.db");

			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
				// https://github.com/praeclarum/sqlite-net/wiki
				// In general, it will execute an automatic migration
				conn.CreateTable<WordModel>();

				// Load ViewModel
				RefreshWordList(conn);
			}

			mDictionarySearcher = new DictionarySearch();
			mDictionarySearcher.SearchCompleted += OnSearchCompleted;
		}

		public DictionarySearch DictionarySearcher 
		{
			get { return mDictionarySearcher; } 
		}

		public List<string> WordList
		{
			get { return mWordsList; }
		}

		private void OnSearchCompleted(object sender, DictionarySearch.SearchCompletedEventArgs e)
		{
			if (e == null)
				return;

			var newWordModel = new WordModel();
			newWordModel.Word = e.FoundWord.Word;
			newWordModel.WaveFileName = e.FoundWord.Waves.First().Item2;
			
			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
				conn.Insert(newWordModel);
			}
			
			mWordsList.Add(e.FoundWord.Word);
		}

		private void RefreshWordList(SQLite.SQLiteConnection conn)
		{
			mWordsList = new List<string>();

			var query = conn.Table<WordModel>(); 	// .Select(w => w.WaveFileName).ToList();

			foreach (var wordModel in query)
			{
				mWordsList.Add(wordModel.Word);
			}
		}

		DictionarySearch mDictionarySearcher;
		string mDatabasePath;
		List<string> mWordsList;
	}

	[Serializable]
	public class WordModel
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		public string Word { get; set; }
		public string WaveFileName { get; set; }
	}
}

