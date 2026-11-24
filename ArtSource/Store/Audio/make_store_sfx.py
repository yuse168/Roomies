"""コンビニの効果音を、計算だけで作る（録音や外部の素材は使っていない。2026/11/24）。

使い方（プロジェクトの直下で）:
    python ArtSource/Store/Audio/make_store_sfx.py

Assets/Resources/Audio/store/ に WAV（44.1kHz・16bit・モノラル）を書き出す。乱数の種は固定なので、何度実行しても同じ音になる。
新しい音を足すときは、いちばん最後に足す（途中に足すと乱数の順が変わり、前からある音が変わってしまう）。
音量のつり合いは Roomies/Audio/Rebuild Sfx Levels（SfxLevelsBuilder.cs の表）。

入店のチャイムは、オリジナルの 2 音（実在の店のチャイムのメロディーはまねしない）。
"""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets", "Resources", "Audio", "store")
rng = np.random.default_rng(20261124)


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def noise(seconds):
    return rng.uniform(-1, 1, int(SR * seconds))


def lowpass(x, cutoff):
    a = 1 - np.exp(-2 * np.pi * np.broadcast_to(cutoff, x.shape) / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def tone(freq, seconds, tau, harmonics=((1, 1.0),)):
    x = t(seconds)
    y = sum(amp * np.sin(2 * np.pi * freq * h * x) for h, amp in harmonics)
    return y * np.exp(-x / tau)


def click(freq, seconds=0.05, tau=0.008, bright=0.5):
    n = int(SR * seconds)
    x = np.arange(n) / SR
    body = np.sin(2 * np.pi * freq * x) + 0.5 * np.sin(2 * np.pi * freq * 2.37 * x)
    return (body * (1 - bright) + highpass(rng.uniform(-1, 1, n), 2500) * bright) * np.exp(-x / tau)


def place(buf, sound, at, gain=1.0):
    i = int(SR * at)
    j = min(len(buf), i + len(sound))
    if i < len(buf):
        buf[i:j] += sound[: j - i] * gain


def save(name, x, peak=0.85, fade=0.03):
    x = np.asarray(x, dtype=np.float64)
    x = x - x.mean()
    m = np.abs(x).max()
    if m > 0:
        x = x / m * peak
    n = int(SR * fade)
    x[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    f = int(SR * 0.003)
    x[:f] *= np.linspace(0, 1, f)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    print(f"{name}.wav  {len(x) / SR:.2f}s")


BELL = ((1, 1.0), (2.0, 0.28), (3.0, 0.1), (4.2, 0.06))


def door_chime():
    """入店のチャイム：やわらかい鐘の 2 音（ソ → ミ）。"""
    y = np.zeros(int(SR * 1.5))
    place(y, tone(783.99, 1.3, 0.42, BELL), 0.0, 1.0)
    place(y, tone(659.25, 1.2, 0.48, BELL), 0.36, 0.9)
    return lowpass(y, 5200)


def door_slide():
    """自動ドアが動く：モーターの小さなうなりと、空気の流れ。"""
    sec = 0.55
    x = t(sec)
    hum = (np.sin(2 * np.pi * 110 * x) + 0.4 * np.sin(2 * np.pi * 220 * x)) * 0.25
    air = lowpass(noise(sec), 900) * 0.8
    env = np.sin(np.pi * np.clip(x / sec, 0, 1)) ** 1.2
    return (hum + air) * env


def pick():
    """商品を手に取る：上へ上がる「ポン」。"""
    sec = 0.16
    x = t(sec)
    f = 520 + 900 * (x / sec) ** 0.7
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-x / 0.05)
    place(y, click(1800, 0.03, 0.004, 0.3), 0, 0.4)
    return y


def putback():
    """棚に戻す：下がる「ポン」。"""
    sec = 0.16
    x = t(sec)
    f = 1200 - 650 * (x / sec) ** 0.7
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-x / 0.05)


def basket_full():
    """カゴがいっぱいになった：明るい 3 音。"""
    y = np.zeros(int(SR * 0.6))
    for i, f in enumerate((1046.5, 1318.5, 1568.0)):
        n = tone(f, 0.35, 0.09, BELL)
        n[: int(SR * 0.003)] *= np.linspace(0, 1, int(SR * 0.003))
        place(y, n, i * 0.07, 1.0 - i * 0.1)
    return y


def putdown():
    """カウンターに置く：軽いコトッ。"""
    return lowpass(click(620, 0.07, 0.012, 0.25), 3000)


def scan():
    """読み取り：ピッ（少し角のある電子音）。"""
    sec = 0.11
    x = t(sec)
    y = np.sign(np.sin(2 * np.pi * 2650 * x)) * 0.6 + np.sin(2 * np.pi * 2650 * x) * 0.4
    y = lowpass(y, 6000)
    y[: int(SR * 0.004)] *= np.linspace(0, 1, int(SR * 0.004))
    y[-int(SR * 0.01):] *= np.linspace(1, 0, int(SR * 0.01))
    return y


def tick():
    """金額が回る（1 目盛り）。"""
    return click(3200, 0.025, 0.004, 0.35)


def register():
    """レジの引き出しが開く：すべる音 → 止まるガチャッ → 小さなベル。"""
    y = np.zeros(int(SR * 0.9))
    x = t(0.18)
    place(y, lowpass(noise(0.18), 1600) * np.sin(np.pi * x / 0.18) * 0.6, 0, 1.0)
    place(y, click(420, 0.08, 0.015, 0.4), 0.17, 1.0)
    place(y, tone(2093.0, 0.7, 0.2, ((1, 1.0), (2.76, 0.3), (5.4, 0.12))), 0.2, 0.55)
    return y


def coins():
    """おつり：コインが 4 枚、チャリン。"""
    y = np.zeros(int(SR * 0.6))
    at = 0.0
    for i in range(4):
        f = rng.uniform(3400, 4600)
        c = tone(f, 0.2, 0.04, ((1, 1.0), (1.48, 0.6), (2.2, 0.3)))
        place(c, click(f, 0.02, 0.003, 0.6), 0, 0.5)
        place(y, c, at, rng.uniform(0.6, 1.0))
        at += rng.uniform(0.05, 0.09)
    return y


def receipt():
    """レシートが出てくる：小さなプリンターのジジジ。"""
    sec = 0.55
    x = t(sec)
    buzz = highpass(lowpass(noise(sec), 5000), 900) * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 38 * x)))
    return buzz * np.sin(np.pi * np.clip(x / sec, 0, 1)) ** 0.6 * 0.6


def bag():
    """レジ袋：カサッ（こすれる音が 3 回）。"""
    y = np.zeros(int(SR * 0.45))
    for at, g in ((0.0, 1.0), (0.09, 0.7), (0.2, 0.5)):
        n = 0.12
        x = t(n)
        place(y, highpass(lowpass(noise(n), 7000), 1500) * np.exp(-x / 0.035), at, g)
    return y


# ---- レジ打ちのバイト（2026/11/24 夕方に足した。前の音の乱数の順を変えないよう、いちばん最後に作る）
def timecard():
    """タイムカードを押す：差しこむすべり → ガチャン（金属の打刻）→ 小さなベル。"""
    y = np.zeros(int(SR * 0.7))
    x = t(0.09)
    place(y, highpass(lowpass(noise(0.09), 4000), 700) * np.sin(np.pi * x / 0.09) * 0.5, 0, 1.0)
    place(y, click(300, 0.1, 0.02, 0.35), 0.09, 1.0)
    place(y, click(1500, 0.05, 0.006, 0.6), 0.1, 0.6)
    place(y, tone(1760.0, 0.5, 0.14, ((1, 1.0), (2.76, 0.25))), 0.13, 0.35)
    return y


def job_good():
    """お会計がうまくいった：明るく上がる 2 音（ポロン）。"""
    y = np.zeros(int(SR * 0.5))
    for i, f in enumerate((1318.5, 1975.5)):
        place(y, tone(f, 0.32, 0.08, BELL), i * 0.075, 1.0 - i * 0.15)
    return y


def job_miss():
    """ミス：低く下がる、やわらかいブブー（責めすぎない短さ）。"""
    sec = 0.38
    x = t(sec)
    f = 220 - 70 * (x / sec)
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = (np.sign(np.sin(ph)) * 0.4 + np.sin(ph) * 0.6) * np.exp(-x / 0.18)
    return lowpass(y, 1800)


def job_end():
    """バイト終了：上がっていく 4 音と、最後の和音。"""
    y = np.zeros(int(SR * 1.3))
    notes = (783.99, 987.77, 1174.66, 1567.98)
    for i, f in enumerate(notes):
        place(y, tone(f, 0.4, 0.1, BELL), i * 0.09, 0.8)
    for f in (1174.66, 1567.98, 1975.53):
        place(y, tone(f, 0.9, 0.32, BELL), 0.4, 0.45)
    return lowpass(y, 6000)


if __name__ == "__main__":
    save("door_chime", door_chime(), 0.75, fade=0.15)
    save("door_slide", door_slide(), 0.55)
    save("pick", pick(), 0.7)
    save("putback", putback(), 0.6)
    save("basket_full", basket_full(), 0.7, fade=0.08)
    save("putdown", putdown(), 0.7)
    save("scan", scan(), 0.6, fade=0.005)
    save("tick", tick(), 0.5, fade=0.005)
    save("register", register(), 0.8, fade=0.1)
    save("coins", coins(), 0.7, fade=0.06)
    save("receipt", receipt(), 0.5)
    save("bag", bag(), 0.6)
    save("timecard", timecard(), 0.75, fade=0.1)
    save("job_good", job_good(), 0.7, fade=0.08)
    save("job_miss", job_miss(), 0.6, fade=0.06)
    save("job_end", job_end(), 0.75, fade=0.2)
