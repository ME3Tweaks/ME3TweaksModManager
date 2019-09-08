using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Gammtek.Conduit.Windows
{
	public static class WindowsApi
	{
		public static class Gdi32
		{
			private const string Gdi32DllName = "gdi32.dll";

			[DllImport(Gdi32DllName)]
			public static extern bool DeleteDC(IntPtr hdc);

			[DllImport(Gdi32DllName)]
			public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

			[DllImport(Gdi32DllName)]
			public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hobject);
		}

		public static class Kernel32
		{
			private const string Kernel32DllName = "kernel32.dll";

			[DllImport(Kernel32DllName)]
			public static extern bool AllocConsole();

			[DllImport(Kernel32DllName)]
			public static extern bool FreeConsole();

			[DllImport(Kernel32DllName)]
			public static extern IntPtr GetConsoleWindow();

			[DllImport(Kernel32DllName)]
			public static extern int GetConsoleOutputCP();
		}

		public static class Ole32
		{
			private const string Ole32DllName = "ole32.dll";

			[DllImport(Ole32DllName)]
			public static extern int CoInitialize(IntPtr unused);
		}

		public static class Shlwapi
		{
			private const string ShlwapiDllName = "shlwapi.dll";

			[DllImport(ShlwapiDllName)]
			public static extern int HashData(byte[] data, uint dataSize,
				[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] hash, uint hashSize);

			[DllImport(ShlwapiDllName)]
			public static extern IntPtr PathAddBackslash(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathAddExtension(StringBuilder path, string extension);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathAppend(StringBuilder path, string more);

			[DllImport(ShlwapiDllName)]
			public static extern string PathBuildRoot(StringBuilder result, int drive);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathCanonicalize(StringBuilder result, string path);

			[DllImport(ShlwapiDllName)]
			public static extern IntPtr PathCombine(StringBuilder result, string directory, string file);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathCompactPath(IntPtr dc, StringBuilder result, uint width);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathCompactPathEx(StringBuilder result, string path, uint maxLength, uint flags);

			[DllImport(ShlwapiDllName)]
			public static extern int PathCommonPrefix(string path1, string path2, StringBuilder result);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathFileExists(string path);

			[DllImport(ShlwapiDllName)]
			public static extern string PathFindExtension(string path);

			[DllImport(ShlwapiDllName)]
			public static extern string PathFindFileName(string path);

			[DllImport(ShlwapiDllName)]
			public static extern string PathFindNextComponent(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathFindOnPath(StringBuilder file, string[] otherDirectories);

			[DllImport(ShlwapiDllName)]
			public static extern string PathGetArgs(string path);

			[DllImport(ShlwapiDllName)]
			public static extern string PathFindSuffixArray(string path, string[] suffixes, int arraySize);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsLFNFileSpec(string path);

			[DllImport(ShlwapiDllName)]
			public static extern PathCharType PathGetCharType(char ch);

			[DllImport(ShlwapiDllName)]
			public static extern int PathGetDriveNumber(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsDirectory(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsDirectoryEmpty(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsFileSpec(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsPrefix(string prefix, string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsRelative(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsRoot(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsSameRoot(string path1, string path2);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsUNC(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsNetworkPath(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsUNCServer(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsUNCServerShare(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsContentType(string path, string contentType);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsURL(string path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathMakePretty(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathMatchSpec(string file, string pszSpec);

			[DllImport(ShlwapiDllName)]
			public static extern int PathParseIconLocation(StringBuilder pszIconFile);

			[DllImport(ShlwapiDllName)]
			public static extern void PathQuoteSpaces(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathRelativePathTo(StringBuilder result, string from, FileAttributes fromAttributes,
				string to, FileAttributes toAttributes);

			[DllImport(ShlwapiDllName)]
			public static extern void PathRemoveArgs(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern string PathRemoveBackslash(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern void PathRemoveBlanks(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern void PathRemoveExtension(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathRemoveFileSpec(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathRenameExtension(StringBuilder path, string extension);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathSearchAndQualify(string path, StringBuilder result, uint bufferSize);

			[DllImport(ShlwapiDllName)]
			public static extern string PathSkipRoot(string path);

			[DllImport(ShlwapiDllName)]
			public static extern void PathStripPath(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathStripToRoot(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern void PathUnquoteSpaces(StringBuilder lpsz);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathMakeSystemFolder(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathUnmakeSystemFolder(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathIsSystemFolder(string path, FileAttributes dwAttrb);

			[DllImport(ShlwapiDllName)]
			public static extern void PathUndecorate(StringBuilder path);

			[DllImport(ShlwapiDllName)]
			public static extern bool PathUnExpandEnvStrings(string path, StringBuilder result, uint cchBuf);

			[DllImport(ShlwapiDllName)]
			public static extern string StrFormatByteSize64(long value, StringBuilder result, uint size);

			[DllImport(ShlwapiDllName)]
			public static extern int SHAutoComplete(IntPtr hwndEdit, AutoComplete flags);

			/*[DllImport(ShlwapiDllName)]
			public static extern DialogResult SHMessageBoxCheck(IntPtr hwnd, string text, string title, uint type,
				DialogResult defaultValue, string registryValue);*/
		}

		public static class User32
		{
			private const string User32DllName = "user32.dll";

			[DllImport(User32DllName)]
			public static extern IntPtr GetDC(IntPtr hwnd);

			[DllImport(User32DllName)]
			public static extern IntPtr GetWindow(IntPtr wnd, uint cmd);

			[DllImport(User32DllName)]
			public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
		}
	}
}