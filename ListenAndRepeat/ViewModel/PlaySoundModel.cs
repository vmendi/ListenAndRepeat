using System;
using MonoTouch.AVFoundation;
using System.IO;
using System.Linq;

namespace ListenAndRepeat.ViewModel
{
	public class PlaySoundModel
	{
		public PlaySoundModel()
		{
		}

		public void PlaySound(WordModel theWord)
		{
			if (theWord.Status == WordStatus.COMPLETE)
			{
				var localFile = Path.Combine ("file://", MainModel.GetSoundsDirectory (), theWord.WaveFileNames.First());
				mCurrentSound = AVAudioPlayer.FromData (MonoTouch.Foundation.NSData.FromFile(localFile));
				mCurrentSound.Play ();
			}
		}

		AVAudioPlayer mCurrentSound;
	}
}

