using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class UIAssetGenerator
    {
        public const string ArtDirectory = "Assets/_Project/Art/UI";
        public const string MaterialsDirectory = "Assets/_Project/Art/Materials";
        public const string MainMenuBackdropMatPath = MaterialsDirectory + "/M_MainMenuBackdrop.mat";
        public const string LobbyBackdropMatPath = MaterialsDirectory + "/M_LobbyBackdrop.mat";

        public const string BgMainMenu = "Bg_MainMenu_SciFi.png";
        public const string BgLobby = "Bg_Lobby_Hangar.png";
        public const string BtnCyan = "Spr_Btn_Bevel_Cyan.png";
        public const string BtnAmber = "Spr_Btn_Bevel_Amber.png";
        public const string BtnGreen = "Spr_Btn_Bevel_Green.png";
        public const string BtnRed = "Spr_Btn_Bevel_Red.png";
        public const string BtnNeutral = "Spr_Btn_Bevel_Neutral.png";
        public const string PanelFrame = "Spr_Panel_Frame.png";
        public const string CardCyan = "Spr_Card_Cyan.png";
        public const string CardAmber = "Spr_Card_Amber.png";
        public const string SliderTrack = "Spr_Slider_Track.png";
        public const string SliderFill = "Spr_Slider_Fill.png";
        public const string SliderKnob = "Spr_Slider_Knob.png";
        public const string ToggleBox = "Spr_Toggle_Box.png";
        public const string ToggleCheck = "Spr_Toggle_Check.png";

        private static readonly int GridScaleId = Shader.PropertyToID("_GridScale");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int GridIntensityId = Shader.PropertyToID("_GridIntensity");
        private static readonly int ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int LateralGlowPowerId = Shader.PropertyToID("_LateralGlowPower");

        [MenuItem("TPOB/UI/0. Generar Arte 2D de Botones, Controles y Fondos")]
        public static void EnsureUIAssets()
        {
            if (!Directory.Exists(ArtDirectory))
            {
                Directory.CreateDirectory(ArtDirectory);
            }

            if (!Directory.Exists(MaterialsDirectory))
            {
                Directory.CreateDirectory(MaterialsDirectory);
            }

            GenerateBevelButtonPng(Path.Combine(ArtDirectory, BtnCyan),
                new Color(0.05f, 0.13f, 0.20f, 0.94f),
                new Color(0.02f, 0.07f, 0.12f, 0.96f),
                new Color(0.0f, 0.94f, 1.0f, 1.0f));

            GenerateBevelButtonPng(Path.Combine(ArtDirectory, BtnAmber),
                new Color(0.20f, 0.12f, 0.04f, 0.94f),
                new Color(0.11f, 0.06f, 0.02f, 0.96f),
                new Color(1.0f, 0.58f, 0.0f, 1.0f));

            GenerateBevelButtonPng(Path.Combine(ArtDirectory, BtnGreen),
                new Color(0.04f, 0.19f, 0.13f, 0.94f),
                new Color(0.02f, 0.10f, 0.07f, 0.96f),
                new Color(0.0f, 0.95f, 0.58f, 1.0f));

            GenerateBevelButtonPng(Path.Combine(ArtDirectory, BtnRed),
                new Color(0.22f, 0.06f, 0.08f, 0.94f),
                new Color(0.12f, 0.03f, 0.04f, 0.96f),
                new Color(1.0f, 0.24f, 0.35f, 1.0f));

            GenerateBevelButtonPng(Path.Combine(ArtDirectory, BtnNeutral),
                new Color(0.10f, 0.14f, 0.20f, 0.94f),
                new Color(0.06f, 0.08f, 0.12f, 0.96f),
                new Color(0.45f, 0.68f, 0.88f, 0.95f));

            GeneratePanelFramePng(Path.Combine(ArtDirectory, PanelFrame),
                new Color(0.05f, 0.08f, 0.12f, 0.95f),
                new Color(0.0f, 0.88f, 1.0f, 0.85f),
                false);

            GeneratePanelFramePng(Path.Combine(ArtDirectory, CardCyan),
                new Color(0.04f, 0.09f, 0.14f, 0.92f),
                new Color(0.0f, 0.94f, 1.0f, 0.95f),
                true);

            GeneratePanelFramePng(Path.Combine(ArtDirectory, CardAmber),
                new Color(0.13f, 0.08f, 0.04f, 0.92f),
                new Color(1.0f, 0.58f, 0.0f, 0.95f),
                true);

            GenerateSliderTrackPng(Path.Combine(ArtDirectory, SliderTrack));
            GenerateSliderFillPng(Path.Combine(ArtDirectory, SliderFill));
            GenerateSliderKnobPng(Path.Combine(ArtDirectory, SliderKnob));
            GenerateToggleBoxPng(Path.Combine(ArtDirectory, ToggleBox));
            GenerateToggleCheckPng(Path.Combine(ArtDirectory, ToggleCheck));

            AssetDatabase.Refresh();

            ConfigureSpriteImporter(ArtDirectory + "/" + BgMainMenu, Vector4.zero);
            ConfigureSpriteImporter(ArtDirectory + "/" + BgLobby, Vector4.zero);
            ConfigureSpriteImporter(ArtDirectory + "/" + BtnCyan, new Vector4(20, 14, 20, 14));
            ConfigureSpriteImporter(ArtDirectory + "/" + BtnAmber, new Vector4(20, 14, 20, 14));
            ConfigureSpriteImporter(ArtDirectory + "/" + BtnGreen, new Vector4(20, 14, 20, 14));
            ConfigureSpriteImporter(ArtDirectory + "/" + BtnRed, new Vector4(20, 14, 20, 14));
            ConfigureSpriteImporter(ArtDirectory + "/" + BtnNeutral, new Vector4(20, 14, 20, 14));
            ConfigureSpriteImporter(ArtDirectory + "/" + PanelFrame, new Vector4(28, 28, 28, 28));
            ConfigureSpriteImporter(ArtDirectory + "/" + CardCyan, new Vector4(28, 28, 28, 28));
            ConfigureSpriteImporter(ArtDirectory + "/" + CardAmber, new Vector4(28, 28, 28, 28));
            ConfigureSpriteImporter(ArtDirectory + "/" + SliderTrack, new Vector4(10, 6, 10, 6));
            ConfigureSpriteImporter(ArtDirectory + "/" + SliderFill, new Vector4(10, 6, 10, 6));
            ConfigureSpriteImporter(ArtDirectory + "/" + SliderKnob, Vector4.zero);
            ConfigureSpriteImporter(ArtDirectory + "/" + ToggleBox, new Vector4(10, 10, 10, 10));
            ConfigureSpriteImporter(ArtDirectory + "/" + ToggleCheck, Vector4.zero);

            EnsureBackdropMaterials();
            AssetDatabase.SaveAssets();
        }

        public static Sprite LoadSprite(string fileName)
        {
            string path = ArtDirectory + "/" + fileName;
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr == null)
            {
                ConfigureSpriteImporter(path, Vector4.zero);
                spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return spr;
        }

        public static Material LoadBackdropMaterial(bool isLobby)
        {
            string path = isLobby ? LobbyBackdropMatPath : MainMenuBackdropMatPath;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureBackdropMaterials();
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return mat;
        }

        private static void EnsureBackdropMaterials()
        {
            Shader shader = Shader.Find("TPOB/UI/CyberGridBackdrop");
            if (shader == null)
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Shaders/CyberGridBackdrop.shader");
            }

            if (shader == null)
            {
                return;
            }

            CreateOrUpdateBackdropMat(MainMenuBackdropMatPath, shader, 28f, 0.25f, 0.20f, 0.05f, 0.85f);
            CreateOrUpdateBackdropMat(LobbyBackdropMatPath, shader, 24f, 0.20f, 0.16f, 0.05f, 0.90f);
        }

        private static void CreateOrUpdateBackdropMat(
            string matPath,
            Shader shader,
            float gridScale,
            float scrollSpeed,
            float gridIntensity,
            float scanlineIntensity,
            float lateralGlow)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetFloat(GridScaleId, gridScale);
            mat.SetFloat(ScrollSpeedId, scrollSpeed);
            mat.SetFloat(GridIntensityId, gridIntensity);
            mat.SetFloat(ScanlineIntensityId, scanlineIntensity);
            mat.SetFloat(LateralGlowPowerId, lateralGlow);
            EditorUtility.SetDirty(mat);
        }

        private static void ConfigureSpriteImporter(string assetPath, Vector4 border)
        {
            if (!File.Exists(assetPath))
            {
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void GenerateBevelButtonPng(string fullPath, Color topBg, Color bottomBg, Color borderCol)
        {
            const int w = 256;
            const int h = 64;
            const int chamfer = 12;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float ty = y / (float)(h - 1);
                Color gradBg = Color.Lerp(bottomBg, topBg, ty);

                for (int x = 0; x < w; x++)
                {
                    int distBottomLeft = x + y;
                    int distTopRight = (w - 1 - x) + (h - 1 - y);
                    int distTopLeft = x + (h - 1 - y);
                    int distBottomRight = (w - 1 - x) + y;

                    if (distBottomLeft < chamfer || distTopRight < chamfer || distTopLeft < 5 || distBottomRight < 5)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    bool isOuterBorder = x < 2 || x >= w - 2 || y < 2 || y >= h - 2 ||
                                         distBottomLeft < chamfer + 2 || distTopRight < chamfer + 2 ||
                                         distTopLeft < 7 || distBottomRight < 7;

                    bool isLeftAccentStripe = x >= 5 && x <= 11 && y >= 8 && y < h - 8 && distBottomLeft >= chamfer + 4;
                    bool isInnerHighlight = (y == h - 4 && x > 16 && x < w - 18);

                    Color c = gradBg;
                    if ((y % 4) == 0)
                    {
                        c.r *= 0.93f;
                        c.g *= 0.93f;
                        c.b *= 0.93f;
                    }

                    if (isInnerHighlight)
                    {
                        c = Color.Lerp(c, borderCol, 0.35f);
                    }

                    if (isLeftAccentStripe)
                    {
                        c = borderCol;
                    }
                    else if (isOuterBorder)
                    {
                        c = borderCol;
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GeneratePanelFramePng(string fullPath, Color bodyColor, Color borderCol, bool topHeaderBar)
        {
            const int w = 256;
            const int h = 256;
            const int chamfer = 18;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int dBL = x + y;
                    int dTR = (w - 1 - x) + (h - 1 - y);
                    int dTL = x + (h - 1 - y);
                    int dBR = (w - 1 - x) + y;

                    if (dBL < chamfer || dTR < chamfer || dTL < chamfer || dBR < chamfer)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    bool isBorder = x < 3 || x >= w - 3 || y < 3 || y >= h - 3 ||
                                    dBL < chamfer + 3 || dTR < chamfer + 3 || dTL < chamfer + 3 || dBR < chamfer + 3;

                    bool isCornerBracket = (x < 28 || x >= w - 28) && (y < 28 || y >= h - 28) &&
                                           (x < 6 || x >= w - 6 || y < 6 || y >= h - 6);

                    bool isTopHeader = topHeaderBar && y >= h - 10 && y < h - 3;

                    Color c = bodyColor;
                    if (isTopHeader)
                    {
                        c = Color.Lerp(bodyColor, borderCol, 0.75f);
                    }
                    if (isBorder || isCornerBracket)
                    {
                        c = borderCol;
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateSliderTrackPng(string fullPath)
        {
            const int w = 128;
            const int h = 24;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            Color bg = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            Color border = new Color(0.22f, 0.36f, 0.48f, 0.9f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool edge = x < 2 || x >= w - 2 || y < 2 || y >= h - 2;
                    Color c = edge ? border : bg;
                    if (!edge && (x % 16 == 0) && y > 5 && y < h - 5)
                    {
                        c = new Color(0.14f, 0.22f, 0.30f, 0.9f);
                    }
                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateSliderFillPng(string fullPath)
        {
            const int w = 128;
            const int h = 24;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            Color leftCol = new Color(0.0f, 0.65f, 0.85f, 0.95f);
            Color rightCol = new Color(0.0f, 0.96f, 1.0f, 1.0f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (y < 2 || y >= h - 2)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    Color c = Color.Lerp(leftCol, rightCol, x / (float)(w - 1));
                    if (((x + y) % 12) < 3)
                    {
                        c = Color.Lerp(c, Color.white, 0.18f);
                    }
                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateSliderKnobPng(string fullPath)
        {
            const int size = 36;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            Color rim = new Color(0.0f, 0.96f, 1.0f, 1.0f);
            Color core = new Color(0.08f, 0.18f, 0.26f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Abs(x - size / 2);
                    int dy = Mathf.Abs(y - size / 2);
                    if (dx > 13 || dy > 15 || (dx + dy) > 22)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    bool isBorder = dx >= 11 || dy >= 13 || (dx + dy) >= 19;
                    bool isCenterDot = dx <= 3 && dy <= 5;
                    pixels[y * size + x] = (isBorder || isCenterDot) ? rim : core;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateToggleBoxPng(string fullPath)
        {
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            Color bg = new Color(0.05f, 0.09f, 0.14f, 0.95f);
            Color border = new Color(0.0f, 0.85f, 1.0f, 0.9f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dBL = x + y;
                    int dTR = (size - 1 - x) + (size - 1 - y);
                    if (dBL < 6 || dTR < 6)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    bool isEdge = x < 3 || x >= size - 3 || y < 3 || y >= size - 3 || dBL < 9 || dTR < 9;
                    pixels[y * size + x] = isEdge ? border : bg;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void GenerateToggleCheckPng(string fullPath)
        {
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            Color glow = new Color(0.0f, 0.96f, 1.0f, 1.0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = x >= 10 && x < size - 10 && y >= 10 && y < size - 10 &&
                                  (x + y) >= 24 && ((size - 1 - x) + (size - 1 - y)) >= 24;
                    pixels[y * size + x] = inside ? glow : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
