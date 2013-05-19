using System;
using MonoTouch.AVFoundation;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ListenAndRepeat.ViewModel
{
	public class PlaySoundModel
	{
		public void PlaySound(WordModel theWord, List<WaveModel> waves)
		{
			if (theWord.Status == WordStatus.COMPLETE)
			{
                int lastPlayedIdx = -1;
                if (mLastPlayed.ContainsKey(theWord.Word))
                    lastPlayedIdx = mLastPlayed[theWord.Word];
               
                if (lastPlayedIdx < waves.Count - 1)
                    lastPlayedIdx++;
                else
                    lastPlayedIdx = 0;

				var localFile = Path.Combine ("file://", MainModel.GetSoundsDirectory (), waves[lastPlayedIdx].WaveFileName);
				mCurrentSound = AVAudioPlayer.FromData (MonoTouch.Foundation.NSData.FromFile(localFile));
				mCurrentSound.Play ();

                mLastPlayed[theWord.Word] = lastPlayedIdx;
			}
		}

        Dictionary<string, int> mLastPlayed = new Dictionary<string, int>();
		AVAudioPlayer mCurrentSound;
	}
}

