using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.localizations;
using LegendaryExplorerCore.Unreal.Classes;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Class that provides user facing localizations for ME3Explorer's Texture2D.cs class, which can throw exceptions we need to be able to localize
    /// </summary>
    public static class T2DLocalizationShim
    {
        private static string GetLocalizedCouldNotFetchTextureDataMessage(string export, string file) =>
            M3L.GetString(M3L.string_error_couldNotFetchTextureData, export, file);
        private static string GetLocalizedCouldNotFindME1TexturePackageMessage(string file) =>
            M3L.GetString(M3L.string_error_me1TextureFileNotFound, file);
        private static string GetLocalizedCouldNotFindME2ME3TextureCacheMessage(string file) => 
            M3L.GetString(M3L.string_error_me23TextureTFCNotFound, file);

        private static string GetLocalizedTextureExceptionExternalMessage(string exceptionMessage, string file, string storageType, string offset) =>
            M3L.GetString(M3L.string_interp_error_textureExceptionExternal, exceptionMessage, file, storageType, offset);
        private static string GetLocalizedTextureExceptionInternalMessage(string exceptionMessage, string storageType) =>
            M3L.GetString(M3L.string_interp_error_textureExceptionInternal, exceptionMessage, storageType);

        /// <summary>
        /// Sets up the localization shim for Texture2D in ME3Explorer
        /// </summary>
        public static void SetupTexture2DLocalizationShim()
        {
            Texture2D.GetLocalizedCouldNotFetchTextureDataMessage = GetLocalizedCouldNotFetchTextureDataMessage;
            Texture2D.GetLocalizedCouldNotFindME1TexturePackageMessage = GetLocalizedCouldNotFindME1TexturePackageMessage;
            Texture2D.GetLocalizedCouldNotFindME2ME3TextureCacheMessage = GetLocalizedCouldNotFindME2ME3TextureCacheMessage;
            Texture2D.GetLocalizedTextureExceptionExternalMessage = GetLocalizedTextureExceptionExternalMessage;
            Texture2D.GetLocalizedTextureExceptionInternalMessage = GetLocalizedTextureExceptionInternalMessage;
        }
    }
}
