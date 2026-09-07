"""Validate baked PCM assets without requiring a Unity play session."""
import array
import json
import math
from pathlib import Path
import sys
import wave

root = Path(__file__).resolve().parents[1]
results = []
for path in sorted((root / "Assets/Resources/RoomiesAudio").glob("*.wav")):
    with wave.open(str(path), "rb") as wav:
        assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (2, 2, 44100), path
        samples = array.array("h", wav.readframes(wav.getnframes()))
    if sys.byteorder != "little":
        samples.byteswap()
    peak = max(abs(value) for value in samples) / 32768
    rms = math.sqrt(sum(value * value for value in samples) / len(samples)) / 32768
    assert 0.005 < rms and peak < 0.99, (path, peak, rms)
    result = dict(name=path.stem, seconds=len(samples) / 88200, peak=peak, rms=rms)
    if path.stem.endswith("_loop"):
        seam = max(abs(samples[0] - samples[-2]), abs(samples[1] - samples[-1])) / 32768
        result["loop_seam_delta"] = seam
        assert seam < 0.03, (path, seam)
    results.append(result)
assert len(results) == 18, len(results)
report = root / "ArtReview/2026-09-06/audio-pcm-validation.json"
report.write_text(json.dumps(results, indent=2), encoding="utf-8")
print(f"PASS: {len(results)} stereo PCM files, no clipping/silence; all 3 loop seams < 0.03.")
