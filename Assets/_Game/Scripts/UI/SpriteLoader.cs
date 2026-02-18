using UnityEngine;

namespace FantasyGuildmaster.UI
{
    public static class SpriteLoader
    {
        public static Sprite TryLoadSprite(string path, string fallbackPath)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var sprite = Resources.Load<Sprite>(path);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                return Resources.Load<Sprite>(fallbackPath);
            }

            return null;
        }
    }
}
