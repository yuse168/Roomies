#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

/// <summary>
/// エモートの確認（Play Mode）：Roomies/Player/Emote Check (Play Mode)。先に Town/Walk - Enter GameRoom As Host で入っておく。
/// キーは本当に押したことにする（InputSystem に入力を送る）。Shift でダッシュ → エモートの輪が出ない・勝手にエモートが出ない →
/// エモート → 三人称の視点 → マウスで見回す → 壁で縮む → 歩くと一人称に戻る → E でも戻る → 一回きりのエモートは終わると戻る。
/// 写真は Temp/RoomiesPc/emote/（1920×1080 の Game ビュー）、結果は Temp/RoomiesPc/emote_check.txt。
/// </summary>
internal static partial class RoomiesCasinoDemo
{
    const string EmoteOut = "Temp/RoomiesPc/emote_check.txt";

    [MenuItem("Roomies/Player/Emote Check (Play Mode)")]
    static void EmoteCheck()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Emote] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(EmoteCheckRoutine(runner));
    }

    static void PressKeys(params Key[] keys)
    {
        var state = new KeyboardState();
        foreach (var k in keys) state.Set(k, true);
        InputSystem.QueueStateEvent(Keyboard.current, state);
    }

    static void MoveMouse(Vector2 delta) => InputSystem.QueueStateEvent(Mouse.current, new MouseState { delta = delta });

    static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    static IEnumerator EmoteCheckRoutine(Runner runner)
    {
        Directory.CreateDirectory("Temp/RoomiesPc/emote");
        File.Delete(EmoteOut);
        foreach (var f in Directory.GetFiles("Temp/RoomiesPc/emote")) try { File.Delete(f); } catch (IOException) { }
        var log = new System.Text.StringBuilder();
        int ok = 0, ng = 0;
        void Expect(bool good, string what) { if (good) ok++; else ng++; log.AppendLine((good ? "OK  " : "NG  ") + what); }
        int prevSize = SetGameViewSize(VW, VH);
        bool asyncBefore = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var r = MakeRig(runner);
        var pm = r.pm;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo sprinting = typeof(PlayerMovement).GetField("isSprinting", flags), wheelOpen = typeof(PlayerMovement).GetField("isEmoteWheelOpen", flags), orbitYaw = typeof(PlayerMovement).GetField("orbitYaw", flags);
        IEnumerator Shot(string name)
        {
            string path = Path.GetFullPath($"Temp/RoomiesPc/emote/{name}.png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return Frames(3);
        }

        // ---- キーの割り当て：ダッシュは Shift、エモートは別のキー。ほかとも重ならない
        var seen = new System.Collections.Generic.Dictionary<Key, GameAction>();
        bool distinct = true;
        foreach (GameAction a in System.Enum.GetValues(typeof(GameAction)))
        {
            Key k = GameSettings.GetKeyBinding(a);
            if (seen.ContainsKey(k)) distinct = false;
            seen[k] = a;
        }
        Key emoteKey = GameSettings.GetKeyBinding(GameAction.Emote);
        Expect(GameSettings.GetKeyBinding(GameAction.Sprint) == Key.LeftShift && emoteKey != Key.LeftShift && emoteKey != Key.RightShift && distinct,
            $"keys: dash = {GameSettings.GetKeyBinding(GameAction.Sprint)}, emote = {emoteKey}, every action has its own key");

        // ---- 部屋の真ん中で、Shift + W でダッシュ（エモートの輪は出ない。離してもエモートは出ない）
        SetTime(1);
        Teleport(r.cc, new Vector3(18.5f, 3.35f, 18.5f));
        r.cc.transform.rotation = Quaternion.Euler(0, 0, 0);
        yield return Frames(20);
        bool sprintSeen = false, wheelSeen = false;
        for (int i = 0; i < 30; i++)
        {
            PressKeys(Key.LeftShift, Key.W);
            yield return null;
            sprintSeen |= (bool)sprinting.GetValue(pm);
            wheelSeen |= (bool)wheelOpen.GetValue(pm);
        }
        PressKeys();
        yield return Frames(20);
        Expect(sprintSeen && !wheelSeen && !pm.IsEmoting && !pm.IsEmoteCameraActive, $"dash: holding Shift sprints, the emote wheel does not open, no emote after release (sprint {sprintSeen}, wheel {wheelSeen}, emoting {pm.IsEmoting})");

        // ---- エモート：三人称の視点（自分の画面だけ）。はじめはキャラの正面から
        Teleport(r.cc, new Vector3(18.5f, 3.35f, 18.5f));
        r.cc.transform.rotation = Quaternion.Euler(0, 200, 0);
        yield return Frames(20);
        Vector3 fpLocal = pm.cameraTransform.localPosition;
        pm.DebugPlayEmote(1);
        yield return WaitUntil(() => pm.EmoteCameraWeight >= .999f, 2f);
        Vector3 head = r.cc.transform.position + Vector3.up * .45f;
        float dist = (pm.cameraTransform.position - head).magnitude;
        Vector3 toCam = pm.cameraTransform.position - r.cc.transform.position; toCam.y = 0;
        float frontAngle = Vector3.Angle(r.cc.transform.forward, toCam);
        Expect(pm.IsEmoteCameraActive && dist > 1.5f && frontAngle < 40f, $"emote: third-person view in front of the character (camera {dist:F2} m from the head, {frontAngle:F0} deg from the front)");
        yield return WaitUntil(() => false, .6f);
        yield return Shot("1_emote_front");

        // ---- マウスで見回す（本当にマウスを動かしたことにする）：体は回らず、カメラが回る
        float yaw0 = (float)orbitYaw.GetValue(pm), body0 = r.cc.transform.eulerAngles.y;
        for (int i = 0; i < 20; i++) { MoveMouse(new Vector2(40f, 4f)); yield return null; }
        MoveMouse(Vector2.zero);
        yield return Frames(5);
        float yaw1 = (float)orbitYaw.GetValue(pm);
        Expect(Mathf.Abs(Mathf.DeltaAngle(yaw0, yaw1)) > 20f && Mathf.Abs(Mathf.DeltaAngle(body0, r.cc.transform.eulerAngles.y)) < 1f && pm.IsEmoting,
            $"look around: the mouse turns the camera ({yaw0:F0} -> {yaw1:F0} deg) and not the body, the emote keeps playing");
        pm.DebugSetOrbit(body0 + 110f, 18f);
        yield return WaitUntil(() => false, .5f);
        yield return Shot("2_emote_side");
        pm.DebugSetOrbit(body0 - 30f, 40f);
        yield return WaitUntil(() => false, .5f);
        yield return Shot("3_emote_above");

        // ---- 壁：カメラが壁の中に入らない（縮む）
        pm.DebugStopEmote();
        yield return WaitUntil(() => !pm.IsEmoteCameraActive, 2f);
        Teleport(r.cc, new Vector3(21.2f, 3.35f, 18.6f));   // 東の壁（x ≈ 22.7）のそば
        r.cc.transform.rotation = Quaternion.Euler(0, 270, 0);
        yield return Frames(20);
        pm.DebugPlayEmote(1);
        yield return WaitUntil(() => pm.EmoteCameraWeight >= .999f, 2f);
        pm.DebugSetOrbit(-90f, 10f);   // カメラを壁のほう（+X）へ
        yield return Frames(10);
        head = r.cc.transform.position + Vector3.up * .45f;
        float wallDist = (pm.cameraTransform.position - head).magnitude;
        bool insideWall = Physics.CheckSphere(pm.cameraTransform.position, .08f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Physics.CheckSphere(pm.cameraTransform.position, .08f, 1 << r.cc.gameObject.layer, QueryTriggerInteraction.Ignore);
        Expect(wallDist < 2.9f && !insideWall, $"wall: the camera shortens at the wall ({wallDist:F2} m) and is not inside it");
        yield return Shot("4_emote_wall");

        // ---- 歩くとエモートが止まり、一人称になめらかに戻る
        PressKeys(Key.W);
        yield return Frames(3);
        PressKeys();
        float back = 0f;
        float midWeight = -1f;
        while (pm.IsEmoteCameraActive && back < 2f) { yield return null; back += Time.deltaTime; if (midWeight < 0 && back > .12f) midWeight = pm.EmoteCameraWeight; }
        yield return Frames(30);
        Expect(!pm.IsEmoting && !pm.IsEmoteCameraActive && midWeight > .05f && midWeight < .95f && (pm.cameraTransform.localPosition - fpLocal).magnitude < .2f,
            $"walk: the emote stops and the view blends back to first person ({back:F2}s, weight {midWeight:F2} on the way, camera {(pm.cameraTransform.localPosition - fpLocal).magnitude:F2} m from the eye)");

        // ---- E（使う）でも止まる
        Teleport(r.cc, new Vector3(18.5f, 3.35f, 18.5f));
        yield return Frames(10);
        pm.DebugPlayEmote(1);
        yield return WaitUntil(() => pm.EmoteCameraWeight >= .999f, 2f);
        PressKeys(GameSettings.GetKeyBinding(GameAction.Interact));
        yield return Frames(2);
        PressKeys();
        yield return WaitUntil(() => !pm.IsEmoteCameraActive, 2f);
        Expect(!pm.IsEmoting && !pm.IsEmoteCameraActive, "interact: pressing E during an emote stops it and returns to first person");

        // ---- 一回きりのエモートは、動きが終わったら一人称に戻る（くり返すダンスなどは、続けている間は三人称のまま）
        var sb = new System.Text.StringBuilder();
        int endedAlone = 0;
        for (int e = 1; e <= 6; e++)
        {
            pm.DebugPlayEmote(e);
            yield return WaitUntil(() => pm.EmoteCameraWeight >= .999f, 2f);
            float t = 0f;
            while (pm.IsEmoteCameraActive && t < 8f) { yield return null; t += Time.deltaTime; }
            bool alone = !pm.IsEmoteCameraActive;
            t += .35f;   // 三人称になるまでの分も足す（エモートが始まってからの秒数）
            if (alone) endedAlone++;
            sb.Append($" #{e}:{(alone ? $"ends {t:F1}s" : "loops")}");
            pm.DebugStopEmote();
            yield return WaitUntil(() => !pm.IsEmoteCameraActive, 2f);
            yield return Frames(10);
        }
        Expect(endedAlone >= 1, "one-shot: emotes that play once return to first person by themselves;" + sb);

        ShaderUtil.allowAsyncCompilation = asyncBefore;
        RestoreGameViewSize(prevSize);
        File.WriteAllText(EmoteOut, $"emote check: ok {ok}, ng {ng}\n" + log);
        Object.Destroy(runner.gameObject);
    }
}
#endif
