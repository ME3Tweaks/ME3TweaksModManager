using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME3Explorer.Unreal
{
    public static class UnrealFlags
    {

        /// <summary>
        /// Flags describing an class instance. This code is from UEExplorer.
        ///
        /// Note:
        ///     This is valid for UE3 as well unless otherwise noted.
        ///
        /// @Redefined( Version, Clone )
        ///     The flag is redefined in (Version) as (Clone)
        ///
        /// @Removed( Version )
        ///     The flag is removed in (Version)
        ///
        /// @Moved( Version, New )
        ///     The flag was moved since (Version) to a different value (New)
        /// </summary>
        [Flags]
        public enum EClassFlags : uint
        {
            None = 0x00000000U,
            Abstract = 0x00000001U,
            Compiled = 0x00000002U,
            Config = 0x00000004U,
            Transient = 0x00000008U,
            Parsed = 0x00000010U,
            Localized = 0x00000020U,
            SafeReplace = 0x00000040U,

            NoExport = 0x00000100U,
            Placeable = 0x00000200U,
            PerObjectConfig = 0x00000400U,
            NativeReplication = 0x00000800U,
            EditInlineNew = 0x00001000U,
            CollapseCategories = 0x00002000U,
            ExportStructs = 0x00004000U,      // @Removed(UE3 in early but not latest)
            HasComponents = 0x00400000U,      // @Redefined Class has component properties.
            Hidden = 0x00800000U,      // @Redefined Don't show this class in the editor class browser or edit inline new menus.
            Deprecated = 0x01000000U,      // @Redefined Don't save objects of this class when serializing
            HideDropDown2 = 0x02000000U,
            Exported = 0x04000000U,
            NativeOnly = 0x20000000U
        }

        [Flags]
        //from https://github.com/EliotVU/Unreal-Library/blob/23da0b1d42d90ccf8bc7d64051ffd38a8088ec93/src/UnrealFlags.cs#L17
        public enum EPackageFlags : uint
        {
            // 028A0009 : A cooked and compressed package
            // 00280009 : A cooked package
            // 00020001 : A ordinary package

            /// <summary>
            /// Whether clients are allowed to download the package from the server.
            /// </summary>
            AllowDownload = 0x00000001U,

            /// <summary>
            /// Whether clients can skip downloading the package but still able to join the server.
            /// </summary>
            ClientOptional = 0x00000002U,

            /// <summary>
            /// Only necessary to load on the server.
            /// </summary>
            ServerSideOnly = 0x00000004U,

            /// <summary>
            /// The package is cooked.
            /// </summary>
            Cooked = 0x00000008U,      // @Redefined

            Unsecure = 0x00000010U,

            SavedWithNewerVersion = 0x00000020U,

            /// <summary>
            /// Clients must download the package.
            /// </summary>
            Need = 0x00008000U,

            /// <summary>
            /// Package holds map data.
            /// </summary>
            Map = 0x00020000U,

            DisallowLazyLoading = 0x00080000,

            /// <summary>
            /// Package has ME3Explorer appended name table. This is an unused flag by the engine and is present only due to ME3Explorer modified files
            /// </summary>
            ME3ExplorerAppendedNameTable = 0x00100000U,

            /// <summary>
            /// Package contains classes.
            /// </summary>
            Script = 0x00200000U,


            /// <summary>
            /// The package was build with -Debug
            /// </summary>
            Debug = 0x00400000U,
            RequireImportsAlreadyLoaded = 0x00800000U,

            SelfContainedLighting = 0x01000000U,
            Compressed = 0x02000000U,
            FullyCompressed = 0x04000000U,

            ContainsInlinedShaders = 0x08000000U,

            ContainsFaceFXData = 0x10000000U,

            /// <summary>
            /// Whether package has metadata exported(anything related to the editor).
            /// </summary>
            NoExportsData = 0x20000000U,

            /// <summary>
            /// Package's source is stripped.
            /// </summary>
            Stripped = 0x40000000U,

            Protected = 0x80000000U,
        }

        [Flags]
        public enum EPropertyFlags : ulong
        {
            None = 0,
            Editable = 0x0000000000000001U,
            Const = 0x0000000000000002U,
            Input = 0x0000000000000004U,
            ExportObject = 0x0000000000000008U,
            OptionalParm = 0x0000000000000010U,
            Net = 0x0000000000000020U,
            EditFixedSize = 0x0000000000000040U, // also EditConstArray
            Parm = 0x0000000000000080U,
            OutParm = 0x0000000000000100U,
            SkipParm = 0x0000000000000200U,
            ReturnParm = 0x0000000000000400U,
            CoerceParm = 0x0000000000000800U,
            Native = 0x0000000000001000U,
            Transient = 0x0000000000002000U,
            Config = 0x0000000000004000U,
            Localized = 0x0000000000008000U,
            Travel = 0x0000000000010000U,
            EditConst = 0x0000000000020000U,
            GlobalConfig = 0x0000000000040000U,
            Component = 0x0000000000080000U,
            Init = 0x0000000000100000U,
            DuplicateTransient = 0x0000000000200000U,
            NeedCtorLink = 0x0000000000400000U,
            NoExport = 0x0000000000800000U,
            NoImport = 0x0000000001000000U,
            NoClear = 0x0000000002000000U,
            EditInline = 0x0000000004000000U,
            EdFindable = 0x0000000008000000U,
            EditInlineUse = 0x0000000010000000U,
            Deprecated = 0x0000000020000000U,
            DataBinding = 0x0000000040000000U, // also EditInlineNotify
            SerializeText = 0x0000000080000000U,
            RepNotify = 0x0000000100000000U,
            Interp = 0x0000000200000000U,
            NonTransactional = 0x0000000400000000U,
            EditorOnly = 0x0000000800000000U,
            NotForConsole = 0x0000001000000000U,
            RepRetry = 0x0000002000000000U,
            PrivateWrite = 0x0000004000000000U,
            ProtectedWrite = 0x0000008000000000U,
            Archetype = 0x0000010000000000U,
            EditHide = 0x0000020000000000U,
            EditTextBox = 0x0000040000000000U,
            CrossLevelPassive = 0x0000100000000000U,
            CrossLevelActive = 0x0000200000000000U
        }

        public static Dictionary<EPropertyFlags, string> propertyflagsdesc = new Dictionary<EPropertyFlags, string>
        {
            [EPropertyFlags.None] = "",
            [EPropertyFlags.Editable] = "",
            [EPropertyFlags.Const] = "Constant",
            [EPropertyFlags.Input] = "",
            [EPropertyFlags.ExportObject] = "",
            [EPropertyFlags.OptionalParm] = "",
            [EPropertyFlags.Net] = "",
            [EPropertyFlags.EditFixedSize] = "also EditConstArray",
            [EPropertyFlags.Parm] = "",
            [EPropertyFlags.OutParm] = "",
            [EPropertyFlags.SkipParm] = "",
            [EPropertyFlags.ReturnParm] = "",
            [EPropertyFlags.CoerceParm] = "",
            [EPropertyFlags.Native] = "",
            [EPropertyFlags.Transient] = "",
            [EPropertyFlags.Config] = "",
            [EPropertyFlags.Localized] = "",
            [EPropertyFlags.Travel] = "",
            [EPropertyFlags.EditConst] = "",
            [EPropertyFlags.GlobalConfig] = "",
            [EPropertyFlags.Component] = "",
            [EPropertyFlags.Init] = "",
            [EPropertyFlags.DuplicateTransient] = "",
            [EPropertyFlags.NeedCtorLink] = "",
            [EPropertyFlags.NoExport] = "",
            [EPropertyFlags.NoImport] = "",
            [EPropertyFlags.NoClear] = "",
            [EPropertyFlags.EditInline] = "",
            [EPropertyFlags.EdFindable] = "",
            [EPropertyFlags.EditInlineUse] = "",
            [EPropertyFlags.Deprecated] = "",
            [EPropertyFlags.DataBinding] = "also EditInlineNotify",
            [EPropertyFlags.SerializeText] = "",
            [EPropertyFlags.RepNotify] = "",
            [EPropertyFlags.Interp] = "",
            [EPropertyFlags.NonTransactional] = "",
            [EPropertyFlags.EditorOnly] = "",
            [EPropertyFlags.NotForConsole] = "",
            [EPropertyFlags.RepRetry] = "Retries replication if replication fails",
            [EPropertyFlags.PrivateWrite] = "",
            [EPropertyFlags.ProtectedWrite] = "",
            [EPropertyFlags.Archetype] = "",
            [EPropertyFlags.EditHide] = "",
            [EPropertyFlags.EditTextBox] = "",
            [EPropertyFlags.CrossLevelPassive] = "",
            [EPropertyFlags.CrossLevelActive] = "",
        };

        public static Dictionary<EClassFlags, string> classflagdesc = new Dictionary<EClassFlags, string>
        {
            [EClassFlags.None] = "",
            [EClassFlags.Abstract] = "",
            [EClassFlags.Compiled] = "",
            [EClassFlags.Config] = "",
            [EClassFlags.Transient] = "",
            [EClassFlags.Parsed] = "",
            [EClassFlags.Localized] = "",
            [EClassFlags.SafeReplace] = "",
            [EClassFlags.NoExport] = "",
            [EClassFlags.Placeable] = "",
            [EClassFlags.PerObjectConfig] = "",
            [EClassFlags.NativeReplication] = "",
            [EClassFlags.EditInlineNew] = "",
            [EClassFlags.CollapseCategories] = "",
            [EClassFlags.ExportStructs] = "",      // @Removed(UE3 in early but not latest)

            [EClassFlags.HasComponents] = "Class has component properties",      // @Redefined Class has component properties.
            [EClassFlags.Hidden] = "Don't show this class in the editor class browser or edit inline new menus",     // @Redefined .
            [EClassFlags.Deprecated] = "Don't save objects of this class when serializing",      // @Redefined 
            [EClassFlags.HideDropDown2] = "",
            [EClassFlags.Exported] = "",
            [EClassFlags.NativeOnly] = ""
        };

        [Flags]
        public enum EObjectFlags : ulong
        {
            InSingularFunc = 0x0000000000000002, // In a singular function.
            ClassDefaultObject = 0x0000000000000200, // this object is its class's default object
            IsCrossLevelReferenced = 0x0000000000400000, // This object has been pointed to by a cross-level reference, and therefore requires additional cleanup upon deletion
            ArchetypeObject = 0x0000000000000400, // this object is a template for another object - treat like a class default object
            Transactional = 0x0000000100000000,   // Object is transactional.
            Unreachable = 0x0000000200000000,	// Object is not reachable on the object graph.
            Public = 0x0000000400000000,	// Object is visible outside its package.
            TagImp = 0x0000000800000000,	// Temporary import tag in load/save.
            TagExp = 0x0000001000000000,	// Temporary export tag in load/save.
            Obsolete = 0x0000002000000000,   // Object marked as obsolete and should be replaced.
            TagGarbage = 0x0000004000000000,	// Check during garbage collection.
            Final = 0x0000008000000000,	// Object is not visible outside of class.
            PerObjectLocalized = 0x0000010000000000,	// Object is localized by instance name00000000, not by class.
            NeedLoad = 0x0000020000000000,   // During load00000000, indicates object needs loading.
            HighlightedName = 0x0000040000000000,	// A hardcoded name which should be syntax-highlighted.
            EliminateObject = 0x0000040000000000,   // NULL out references to this during garbage collecion.
            RemappedName = 0x0000080000000000,   // Name is remapped.
            Suppress = 0x0000100000000000,	//warning: Mirrored in UnName.h. Suppressed log name.
            StateChanged = 0x0000100000000000,   // Object did a state change.
            InEndState = 0x0000200000000000,   // Within an EndState call.
            Transient = 0x0000400000000000,	// Don't save object.
            Preloading = 0x0000800000000000,   // Data is being preloaded from file.
            LoadForClient = 0x0001000000000000,	// In-file load for client.
            LoadForServer = 0x0002000000000000,	// In-file load for client.
            LoadForEdit = 0x0004000000000000,	// In-file load for client.
            Standalone = 0x0008000000000000,   // Keep object around for editing even if unreferenced.
            NotForClient = 0x0010000000000000,	// Don't load this object for the game client.
            NotForServer = 0x0020000000000000,	// Don't load this object for the game server.
            NotForEdit = 0x0040000000000000,	// Don't load this object for the editor.
            Destroyed = 0x0080000000000000,	// Object Destroy has already been called.
            NeedPostLoad = 0x0100000000000000,   // Object needs to be postloaded.
            HasStack = 0x0200000000000000,	// Has execution stack.
            Native = 0x0400000000000000,   // Native (UClass only).
            Marked = 0x0800000000000000,   // Marked (for debugging).
            ErrorShutdown = 0x1000000000000000,	// ShutdownAfterError called.
            DebugPostLoad = 0x2000000000000000,   // For debugging Serialize calls.
            DebugSerialize = 0x4000000000000000,   // For debugging Serialize calls.
            DebugDestroy = 0x8000000000000000,   // For debugging Destroy calls.
        }

        public static string[] flagdesc =
        {
            "Transactional , 0000000100000000,    Object is transactional.",
            "Unreachable , 0000000200000000,	 Object is not reachable on the object graph.",
            "Public , 0000000400000000,	 Object is visible outside its package.",
            "TagImp , 0000000800000000,	 Temporary import tag in loadsave.",
            "TagExp , 0000001000000000,	 Temporary export tag in loadsave.",
            "Obsolete , 0000002000000000,    Object marked as obsolete and should be replaced.",
            "TagGarbage , 0000004000000000,	 Check during garbage collection.",
            "Final , 0000008000000000,	 Object is not visible outside of class.",
            "PerObjectLocalized , 0000010000000000,	 Object is localized by instance name00000000, not by class.",
            "NeedLoad , 0000020000000000,    During load00000000, indicates object needs loading.",
            "HighlightedName , 0000040000000000,	 A hardcoded name which should be syntax-highlighted.",
            "EliminateObject , 0000040000000000,    NULL out references to this during garbage collecion.",
            "RemappedName , 0000080000000000,    Name is remapped.",
            "Suppress , 0000100000000000,	warning: Mirrored in UnName.h. Suppressed log name.",
            "StateChanged , 0000100000000000,    Object did a state change.",
            "InEndState , 0000200000000000,    Within an EndState call.",
            "Transient , 0000400000000000,	 Don't save object.",
            "Preloading , 0000800000000000,    Data is being preloaded from file.",
            "LoadForClient , 0001000000000000,	 In-file load for client.",
            "LoadForServer , 0002000000000000,	 In-file load for client.",
            "LoadForEdit , 0004000000000000,	 In-file load for client.",
            "Standalone , 0008000000000000,    Keep object around for editing even if unreferenced.",
            "NotForClient , 0010000000000000,	 Don't load this object for the game client.",
            "NotForServer , 0020000000000000,	 Don't load this object for the game server.",
            "NotForEdit , 0040000000000000,	 Don't load this object for the editor.",
            "Destroyed , 0080000000000000,	 Object Destroy has already been called.",
            "NeedPostLoad , 0100000000000000,    Object needs to be postloaded.",
            "HasStack , 0200000000000000,	 Has execution stack.",
            "Native , 0400000000000000,    Native (UClass only).",
            "Marked , 0800000000000000,    Marked (for debugging).",
            "ErrorShutdown , 1000000000000000,	 ShutdownAfterError called.",
            "DebugPostLoad , 2000000000000000,    For debugging Serialize calls.",
            "DebugSerialize , 4000000000000000,    For debugging Serialize calls.",
            "DebugDestroy , 8000000000000000,    For debugging Destroy calls."
        };

        [Flags]
        public enum EExportFlags : uint
        {
            ForcedExport = 1,
            ScriptPatcherExport = 2,
            MemberFieldPatchPending = 4,
            AllFlags = uint.MaxValue, 
        }
    }
}
