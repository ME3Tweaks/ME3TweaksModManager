using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class PathDriveLetter : IPathDriveLetter
		{
			internal PathDriveLetter(string driveName)
			{
				Debug.Assert(IsValidDriveName(driveName));
				Letter = driveName[0];
				Debug.Assert(char.IsLetter(Letter));
			}

			public DriveInfo DriveInfo
			{
				get
				{
					var drive = DriveInfo.GetDrives().FirstOrDefault(x => x.Name == $@"{Letter}:\");

					if (drive != null)
					{
						try
						{
							if (drive.IsReady)
							{
								return drive;
							}
						}
						catch (Exception exception)
						{
							throw new DriveNotFoundException(exception.Message);
						}
					}

					throw new DriveNotFoundException($@"Drive ""{Letter}"" could not be found.");

					/*try
					{
						var drive = new DriveInfo(Letter.ToString());

						// Need to call drive.DriveFormat to force a DriveNotFoundException if the drive doesn't exist.
						var unused = drive.DriveFormat;

						return drive;
					}
					catch (Exception ex)
					{
						// Make sure whatever the exception thrown, a DriveNotFoundException is re-thrown.
						// 14Aout2011: Actually we didn't find a way to provoque an exception different than DriveNotFoundException when calling drive.DriveFormat.
						// new DriveInfo() on a non-existing drive doesn't work!
						throw new DriveNotFoundException(ex.Message);
					}*/
				}
			}

			public char Letter { get; }

			public override bool Equals(object obj)
			{
				var drive = obj as IPathDriveLetter;

				return drive != null 
					&& string.Equals(Letter.ToString(), drive.ToString(), StringComparison.CurrentCultureIgnoreCase);
			}

			public override int GetHashCode()
			{
				return char.ToLower(Letter).GetHashCode();
			}

			public bool NotEquals(object obj)
			{
				return !Equals(obj);
			}

			public override string ToString()
			{
				return Letter.ToString();
			}

			private static bool IsValidDriveName(string driveName)
			{
				Debug.Assert(driveName != null);

				// We have confirmation that the drive is just the first letter
				// of the absolute path in the documentation of the:
				// System.IO.DriveInfo..ctor(driveName)
				// This ctor sends System.ArgumentException if:
				//     The first letter of driveName is not an uppercase or lowercase letter from
				//     'a' to 'z'.
				return driveName.Length == 1 && char.IsLetter(driveName[0]);
			}
		}
	}
}
