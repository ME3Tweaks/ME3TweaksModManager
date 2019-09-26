using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    /// <summary>
    /// Contains methods useful for handling file paths both in and out of mod archives
    /// </summary>
    public class FilesystemInterposer
    {
        /// <summary>
        /// Combines paths and will attempt to keep the separator style
        /// </summary>
        /// <param name="archiveFilesystem">If this is an archive filesystem or not</param>
        /// <param name="paths">Paths to combine</param>
        /// <returns>Combined path</returns>
        public static string PathCombine(bool archiveFilesystem, string pathBase, params string[] paths)
        {
            char separator = '\\'; // IsInArchive ? '/' : '\\';
            if (paths == null || !paths.Any())
                return pathBase;

            if (archiveFilesystem)
            {
                pathBase = pathBase.TrimStart('\\', '/'); //archive paths don't start with a / or \
            }

            #region Remove path end slash

            var slash = new[] { '/', '\\' };
            Action<StringBuilder> removeLastSlash = null;
            removeLastSlash = (sb) =>
            {
                if (sb.Length == 0) return;
                if (!slash.Contains(sb[sb.Length - 1])) return;
                sb.Remove(sb.Length - 1, 1);
                removeLastSlash(sb);
            };

            #endregion Remove path end slash

            #region Combine

            var pathSb = new StringBuilder();
            bool skipFirst = pathBase == ""; //If the base path is "", don't apply a separator.
            bool skippedFirst = false;
            pathSb.Append(pathBase);
            removeLastSlash(pathSb);
            foreach (var path in paths)
            {
                if (!skipFirst || skippedFirst)
                {
                    pathSb.Append(separator);
                }

                pathSb.Append(path);
                removeLastSlash(pathSb);
            }

            #endregion Combine

            #region Append slash if last path contains
            if (paths.Count() > 2)
            {
                if (slash.Contains(paths.Last().Last()))
                    pathSb.Append(separator);
            }
            #endregion Append slash if last path contains

            return pathSb.ToString();
        }

        /// <summary>
        /// Checks if a directory exists. This method will look an archive file if one is specified.
        /// </summary>
        /// <param name="path">Path to determine if it's a directory</param>
        /// <param name="archive">Archive to look in. Not specifying this will look on the local filesystem instead</param>
        /// <returns></returns>
        public static bool DirectoryExists(string path, SevenZipExtractor archive = null)
        {
            if (archive != null)
            {
                path = path.TrimStart('\\', '/'); //archive paths don't start with a / \ but path combining will append one of these.
                var entry = archive.ArchiveFileData.FirstOrDefault(x => x.FileName.Equals(path, StringComparison.InvariantCultureIgnoreCase));
                return !string.IsNullOrEmpty(entry.FileName) && entry.IsDirectory; //must check filename is populated as this is a struct
            }
            else
            {
                return Directory.Exists(path);
            }
        }

        /// <summary>
        /// Checks if a directory exists. This method will use archive-style filepaths if isInArchive is specified.
        /// </summary>
        /// <param name="path">Path to find parent for</param>
        /// <param name="isInArchive">Specifies if this is an archive filesystem</param>
        /// <returns></returns>
        public static string DirectoryGetParent(string path, bool isInArchive = false)
        {
            if (isInArchive)
            {
                path = path.TrimStart('\\', '/'); //archive paths don't start with a / \ but path combining will append one of these.
                if (path.Contains('\\'))
                {
                    return path.Substring(0, path.LastIndexOf('\\'));
                }

                return "";
            }
            else
            {
                return Directory.GetParent(path).FullName;
            }
        }

        /// <summary>
        /// Checks if a file exists. This method will look in an archive file if one is specified
        /// </summary>
        /// <param name="path">Path of file to find</param>
        /// <param name="archive">Archive to look in. Not specifying this will look on the local filesystem instead</param>
        /// <returns></returns>
        public static bool FileExists(string path, SevenZipExtractor archive = null)
        {
            if (archive != null)
            {
                path = path.TrimStart('\\', '/').Replace('/', '\\'); //archive paths don't start with a / \ but path combining will append one of these.
                var entry = archive.ArchiveFileData.FirstOrDefault(x => x.FileName.Equals(path, StringComparison.InvariantCultureIgnoreCase));
                return !string.IsNullOrEmpty(entry.FileName) && !entry.IsDirectory; //must check filename is populated as this is a struct
            }
            else
            {
                return File.Exists(path);
            }
        }


    }
}
