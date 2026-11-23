"""カジノの効果音を、計算だけで作る（録音や外部の素材は使っていない）。

使い方（プロジェクトの直下で）:
    python ArtSource/Casino/Audio/make_casino_sfx.py

Assets/Resources/Audio/casino/ に WAV（44.1kHz・16bit・モノラル）を書き出す。乱数の種は固定なので、何度実行しても同じ音になる。
音を直したいときは、このファイルの数字を直して実行し直す。音量のつり合いは Roomies/Audio/Rebuild Sfx Levels（SfxLevelsBuilder.cs の表）。
"""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets", "Resources", "Audio", "casino")
rng = np.random.default_rng(20261123)


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def noise(seconds):
    return rng.uniform(-1, 1, int(SR * seconds))


def lowpass(x, cutoff):
    """1 次のローパス（cutoff は Hz。配列なら時間で変わる）。"""
    a = 1 - np.exp(-2 * np.pi * np.broadcast_to(cutoff, x.shape) / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def env(n, attack, release, hold=None):
    """立ち上がり attack 秒、最後の release 秒で消える包み。"""
    e = np.ones(n)
    a, r = int(SR * attack), int(SR * release)
    if a > 0:
        e[:a] = np.linspace(0, 1, a)
    if r > 0:
        e[-r:] *= np.linspace(1, 0, r) ** 2
    return e


def decay(seconds, tau):
    return np.exp(-t(seconds) / tau)


def tone(freq, seconds, tau, harmonics=((1, 1.0),)):
    x = t(seconds)
    y = sum(amp * np.sin(2 * np.pi * freq * h * x) for h, amp in harmonics)
    return y * np.exp(-x / tau)


def click(freq, seconds=0.05, tau=0.008, bright=0.5):
    """硬い物が当たる短い音（共鳴＋雑音）。"""
    n = int(SR * seconds)
    x = np.arange(n) / SR
    body = np.sin(2 * np.pi * freq * x) + 0.5 * np.sin(2 * np.pi * freq * 2.37 * x)
    return (body * (1 - bright) + highpass(rng.uniform(-1, 1, n), 2500) * bright) * np.exp(-x / tau)


def mix(*sounds):
    """長さの違う音を、頭をそろえて重ねる。"""
    y = np.zeros(max(len(x) for x in sounds))
    for x in sounds:
        y[: len(x)] += x
    return y


def place(buf, sound, at, gain=1.0):
    i = int(SR * at)
    j = min(len(buf), i + len(sound))
    if i < len(buf):
        buf[i:j] += sound[: j - i] * gain


def save(name, x, peak=0.85):
    x = np.asarray(x, dtype=np.float64)
    x = x - x.mean()
    m = np.abs(x).max()
    if m > 0:
        x = x / m * peak
    # 頭と終わりを 3ms でつなぐ（プチッという音を防ぐ）
    f = int(SR * 0.003)
    x[:f] *= np.linspace(0, 1, f)
    x[-f:] *= np.linspace(1, 0, f)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    print(f"{name}.wav  {len(x) / SR:.2f}s")


# ---------------------------------------------------------------- エレベーター
def elevator_door(opening):
    """扉が横に滑る音：モーターのうなり＋こすれる音＋止まるときのコトン。"""
    sec = 0.75
    x = t(sec)
    n = len(x)
    speed = np.sin(np.pi * np.clip(x / 0.6, 0, 1)) ** 0.6
    motor = np.sin(2 * np.pi * np.cumsum(70 + 50 * speed) / SR) * 0.35 * speed
    slide = lowpass(noise(sec), 900 + 1500 * speed) * 0.5 * speed
    y = motor + slide
    thud = mix(tone(95 if opening else 70, 0.14, 0.03), click(420, 0.05, 0.01, 0.3) * 0.5)
    place(y, thud, 0.6, 0.9 if not opening else 0.55)
    return y * env(n, 0.03, 0.08)


def elevator_move():
    """動いているあいだ（約 2.5 秒）：低いうなり＋ロープのきしみ。前後は静かに立ち上がって消える。"""
    sec = 2.5
    x = t(sec)
    hum = sum(a * np.sin(2 * np.pi * f * x + p) for f, a, p in ((48, 1.0, 0), (96, 0.5, 1.1), (144, 0.22, 2.0), (61, 0.3, 0.4)))
    hum *= 1 + 0.12 * np.sin(2 * np.pi * 5.5 * x)
    air = lowpass(noise(sec), 500) * 0.6
    creak = lowpass(noise(sec), 2200) * 0.08 * (np.sin(2 * np.pi * 1.3 * x) > 0.75)
    return (hum * 0.5 + air + creak) * env(len(x), 0.35, 0.5)


def elevator_ding():
    """到着の「チン」：鐘の倍音が 2 つ重なる。"""
    sec = 1.4
    bell = [(1, 1.0), (2.76, 0.45), (5.4, 0.22), (8.9, 0.1)]
    y = tone(1318.5, sec, 0.38, bell)
    place(y, tone(1046.5, sec - 0.02, 0.45, bell), 0.02, 0.35)
    place(y, click(3000, 0.03, 0.004, 0.7), 0, 0.5)
    return y


# ---------------------------------------------------------------- ルーレット
def roulette_spin():
    """玉が縁を転がる（約 6 秒）：はじめは速くて高く、だんだん遅く低くなる。終わりのほうで仕切りに当たりはじめる。"""
    sec = 6.0
    x = t(sec)
    k = x / sec
    rate = 9.0 * (1 - k) ** 1.4 + 1.2            # 1 秒あたりの周回
    phase = np.cumsum(rate) / SR
    roll = lowpass(noise(sec), 1400 + 3200 * (1 - k)) * (0.55 + 0.45 * np.sin(2 * np.pi * phase))
    roll += 0.25 * np.sin(2 * np.pi * np.cumsum(180 + 420 * (1 - k)) / SR) * (0.6 + 0.4 * np.sin(2 * np.pi * phase * 2))
    y = roll * (0.35 + 0.65 * (1 - k) ** 0.5)
    # 仕切りに当たる音：3.6 秒から、間がだんだん空く
    at, gap = 3.6, 0.07
    while at < sec - 0.1:
        place(y, click(rng.uniform(1700, 2600), 0.05, 0.006, 0.45), at, rng.uniform(0.35, 0.6))
        gap *= 1.11
        at += gap
    return y * env(len(x), 0.08, 0.25)


def roulette_stop():
    """玉がポケットに落ちて止まる：はねる音が 4 回、だんだん小さく。"""
    y = np.zeros(int(SR * 0.9))
    at, gap, gain = 0.0, 0.16, 1.0
    for _ in range(5):
        place(y, mix(click(rng.uniform(1500, 2200), 0.07, 0.012, 0.35), tone(520, 0.07, 0.015) * 0.3), at, gain)
        at += gap
        gap *= 0.62
        gain *= 0.6
    place(y, lowpass(noise(0.3), 700) * decay(0.3, 0.08), at, 0.25)
    return y


def chip():
    """チップを置く：粘土のチップ同士が当たる、短く乾いた音が 2 つ。"""
    y = np.zeros(int(SR * 0.16))
    place(y, click(2900, 0.06, 0.007, 0.35), 0, 1.0)
    place(y, click(3400, 0.05, 0.006, 0.35), 0.045, 0.6)
    return y


# ---------------------------------------------------------------- ハイ＆ロー
def card():
    """カードをめくる：紙がこすれるシュッ＋台に当たるパチ。"""
    sec = 0.22
    x = t(sec)
    swish = highpass(noise(sec), 1800) * np.exp(-((x - 0.07) / 0.04) ** 2)
    y = lowpass(swish, 7000) * 0.7
    place(y, click(900, 0.05, 0.008, 0.6), 0.13, 0.9)
    return y


def jingle(notes, step, tau, wave_mix=0.25):
    """短い旋律（当たり・外れ）。やわらかい鐘のような音色。"""
    y = np.zeros(int(SR * (step * len(notes) + tau * 4)))
    for i, f in enumerate(notes):
        note = tone(f, tau * 4, tau, [(1, 1.0), (2, wave_mix), (3, wave_mix * 0.4), (4.2, 0.08)])
        note[: int(SR * 0.004)] *= np.linspace(0, 1, int(SR * 0.004))
        place(y, note, i * step, 1.0)
    return y


def win():
    return jingle([659.3, 784.0, 987.8, 1318.5], 0.085, 0.16)          # ミ・ソ・シ・ミ


def lose():
    y = jingle([392.0, 311.1, 233.1], 0.14, 0.2, 0.5)                  # ソ・ミ♭・シ♭（下がる）
    return lowpass(y, 1800)


def cash():
    """手持ちを受け取る：コインが 5 枚ほど続けて落ちる。"""
    y = np.zeros(int(SR * 0.75))
    at = 0.0
    for i in range(6):
        f = rng.uniform(3200, 4400)
        coin = mix(tone(f, 0.22, 0.05, [(1, 1.0), (1.48, 0.6), (2.2, 0.3)]), click(f, 0.03, 0.004, 0.6) * 0.5)
        place(y, coin, at, rng.uniform(0.6, 1.0))
        at += rng.uniform(0.05, 0.095)
    return y


# ---------------------------------------------------------------- チンチロ
def dice():
    """どんぶりの中でサイコロ 3 つが転がる（約 1.3 秒）：陶器に当たる高い音が、はじめは細かく、だんだん間が空いて止まる。"""
    sec = 1.5
    y = np.zeros(int(SR * sec))
    for d in range(3):
        at, gap = rng.uniform(0, 0.05), rng.uniform(0.045, 0.07)
        gain = 1.0
        while at < 1.25:
            f = rng.uniform(2300, 3600)
            hit = mix(click(f, 0.06, 0.009, 0.3), tone(rng.uniform(1100, 1500), 0.09, 0.02) * 0.35)  # 2 つ目はどんぶりの鳴り
            place(y, hit, at, gain * rng.uniform(0.6, 1.0))
            gap *= rng.uniform(1.12, 1.3)
            gain *= 0.88
            at += gap
    place(y, lowpass(noise(1.2), 1500) * (1 - t(1.2) / 1.2) ** 2, 0, 0.12)   # 転がるこすれ
    return y


if __name__ == "__main__":
    save("elevator_open", elevator_door(True), 0.7)
    save("elevator_close", elevator_door(False), 0.7)
    save("elevator_move", elevator_move(), 0.6)
    save("elevator_ding", elevator_ding(), 0.8)
    save("roulette_spin", roulette_spin(), 0.8)
    save("roulette_stop", roulette_stop(), 0.85)
    save("chip", chip(), 0.8)
    save("card", card(), 0.8)
    save("win", win(), 0.8)
    save("lose", lose(), 0.8)
    save("cash", cash(), 0.8)
    save("dice", dice(), 0.85)
