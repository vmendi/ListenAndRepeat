// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the Xcode designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoTouch.Foundation;

namespace ListenAndRepeat
{
	[Register ("WordListController")]
	partial class WordListController
	{
		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem AddButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem EditButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (AddButton != null) {
				AddButton.Dispose ();
				AddButton = null;
			}

			if (EditButton != null) {
				EditButton.Dispose ();
				EditButton = null;
			}
		}
	}
}
