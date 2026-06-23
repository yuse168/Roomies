using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>
/// SteamのフレンドアバターをUI用Spriteとして取得・キャッシュする。
/// iiwakekingのSteamAvatarを参考にRoomies向けに実装（名前空間なし）。
/// </summary>
public static class SteamAvatar
{
    private static readonly Dictionary<ulong, Sprite> _cache = new Dictionary<ulong, Sprite>();

    /// <summary>
    /// 指定SteamIDのアバターSpriteを返す。
    /// まだ読み込まれていない場合はnullを返し、読み込みを要求する（次回呼び出しで取得可能）。
    /// </summary>
    public static Sprite Get(ulong steamId)
    {
        if (!SteamManager.Initialized || steamId == 0) return null;
        if (_cache.TryGetValue(steamId, out var cached)) return cached;

        var cSteamId = new CSteamID(steamId);
        int handle = SteamFriends.GetMediumFriendAvatar(cSteamId);

        if (handle <= 0)
        {
            // フレンド以外はアバター情報がまだ無いことがあるので読み込みを要求する。
            // 読み込み完了後、次回のGet()で取得できる。
            SteamFriends.RequestUserInformation(cSteamId, false);
            return null;
        }

        if (!SteamUtils.GetImageSize(handle, out uint w, out uint h) || w == 0 || h == 0)
            return null;

        int n = (int)(w * h * 4);
        var buf = new byte[n];
        if (!SteamUtils.GetImageRGBA(handle, buf, n)) return null;

        // Steamのデータは上から下。Unityテクスチャは下から上なので行を反転する。
        var flipped = new byte[n];
        int stride = (int)w * 4;
        for (int y = 0; y < h; y++)
            Array.Copy(buf, y * stride, flipped, (int)(h - 1 - y) * stride, stride);

        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(flipped);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        _cache[steamId] = sprite;
        return sprite;
    }
}
