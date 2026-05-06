import sys
import re
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.signal import savgol_filter, find_peaks
from scipy.spatial.transform import Rotation as R

print("script start")

# path to data folder
if len(sys.argv) > 1:
    data_folder = sys.argv[1]
else:
    data_folder = input("path to data folder: ").strip().strip('"')


# settings
SAVGOL_POLY = 3
PEAK_MIN_DISTANCE_S = 0.3
PEAK_MIN_PROMINENCE_DEG = 3.0
GLOBAL_DELAY_S = -0.074  # apply global cross-correlation delay (median from histogram)

# different window sizes to compare
# at 90 Hz sampling rate: window 9 = 100ms, 15 = 167ms, 21 = 233ms, 31 = 344ms
WINDOWS_TO_COMPARE = [9, 13, 15, 17, 21, 31,]


# helpers (same as in the other scripts)

def load_head(path):
    df = pd.read_csv(path)
    df_clean = pd.DataFrame({
        "unity_timestamp": df.iloc[:, 0],
        "qx": df.iloc[:, 5],
        "qy": df.iloc[:, 6],
        "qz": df.iloc[:, 7],
        "qw": df.iloc[:, 8],
        "messages": df.iloc[:, 23],
    })
    return df_clean


def load_gaze(path):
    df = pd.read_csv(path)
    df = df.loc[:, df.columns.str.strip() != ""]
    return df


def parse_trials(head_df, phase):
    trials = []
    pattern = re.compile(r"StartTrial_(\w+?)_t(\d+)_mag([\d.]+)_rad(\d+)")
    pending = {}
    for ts, msg in zip(head_df["unity_timestamp"], head_df["messages"]):
        if not isinstance(msg, str) or msg == "":
            continue
        match = pattern.match(msg)
        if match and match.group(1) == phase:
            trial_idx = int(match.group(2))
            pending[trial_idx] = {
                "trial_idx": trial_idx,
                "magnification": float(match.group(3)),
                "t_start": float(ts),
                "t_stop": None,
            }
        elif msg.startswith(f"StopTrial_{phase}_t"):
            trial_idx = int(msg.split("_t")[-1])
            if trial_idx in pending:
                pending[trial_idx]["t_stop"] = float(ts)
                trials.append(pending[trial_idx])
                del pending[trial_idx]
    return trials


def head_yaw_deg(head_slice):
    q = head_slice[["qx", "qy", "qz", "qw"]].to_numpy(copy=True)
    eul = R.from_quat(q).as_euler("yxz", degrees=True)
    yaw = eul[:, 0]
    yaw = np.rad2deg(np.unwrap(np.deg2rad(yaw)))
    return yaw


def eye_yaw_deg(gaze_slice):
    gx = gaze_slice["combined_eye_gaze.x"].to_numpy()
    gz = gaze_slice["combined_eye_gaze.z"].to_numpy()
    yaw = np.degrees(np.arctan2(gx, gz))
    invalid = (gx == 0) & (gz == 0)
    yaw[invalid] = np.nan
    return yaw


def smooth(x, win):
    """Savitzky-Golay with the chosen window size."""
    x = np.asarray(x, dtype=float)
    nan_mask = np.isnan(x)
    if nan_mask.all():
        return x
    if nan_mask.any():
        idx = np.arange(len(x))
        x = x.copy()
        x[nan_mask] = np.interp(idx[nan_mask], idx[~nan_mask], x[~nan_mask])
    if win > len(x):
        win = len(x) if len(x) % 2 == 1 else len(x) - 1
    if win < SAVGOL_POLY + 2:
        return x
    y = savgol_filter(x, win, SAVGOL_POLY)
    y[nan_mask] = np.nan
    return y


# amplitude calculation: two methods to compare

def get_amplitude_old(t, x):
    """old method: detect head and eye peaks independently, mean of absolute peak values."""
    if len(t) < 5 or np.all(np.isnan(x)):
        return np.nan
    fs = 1.0 / np.median(np.diff(t))
    distance = max(1, int(PEAK_MIN_DISTANCE_S * fs))
    finite = np.isfinite(x)
    if finite.sum() < 5:
        return np.nan
    xs = x[finite]
    pos, _ = find_peaks(xs, distance=distance, prominence=PEAK_MIN_PROMINENCE_DEG)
    neg, _ = find_peaks(-xs, distance=distance, prominence=PEAK_MIN_PROMINENCE_DEG)
    pv = np.concatenate([xs[pos], -xs[neg]])
    if len(pv) == 0:
        return np.nan
    return float(np.mean(np.abs(pv)))


def get_peaks_with_times(t, x):
    """find peaks and return list of (time, signed_amplitude) tuples."""
    if len(t) < 5 or np.all(np.isnan(x)):
        return []
    fs = 1.0 / np.median(np.diff(t))
    distance = max(1, int(PEAK_MIN_DISTANCE_S * fs))
    finite = np.isfinite(x)
    if finite.sum() < 5:
        return []
    xs = x[finite]
    ts = t[finite]
    pos, _ = find_peaks(xs, distance=distance, prominence=PEAK_MIN_PROMINENCE_DEG)
    neg, _ = find_peaks(-xs, distance=distance, prominence=PEAK_MIN_PROMINENCE_DEG)
    peaks = [(ts[i], xs[i]) for i in pos] + [(ts[i], xs[i]) for i in neg]
    peaks.sort(key=lambda p: p[0])
    return peaks


def get_amplitudes_paired(head_t, head_yaw, eye_t, eye_yaw, max_pair=0.3):
    """new method: match each head peak with the closest eye peak in time
    (with opposite sign because of VOR direction). guarantees head and eye
    amplitudes are based on the same set of oscillation cycles."""
    hp = get_peaks_with_times(head_t, head_yaw)
    ep = get_peaks_with_times(eye_t, eye_yaw)
    if not hp or not ep:
        return np.nan, np.nan
    head_amps, eye_amps = [], []
    for h_t, h_amp in hp:
        # eye peak should have opposite sign of head peak (VOR direction)
        same = [(et, ea) for et, ea in ep if np.sign(ea) == -np.sign(h_amp)]
        if not same:
            continue
        c = min(same, key=lambda p: abs(p[0] - h_t))
        if abs(c[0] - h_t) > max_pair:
            continue
        head_amps.append(abs(h_amp))
        eye_amps.append(abs(c[1]))
    if not head_amps:
        return np.nan, np.nan
    return float(np.mean(head_amps)), float(np.mean(eye_amps))


# load all trials once (without smoothing) so we can re-smooth with different windows

print("\nloading trials...")
all_trial_data = []  # list of (phase, mag, head_t, head_yaw_raw, eye_t, eye_yaw_raw)

for phase in ["baseline", "aftereffect"]:
    head_df = load_head(f"{data_folder}/{phase}_head.csv")
    gaze_df = load_gaze(f"{data_folder}/{phase}_gaze.csv")
    trials = parse_trials(head_df, phase)
    for tr in trials:
        h = head_df[(head_df["unity_timestamp"] >= tr["t_start"]) & (head_df["unity_timestamp"] <= tr["t_stop"])]
        g = gaze_df[(gaze_df["unity_timestamp"] >= tr["t_start"]) & (gaze_df["unity_timestamp"] <= tr["t_stop"])]
        if len(h) < 30 or len(g) < 30:
            continue

        head_t = h["unity_timestamp"].to_numpy() - tr["t_start"]
        h_yaw = head_yaw_deg(h)
        h_yaw = h_yaw - np.median(h_yaw)

        eye_t = g["unity_timestamp"].to_numpy() - tr["t_start"] + GLOBAL_DELAY_S
        e_yaw = eye_yaw_deg(g)
        e_yaw = e_yaw - np.nanmedian(e_yaw)

        all_trial_data.append((phase, tr["magnification"], head_t, h_yaw, eye_t, e_yaw))

print(f"loaded {len(all_trial_data)} trials")


# loop over window sizes and compute gains for both methods

print(f"\n*** comparing window sizes ***")
print(f"{'window':>8} {'ms':>6}  {'old-base':>10} {'old-after':>10}  {'new-base':>10} {'new-after':>10}")
print("-" * 65)

results = []  # for plotting

for win in WINDOWS_TO_COMPARE:
    old_base, old_after = [], []
    new_base, new_after = [], []

    for phase, mag, head_t, head_yaw_raw, eye_t, eye_yaw_raw in all_trial_data:
        head_yaw_smoothed = smooth(head_yaw_raw, win)
        eye_yaw_smoothed = smooth(eye_yaw_raw, win)

        # old method: independent means
        oh = get_amplitude_old(head_t, head_yaw_smoothed)
        oe = get_amplitude_old(eye_t, eye_yaw_smoothed)
        if not np.isnan(oh) and oh > 0 and not np.isnan(oe):
            gain = oe / oh
            if phase == "baseline":
                old_base.append(gain)
            else:
                old_after.append(gain)

        # new method: paired peak matching
        nh, ne = get_amplitudes_paired(head_t, head_yaw_smoothed, eye_t, eye_yaw_smoothed)
        if not np.isnan(nh) and nh > 0:
            gain = ne / nh
            if phase == "baseline":
                new_base.append(gain)
            else:
                new_after.append(gain)

    win_ms = win * 1000.0 / 90.0  # at 90 Hz sampling rate
    results.append({
        "window": win,
        "ms": win_ms,
        "old_base": np.mean(old_base),
        "old_after": np.mean(old_after),
        "new_base": np.mean(new_base),
        "new_after": np.mean(new_after),
    })
    print(f"{win:>8} {win_ms:>6.0f}  {np.mean(old_base):>10.3f} {np.mean(old_after):>10.3f}  "
          f"{np.mean(new_base):>10.3f} {np.mean(new_after):>10.3f}")

results_df = pd.DataFrame(results)
results_df.to_csv("window_comparison.csv", index=False)
print(f"\nsaved window_comparison.csv")


# plot

fig, ax = plt.subplots(figsize=(10, 6))

ax.plot(results_df["ms"], results_df["old_base"], "o-", color="darkblue",
        label="old method - baseline", lw=1.5)
ax.plot(results_df["ms"], results_df["old_after"], "s-", color="darkblue",
        alpha=0.5, label="old method - aftereffect", lw=1.5)
ax.plot(results_df["ms"], results_df["new_base"], "o-", color="orange",
        label="paired method - baseline", lw=2)
ax.plot(results_df["ms"], results_df["new_after"], "s-", color="orange",
        alpha=0.5, label="paired method - aftereffect", lw=2)

ax.axhline(1.0, color="black", ls=":", lw=1, label="gain = 1 (perfect VOR)")
ax.axvline(233, color="gray", ls="--", lw=0.5)
ax.text(238, results_df["old_base"].min(), " window=21\n (current)",
        fontsize=9, color="gray", verticalalignment="bottom")

ax.set_xlabel("Savitzky-Golay window (ms)")
ax.set_ylabel("mean VOR gain")
ax.set_title("Effect of smoothing window size on measured VOR gain\n"
             "old method (independent peaks) vs paired peak matching")
ax.grid(alpha=0.3)
ax.legend(fontsize=9, loc="lower left")

plt.tight_layout()
plt.savefig("window_comparison.png", dpi=150, bbox_inches="tight")
print(f"saved window_comparison.png")
plt.show()
