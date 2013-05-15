using System;
using MonoTouch.AVFoundation;
using System.IO;

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
				var localFile = Path.Combine ("file://", MainModel.GetSoundsDirectory (), theWord.WaveFileName);
				mCurrentSound = AVAudioPlayer.FromData (MonoTouch.Foundation.NSData.FromFile(localFile));
				mCurrentSound.Play ();
			}
		}

		AVAudioPlayer mCurrentSound;
	}
}

