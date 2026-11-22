using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// EditMode のテストで Netcode のホストを起動するための準備。
///
/// Netcode はメッセージの種類・RPC・NetworkVariable の読み書き係を、ビルド時に生成された
/// 「RuntimeInitializeOnLoadMethod 付きの初期化メソッド」で登録する。これは Play Mode に入るときにしか
/// 呼ばれないので、EditMode のテストでは登録が空のままになり、<c>StartHost()</c> が
/// "Allowed types is not equal to the number of message type indices" で失敗する。
/// ここでその初期化メソッドをテスト側から1回だけ呼ぶ。
/// </summary>
public static class RoomiesTestNetcode
{
    private static bool gameInitializersRun;

    /// <summary><c>NetworkManager.StartHost()</c> の前に呼ぶ。何度呼んでもよい。</summary>
    public static void EnsureInitialized()
    {
        Assembly netcode = typeof(NetworkManager).Assembly;

        // メッセージの種類は Play Mode を抜けるたびに Netcode 側で空にされるので、空のときだけ入れ直す
        Type provider = netcode.GetType("Unity.Netcode.ILPPMessageProvider");
        FieldInfo listField = provider?.GetField(
            "__network_message_types", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (listField?.GetValue(null) is IList messages && messages.Count == 0)
            RunModuleInitializers(netcode);

        // ゲーム側（RPC と NetworkVariable の型）は足し込み式で消されないので、1回だけ
        if (gameInitializersRun) return;
        gameInitializersRun = true;

        Assembly game = Type.GetType("DayManager, Assembly-CSharp", false)?.Assembly;
        if (game != null) RunModuleInitializers(game);
    }

    /// <summary>
    /// テストの後片付け。EditMode では NetworkManager の OnDestroy が呼ばれず、
    /// 壊したあとも <c>NetworkManager.Singleton</c> に残るので、ここで空にする。
    /// </summary>
    public static void ClearSingleton()
    {
        PropertyInfo singleton = typeof(NetworkManager).GetProperty(
            nameof(NetworkManager.Singleton), BindingFlags.Static | BindingFlags.Public);
        MethodInfo setter = singleton?.GetSetMethod(true);
        setter?.Invoke(null, new object[] { null });
    }

    /// <summary>ゲームを実行中でないこと（壊した後の残骸は「無い」と数える）。</summary>
    public static bool NoActiveSession => NetworkManager.Singleton == null;

    private static void RunModuleInitializers(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        // Netcode のコード生成が作る型だけを対象にする（`__GEN` 名前空間と `<Module>`）。
        // ゲーム自身の RuntimeInitializeOnLoadMethod（UIの準備など）は呼ばない
        foreach (Type type in assembly.GetTypes())
        {
            if (type.Namespace != "__GEN" && type.Name != "<Module>") continue;

            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (method.GetParameters().Length != 0) continue;
                if (method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() == null) continue;
                method.Invoke(null, null);
            }
        }
    }
}
