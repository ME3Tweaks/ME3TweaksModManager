using System;
using System.Collections.Generic;
using System.Linq;
using ME3TweaksCore.Targets;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>One region of the Galaxy at War map, as the lobby draws it.</summary>
    public class GalaxyZone
    {
        /// <summary>The zone's index in the game's own ordering, from SFXGUI_MPLobby's GalaxyAtWarMap.</summary>
        public int Id { get; init; }

        /// <summary>A name for a person. The lobby's own labels come from live war-asset data, not from config.</summary>
        public string Name { get; init; }

        /// <summary>The Texture2D the movie paints this region with.</summary>
        public string ImagePath { get; init; }

        /// <summary>Where the region sits, in the same space a map's GalaxyAtWarMapPosition is written in.</summary>
        public double X0 { get; init; }
        public double Y0 { get; init; }
        public double X1 { get; init; }
        public double Y1 { get; init; }

        /// <summary>The decoded art, or null when it could not be read.</summary>
        public Banner Art { get; set; }

        /// <summary>
        /// Whether a position falls inside this region's painted area.
        ///
        /// The bounding box is only the first test. The regions are irregular polygons whose boxes
        /// overlap each other heavily - Attican's box alone spans nearly the whole map - so the shape
        /// has to come from the art's alpha, which is the same shape the game draws.
        /// </summary>
        public bool Contains(double x, double y, int alphaThreshold = GalaxyMap.AlphaThreshold)
        {
            if (x < X0 || x > X1 || y < Y0 || y > Y1) return false;
            if (Art?.Pixels == null) return false;

            return Art.AlphaAt((x - X0) / (X1 - X0), (y - Y0) / (Y1 - Y0)) >= alphaThreshold;
        }
    }

    /// <summary>
    /// The Galaxy at War map: the picture a map's position is a position ON.
    ///
    /// `GalaxyAtWarMapPosition` is two numbers with no stated units, and until you know what they are
    /// measured against they cannot be drawn - which is why the editor originally scaled them against
    /// their own measured extent, stretching whatever maps happen to exist to fill the box.
    ///
    /// They are coordinates on the lobby's galaxy image, and the geometry below was read out of the
    /// movie that draws it (`GUI_SF_GalaxyAtWar.galaxyAtWar` in BIOP_MP_COMMON) with LECCLI's
    /// find-swf-instance and dump-swf-structure:
    ///
    ///   root -> "screen" (GalaxyAtWar) -> "GAWMap" (GalaxyAtWarMap)
    ///           +- "GalaxyMapMC"       at (307.2, 256.0) scale (0.5999908, 0.5)
    ///           |    +- a 1024x1024 shape at the origin, filled with galaxyAtWar_I31
    ///           +- "positionalMarker"  <- SetPositionalMarker(..., nX, nY) writes here
    ///
    /// The marker is a SIBLING of the map clip, so nX/nY are in GalaxyAtWarMap's space, and the image
    /// - placed at 307.2 = 512 x 0.6 and 256 = 512 x 0.5 - covers exactly x 0..614.4 and y 0..512 of
    /// it. So the space is NOT square even though the texture is: the game stretches the square art
    /// 1.2:1, and anything drawing it undistorted puts every map in the wrong place horizontally.
    ///
    /// The geometry is fixed here rather than read at runtime, because reading it means parsing a SWF
    /// and ModMaker does not edit one. The ART is still read from the install, so a reskin shows.
    /// </summary>
    public class GalaxyMap
    {
        /// <summary>The galaxy itself.</summary>
        public const string BackgroundPath = @"GUI_SF_GalaxyAtWar.galaxyAtWar_I31";

        /// <summary>The extent of the coordinate space map positions are written in.</summary>
        public const double SpaceWidth = 614.4;
        public const double SpaceHeight = 512.0;

        /// <summary>
        /// How opaque a region's art has to be at a point to count as covering it.
        ///
        /// Above zero because the regions are drawn with soft edges, and a pixel that is one part in
        /// two hundred visible is not somewhere a firebase is. Travels to the page in the payload so
        /// that the editor and anything checking a position agree on where the boundaries are.
        /// </summary>
        public const int AlphaThreshold = 32;

        /// <summary>
        /// Big enough to look at, small enough to hand to a web page as a data URI.
        ///
        /// The background ships as a single 4096x4096 mip with nothing smaller behind it, which is
        /// 13MB of PNG and about 18MB once base64'd - so the smaller level is generated, not chosen.
        /// </summary>
        public const int MaxDimension = 512;

        /// <summary>
        /// The five regions, in the game's own order (ZONE_INNERCOUNCIL = 0 ... ZONE_ATTICAN = 4),
        /// with the boxes each one is drawn in.
        /// </summary>
        private static readonly GalaxyZone[] ZoneLayout =
        [
            new() { Id = 0, Name = @"Inner Council Space",  ImagePath = @"GUI_SF_GalaxyAtWar.galaxyAtWar_I3B", X0 = 127.3, Y0 = 256.6, X1 = 364.3, Y1 = 511.6 },
            new() { Id = 1, Name = @"Terminus Systems",     ImagePath = @"GUI_SF_GalaxyAtWar.galaxyAtWar_I43", X0 = 24.0,  Y0 = 28.5,  X1 = 572.4, Y1 = 213.0 },
            new() { Id = 2, Name = @"Earth and Alliance",   ImagePath = @"GUI_SF_GalaxyAtWar.galaxyAtWar_I37", X0 = 257.5, Y0 = 288.1, X1 = 559.9, Y1 = 485.6 },
            new() { Id = 3, Name = @"Outer Council Space",  ImagePath = @"GUI_SF_GalaxyAtWar.galaxyAtWar_I3F", X0 = 10.8,  Y0 = 236.6, X1 = 207.0, Y1 = 435.6 },
            new() { Id = 4, Name = @"Attican Traverse",     ImagePath = @"GUI_SF_GalaxyAtWar.galaxyAtWar_I33", X0 = 34.2,  Y0 = 114.5, X1 = 607.8, Y1 = 357.0 },
        ];

        /// <summary>Every path this class needs out of the install.</summary>
        public static IEnumerable<string> ImagePaths =>
            new[] { BackgroundPath }.Concat(ZoneLayout.Select(z => z.ImagePath));

        /// <summary>The galaxy art, or null when it could not be read.</summary>
        public Banner Background { get; private set; }

        public IReadOnlyList<GalaxyZone> Zones { get; private set; } = [];

        /// <summary>Whatever could not be read, in words. The editor still works without any of it.</summary>
        public List<string> Problems { get; } = new();

        /// <summary>True when there is a picture to draw the positions on.</summary>
        public bool Available => Background?.Png != null;

        /// <summary>
        /// The region a position falls in, or null.
        ///
        /// Null is a real answer, not a failure: plenty of the galaxy belongs to no region, and the
        /// in-development maps parked at (0, 0) are in none of them.
        /// </summary>
        public GalaxyZone ZoneAt(double x, double y) => Zones.FirstOrDefault(z => z.Contains(x, y));

        public static GalaxyMap Load(GameTarget target, Action<int, int> progress = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var map = new GalaxyMap();

            // keepPixels because the zones are asked which maps they contain, and that question is
            // answered by the art's alpha rather than by its bounding box.
            var art = BannerLibrary.Load(target, ImagePaths, progress, MaxDimension, keepPixels: true);

            map.Background = art.For(BackgroundPath);
            if (map.Background?.Png == null)
            {
                map.Problems.Add($@"The Galaxy at War background ({BackgroundPath}) could not be read; " +
                                 @"map positions will be shown on an empty field.");
            }

            var zones = new List<GalaxyZone>();
            foreach (var layout in ZoneLayout)
            {
                var zone = new GalaxyZone
                {
                    Id = layout.Id,
                    Name = layout.Name,
                    ImagePath = layout.ImagePath,
                    X0 = layout.X0,
                    Y0 = layout.Y0,
                    X1 = layout.X1,
                    Y1 = layout.Y1,
                    Art = art.For(layout.ImagePath),
                };

                if (zone.Art?.Pixels == null)
                    map.Problems.Add($@"{layout.Name}'s art ({layout.ImagePath}) could not be read; " +
                                     @"maps will not be attributed to it.");

                zones.Add(zone);
            }

            map.Zones = zones;
            return map;
        }
    }
}
