using System;
using System.IO;
using System.Text;

namespace Gammtek.Conduit.Windows
{
	public static class ShPath
	{
		public const int MaxPath = 260;

		public static string AddBackslash(string path)
		{
			var path1 = new StringBuilder(path, 260);

			if (!(WindowsApi.Shlwapi.PathAddBackslash(path1) != IntPtr.Zero))
			{
				return null;
			}

			return path1.ToString();
		}

		public static bool AddBackslash(StringBuilder path)
		{
			return WindowsApi.Shlwapi.PathAddBackslash(path) != IntPtr.Zero;
		}

		public static string PathAddExtension(string path, string extension)
		{
			var path1 = new StringBuilder(path, 260);

			if (!WindowsApi.Shlwapi.PathAddExtension(path1, extension))
			{
				return null;
			}

			return path1.ToString();
		}

		public static bool AddExtension(StringBuilder path, string extension)
		{
			return WindowsApi.Shlwapi.PathAddExtension(path, extension);
		}

		public static string BuildRoot(int drive)
		{
			var str = WindowsApi.Shlwapi.PathBuildRoot(new StringBuilder(260), drive);

			if (!string.IsNullOrEmpty(str))
			{
				return str;
			}

			return null;
		}

		public static string Canonicalize(string path)
		{
			var result = new StringBuilder(path, 260);

			if (!WindowsApi.Shlwapi.PathCanonicalize(result, path))
			{
				return null;
			}

			return result.ToString();
		}

		public static string Combine(string path, string more)
		{
			var result = new StringBuilder(260);

			if (!(WindowsApi.Shlwapi.PathCombine(result, path, more) != IntPtr.Zero))
			{
				return null;
			}

			return result.ToString();
		}

		public static bool Append(StringBuilder path, string more)
		{
			return WindowsApi.Shlwapi.PathAppend(path, more);
		}

		/*public static bool SetControlText(Control control, string path)
		{
			IWin32Window win32Window = control;
			var result = new StringBuilder(path, 260);
			var dc = WindowsApi.User32.GetDC(win32Window.Handle);
			var compatibleDc = WindowsApi.Gdi32.CreateCompatibleDC(dc);

			WindowsApi.User32.ReleaseDC(win32Window.Handle, dc);
			WindowsApi.Gdi32.SelectObject(compatibleDc, control.Font.ToHfont());

			var flag = WindowsApi.Shlwapi.PathCompactPath(compatibleDc, result, (uint)(control.ClientSize.Width - 10));

			WindowsApi.Gdi32.DeleteDC(compatibleDc);

			if (flag)
			{
				control.Text = result.ToString();
			}

			return flag;
		}*/

		public static string CompactPath(string path, uint maxLength)
		{
			var result = new StringBuilder(path, 260);
			
			if (!WindowsApi.Shlwapi.PathCompactPathEx(result, path, maxLength, 0U))
			{
				return null;
			}

			return result.ToString();
		}

		public static string GetCommonPrefix(string path1, string path2)
		{
			var result = new StringBuilder(260);
			if (WindowsApi.Shlwapi.PathCommonPrefix(path1, path2, result) <= 0)
			{
				return string.Empty;
			}
			return result.ToString();
		}

		public static bool FileExists(string path)
		{
			return WindowsApi.Shlwapi.PathFileExists(path);
		}

		public static string FindExtension(string path)
		{
			return WindowsApi.Shlwapi.PathFindExtension(path);
		}

		public static string FindFileName(string path)
		{
			return WindowsApi.Shlwapi.PathFindFileName(path);
		}

		public static string FindNextComponent(string path)
		{
			return WindowsApi.Shlwapi.PathFindNextComponent(path);
		}

		public static string FindOnPath(string file)
		{
			return FindOnPath(file, null);
		}

		public static string FindOnPath(string file, string[] otherDirectories)
		{
			string[] otherDirectories1 = null;

			if (otherDirectories != null)
			{
				if (otherDirectories.Length > 0 && otherDirectories[otherDirectories.Length - 1] == null)
				{
					otherDirectories1 = otherDirectories;
				}
				else
				{
					otherDirectories1 = new string[otherDirectories.Length + 1];
					Array.Copy(otherDirectories, otherDirectories1, otherDirectories.Length);
					otherDirectories1[otherDirectories.Length] = null;
				}
			}

			var file1 = new StringBuilder(file, 260);

			if (WindowsApi.Shlwapi.PathFindOnPath(file1, otherDirectories1))
			{
				return file1.ToString();
			}

			return null;
		}

		public static string GetArgs(string path)
		{
			return WindowsApi.Shlwapi.PathGetArgs(path);
		}

		public static string FindSuffixArray(string path, string[] suffixes)
		{
			if (suffixes == null)
			{
				return null;
			}

			return WindowsApi.Shlwapi.PathFindSuffixArray(path, suffixes, suffixes.Length);
		}

		public static bool IsLongFileNameFileSpec(string path)
		{
			return WindowsApi.Shlwapi.PathIsLFNFileSpec(path);
		}

		public static PathCharType GetCharType(char c)
		{
			return WindowsApi.Shlwapi.PathGetCharType(c);
		}

		public static int GetDriveNumber(string path)
		{
			return WindowsApi.Shlwapi.PathGetDriveNumber(path);
		}

		public static bool IsDirectory(string path)
		{
			return WindowsApi.Shlwapi.PathIsDirectory(path);
		}

		public static bool IsDirectoryEmpty(string path)
		{
			return WindowsApi.Shlwapi.PathIsDirectoryEmpty(path);
		}

		public static bool IsFileSpec(string path)
		{
			return WindowsApi.Shlwapi.PathIsFileSpec(path);
		}

		public static bool IsPrefix(string prefix, string path)
		{
			return WindowsApi.Shlwapi.PathIsFileSpec(path);
		}

		public static bool IsRelative(string path)
		{
			return WindowsApi.Shlwapi.PathIsRelative(path);
		}

		public static bool IsRoot(string path)
		{
			return WindowsApi.Shlwapi.PathIsRoot(path);
		}

		public static bool IsSameRoot(string path1, string path2)
		{
			return WindowsApi.Shlwapi.PathIsSameRoot(path1, path2);
		}

		public static bool IsUNC(string path)
		{
			return WindowsApi.Shlwapi.PathIsUNC(path);
		}

		public static bool IsNetworkPath(string path)
		{
			return WindowsApi.Shlwapi.PathIsNetworkPath(path);
		}

		public static bool IsUNCServer(string path)
		{
			return WindowsApi.Shlwapi.PathIsUNCServer(path);
		}

		public static bool IsUNCServerShare(string path)
		{
			return WindowsApi.Shlwapi.PathIsUNCServerShare(path);
		}

		public static bool IsContentType(string path, string contentType)
		{
			return WindowsApi.Shlwapi.PathIsContentType(path, contentType);
		}

		public static bool IsURL(string path)
		{
			return WindowsApi.Shlwapi.PathIsURL(path);
		}

		public static string MakePretty(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathMakePretty(path1);

			return path1.ToString();
		}

		public static bool MatchSpec(string file, string spec)
		{
			return WindowsApi.Shlwapi.PathMatchSpec(file, spec);
		}

		public static int ParseIconLocation(string path, out string parsedPath)
		{
			var pszIconFile = new StringBuilder(path, 260);
			var num = WindowsApi.Shlwapi.PathParseIconLocation(pszIconFile);
			parsedPath = pszIconFile.ToString();

			return num;
		}

		public static string QuoteSpaces(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathQuoteSpaces(path1);

			return path1.ToString();
		}

		public static string RelativePathTo(string pathFrom, bool fromPathIsDirectory, string pathTo, bool toPathIsDirectory)
		{
			var result = new StringBuilder(260);

			if (!WindowsApi.Shlwapi.PathRelativePathTo(result, pathFrom, fromPathIsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
					pathTo, toPathIsDirectory ? FileAttributes.Directory : FileAttributes.Normal))
			{
				return null;
			}

			return result.ToString();
		}

		public static string RemoveArgs(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathRemoveArgs(path1);

			return path1.ToString();
		}

		public static string RemoveBackslash(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathRemoveBackslash(path1);

			return path1.ToString();
		}

		public static string RemoveBlanks(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathRemoveBlanks(path1);

			return path1.ToString();
		}

		public static string RemoveExtension(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathRemoveExtension(path1);

			return path1.ToString();
		}

		public static string RemoveFileSpec(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathRemoveFileSpec(path1);

			return path1.ToString();
		}

		public static string RenameExtension(string path, string newExtension)
		{
			var path1 = new StringBuilder(path, 260);

			if (WindowsApi.Shlwapi.PathRenameExtension(path1, newExtension))
			{
				return path1.ToString();
			}

			return null;
		}

		public static string SearchAndQualify(string path)
		{
			var result = new StringBuilder(path, 260);

			if (!WindowsApi.Shlwapi.PathSearchAndQualify(path, result, 260U))
			{
				return null;
			}

			return result.ToString();
		}

		public static string SkipRoot(string path)
		{
			return WindowsApi.Shlwapi.PathSkipRoot(path);
		}

		public static string StripPath(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathStripPath(path1);

			return path1.ToString();
		}

		public static string StripToRoot(string path)
		{
			var path1 = new StringBuilder(path, 260);

			if (!WindowsApi.Shlwapi.PathStripToRoot(path1))
			{
				return null;
			}

			return path1.ToString();
		}

		public static string UnquoteSpaces(string path)
		{
			var lpsz = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathUnquoteSpaces(lpsz);

			return lpsz.ToString();
		}

		public static string MakeSystemFolder(string path)
		{
			var path1 = new StringBuilder(path, 260);

			if (!WindowsApi.Shlwapi.PathMakeSystemFolder(path1))
			{
				return null;
			}

			return path1.ToString();
		}

		public static string UnmakeSystemFolder(string path)
		{
			var path1 = new StringBuilder(path, 260);

			if (!WindowsApi.Shlwapi.PathUnmakeSystemFolder(path1))
			{
				return null;
			}

			return path1.ToString();
		}

		public static bool IsSystemFolder(string path, FileAttributes attributes)
		{
			return WindowsApi.Shlwapi.PathIsSystemFolder(path, attributes);
		}

		public static string Undecorate(string path)
		{
			var path1 = new StringBuilder(path, 260);
			WindowsApi.Shlwapi.PathUndecorate(path1);

			return path1.ToString();
		}

		public static string UnExpandEnvironmentStrings(string path)
		{
			var result = new StringBuilder(path, 260);

			if (!WindowsApi.Shlwapi.PathUnExpandEnvStrings(path, result, 260U))
			{
				return null;
			}

			return result.ToString();
		}
	}
}