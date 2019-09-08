using System.Text;

namespace Gammtek.Conduit.Windows
{
	public static class ShMisc
	{
		public static bool HashData(byte[] data, byte[] hash)
		{
			return WindowsApi.Shlwapi.HashData(data, (uint)data.Length, hash, (uint)hash.Length) != 0;
		}

		public static string StringFormatByteSize(long value)
		{
			var result = new StringBuilder(128);
			return WindowsApi.Shlwapi.StrFormatByteSize64(value, result, (uint)result.Capacity);
		}

		/*public static bool SetAutoComplete(ComboBox control, AutoComplete flags)
		{
			var num = (int)Application.OleRequired();
			WindowsApi.Shlwapi.SHAutoComplete(WindowsApi.User32.GetWindow(control.Handle, 5U), flags);

			return true;
		}*/

		/*public static DialogResult MessageBoxCheck(Control parent, string text, string title, MessageBoxButtons buttons,
			MessageBoxIcon icons, DialogResult defaultValue, string registryValue)
		{
			return WindowsApi.Shlwapi.SHMessageBoxCheck(parent == null ? IntPtr.Zero : parent.Handle, text, title,
				(uint)(buttons | (MessageBoxButtons)icons), defaultValue, registryValue);
		}*/
	}
}