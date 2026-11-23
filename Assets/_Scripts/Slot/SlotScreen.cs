using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スロットの画面と筐体のランプの演出（見た目と音だけ。当たり外れ・配当は SlotMachine が決める）。
///
/// 画面の部品はすべてプレハブの Anchor_Screen の中に置いてあり、ここでは動かすだけ（実行時に物を作らない）。
/// 配置ツール：Roomies/Casino/2 Build Gimmick Prefabs（RoomiesCasinoBuilder.SlotScreen.cs）。
///
/// 1 回の流れ：リールが回る → 左・中・右の順に止まる →（左と中がそろったら）リーチ：まわりが暗くなり、光が走り、右がゆっくり止まる
///            → 当たりならラインが光り、大きさに合わせた演出（小・中・大・7 ぞろい）→ 当たりの額の数字が回って増える。
/// </summary>
public class SlotScreen : MonoBehaviour
{
    public enum Tier { Miss, Small, Mid, Big, Jackpot }

    [Header("シンボルの絵（SlotMachine の slotSymbols と同じ順）")]
    [SerializeField] private Sprite[] symbolSprites = new Sprite[0];
    [Tooltip("回っている間に使う、縦にぶれた絵（同じ順）")]
    [SerializeField] private Sprite[] blurSprites = new Sprite[0];

    [Header("リール（左・中・右。1 本につきマス 5 つ：上から順に）")]
    [SerializeField] private Image[] reelCells = new Image[0];
    [Tooltip("マス 1 つの高さ（Canvas の単位）")]
    [SerializeField, Min(1f)] private float cellHeight = 110f;
    [Tooltip("リールに並ぶシンボルの順（見た目だけ。当たりの確率には関係しない）。3 本ぶんを続けて、1 本あたり同じ長さで入れる")]
    [SerializeField] private int[] reelStrips = new int[0];
    [Tooltip("回る速さ（1 秒あたりのマス）")]
    [SerializeField, Min(1f)] private float spinSpeed = 16f;
    [Tooltip("止まる前に速さを落とす秒数")]
    [SerializeField, Min(0.05f)] private float brakeSeconds = 0.32f;
    [Tooltip("この速さより速い間は、ぶれた絵を使う")]
    [SerializeField, Min(0f)] private float blurAboveSpeed = 9f;

    [Header("当たりのライン")]
    [SerializeField] private Image winLine;
    [SerializeField] private Image winLineGlow;

    [Header("リーチ（まわりを暗くする板・右のリールの枠・走る光）")]
    [SerializeField] private Image[] dimPanels = new Image[0];
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.72f;
    [SerializeField] private Image reachFrame;
    [SerializeField] private RectTransform sweep;
    [SerializeField] private float sweepTravel = 620f;
    [SerializeField] private Image reachLogo;

    [Header("当たりの演出")]
    [SerializeField] private Image flash;
    [SerializeField] private Image sunburst;
    [SerializeField] private Image logo;
    [SerializeField] private Sprite logoWin, logoBigWin, logoJackpot, logoFever;
    [SerializeField] private Image[] coins = new Image[0];
    [SerializeField] private Image[] sparkles = new Image[0];
    [Tooltip("コインが噴き出す所（Canvas の中の位置）と、勢い・重さ")]
    [SerializeField] private Vector2 coinOrigin = new Vector2(0f, -330f);
    [SerializeField] private float coinSpeed = 900f, coinGravity = 1500f;

    [Header("当たりの額（数字が回って増える）")]
    [SerializeField] private TextMeshProUGUI winCounter;
    [SerializeField] private Color counterIdleColor = new Color(0.55f, 0.5f, 0.45f);
    [SerializeField] private Color counterWinColor = new Color(1f, 0.86f, 0.4f);

    [Header("フィーバー中の背景")]
    [SerializeField] private Image feverBackground;
    [SerializeField, Min(0.05f)] private float feverFadeSeconds = 0.5f;

    [Header("筐体のランプ（点いた見た目を出し入れする）と、その明かり")]
    [SerializeField] private GameObject[] lampsLit = new GameObject[0];
    [SerializeField] private Light lampLight;
    [SerializeField] private Color lampColor = new Color(1f, 0.82f, 0.5f), lampFeverColor = new Color(1f, 0.4f, 0.75f);
    [SerializeField, Min(0f)] private float lampLightIdle = 0.6f, lampLightWin = 3.2f;

    [Header("演出の長さ（秒）：小・中・大・7 ぞろい。SlotMachine が同じ長さだけ次の回を待つ")]
    [SerializeField] private float[] celebrateSeconds = { 0.9f, 1.3f, 2.2f, 3.4f };

    private readonly float[] reelPos = new float[3];
    private readonly bool[] reelBlur = new bool[3];
    private Coroutine playing, feverFade;
    private bool fever;
    private float lampClock;
    private int lampMode;          // 0 待機 / 1 回転中 / 2 リーチ / 3 当たり
    private Vector2[] coinVelocity;
    private float[] coinLife;
    private int shownCounter;

    private int StripLength => reelStrips != null && reelStrips.Length >= 3 ? reelStrips.Length / 3 : 0;
    private int SymbolCount => symbolSprites != null ? symbolSprites.Length : 0;

    /// <summary>当たりの演出の長さ（その段の秒数）。外れは 0。</summary>
    public float CelebrateSeconds(Tier tier)
    {
        int i = (int)tier - 1;
        return i >= 0 && celebrateSeconds != null && i < celebrateSeconds.Length ? Mathf.Max(0f, celebrateSeconds[i]) : 0f;
    }

    private void Awake()
    {
        coinVelocity = new Vector2[coins.Length];
        coinLife = new float[coins.Length];
        ResetEffects();
        ShowIdle(0, 1, Mathf.Max(0, SymbolCount - 1));
    }

    private void OnDisable()
    {
        playing = null; feverFade = null;
        ResetEffects();
    }

    // ================================================================ 外から呼ぶ

    /// <summary>止まった状態で、真ん中の段にその 3 つを見せる。</summary>
    public void ShowIdle(int a, int b, int c)
    {
        int[] targets = { a, b, c };
        for (int r = 0; r < 3; r++)
        {
            reelPos[r] = FindStop(r, 0f, targets[r], -1);
            reelBlur[r] = false;
            DrawReel(r);
        }
    }

    public void SetFever(bool on)
    {
        if (fever == on) return;
        fever = on;
        if (feverBackground == null) return;
        if (!isActiveAndEnabled) { SetAlpha(feverBackground, on ? 1f : 0f); return; }
        if (feverFade != null) StopCoroutine(feverFade);
        feverFade = StartCoroutine(FadeFever(on ? 1f : 0f));
    }

    /// <summary>
    /// 1 回ぶんの演出をはじめる。stopTimes：左・中・右が止まる時刻（秒。はじめからの時間）。
    /// reach：左と中がそろっている（右はゆっくり止める）。reward：当たりの額（外れは 0）。
    /// </summary>
    public void Play(int r1, int r2, int r3, float[] stopTimes, bool reach, Tier tier, int reward)
    {
        if (!isActiveAndEnabled) { ShowIdle(r1, r2, r3); return; }
        if (playing != null) StopCoroutine(playing);
        ResetEffects();
        playing = StartCoroutine(PlayRoutine(new[] { r1, r2, r3 }, stopTimes, reach, tier, reward));
    }

    // ================================================================ 1 回ぶん

    private IEnumerator PlayRoutine(int[] targets, float[] stopTimes, bool reach, Tier tier, int reward)
    {
        Vector3 at = transform.position;
        lampMode = 1;
        SetCounter(0, false);
        GameAudio.PlayAt("casino/slot_start", at);
        string reelChannel = "casino/slot_reel#" + GetInstanceID();

        float t = 0f, nextReelSound = 0f;
        float[] brakeFrom = new float[3], brakeTo = new float[3], brakeStart = new float[3];
        int[] state = new int[3];       // 0 回っている / 1 止まりかけ / 2 止まった
        bool reachShown = false;

        while (state[2] < 2)
        {
            t += Time.deltaTime;

            if (t >= nextReelSound)
            {
                GameAudio.PlayExclusiveAt("casino/slot_reel", reelChannel, at, 1f, 1f);
                nextReelSound = t + 0.9f;
            }

            for (int r = 0; r < 3; r++)
            {
                if (state[r] == 2) continue;
                bool slow = reach && r == 2;     // リーチのときの右のリールは、ゆっくり長く止める
                float brake = slow ? Mathf.Max(brakeSeconds, stopTimes[2] - stopTimes[1] - 0.15f) : brakeSeconds;

                if (state[r] == 0)
                {
                    reelPos[r] += spinSpeed * Time.deltaTime;
                    reelBlur[r] = spinSpeed > blurAboveSpeed;
                    if (t >= stopTimes[r] - brake)
                    {
                        state[r] = 1;
                        brakeStart[r] = t;
                        brakeFrom[r] = reelPos[r];
                        // 止まる位置：いまの勢いのまま少し進んだ先で、真ん中にその絵が来る所。
                        // リーチで外れるときは、そろうはずの絵が 1 つずれて止まる所を選ぶ（惜しい外れ）
                        float ahead = reelPos[r] + (slow ? spinSpeed * brake * 0.42f : spinSpeed * brake * 0.5f);
                        int tease = slow && targets[2] != targets[1] ? targets[1] : -1;
                        brakeTo[r] = FindStop(r, ahead, targets[r], tease);
                    }
                }

                if (state[r] == 1)
                {
                    float u = Mathf.Clamp01((t - brakeStart[r]) / brake);
                    float eased = slow ? 1f - Mathf.Pow(1f - u, 2.6f) : EaseOutBack(u);
                    reelPos[r] = Mathf.LerpUnclamped(brakeFrom[r], brakeTo[r], eased);
                    reelBlur[r] = false;
                    if (t >= stopTimes[r])
                    {
                        state[r] = 2;
                        reelPos[r] = brakeTo[r];
                        GameAudio.PlayAt(r == 0 ? "casino/slot_stopa" : r == 1 ? "casino/slot_stopb" : "casino/slot_stopc", at);
                        StartCoroutine(PunchCell(reelCells[r * 5 + 2]));
                    }
                }
                DrawReel(r);
            }

            // 中が止まって、左とそろっていたらリーチ
            if (reach && !reachShown && state[1] == 2)
            {
                reachShown = true;
                lampMode = 2;
                GameAudio.PlayAt("casino/slot_reach", at);
                StartCoroutine(ReachRoutine(Mathf.Max(0.2f, stopTimes[2] - t)));
            }
            yield return null;
        }

        // 止まった：リーチの暗さを戻す
        SetReach(0f);
        if (reachLogo != null) reachLogo.gameObject.SetActive(false);
        if (sweep != null) sweep.gameObject.SetActive(false);

        if (tier == Tier.Miss)
        {
            if (reach) GameAudio.PlayAt("casino/slot_reach_miss", at);
            else GameAudio.PlayAt("casino/slot_lose", at, 0.7f);
            lampMode = 0;
            playing = null;
            yield break;
        }

        lampMode = 3;
        yield return Celebrate(tier, reward, at);
        lampMode = 0;
        playing = null;
    }

    /// <summary>リーチのあいだ：まわりを暗くし、右のリールの枠を明滅させ、光を走らせる。</summary>
    private IEnumerator ReachRoutine(float seconds)
    {
        if (reachLogo != null)
        {
            reachLogo.gameObject.SetActive(true);
            StartCoroutine(Pop(reachLogo.rectTransform, 0.25f, 1.25f));
        }
        if (sweep != null) sweep.gameObject.SetActive(true);

        float t = 0f;
        while (t < seconds && lampMode == 2)
        {
            t += Time.deltaTime;
            SetReach(Mathf.Clamp01(t / 0.25f));
            if (reachFrame != null) SetAlpha(reachFrame, 0.55f + 0.45f * Mathf.Sin(t * 22f));
            if (winLineGlow != null) { winLineGlow.gameObject.SetActive(true); SetAlpha(winLineGlow, 0.35f + 0.3f * Mathf.Sin(t * 16f)); }
            if (sweep != null)
            {
                float u = Mathf.Repeat(t / 0.7f, 1f);
                sweep.anchoredPosition = new Vector2(Mathf.Lerp(-sweepTravel * 0.5f, sweepTravel * 0.5f, u), sweep.anchoredPosition.y);
            }
            yield return null;
        }
        if (winLineGlow != null) winLineGlow.gameObject.SetActive(false);
    }

    private IEnumerator Celebrate(Tier tier, int reward, Vector3 at)
    {
        float seconds = CelebrateSeconds(tier);
        bool big = tier >= Tier.Big, jackpot = tier == Tier.Jackpot;

        GameAudio.PlayAt(tier == Tier.Small ? "casino/slot_win_small" : tier == Tier.Mid ? "casino/slot_win_mid" : jackpot ? "casino/slot_jackpot" : "casino/slot_win_big", at,
            jackpot ? 1.2f : 1f);

        if (winLine != null) winLine.gameObject.SetActive(true);
        if (winLineGlow != null) winLineGlow.gameObject.SetActive(true);
        for (int r = 0; r < 3; r++) StartCoroutine(PunchCell(reelCells[r * 5 + 2], tier >= Tier.Mid ? 1.35f : 1.2f, 0.3f));

        if (logo != null)
        {
            logo.sprite = jackpot ? logoJackpot : big ? logoBigWin : logoWin;
            logo.SetNativeSize();
            logo.gameObject.SetActive(tier >= Tier.Mid);
            if (tier >= Tier.Mid) StartCoroutine(Pop(logo.rectTransform, 0.3f, big ? 1.5f : 1.25f));
        }
        if (sunburst != null) sunburst.gameObject.SetActive(big);
        if (tier >= Tier.Mid) foreach (Image s in sparkles) if (s != null) s.gameObject.SetActive(true);

        int coinCount = jackpot ? coins.Length : big ? coins.Length / 2 : 0;
        float coinEvery = coinCount > 0 ? Mathf.Min(0.09f, seconds * 0.6f / coinCount) : 0f;
        float nextCoin = 0f, nextTick = 0f;
        int launched = 0;
        float countSeconds = seconds * (big ? 0.7f : 0.6f);
        bool swappedLogo = false;

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float blink = Mathf.Sin(t * (big ? 30f : 20f)) > 0f ? 1f : 0.25f;
            if (winLine != null) SetAlpha(winLine, blink);
            if (winLineGlow != null) SetAlpha(winLineGlow, blink * 0.8f);

            if (flash != null)
            {
                // 大当たり：はじめに画面いっぱいのフラッシュ。7 ぞろいは 3 回くり返す
                float f = 0f;
                if (big) f = Mathf.Clamp01(1f - t / 0.3f) * 0.8f;
                if (jackpot) f = Mathf.Max(f, Mathf.Clamp01(1f - Mathf.Repeat(t, 0.4f) / 0.22f) * (t < 1.2f ? 0.7f : 0f));
                flash.gameObject.SetActive(f > 0.01f);
                SetAlpha(flash, f);
            }
            if (sunburst != null && big)
            {
                sunburst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * (jackpot ? 80f : 50f));
                SetAlpha(sunburst, Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((seconds - t) / 0.3f) * 0.85f);
            }
            for (int i = 0; i < sparkles.Length; i++)
            {
                if (sparkles[i] == null || tier < Tier.Mid) continue;
                float k = Mathf.Abs(Mathf.Sin(t * 6f + i * 1.7f));
                sparkles[i].rectTransform.localScale = Vector3.one * (0.3f + k);
                SetAlpha(sparkles[i], k);
            }

            // 7 ぞろい：途中でロゴを FEVER に替える
            if (jackpot && !swappedLogo && t > seconds * 0.55f && logo != null && logoFever != null)
            {
                swappedLogo = true;
                logo.sprite = logoFever; logo.SetNativeSize();
                StartCoroutine(Pop(logo.rectTransform, 0.3f, 1.5f));
            }

            // コイン
            while (launched < coinCount && t >= nextCoin)
            {
                LaunchCoin(launched++);
                nextCoin += coinEvery;
                GameAudio.PlayAt("casino/slot_coin", at, 0.7f, 0.12f, 14f);
            }
            StepCoins(Time.deltaTime);

            // 当たりの額：数字が回って増える
            int shown = Mathf.RoundToInt(reward * Mathf.Clamp01(t / Mathf.Max(0.05f, countSeconds)));
            if (shown != shownCounter)
            {
                SetCounter(shown, true);
                if (t >= nextTick) { GameAudio.PlayAt("casino/slot_tick", at, 0.6f, 0f, 10f); nextTick = t + 0.05f; }
            }
            yield return null;
        }

        SetCounter(reward, true);
        // 片付け（コインは落ちきるまで動かし続ける）
        if (winLine != null) winLine.gameObject.SetActive(false);
        if (winLineGlow != null) winLineGlow.gameObject.SetActive(false);
        if (flash != null) flash.gameObject.SetActive(false);
        if (sunburst != null) sunburst.gameObject.SetActive(false);
        if (logo != null) logo.gameObject.SetActive(false);
        foreach (Image s in sparkles) if (s != null) s.gameObject.SetActive(false);
        float tail = 0f;
        while (tail < 1.2f && AnyCoinAlive()) { tail += Time.deltaTime; StepCoins(Time.deltaTime); yield return null; }
        foreach (Image c in coins) if (c != null) c.gameObject.SetActive(false);
    }

    // ================================================================ リールの絵

    /// <summary>
    /// from より先で、真ん中に target が来る位置（整数）。tease が 0 以上なら、その絵が 1 つ上か下に来る所を先に探す。
    /// </summary>
    private float FindStop(int reel, float from, int target, int tease)
    {
        int length = StripLength;
        int start = Mathf.CeilToInt(from);
        if (length <= 0) return start;
        if (tease >= 0)
            for (int i = 0; i < length; i++)
                if (StripAt(reel, start + i) == target && (StripAt(reel, start + i + 1) == tease || StripAt(reel, start + i - 1) == tease))
                    return start + i;
        for (int i = 0; i < length; i++)
            if (StripAt(reel, start + i) == target) return start + i;
        return start;
    }

    private int StripAt(int reel, int index)
    {
        int length = StripLength;
        if (length <= 0) return 0;
        int i = ((index % length) + length) % length;
        return Mathf.Clamp(reelStrips[reel * length + i], 0, Mathf.Max(0, SymbolCount - 1));
    }

    /// <summary>リール 1 本の 5 マスを、いまの位置に合わせて描く（位置が増えると、絵は下へ流れる）。</summary>
    private void DrawReel(int reel)
    {
        if (reelCells == null || reelCells.Length < reel * 5 + 5 || SymbolCount == 0) return;
        float pos = reelPos[reel];
        int whole = Mathf.FloorToInt(pos);
        float frac = pos - whole;
        bool blur = reelBlur[reel] && blurSprites != null && blurSprites.Length >= SymbolCount;
        for (int k = 0; k < 5; k++)
        {
            Image cell = reelCells[reel * 5 + k];
            if (cell == null) continue;
            int row = 2 - k;                       // +2 がいちばん上
            int symbol = StripAt(reel, whole + row);
            cell.sprite = blur ? blurSprites[symbol] : symbolSprites[symbol];
            cell.rectTransform.anchoredPosition = new Vector2(0f, (row - frac) * cellHeight);
        }
    }

    // ================================================================ 小さな動き

    private static float EaseOutBack(float u)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        float v = u - 1f;
        return 1f + c3 * v * v * v + c1 * v * v;
    }

    private IEnumerator PunchCell(Image cell, float scale = 1.18f, float seconds = 0.16f)
    {
        if (cell == null) yield break;
        RectTransform rect = cell.rectTransform;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            rect.localScale = Vector3.one * Mathf.Lerp(scale, 1f, t / seconds);
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    private IEnumerator Pop(RectTransform rect, float seconds, float from)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / seconds);
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, EaseOutBack(u));
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    private IEnumerator FadeFever(float goal)
    {
        float from = feverBackground.color.a, t = 0f;
        while (t < feverFadeSeconds)
        {
            t += Time.deltaTime;
            SetAlpha(feverBackground, Mathf.Lerp(from, goal, t / feverFadeSeconds));
            yield return null;
        }
        SetAlpha(feverBackground, goal);
        feverFade = null;
    }

    private void SetReach(float amount)
    {
        foreach (Image d in dimPanels)
        {
            if (d == null) continue;
            d.gameObject.SetActive(amount > 0.01f);
            SetAlpha(d, dimAlpha * amount);
        }
        if (reachFrame != null) reachFrame.gameObject.SetActive(amount > 0.01f);
    }

    private void SetCounter(int value, bool winning)
    {
        shownCounter = value;
        if (winCounter == null) return;
        winCounter.text = value.ToString("N0");
        winCounter.color = winning && value > 0 ? counterWinColor : counterIdleColor;
    }

    private static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color; c.a = a; g.color = c;
    }

    private void ResetEffects()
    {
        SetReach(0f);
        if (winLine != null) winLine.gameObject.SetActive(false);
        if (winLineGlow != null) winLineGlow.gameObject.SetActive(false);
        if (flash != null) flash.gameObject.SetActive(false);
        if (sunburst != null) sunburst.gameObject.SetActive(false);
        if (logo != null) logo.gameObject.SetActive(false);
        if (reachLogo != null) reachLogo.gameObject.SetActive(false);
        if (sweep != null) sweep.gameObject.SetActive(false);
        foreach (Image s in sparkles) if (s != null) s.gameObject.SetActive(false);
        foreach (Image c in coins) if (c != null) c.gameObject.SetActive(false);
        foreach (Image cell in reelCells) if (cell != null) cell.rectTransform.localScale = Vector3.one;
        if (feverBackground != null) SetAlpha(feverBackground, fever ? 1f : 0f);
        lampMode = 0;
    }

    // ================================================================ コイン

    private void LaunchCoin(int i)
    {
        if (i < 0 || i >= coins.Length || coins[i] == null) return;
        float angle = Mathf.Lerp(55f, 125f, Random.value) * Mathf.Deg2Rad;
        coinVelocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * coinSpeed * Random.Range(0.75f, 1.15f);
        coinLife[i] = 1.6f;
        RectTransform rect = coins[i].rectTransform;
        rect.anchoredPosition = coinOrigin + new Vector2(Random.Range(-60f, 60f), 0f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        coins[i].gameObject.SetActive(true);
    }

    private void StepCoins(float dt)
    {
        for (int i = 0; i < coins.Length; i++)
        {
            if (coins[i] == null || coinLife[i] <= 0f) continue;
            coinLife[i] -= dt;
            coinVelocity[i].y -= coinGravity * dt;
            RectTransform rect = coins[i].rectTransform;
            rect.anchoredPosition += coinVelocity[i] * dt;
            rect.Rotate(0f, 0f, 420f * dt);
            // 横の向きに回っているように、幅を伸び縮みさせる
            rect.localScale = new Vector3(Mathf.Abs(Mathf.Cos(coinLife[i] * 9f + i)) * 0.8f + 0.2f, 1f, 1f);
            if (coinLife[i] <= 0f) coins[i].gameObject.SetActive(false);
        }
    }

    private bool AnyCoinAlive()
    {
        for (int i = 0; i < coinLife.Length; i++) if (coinLife[i] > 0f) return true;
        return false;
    }

    // ================================================================ 筐体のランプ

    private void Update()
    {
        if (lampsLit == null || lampsLit.Length == 0) return;
        float speed = lampMode == 0 ? (fever ? 6f : 2.2f) : lampMode == 1 ? 12f : lampMode == 2 ? 20f : 26f;
        lampClock += Time.deltaTime * speed;
        int step = Mathf.FloorToInt(lampClock);
        for (int i = 0; i < lampsLit.Length; i++)
        {
            if (lampsLit[i] == null) continue;
            bool on = lampMode == 3 ? step % 2 == 0                        // 当たり：全部がいっせいに点滅
                : lampMode == 2 ? (i + step) % 2 == 0                      // リーチ：互い違い
                : (i + step) % 3 == 0;                                     // 待機・回転中：流れる
            if (lampsLit[i].activeSelf != on) lampsLit[i].SetActive(on);
        }
        if (lampLight != null)
        {
            lampLight.color = fever ? lampFeverColor : lampColor;
            float goal = lampMode == 3 ? (step % 2 == 0 ? lampLightWin : lampLightIdle) : lampMode == 2 ? lampLightWin * 0.5f : lampLightIdle;
            lampLight.intensity = Mathf.MoveTowards(lampLight.intensity, goal, Time.deltaTime * 30f);
        }
    }
}
