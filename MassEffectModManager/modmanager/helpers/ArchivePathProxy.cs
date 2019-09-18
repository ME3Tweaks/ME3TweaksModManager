using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager.helpers
{
    public static class ArchivePathProxy
    {
        /// <summary>
        /// Combines paths, but will filter out any null or empty items.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static string PathCombine(string pathBase, char separator = '/', params string[] paths)
        {
            if (paths == null || !paths.Any())
                return pathBase;

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
            pathSb.Append(pathBase);
            removeLastSlash(pathSb);
            foreach (var path in paths)
            {
                pathSb.Append(separator);
                pathSb.Append(path);
                removeLastSlash(pathSb);
            }
            #endregion Combine

            #region Append slash if last path contains
            if (slash.Contains(paths.Last().Last()))
                pathSb.Append(separator);
            #endregion Append slash if last path contains

            return pathSb.ToString();
        }

        internal static string GetFileName(string modPath)
        {
            return modPath ?? "In-Memory Mod";
        }

        internal static string GetDisplayValue(string modPath)
        {
            throw new NotImplementedException();
        }
    }
}
