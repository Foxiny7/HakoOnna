"""Luminance around the HDRI moon (annuli in degrees) and per-azimuth horizon stats.
usage: hdri_moon_stats.py <file.hdr> <moon u> <moon v>
"""
import sys
import cv2
import numpy as np

img = cv2.imread(sys.argv[1], cv2.IMREAD_ANYDEPTH | cv2.IMREAD_COLOR)[:, :, ::-1].astype(np.float64)
h, w, _ = img.shape
mu, mv = float(sys.argv[2]), float(sys.argv[3])
lum = 0.2126 * img[:, :, 0] + 0.7152 * img[:, :, 1] + 0.0722 * img[:, :, 2]
# refine the moon centre: brightest pixel within 3 deg of the given uv
cy, cx = int(mv * h), int(mu * w)
r = int(3 / 360 * w)
win = lum[cy - r:cy + r, cx - r:cx + r]
dy, dx = np.unravel_index(np.argmax(win), win.shape)
cy, cx = cy - r + dy, cx - r + dx
print(f"{w}x{h} moon px ({cx},{cy}) u {(cx + .5) / w:.5f} v {(cy + .5) / h:.5f} peak {lum[cy, cx]:.2f}")
el0 = np.radians(90 - (cy + .5) / h * 180)
az0 = np.radians((cx + .5) / w * 360)
ys = slice(max(0, cy - int(12 / 180 * h)), cy + int(12 / 180 * h))
xs = slice(cx - int(14 / 360 * w), cx + int(14 / 360 * w))
yy, xx = np.mgrid[ys, xs]
el = np.radians(90 - (yy + .5) / h * 180)
az = np.radians((xx + .5) / w * 360)
cosd = np.sin(el) * np.sin(el0) + np.cos(el) * np.cos(el0) * np.cos(az - az0)
d = np.degrees(np.arccos(np.clip(cosd, -1, 1)))
L = lum[ys, xs]
C = img[ys, xs]
for a, b in ((0, .3), (.3, .6), (.6, 1), (1, 1.5), (1.5, 2), (2, 3), (3, 4), (4, 6), (6, 9), (9, 12)):
    m = (d >= a) & (d < b)
    if m.any():
        c = C[m].mean(axis=0)
        print(f"  {a:4.1f}-{b:4.1f} deg: mean {L[m].mean():7.3f}  p50 {np.percentile(L[m], 50):7.3f}  p90 {np.percentile(L[m], 90):7.3f}  max {L[m].max():7.3f}  rgb ({c[0]:.3f},{c[1]:.3f},{c[2]:.3f})")
# the point 10 deg straight below the moon (where our moon will be drawn)
for de in (6, 8, 10, 12):
    y = int((90 - (np.degrees(el0) - de)) / 180 * h)
    p = img[y - 3:y + 4, cx - 3:cx + 4].reshape(-1, 3).mean(axis=0)
    print(f"  {de} deg below: rgb ({p[0]:.3f},{p[1]:.3f},{p[2]:.3f}) lum {0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]:.3f}")
# horizon band 0..4 deg per 30 deg of u, and 20..40 deg band
for lo, hi in ((0, 4), (4, 10), (20, 40)):
    y0, y1 = int((90 - hi) / 180 * h), int((90 - lo) / 180 * h)
    row = []
    for k in range(12):
        x0, x1 = int(k / 12 * w), int((k + 1) / 12 * w)
        p = img[y0:y1, x0:x1].reshape(-1, 3).mean(axis=0)
        row.append(f"{k * 30:3d}:{0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]:.3f}/{p[0] / max(p[2], 1e-6):.2f}")
    print(f"  elev {lo}-{hi} (u deg: lum/R÷B):", " ".join(row))
print("  zenith 70-90 mean", lum[: int(20 / 180 * h)].mean())
