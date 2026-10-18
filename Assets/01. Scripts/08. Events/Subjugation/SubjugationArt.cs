using System;
using UnityEngine;

namespace DOTORION.Events.Subjugation
{
    /// <summary>Approved authored pixel sprites, without the rejected averaging/mosaic conversion.</summary>
    public sealed class SubjugationArt : IDisposable
    {
        public const int CombatPixelExtent = 48;
        public const int CombatDisplayExtent = 48;
        public const int PortraitPixelExtent = 32;
        public const int CombatPixelScale = 1;
        public const string ResourcePath = "DOTORION/Events/Art/ChiikawaApprovedHeroes-v4";
        public const string EnemyResourcePath = "DOTORION/Events/Art/ChiikawaApprovedEnemies-v4";
        public const string StateResourcePath = "DOTORION/Events/Art/ChiikawaApprovedStates-v4";
        public const int MonsterCount = 10;
        public static readonly string[] MonsterNames = { "키메라", "그 아이", "고블린", "데카츠요", "거대한 새", "두부 의태형", "거대 장수풍뎅이", "세이렌", "야만바", "검은 유성" };

        // Explicit top-left source regions keep weapons, ears and tails whole even where
        // the generated silhouettes extend beyond geometric atlas cell boundaries.
        private static readonly RectInt[] CombatBounds = {
            new RectInt(90, 148, 434, 372), new RectInt(583, 146, 450, 375), new RectInt(1105, 43, 385, 480),
            new RectInt(37, 563, 443, 407), new RectInt(552, 566, 424, 407), new RectInt(1041, 563, 467, 410)
        };
        private static readonly RectInt[] FaceBounds = {
            new RectInt(93, 151, 302, 246), new RectInt(595, 148, 303, 250), new RectInt(1109, 44, 305, 380),
            new RectInt(155, 564, 325, 277), new RectInt(557, 568, 418, 287), new RectInt(1095, 566, 345, 258)
        };
        private static readonly RectInt[] StateBounds = {
            new RectInt(32, 100, 226, 247), new RectInt(323, 100, 233, 247), new RectInt(615, 30, 235, 317),
            new RectInt(866, 98, 307, 253), new RectInt(1188, 97, 287, 254), new RectInt(1496, 100, 260, 253),
            new RectInt(30, 375, 230, 250), new RectInt(317, 374, 239, 252), new RectInt(615, 347, 235, 286),
            new RectInt(865, 376, 310, 257), new RectInt(1185, 374, 291, 258), new RectInt(1495, 373, 264, 261),
            new RectInt(16, 650, 263, 227), new RectInt(300, 650, 279, 227), new RectInt(581, 653, 292, 210),
            new RectInt(873, 652, 311, 181), new RectInt(1180, 651, 281, 181), new RectInt(1496, 650, 261, 220)
        };
        private readonly SubjugationFrame[] _frames = new SubjugationFrame[24];
        private readonly SubjugationFrame[] _monsters = new SubjugationFrame[MonsterCount];
        private readonly SubjugationFrame[] _down = new SubjugationFrame[6];
        private readonly Sprite[] _portraits = new Sprite[6];
        private readonly Texture2D[] _portraitTextures = new Texture2D[6];
        public bool IsLoaded { get; }

        public SubjugationArt()
        {
            var heroes = Load(ResourcePath); var enemies = Load(EnemyResourcePath); var states = Load(StateResourcePath);
            if (heroes.width != 1536 || heroes.height != 1024 || enemies.width != 1983 || enemies.height != 793 || states.width != 1774 || states.height != 887)
                throw new InvalidOperationException("Approved pixel atlas dimensions changed; update its recorded sprite regions.");
            var heroPixels = heroes.GetPixels32(); var enemyPixels = enemies.GetPixels32(); var statePixels = states.GetPixels32();
            for (var id = 0; id < 6; id++)
            {
                _frames[id] = Read(heroes, heroPixels, CombatBounds[id], CombatPixelExtent);
                _frames[6 + id] = Read(states, statePixels, StateBounds[id], CombatPixelExtent);
                _frames[12 + id] = Read(states, statePixels, StateBounds[6 + id], CombatPixelExtent);
                _down[id] = Read(states, statePixels, StateBounds[12 + id], CombatPixelExtent);
                var face = _frames[18 + id] = Read(heroes, heroPixels, FaceBounds[id], PortraitPixelExtent);
                var texture = new Texture2D(face.MasterWidth, face.MasterHeight, TextureFormat.RGBA32, false)
                    { name = "Approved pixel portrait " + SubjugationGame.Names[id], filterMode = FilterMode.Point };
                texture.SetPixels32(face.PixelArt(face.MasterWidth, face.MasterHeight)); texture.Apply(false);
                _portraitTextures[id] = texture;
                _portraits[id] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), PortraitPixelExtent, 0, SpriteMeshType.FullRect);
            }
            // This sheet's transparent row gutter is between y=419 and y=425.
            const int splitY = 422;
            var columns = new[] { new[] { 0, 402, 832, 1155, 1606, 1983 }, new[] { 0, 430, 798, 1202, 1582, 1983 } };
            for (var id = 0; id < MonsterCount; id++)
            {
                var col = id % 5; var row = id / 5;
                var left = columns[row][col]; var right = columns[row][col + 1];
                var top = row == 0 ? 0 : splitY; var bottom = row == 0 ? splitY : enemies.height;
                _monsters[id] = Read(enemies, enemyPixels, new RectInt(left, top, right - left, bottom - top), CombatPixelExtent);
            }
            IsLoaded = true;
        }
        private static Texture2D Load(string path)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null || !texture.isReadable) throw new InvalidOperationException("Approved pixel atlas missing or unreadable: " + path);
            return texture;
        }
        private static SubjugationFrame Read(Texture2D atlas, Color32[] pixels, RectInt region, int extent)
        {
            int minX = region.xMax, maxX = -1, minY = region.yMax, maxY = -1;
            for (var y = region.y; y < region.yMax; y++) for (var x = region.x; x < region.xMax; x++)
            {
                if (pixels[(atlas.height - 1 - y) * atlas.width + x].a < 200) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            if (maxX < minX) throw new InvalidOperationException("Empty approved sprite region.");
            var w = maxX - minX + 1; var h = maxY - minY + 1; var source = new Color32[w * h];
            for (var y = 0; y < h; y++) Array.Copy(pixels, (atlas.height - 1 - maxY + y) * atlas.width + minX, source, y * w, w);
            return new SubjugationFrame(source, w, h, Math.Max(w, h), extent, authoredPixels: true);
        }
        public SubjugationFrame Frame(int id, int activity) => _frames[(activity == 2 ? 2 : activity == 0 ? 0 : 1) * 6 + id];
        public SubjugationFrame PortraitFrame(int id) => _frames[18 + id];
        public Sprite Portrait(int id) => _portraits[id];
        public SubjugationFrame Monster(int round) => _monsters[(round - 1) % MonsterCount];
        public SubjugationFrame Down(int id) => _down[id];
        public void Dispose()
        {
            foreach (var portrait in _portraits) SubjugationPixels.Release(portrait);
            foreach (var texture in _portraitTextures) SubjugationPixels.Release(texture);
        }
    }

    /// <summary>Untouched source pixels, resized once to the final physical display dimensions.</summary>
    public sealed class SubjugationFrame
    {
        public readonly Color32[] Source;
        public readonly int Width, Height, Extent;
        private readonly int _pixelExtent;
        private readonly bool _enemyPalette;
        public bool UsesAuthoredPixels { get; }
        public int PixelExtent => _pixelExtent;
        public int MasterWidth => Math.Max(1, Mathf.RoundToInt(Width * (float)_pixelExtent / Extent));
        public int MasterHeight => Math.Max(1, Mathf.RoundToInt(Height * (float)_pixelExtent / Extent));
        private Color32[] _pixelMaster;
        private int _masterWidth, _masterHeight;
        private static readonly Color32[] Palette =
        {
            new Color32(57, 27, 17, 255), new Color32(85, 48, 34, 255), new Color32(132, 84, 58, 255),
            new Color32(255, 253, 246, 255), new Color32(240, 234, 217, 255), new Color32(255, 238, 190, 255),
            new Color32(255, 245, 211, 255), new Color32(242, 215, 158, 255), new Color32(116, 179, 214, 255),
            new Color32(156, 208, 236, 255), new Color32(201, 233, 248, 255), new Color32(48, 109, 152, 255),
            new Color32(247, 158, 181, 255), new Color32(255, 195, 205, 255), new Color32(233, 106, 143, 255),
            new Color32(239, 143, 84, 255), new Color32(248, 172, 108, 255), new Color32(178, 191, 202, 255),
            new Color32(222, 232, 238, 255), new Color32(94, 114, 132, 255), new Color32(246, 201, 52, 255),
            new Color32(255, 225, 96, 255),
            // Canonical enemy colors: goblin green, Yamanba olive, siren red and black star charcoal.
            new Color32(178, 215, 119, 255), new Color32(218, 235, 172, 255), new Color32(91, 124, 69, 255),
            new Color32(101, 110, 68, 255), new Color32(55, 54, 64, 255), new Color32(75, 74, 83, 255),
            new Color32(205, 79, 60, 255), new Color32(137, 58, 47, 255)
        };
        private readonly System.Collections.Generic.Dictionary<long, Color32[]> _sizes =
            new System.Collections.Generic.Dictionary<long, Color32[]>();
        private readonly System.Collections.Generic.Dictionary<long, Color32[]> _pixelSizes =
            new System.Collections.Generic.Dictionary<long, Color32[]>();

        public SubjugationFrame(Color32[] source, int width, int height, int extent, int pixelExtent = SubjugationArt.CombatPixelExtent, bool enemyPalette = false, bool authoredPixels = false)
        { Source = source; Width = width; Height = height; Extent = extent; _pixelExtent = pixelExtent; _enemyPalette = enemyPalette; UsesAuthoredPixels = authoredPixels; }

        public Color32[] PixelArt(int width, int height)
        {
            var key = ((long)width << 32) | (uint)height;
            if (_pixelSizes.TryGetValue(key, out var cached)) return cached;
            if (_pixelMaster == null)
            {
                _masterWidth = MasterWidth;
                _masterHeight = MasterHeight;
                _pixelMaster = new Color32[_masterWidth * _masterHeight];
                if (UsesAuthoredPixels)
                {
                    // Keep authored colors and crisp features: sample original pixel centers.
                    // No area average, palette remap or rejected 24-dot mosaic conversion.
                    for (var y = 0; y < _masterHeight; y++) for (var x = 0; x < _masterWidth; x++)
                    {
                        var sx = Math.Min(Width - 1, (int)((x + .5) * Width / _masterWidth));
                        var sy = Math.Min(Height - 1, (int)((y + .5) * Height / _masterHeight));
                        var pixel = Source[sy * Width + sx];
                        if (pixel.a < 200) continue;
                        pixel.a = 255; _pixelMaster[y * _masterWidth + x] = pixel;
                    }
                }
                else
                {
                    var reduced = Resize(_masterWidth, _masterHeight);
                    for (var i = 0; i < reduced.Length; i++)
                    {
                        var p = reduced[i];
                        if (p.a < 128) continue;
                        var best = int.MaxValue;
                        // Green/olive enemy shades must not tint averaged hero face pixels.
                        var paletteCount = _enemyPalette ? Palette.Length : 22;
                        for (var paletteIndex = 0; paletteIndex < paletteCount; paletteIndex++)
                        {
                            var candidate = Palette[paletteIndex];
                            var dr = p.r - candidate.r; var dg = p.g - candidate.g; var db = p.b - candidate.b;
                            var distance = dr * dr + dg * dg + db * db;
                            if (distance < best) { best = distance; _pixelMaster[i] = candidate; }
                        }
                    }
                }
            }
            var output = new Color32[width * height];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                output[y * width + x] = _pixelMaster[y * _masterHeight / height * _masterWidth + x * _masterWidth / width];
            _pixelSizes[key] = output;
            return output;
        }

        public Color32[] Resize(int width, int height)
        {
            var key = ((long)width << 32) | (uint)height;
            if (_sizes.TryGetValue(key, out var cached)) return cached;
            var output = new Color32[width * height];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var x0 = (double)x * Width / width; var x1 = (double)(x + 1) * Width / width;
                var y0 = (double)y * Height / height; var y1 = (double)(y + 1) * Height / height;
                double alpha = 0, red = 0, green = 0, blue = 0;
                for (var sy = (int)y0; sy < Math.Min(Height, (int)Math.Ceiling(y1)); sy++)
                for (var sx = (int)x0; sx < Math.Min(Width, (int)Math.Ceiling(x1)); sx++)
                {
                    var weight = (Math.Min(x1, sx + 1) - Math.Max(x0, sx)) * (Math.Min(y1, sy + 1) - Math.Max(y0, sy));
                    var p = Source[sy * Width + sx]; var a = p.a * weight;
                    alpha += a; red += p.r * a; green += p.g * a; blue += p.b * a;
                }
                if (alpha > 0) output[y * width + x] = new Color32(
                    (byte)Math.Round(red / alpha), (byte)Math.Round(green / alpha), (byte)Math.Round(blue / alpha),
                    (byte)Math.Max(0, Math.Min(255, Math.Round(alpha / ((x1 - x0) * (y1 - y0))))));
            }
            _sizes[key] = output;
            return output;
        }
    }
}
