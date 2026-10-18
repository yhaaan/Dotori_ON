using System;
using UnityEngine;

namespace DOTORION.Events.Subjugation
{
    /// <summary>A tiny native-size pixel canvas shared by the Unity preview and the transparent Windows window.</summary>
    public sealed class SubjugationPixels : IDisposable
    {
        public const int Width = 480, Height = 64;
        public Color32[] Pixels { get; private set; }
        public Texture2D Texture { get; private set; }
        public int RenderWidth => Texture.width;
        public int RenderHeight => Texture.height;
        private float _scale = 1;
        private SubjugationFrame _success, _failure, _repeatOn, _repeatOff;
        private Font _font;
        private readonly System.Collections.Generic.Dictionary<int, SubjugationFrame> _roundBanners = new System.Collections.Generic.Dictionary<int, SubjugationFrame>();
        private const int BannerWidth = 124, BannerHeight = 22;
        public bool AutoHovered;
        public void SetFont(Font font)
        {
            _font = font; _roundBanners.Clear();
            _success = SubjugationText.Rasterize(font, "성공", 88, 36, 24, FontStyle.Bold);
            _failure = SubjugationText.Rasterize(font, "실패", 88, 36, 24, FontStyle.Bold);
            _repeatOn = SubjugationText.Rasterize(font, "현재 라운드 반복 : ON", 176, 20, 12);
            _repeatOff = SubjugationText.Rasterize(font, "현재 라운드 반복 : OFF", 176, 20, 12);
        }
        public static int PartyX(int count, int slot) => 209 - (count - 1 - slot) * 48;
        private readonly SubjugationArt _art;
        private readonly bool _ownsArt;
        private readonly Color32 _ink = new Color32(67, 58, 59, 255);
        private readonly Color32 _blue = new Color32(117, 184, 222, 255);
        private readonly Color32 _pink = new Color32(247, 167, 173, 255);
        private readonly Color32 _gold = new Color32(249, 209, 100, 255);
        private readonly Color32 _bossRed = new Color32(202, 55, 63, 255);

        public SubjugationPixels(SubjugationArt art = null)
        {
            _ownsArt = art == null;
            _art = art ?? new SubjugationArt();
            SetScale(1);
        }

        public void SetScale(float scale)
        {
            var width = Mathf.RoundToInt(Width * Mathf.Clamp(scale, 1, 4));
            var height = Mathf.RoundToInt(Height * Mathf.Clamp(scale, 1, 4));
            if (Texture != null && Texture.width == width && Texture.height == height) return;
            Release(Texture);
            _scale = (float)width / Width;
            Pixels = new Color32[width * height];
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Subjugation pixel art" };
        }

        public void Draw(SubjugationGame game, int activity, float time)
        {
            Array.Clear(Pixels, 0, Pixels.Length);
            // A low, pale sky band; everything above it has genuine zero alpha.
            for (var y = 0; y < 18; y++) Box(0, y, Width, 1, new Color32((byte)(185 + y * 2), (byte)(225 + y), 248, 255));
            Box(0, 0, Width, 2, new Color32(105, 157, 96, 255));
            var scroll = time * 12;
            for (var i = 0; i < 5; i++)
            {
                var x = (int)((i * 123 + 480 - scroll % 615) % 615) - 30;
                Mountain(x, 5);
                Tree((x + 70) % 480, 2);
            }
            var party = 0;
            for (var i = 0; i < 6; i++)
            {
                if (!game.Save.party[i]) continue;
                var x = PartyX(game.PartyCount, party++);
                var down = game.IsDown(i);
                var phase = activity == 0 && !down ? game.AttackFrame(i) : 0;
                var forward = phase == 1 ? -2 : phase == 2 ? 6 : phase == 3 ? 2 : 0;
                var hop = activity == 0 && game.Outcome == 0 && !down ? (i == 2 ? new[] { 0, 4, 7, 2 }[phase] : i == 3 ? new[] { 0, 6, 4, 0 }[phase] : 0) : 0;
                if (down) Blit(_art.Down(i), x, 3, SubjugationArt.CombatDisplayExtent, true, _ink);
                else Character(x + forward, 2 + hop, i, activity, phase);
                if (activity == 0 && game.Outcome == 0 && !down && phase == 2) AttackEffect(x + 50, 17 + hop, i);
                if (activity == 0)
                {
                    Box(x + 16, 55 + hop, 32, 3, _ink);
                    Box(x + 17, 56 + hop, (int)(30 * game.MemberHealth(i) / game.MaxHealth(i)), 1, new Color32(117, 219, 130, 255));
                }
            }
            if (activity == 0 && game.Outcome != 1)
            {
                const int x = 265;
                Blit(_art.Monster(game.Save.round), x, 4, SubjugationArt.CombatDisplayExtent, true, _ink);
                Box(x + 8, 55, 36, 3, _ink);
                Box(x + 9, 56, (int)(34 * game.EnemyHp / game.EnemyMaxHp), 1, game.IsBossRound ? _bossRed : _pink);
            }
            if (activity == 0)
            {
                var slot = 0;
                var slots = new int[6];
                for (var i = 0; i < 6; i++) if (game.Save.party[i]) slots[i] = slot++;
                foreach (var number in game.Numbers)
                {
                    var x = number.enemy ? PartyX(game.PartyCount, slots[number.target]) + 20 : number.healing ? PartyX(game.PartyCount, slots[number.target]) + 15 : 269 + number.actor * 4;
                    var y = 28 + (int)(number.age * 23) + (number.enemy ? 0 : number.actor * 3);
                    Digits((number.healing ? "+" : "") + Math.Ceiling(number.amount), x, y, number.healing ? new Color32(27, 137, 72, 255) : number.enemy ? new Color32(202, 55, 63, 255) : _ink);
                }
            }
            Box(348, 24, 102, 18, new Color32(236, 248, 255, 255));
            Box(453, 24, 24, 18, game.RepeatRound ? new Color32(126, 211, 174, 255) : new Color32(193, 202, 208, 255));
            // Two opposed arrows form the repeat loop; green means repeat ON.
            Box(458, 35, 12, 2, _ink); Box(468, 30, 2, 6, _ink);
            Box(466, 33, 2, 2, _ink); Box(464, 31, 2, 2, _ink);
            Box(459, 28, 11, 2, _ink); Box(458, 29, 2, 5, _ink);
            Box(460, 30, 2, 2, _ink); Box(462, 32, 2, 2, _ink);
            Digits("R." + game.Save.round, 358, 27, game.IsBossRound ? _bossRed : _ink);
            // Coin area is the only button. Native hit testing uses this same rectangle.
            Box(348, 3, 129, 18, new Color32(236, 248, 255, 255));
            Box(348, 3, 129, 1, _blue);
            Oval(353, 7, 9, 9, _ink); Oval(354, 8, 7, 7, _gold);
            var coins = game.Save.coins > 9999999 ? "9999999+" : game.Save.coins.ToString();
            Digits(coins, 369, 8, _ink);
            Box(3, 13, 2, 2, _blue); Box(7, 13, 2, 2, _blue);
            Box(3, 9, 2, 2, _blue); Box(7, 9, 2, 2, _blue);
            if (AutoHovered)
            {
                Box(297, 44, 180, 20, new Color32(245, 251, 255, 255));
                Blit(game.RepeatRound ? _repeatOn : _repeatOff, 299, 44, 176, false, _ink);
            }
            if (activity == 0 && game.Outcome == 0 && game.RoundBannerRemaining > 0) RoundBanner(game);
            if (activity == 0 && game.Outcome != 0)
            {
                // Source-over black keeps the scene visible and covers transparent window areas too.
                const float opacity = .6f;
                for (var i = 0; i < Pixels.Length; i++)
                {
                    var background = Pixels[i];
                    var remaining = background.a / 255f * (1 - opacity);
                    var alpha = opacity + remaining;
                    Pixels[i] = new Color32((byte)(background.r * remaining / alpha),
                        (byte)(background.g * remaining / alpha), (byte)(background.b * remaining / alpha),
                        (byte)Mathf.RoundToInt(alpha * 255));
                }
                var rewardText = "+" + game.LastClearReward.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var rewardWidth = game.Outcome == 1 ? 22 + rewardText.Length * 12 : 0;
                var resultLeft = (Width - 88 - rewardWidth) / 2;
                Blit(game.Outcome == 1 ? _success : _failure, resultLeft, (Height - 36) / 2,
                    88, false, new Color32(255, 255, 255, 255));
                if (game.Outcome == 1)
                {
                    var coinX = resultLeft + 92;
                    Oval(coinX, 25, 14, 14, new Color32(196, 139, 36, 255));
                    Oval(coinX + 2, 27, 10, 10, _gold);
                    Box(coinX + 6, 29, 2, 6, new Color32(196, 139, 36, 255));
                    Digits(rewardText, coinX + 18, 24, _gold, 3);
                }
            }
            Texture.SetPixels32(Pixels);
            Texture.Apply(false);
        }

        /// <summary>"ROUND n" on a small opaque plate above the fight. Opaque so the keyed Windows strip needs no alpha surface.</summary>
        private void RoundBanner(SubjugationGame game)
        {
            var round = game.Save.round;
            if (!_roundBanners.TryGetValue(round, out var text))
            {
                if (_font == null) return;
                text = _roundBanners[round] = SubjugationText.Rasterize(_font, "ROUND " + round, BannerWidth, BannerHeight, 15, FontStyle.Bold);
            }
            const int left = 220 - BannerWidth / 2, bottom = 36;
            var plate = game.IsBossRound ? _bossRed : new Color32(52, 58, 84, 255);
            Box(left + 1, bottom, BannerWidth - 2, BannerHeight, plate);
            Box(left, bottom + 1, BannerWidth, BannerHeight - 2, plate);
            Box(left + 1, bottom + 1, BannerWidth - 2, 1, _gold);
            Box(left + 1, bottom + BannerHeight - 2, BannerWidth - 2, 1, _gold);
            Blit(text, left, bottom, BannerWidth, false, new Color32(255, 255, 255, 255));
        }

        private void Character(int x, int y, int id, int activity, int phase)
        {
            // Four discrete pose keys per fighter: pull back, wind up, impact, recover.
            // Affine transforms preserve the approved faces and Usagi's distinctive mouth.
            var lean = activity != 0 ? 0 : id == 0 ? new[] { 0, -4, 7, 2 }[phase] : id == 1 ? new[] { 0, -6, 10, 3 }[phase] : id == 2 ? new[] { 0, -3, 12, 4 }[phase] : id == 3 ? new[] { 0, -9, 6, 0 }[phase] : id == 4 ? new[] { 0, 2, -2, 0 }[phase] : new[] { 0, -10, 14, 4 }[phase];
            Blit(_art.Frame(id, activity), x, y + 2, SubjugationArt.CombatDisplayExtent, true, _ink, lean);
        }
        private void AttackEffect(int x, int y, int id)
        {
            if (id == 4)
            {
                Box(x - 24, y + 14, 3, 11, new Color32(34, 171, 108, 255));
                Box(x - 28, y + 18, 11, 3, new Color32(34, 171, 108, 255)); return;
            }
            var color = id == 1 ? _blue : id == 0 ? _pink : _gold;
            if (id <= 2) { Box(x, y + 4, 12, 1, color); Box(x + 8, y + 1, 2, 7, color); }
            else for (var i = 0; i < 10; i++) Box(x + i, y + (id == 5 ? i : 9 - i), 2, 2, color);
        }
        private void Blit(SubjugationFrame source, int x, int y, int extent, bool pixelArt, Color32 tint, int lean = 0)
        {
            if (source == null) return;
            var factor = (float)extent / source.Extent;
            var step = SubjugationArt.CombatPixelScale;
            // Derive output size from the reduced master, not from a second independent
            // aspect-ratio rounding. Every hero and monster dot occupies exactly the same grid.
            var logicalWidth = pixelArt ? source.MasterWidth * step : source.Width * factor;
            var logicalHeight = pixelArt ? source.MasterHeight * step : source.Height * factor;
            var width = Math.Max(1, Mathf.RoundToInt(logicalWidth * _scale));
            var height = Math.Max(1, Mathf.RoundToInt(logicalHeight * _scale));
            var resized = pixelArt ? source.PixelArt(width, height) : source.Resize(width, height);
            var logicalLeft = x + (pixelArt ? (64 - logicalWidth) / 2 : 0);
            if (pixelArt) { logicalLeft = Mathf.RoundToInt(logicalLeft / step) * step; y = Mathf.RoundToInt((float)y / step) * step; }
            var left = Mathf.RoundToInt(logicalLeft * _scale);
            var bottom = Mathf.RoundToInt(y * _scale);
            for (var row = 0; row < height; row++) for (var col = 0; col < width; col++)
            {
                var pixel = resized[row * width + col];
                if (pixel.a == 0) continue;
                if (!pixelArt) { pixel.r = tint.r; pixel.g = tint.g; pixel.b = tint.b; }
                var masterRow = row * source.MasterHeight / height;
                var shear = pixelArt ? Mathf.RoundToInt((float)lean * masterRow / source.MasterHeight / step) * step : 0;
                var px = left + col + Mathf.RoundToInt(shear * _scale); var py = bottom + row;
                if (px < 0 || px >= RenderWidth || py < 0 || py >= RenderHeight) continue;
                var index = py * RenderWidth + px;
                var background = Pixels[index];
                var a = pixel.a / 255f; var b = background.a / 255f * (1 - a); var alpha = a + b;
                Pixels[index] = new Color32((byte)((pixel.r * a + background.r * b) / alpha),
                    (byte)((pixel.g * a + background.g * b) / alpha), (byte)((pixel.b * a + background.b * b) / alpha),
                    (byte)Mathf.RoundToInt(alpha * 255));
            }
        }

        private void Tree(int x, int y)
        {
            Box(x + 5, y, 3, 7, new Color32(133, 113, 90, 255));
            for (var i = 0; i < 13; i++) Box(x + i / 2, y + 5 + i, Math.Max(1, 14 - i), 1, new Color32(104, 166, 130, 255));
        }
        private void Mountain(int x, int y)
        {
            for (var i = 0; i < 16; i++) Box(x + i, y + i, 33 - i * 2, 1, new Color32(161, 196, 210, 255));
        }
        private void Oval(int x, int y, int w, int h, Color32 color)
        {
            for (var j = 0; j < h; j++) for (var i = 0; i < w; i++)
                if (Math.Pow((i + .5 - w / 2d) / (w / 2d), 2) + Math.Pow((j + .5 - h / 2d) / (h / 2d), 2) <= 1) Box(x + i, y + j, 1, 1, color);
        }
        private void Box(int x, int y, int w, int h, Color32 color)
        {
            for (var j = Math.Max(0, Mathf.RoundToInt(y * _scale)); j < Math.Min(RenderHeight, Mathf.RoundToInt((y + h) * _scale)); j++)
                for (var i = Math.Max(0, Mathf.RoundToInt(x * _scale)); i < Math.Min(RenderWidth, Mathf.RoundToInt((x + w) * _scale)); i++) Pixels[j * RenderWidth + i] = color;
        }
        private static readonly string[] Glyphs = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001", "111100111001111", "111100111101111", "111001010010010", "111101111101111", "111101111001111", "110101110101101", "000000000000010", "111001010100111", "000010111010000" };
        private void Digits(string value, int x, int y, Color32 color, int size = 2)
        {
            foreach (var c in value)
            {
                var index = c >= '0' && c <= '9' ? c - '0' : c == 'R' ? 10 : c == '.' ? 11 : c == 'Z' ? 12 : 13;
                for (var row = 0; row < 5; row++) for (var col = 0; col < 3; col++)
                    if (Glyphs[index][row * 3 + col] == '1') Box(x + col * size, y + (4 - row) * size, size, size, color);
                x += size * 4;
            }
        }
        // Chiikawa's pink sasumata: a U fork with white teeth on one pink shaft. Fill only, top row first;
        // the dark outline is derived around it.
        private static readonly string[] SasumataRows =
        {
            "........................",
            "....PP............PP....",
            "...W.PP..........PP.W...",
            "...WWPP..........PPWW...",
            ".....PP.W......W.PP.....",
            ".....PPWW......WWPP.....",
            "...W.PP..........PP.W...",
            "...WWPP..........PPWW...",
            ".....PP..........PP.....",
            "...W.PP..........PP.W...",
            "...WWPP..........PPWW...",
            ".....PPP........PPP.....",
            "......PPP......PPP......",
            ".......PPPPPPPPPP.......",
            ".........PPPPPP.........",
            "..........PPPP..........",
            "...........PP...........",
            "...........PP...........",
            "...........PP...........",
            "...........PP...........",
            "...........PP...........",
            "...........PP...........",
            "...........PP...........",
            "...........PP...........", // The shaft runs off the bottom edge, uncapped.
        };

        public static Sprite WeaponIcon()
        {
            const int size = 24;
            bool Filled(int x, int y) => x >= 0 && y >= 0 && x < size && y < size && SasumataRows[y][x] != '.';
            var outline = new Color32(74, 40, 44, 255);
            var pink = new Color32(247, 158, 181, 255); var pinkShade = new Color32(222, 118, 150, 255);
            var white = new Color32(255, 255, 255, 255); var whiteShade = new Color32(214, 220, 232, 255);
            var p = new Color32[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var c = SasumataRows[y][x];
                Color32 color;
                if (c == 'P') color = SasumataRows[y][Math.Min(size - 1, x + 1)] != 'P' && x > 0 && SasumataRows[y][x - 1] == 'P' ? pinkShade : pink;
                else if (c == 'W') color = white;
                else if (c == 'G') color = Filled(x, y + 1) ? white : whiteShade;
                else if (Filled(x + 1, y) || Filled(x - 1, y) || Filled(x, y + 1) || Filled(x, y - 1)) color = outline;
                else continue;
                p[(size - 1 - y) * size + x] = color; // Texture rows run bottom-up.
            }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Chiikawa sasumata" };
            t.SetPixels32(p); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 1);
        }
        public void Dispose()
        {
            Release(Texture);
            if (_ownsArt) _art.Dispose();
        }
        public static void Release(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
