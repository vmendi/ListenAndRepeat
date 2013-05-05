using System;
using System.Collections.Generic;
using System.Linq;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using ListenAndRepeat.ViewModel;

namespace ListenAndRepeat
{
	public class Application
	{
		// This is the main entry point of the application.
		static void Main (string[] args)
		{
			MainModel.RegisterServices();

			UIApplication.Main (args, null, "AppDelegate");
		}
	}
}
