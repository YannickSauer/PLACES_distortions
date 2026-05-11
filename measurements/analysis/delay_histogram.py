import sys
import re
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.signal import savgol_filter
from scipy.spatial.transform import Rotation as R

print("script start")

# path to data folder. expects:
#   baseline_head.csv, baseline_gaze.csv
#   aftereffect_head.csv, aftereffect_gaze.csv
if len(sys.argv) > 1:
    data_folder = sys.argv[1]
else:
    data_folder = input("path to data folder: ").strip().strip('"')


# Savitzky-Golay smoothing settings (must match the timeseries script for consistency)
SAVGOL_WINDOW = 15  # 167 ms at 90 Hz sampling rate
SAVGOL_POLY = 3


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


def smooth(x):
    x = np.asarray(x, dtype=float)
    nan_mask = np.isnan(x)
    if nan_mask.all():
        return x
    if nan_mask.any():
        idx = np.arange(len(x))
        x = x.copy()
        x[nan_mask] = np.interp(idx[nan_mask], idx[~nan_mask], x[~nan_mask])
    win = SAVGOL_WINDOW
    if win > len(x):
        win = len(x) if len(x) % 2 == 1 else len(x) - 1
    if win < SAVGOL_POLY + 2:
        return x
    y = savgol_filter(x, win, SAVGOL_POLY)
    y[nan_mask] = np.nan
    return y


def measure_delay(head_t, head_yaw, eye_t, eye_yaw):
    """cross-correlation between head and -eye, returns delay in seconds."""
    t_min = max(head_t[0], eye_t[0])
    t_max = min(head_t[-1], eye_t[-1])
    n_samples = 500
    t_common = np.linspace(t_min, t_max, n_samples)

    head_finite = np.isfinite(head_yaw)
    eye_finite = np.isfinite(eye_yaw)
    if head_finite.sum() < 5 or eye_finite.sum() < 5:
        return np.nan

    head_interp = np.interp(t_common, head_t[head_finite], head_yaw[head_finite])
    eye_interp = np.interp(t_common, eye_t[eye_finite], eye_yaw[eye_finite])

    corr = np.correlate(head_interp, -eye_interp, mode="full")
    lags = np.arange(-n_samples + 1, n_samples)
    dt = t_common[1] - t_common[0]
    max_lag = int(0.3 / dt)
    valid = (lags >= -max_lag) & (lags <= max_lag)
    best_lag = lags[valid][np.argmax(corr[valid])]
    return best_lag * dt


def get_trial_signals(head_df, gaze_df, trial):
    t_start = trial["t_start"]
    t_stop = trial["t_stop"]
    h = head_df[(head_df["unity_timestamp"] >= t_start) & (head_df["unity_timestamp"] <= t_stop)]
    g = gaze_df[(gaze_df["unity_timestamp"] >= t_start) & (gaze_df["unity_timestamp"] <= t_stop)]
    if len(h) < 30 or len(g) < 30:
        return None
    head_t = h["unity_timestamp"].to_numpy() - t_start
    h_yaw = head_yaw_deg(h)
    h_yaw = smooth(h_yaw)
    h_yaw = h_yaw - np.nanmedian(h_yaw)
    eye_t = g["unity_timestamp"].to_numpy() - t_start
    e_yaw = eye_yaw_deg(g)
    e_yaw = smooth(e_yaw)
    e_yaw = e_yaw - np.nanmedian(e_yaw)
    return {
        "trial": trial,
        "head_t": head_t,
        "head_yaw": h_yaw,
        "eye_t": eye_t,
        "eye_yaw": e_yaw,
    }


# collect delays from all trials, both phases

records = []  # one entry per trial: {phase, trial_idx, mag, delay_ms}

for phase in ["baseline", "aftereffect"]:
    print(f"\n*** {phase} ***")
    head_df = load_head(f"{data_folder}/{phase}_head.csv")
    gaze_df = load_gaze(f"{data_folder}/{phase}_gaze.csv")
    trials = parse_trials(head_df, phase)
    print(f"found {len(trials)} trials")

    for tr in trials:
        s = get_trial_signals(head_df, gaze_df, tr)
        if s is None:
            continue
        d = measure_delay(s["head_t"], s["head_yaw"], s["eye_t"], s["eye_yaw"])
        if not np.isnan(d):
            records.append({
                "phase": phase,
                "trial": tr["trial_idx"],
                "magnification": tr["magnification"],
                "delay_ms": d * 1000,
            })

df = pd.DataFrame(records)
print(f"\ntotal trials with valid delay: {len(df)}")


# statistics

delays_ms = df["delay_ms"].to_numpy()
median_ms = np.median(delays_ms)
mean_ms = np.mean(delays_ms)
std_ms = np.std(delays_ms)

print(f"\n*** delay statistics ***")
print(f"median: {median_ms:.1f} ms")
print(f"mean:   {mean_ms:.1f} ms")
print(f"std:    {std_ms:.1f} ms")
print(f"min:    {delays_ms.min():.1f} ms")
print(f"max:    {delays_ms.max():.1f} ms")


# histogram plot

fig, ax = plt.subplots(figsize=(10, 6))

# main histogram. bins of 5 ms wide 
bin_width = 5  # ms
bins = np.arange(delays_ms.min() - bin_width, delays_ms.max() + bin_width * 2, bin_width)
ax.hist(delays_ms, bins=bins, color="steelblue", edgecolor="black", alpha=0.7)

# vertical lines for median and mean to find the peak easily
ax.axvline(median_ms, color="red", lw=2, ls="--",
           label=f"median = {median_ms:.1f} ms")
ax.axvline(mean_ms, color="orange", lw=2, ls=":",
           label=f"mean = {mean_ms:.1f} ms")

ax.set_xlabel("cross-correlation delay (ms)")
ax.set_ylabel("number of trials")
ax.set_title(f"Distribution of head-eye delays across all trials\n"
             f"(n = {len(df)} trials from both phases, all magnifications)")
ax.legend(fontsize=10)
ax.grid(alpha=0.3)

plt.tight_layout()
plt.savefig(f"{data_folder}/delay_histogram.png", dpi=150, bbox_inches="tight")
print(f"\nsaved delay_histogram.png")

# also save the per-trial data for later inspection
df.to_csv(f"{data_folder}/delays.csv", index=False)
print(f"saved delays.csv")

plt.show()
