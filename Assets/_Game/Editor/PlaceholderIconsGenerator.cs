#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FantasyGuildmaster.Editor
{
    public static class PlaceholderIconsGenerator
    {
        private const int IconSize = 256;

        private static readonly string[] RegionIcons =
        {
            "region_northmarch",
            "region_blackfen",
            "region_fallback"
        };

        private static readonly string[] ContractIcons =
        {
            "contract_generic",
            "contract_hunt",
            "contract_escort"
        };

        private static readonly string[] UiIcons =
        {
            "travel_token",
            "squad_token"
        };

        [MenuItem("Tools/FantasyGuildmaster/Generate Placeholder Icons")]
        public static void GenerateMenu()
        {
            GenerateIcons(false);
        }

        public static int GenerateIcons(bool overwrite)
        {
            EnsureFolder("Assets/_Game/Resources");
            EnsureFolder("Assets/_Game/Resources/Icons");
            EnsureFolder("Assets/_Game/Resources/Icons/Regions");
            EnsureFolder("Assets/_Game/Resources/Icons/Contracts");
            EnsureFolder("Assets/_Game/Resources/Icons/UI");

            var created = 0;
            created += GenerateGroup("Assets/_Game/Resources/Icons/Regions", RegionIcons, overwrite, 0);
            created += GenerateGroup("Assets/_Game/Resources/Icons/Contracts", ContractIcons, overwrite, 1);
            created += GenerateGroup("Assets/_Game/Resources/Icons/UI", UiIcons, overwrite, 2);

            AssetDatabase.Refresh();
            Debug.Log($"Created {created} icons");
            return created;
        }

        public static bool HasRequiredIcons()
        {
            return HasFiles("Assets/_Game/Resources/Icons/Regions", RegionIcons)
                && HasFiles("Assets/_Game/Resources/Icons/Contracts", ContractIcons)
                && HasFiles("Assets/_Game/Resources/Icons/UI", UiIcons);
        }

        private static bool HasFiles(string folder, IReadOnlyList<string> names)
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (!File.Exists(Path.Combine(folder, names[i] + ".png")))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GenerateGroup(string folder, IReadOnlyList<string> names, bool overwrite, int style)
        {
            var created = 0;
            for (var i = 0; i < names.Count; i++)
            {
                var path = Path.Combine(folder, names[i] + ".png");
                if (!overwrite && File.Exists(path))
                {
                    continue;
                }

                var texture = BuildIconTexture(names[i], style + i);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                created++;
            }

            return created;
        }

        private static Texture2D BuildIconTexture(string key, int style)
        {
            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconSize * IconSize];

            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 0);
            }

            DrawShape(pixels, style);
            DrawStripe(pixels, style);

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.name = key;
            return texture;
        }

        private static void DrawShape(Color32[] pixels, int style)
        {
            var cx = IconSize / 2;
            var cy = IconSize / 2;
            var radius = 88;
            var color = style % 3 switch
            {
                0 => new Color32(172, 60, 60, 255),
                1 => new Color32(76, 134, 94, 255),
                _ => new Color32(112, 112, 124, 255)
            };

            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var index = y * IconSize + x;

                    if (style % 2 == 0)
                    {
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            pixels[index] = color;
                        }
                    }
                    else
                    {
                        var adx = Mathf.Abs(dx);
                        var ady = Mathf.Abs(dy);
                        if (adx < radius * (1f - (ady / (float)radius) * 0.5f) && ady < radius)
                        {
                            pixels[index] = color;
                        }
                    }
                }
            }
        }

        private static void DrawStripe(Color32[] pixels, int style)
        {
            var stripeColor = style % 2 == 0 ? new Color32(240, 220, 180, 220) : new Color32(220, 230, 250, 220);
            var yStart = 116;
            var yEnd = 140;
            for (var y = yStart; y <= yEnd; y++)
            {
                for (var x = 72; x <= 184; x++)
                {
                    var index = y * IconSize + x;
                    if (pixels[index].a > 0)
                    {
                        pixels[index] = stripeColor;
                    }
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
#endif
