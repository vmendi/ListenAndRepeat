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
			ServiceContainer.Register<DictionarySearchModel>();
			ServiceContainer.Register<PlaySoundModel>();
		}

		public EventHandler WordsListChanged;

		public MainModel()
		{
			var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			mDatabasePath = Path.Combine(documentsPath, "..", "Library", "db_sqlite-net.db");

			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
				// https://github.com/praeclarum/sqlite-net/wiki
				// In general, it will execute an automatic migration
				conn.CreateTable<WordModel>();

				RefreshMaxIdxOrder(conn);
				RefreshWordsList(conn);
			}

			mDictionarySearcher = ServiceContainer.Resolve<DictionarySearchModel>();
			mDictionarySearcher.SearchCompleted += OnSearchCompleted;
		}
	
		public List<WordModel> WordsList
		{
			get { return mWordsList; }
		}

		private void OnSearchCompleted(object sender, SearchCompletedEventArgs e)
		{
			if (e.FoundWord != null)
			{
				var newWordModel = new WordModel();
				newWordModel.Word = e.FoundWord.Word;
				newWordModel.WaveFileName = e.FoundWord.Waves.First().Item2;
				newWordModel.IdxOrder = mMaxIdxOrder++;

				using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
				{
					conn.Insert(newWordModel);
					RefreshWordsList(conn);
				}
			}
		}

		private void RefreshWordsList(SQLite.SQLiteConnection conn)
		{
			mWordsList = conn.Table<WordModel>().OrderBy(word => word.IdxOrder).ToList();

			var method = WordsListChanged;
			if (method != null)
				method(this, EventArgs.Empty);
		}

		public void RemoveWordAt(int idx)
		{
			using (var conn = new SQLite.SQLiteConnection(mDatabasePath))
			{
				conn.Delete(mWordsList[idx]);
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

		private void RefreshMaxIdxOrder(SQLiteConnection conn)
		{
			if (conn.Table<WordModel>().Count() != 0)
				mMaxIdxOrder = conn.Table<WordModel>().Max(w => w.IdxOrder);
			else
				mMaxIdxOrder = 0;
		}

		static public string GetSoundsDirectory()
		{
			var documentsPath =	Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			return Path.Combine(documentsPath, "..", "Library", "Sounds");
		}

		DictionarySearchModel mDictionarySearcher;
		string mDatabasePath;
		List<WordModel> mWordsList;
		int mMaxIdxOrder;
	}

	[Serializable]
	public class WordModel
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		public int    IdxOrder { get; set; }
		public string Word { get; set; }
		public string WaveFileName { get; set; }
	}
}

