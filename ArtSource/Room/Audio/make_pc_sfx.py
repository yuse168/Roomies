"""部屋の PC の効果音を、計算だけで作る（録音や外部の素材は使っていない。2026/11/25）。

使い方（プロジェクトの直下で）:
    python ArtSource/Room/Audio/make_pc_sfx.py

Assets/Resources/Audio/pc/ に WAV（44.1kHz・16bit・モノラル）を書き出す。乱数の種は固定。新しい音は最後に足す。
音量のつり合いは Roomies/Audio/Rebuild Sfx Levels（SfxLevelsBuilder.cs の表）。
"""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets", "Resources", "Audio", "pc")
rng = np.random.default_rng(20261125)


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def tone(freq, seconds, tau, harmonics=((1, 1.0),)):
    x = t(seconds)
    return sum(a * np.sin(2 * np.pi * freq * h * x) for h, a in harmonics) * np.exp(-x / tau)


def place(buf, sound, at, gain=1.0):
    i = int(SR * at)
    j = min(len(buf), i + len(sound))
    buf[i:j] += sound[: j - i] * gain


def save(name, x, peak=0.8, fade=0.03):
    x = np.asarray(x, dtype=np.float64)
    x = x - x.mean()
    x = x / max(1e-9, np.abs(x).max()) * peak
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


SOFT = ((1, 1.0), (2.0, 0.18), (3.0, 0.05))


def open_():
    """PC を開く：やわらかく上がる 3 音（ポロロン）。"""
    y = np.zeros(int(SR * 0.7))
    for i, f in enumerate((659.25, 880.0, 1318.5)):
        place(y, tone(f, 0.5, 0.14, SOFT), i * 0.07, 1.0 - i * 0.12)
    return y


def close():
    """PC を閉じる：下がる 2 音。"""
    y = np.zeros(int(SR * 0.5))
    for i, f in enumerate((987.77, 659.25)):
        place(y, tone(f, 0.38, 0.1, SOFT), i * 0.07, 1.0 - i * 0.1)
    return y


def click():
    """ボタン：小さく丸いポッ。"""
    sec = 0.07
    x = t(sec)
    f = 1500 - 500 * (x / sec)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-x / 0.018)


if __name__ == "__main__":
    save("open", open_(), 0.7, fade=0.12)
    save("close", close(), 0.65, fade=0.1)
    save("click", click(), 0.6, fade=0.01)
