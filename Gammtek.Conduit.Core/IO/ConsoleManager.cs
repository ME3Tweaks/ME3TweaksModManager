using System;
using System.IO;
using System.Reflection;
using System.Security;
using Gammtek.Conduit.Windows;

namespace Gammtek.Conduit.IO
{
	[SuppressUnmanagedCodeSecurity]
	public static class ConsoleManager
	{
		public static bool HasConsole
		{
			get { return WindowsApi.Kernel32.GetConsoleWindow() != IntPtr.Zero; }
		}

		public static void Show()
		{
			if (HasConsole)
			{
				return;
			}

			WindowsApi.Kernel32.AllocConsole();
			InvalidateOutAndError();
		}

		public static void Hide()
		{
			if (!HasConsole)
			{
				return;
			}

			SetOutAndErrorNull();
			WindowsApi.Kernel32.FreeConsole();
		}

		public static void Toggle()
		{
			if (HasConsole)
			{
				Hide();
			}
			else
			{
				Show();
			}
		}

		private static void InvalidateOutAndError()
		{
			var type = typeof (Console);

			var @out = type.GetField("_out",
				BindingFlags.Static | BindingFlags.NonPublic);

			var error = type.GetField("_error",
				BindingFlags.Static | BindingFlags.NonPublic);

			var initializeStdOutError = type.GetMethod("InitializeStdOutError",
				BindingFlags.Static | BindingFlags.NonPublic);

			/*Debug.Assert(_out != null);
			Debug.Assert(_error != null);

			Debug.Assert(_InitializeStdOutError != null);*/

			if (@out != null)
			{
				@out.SetValue(null, null);
			}

			if (error != null)
			{
				error.SetValue(null, null);
			}

			initializeStdOutError.Invoke(null, new object[] {true});
		}

		public static void SetOutAndErrorNull()
		{
			Console.SetOut(TextWriter.Null);
			Console.SetError(TextWriter.Null);
		}
	}
}