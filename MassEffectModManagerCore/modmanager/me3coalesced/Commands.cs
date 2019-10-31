using System.Windows.Input;

namespace MassEffect3.CoalesceTool
{
	public static class Commands
	{
		static Commands()
		{
			// FileBrowse
			Browse = new RoutedUICommand("Browse", "Brwose", typeof(Commands));

			// ConvertTo
			ConvertTo = new RoutedUICommand("Convert", "ConvertTo", typeof(Commands));

			// ConvertToBinary
			ConvertToBinary = new RoutedUICommand("Convert", "ConvertToBinary", typeof(Commands));

			// ConvertToXml
			ConvertToXml = new RoutedUICommand("Convert", "ConvertToXml", typeof(Commands));

			// Exit
			Exit = new RoutedUICommand("Exit", "Exit", typeof (Commands),
				new InputGestureCollection {new KeyGesture(Key.F4, ModifierKeys.Alt)});
		}

		public static RoutedUICommand Browse { get; private set; }

		public static RoutedUICommand ConvertTo { get; private set; }

		public static RoutedUICommand ConvertToBinary { get; private set; }

		public static RoutedUICommand ConvertToXml { get; private set; }

		public static RoutedUICommand Exit { get; private set; }
	}
}
