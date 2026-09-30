using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.Targets;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>One resolved banner: the art, and where it came from.</summary>
    public class Banner
    {
        /// <summary>The instanced full path the config asked for.</summary>
        public string Path { get; init; }

        /// <summary>PNG bytes of the top mip. Null when the texture resolved but could not be decoded.</summary>
        public byte[] Png { get; init; }

        public int Width { get; init; }
        public int Height { get; init; }

        /// <summary>
        /// The decoded BGRA pixels, when the caller asked to keep them. Null otherwise.
        ///
        /// Kept only on request because it is the whole picture in memory uncompressed, and almost
        /// every caller wants a picture to show rather than pixels to ask questions of.
        /// </summary>
        public byte[] Pixels { get; init; }

        /// <summary>
        /// The alpha at a point given as a fraction of the image, or 0 outside it.
        ///
        /// This is how the Galaxy at War zones answer "which zone is this map in". The zones are
        /// irregular polygons that overlap each other's bounding boxes heavily, so a box test gets
        /// the answer wrong for most of the map - but the art already carries the shape exactly, as
        /// its alpha, and that is the same shape the game draws.
        /// </summary>
        public int AlphaAt(double u, double v)
        {
            if (Pixels == null || Width <= 0 || Height <= 0) return 0;
            if (u is < 0 or > 1 || v is < 0 or > 1) return 0;

            var x = Math.Clamp((int)(u * Width), 0, Width - 1);
            var y = Math.Clamp((int)(v * Height), 0, Height - 1);
            return Pixels[(y * Width + x) * 4 + 3];
        }

        /// <summary>The package the export was found in. Diagnostic, and the answer to "which one won".</summary>
        public string SourceFile { get; init; }

        /// <summary>Why there is no art, in words a person can act on. Null when there is art.</summary>
        public string Problem { get; init; }

        /// <summary>A `data:` URI for the page, or null. Nothing is written to disk.</summary>
        public string DataUri => Png == null ? null : @"data:image/png;base64," + Convert.ToBase64String(Png);
    }

    /// <summary>
    /// Extracts the match-settings banners out of the installed game.
    ///
    /// Maps, factions and difficulties each carry a 512x128 DXT1 banner, and they are the same asset
    /// in every respect. The legacy app shipped its own copies of these; it should not. The real
    /// texture is in the game, is already the right art for whatever version of Milan is installed,
    /// and arrives already attached when Milan adds a difficulty or reskins a map - which is the whole
    /// argument for reading rather than embedding.
    ///
    /// Two things make this more than a file read. The instanced full path names a top-level PACKAGE,
    /// not a file - `GUI_MPImages.MatchSettings.OptMapDish` lives in `SFXMPImagesMatchSettings.pcc` -
    /// so the export has to be found rather than opened. And a texture may hold its mips in the
    /// package (`NeverStream`) or in a TFC, and both forms are in use across every picker.
    /// </summary>
    public class BannerLibrary
    {
        private readonly Dictionary<string, Banner> _byPath = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Decoded art keyed by resolved export, so two paths pointing at one texture decode once.
        ///
        /// Keyed by export rather than by path deliberately: several maps share a banner, and a mod
        /// can repoint one path at another's texture.
        /// </summary>
        private readonly Dictionary<string, Banner> _byExport = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Paths that were asked for and not found, with the reason.</summary>
        public List<string> Problems { get; } = new();

        /// <summary>Packages that were opened looking for banners. Diagnostic; it is the cost of this class.</summary>
        public int PackagesSearched { get; private set; }

        public Banner this[string path] => path == null ? null : _byPath.GetValueOrDefault(path);

        /// <summary>The banner for a path, or null. Never throws for an absent one.</summary>
        public Banner For(string path) => this[path];

        /// <summary>
        /// Resolves and decodes every requested banner.
        ///
        /// One pass over the install's packages, stopping as soon as everything asked for is found -
        /// which in practice is early, because all the banners live in about five packages. Packages
        /// are opened with the export table only and no export data, the same way the class scan
        /// reads them.
        /// </summary>
        /// <param name="maxDimension">
        /// Largest size to keep, on either axis; 0 for the texture's own. A texture does not
        /// necessarily SHIP a smaller mip to fall back on - the galaxy map's art has exactly one, at
        /// 4096 - so the chain is generated rather than picked.
        /// </param>
        /// <param name="keepPixels">Keep the decoded pixels as well as the PNG, for <see cref="Banner.AlphaAt"/>.</param>
        public static BannerLibrary Load(GameTarget target, IEnumerable<string> paths, Action<int, int> progress = null,
                                         int maxDimension = 0, bool keepPixels = false)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var library = new BannerLibrary();
            var wanted = new HashSet<string>(paths?.Where(p => !string.IsNullOrWhiteSpace(p)) ?? [],
                                             StringComparer.OrdinalIgnoreCase);
            if (wanted.Count == 0) return library;

            // A bare object name - "OptMapArmaxBlasteroid" rather than "Package.Group.Object" - cannot
            // be looked up: it does not say which package to open, and searching every package for a
            // matching name can match more than one. Reported, not guessed at.
            foreach (var unqualified in wanted.Where(p => !p.Contains('.')).ToList())
            {
                wanted.Remove(unqualified);
                library.Fail(unqualified, @"the config names a texture without naming its package, so there is nothing to look up");
            }

            var files = MELoadedFiles.GetFilesLoadedInGame(target.Game, forceReload: true, gameRootOverride: target.TargetPath)
                                     .Where(f => !ModMakerCatalogue.IsInCompilerOutput(f.Value))
                                     .ToList();

            // Mount order puts DLC last in this enumeration, and a DLC's texture must win over the
            // basegame's - so it is walked backwards and the first hit for a path is kept.
            for (int i = files.Count - 1; i >= 0 && wanted.Count > 0; i--)
            {
                var file = files[i].Value;
                progress?.Invoke(files.Count - i, files.Count);
                library.PackagesSearched++;

                try
                {
                    using var pack = MEPackageHandler.UnsafePartialLoad(file, _ => false);
                    var hits = pack.Exports.Where(x => x.ClassName == @"Texture2D" && wanted.Contains(x.InstancedFullPath))
                                           .ToList();
                    if (hits.Count == 0) continue;

                    // Reopened fully only when this package actually holds something wanted. The
                    // predicate above loads no export data, which is what keeps the search cheap.
                    using var full = MEPackageHandler.OpenMEPackage(file, forceLoadFromDisk: true);
                    foreach (var hit in hits)
                    {
                        var export = full.FindExport(hit.InstancedFullPath);
                        if (export == null) continue;

                        wanted.Remove(hit.InstancedFullPath);
                        library.Decode(hit.InstancedFullPath, export, target, file, maxDimension, keepPixels);
                    }
                }
                catch (Exception e)
                {
                    // A package that will not open costs its own banners, never the scan.
                    library.Problems.Add($@"{System.IO.Path.GetFileName(file)}: {e.Message}");
                }
            }

            foreach (var missing in wanted)
                library.Fail(missing, @"no Texture2D with this path exists in the installed game");

            return library;
        }

        private void Decode(string path, ExportEntry export, GameTarget target, string file,
                            int maxDimension = 0, bool keepPixels = false)
        {
            var key = $@"{System.IO.Path.GetFileName(file)}|{export.InstancedFullPath}";
            if (_byExport.TryGetValue(key, out var already))
            {
                _byPath[path] = already;
                return;
            }

            Banner banner;
            try
            {
                var texture = new Texture2D(export);
                var mip = texture.GetTopMip();

                // useLowerMipsIfTFCMissing: a missing TFC is a broken install, not a reason to show
                // nothing - a smaller mip of the right art is still the right art.
                var raw = texture.GetImageBytesForMip(mip, target.Game, useLowerMipsIfTFCMissing: true,
                                                      out var used, target.TargetPath);

                var format = Image.getPixelFormatType(texture.TextureFormat);
                var pixelFormat = format;
                var pixels = raw;
                int width = used.width, height = used.height;

                if (maxDimension > 0 && (width > maxDimension || height > maxDimension))
                {
                    // correctMips asked for ARGB is a repeated box filter and nothing else - no block
                    // compression on the way down - so a texture that ships one 4096 mip and nothing
                    // smaller still yields a clean small one. The levels are the point, not the format.
                    var chain = Image.LoadFromRaw(raw, format, width, height);
                    chain.correctMips(PixelFormat.ARGB);

                    var level = chain.mipMaps.FirstOrDefault(m => m.width <= maxDimension && m.height <= maxDimension)
                                ?? chain.mipMaps[^1];
                    pixels = level.data;
                    width = level.width;
                    height = level.height;
                    pixelFormat = PixelFormat.ARGB;
                }

                using var png = Image.convertToPng(pixels, width, height, pixelFormat);

                byte[] argb = null;
                if (keepPixels)
                {
                    // convertRawToARGB takes its dimensions by ref because a block-compressed mip
                    // smaller than one block decodes to a whole block - so it is given copies, and
                    // the banner keeps the size it actually reports.
                    int pixelWidth = width, pixelHeight = height;
                    argb = pixelFormat == PixelFormat.ARGB
                        ? pixels
                        : Image.convertRawToARGB(pixels, ref pixelWidth, ref pixelHeight, pixelFormat);

                    // A decode that came back a different size than the picture would make every
                    // AlphaAt answer wrong by an offset, which is worse than having no answer.
                    if (pixelWidth != width || pixelHeight != height) argb = null;
                }

                banner = new Banner
                {
                    Path = path,
                    Png = png.ToArray(),
                    Width = width,
                    Height = height,
                    Pixels = argb,
                    SourceFile = System.IO.Path.GetFileName(file),
                };
            }
            catch (Exception e)
            {
                // Resolved but undecodable is a different fact from absent, and the editor says so
                // rather than showing an empty box for both.
                banner = new Banner
                {
                    Path = path,
                    SourceFile = System.IO.Path.GetFileName(file),
                    Problem = $@"the texture was found but could not be decoded: {e.Message}",
                };
                Problems.Add($@"{path}: {banner.Problem}");
            }

            _byExport[key] = banner;
            _byPath[path] = banner;
        }

        private void Fail(string path, string reason)
        {
            _byPath[path] = new Banner { Path = path, Problem = reason };
            Problems.Add($@"{path}: {reason}");
        }
    }
}
