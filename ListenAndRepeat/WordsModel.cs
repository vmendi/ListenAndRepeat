using System;
using System.Collections.Generic;

namespace ListenAndRepeat
{
	public class WordsModel
	{
		public WordsModel()
		{
			mWordsList.AddRange(new string[] {"Vegetables","Fruits","Flowers","Legumes","Bulbs","Tubers"});
		}

		public List<string> WordList
		{
			get { return mWordsList; }
		}

		List<string> mWordsList = new List<string>();
	}
}

