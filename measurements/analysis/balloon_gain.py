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


# Savitzky-Golay smoothing settings (same as in the other scripts)
SAVGOL_WINDOW = 15
SAVGOL_POLY = 3

# eye-tracker delay (use the same global value as in the other scripts)
GLOBAL_DELAY_S = -0.077

# sliding window for gain estimation
WINDOW_SECONDS = 30
STEP_SECONDS = 10

# peak detection settings.
# during top-ups and adaptation the participant moves the head freely while
# popping balloons, so amplitudes vary. we use settings somewhere between
# the swimtest (~10 deg) and adaptation full sweeps (~90 deg).
PEAK_MIN_DISTANCE_S = 0.5
PEAK_MIN_PROMINENCE_DEG = 5.0


# helpers (same as in the other scripts)

def load_head(path):
    df = pd.read_csv(path, low_memory=False)
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


def head_yaw_deg(head_df):
    q = head_df[["qx", "qy", "qz", "qw"]].to_numpy(copy=True)
    eul = R.from_quat(q).as_euler("yxz", degrees=True)
    yaw = eul[:, 0]
    yaw = np.rad2deg(np.unwrap(np.deg2rad(yaw)))
    return yaw


def eye_yaw_deg(gaze_df):
    gx = gaze_df["combined_eye_gaze.x"].to_numpy()
    gz = gaze_df["combined_eye_gaze.z"].to_numpy()
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


def get_amplitudes_paired(head_t, head_yaw, eye_t, eye_yaw, max_pair=0.5):
    """match each head peak with the closest eye peak in time."""
    hp = get_peaks_with_times(head_t, head_yaw)
    ep = get_peaks_with_times(eye_t, eye_yaw)
    if not hp or not ep:
        return np.nan, np.nan
    head_amps, eye_amps = [], []
    for h_t, h_amp in hp:
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


def parse_topup_intervals(head_df, phase):
    """find StartTopUp/StopTopUp markers in the messages column.
    returns a list of (round_idx, t_start, t_stop) tuples."""
    intervals = []
    pattern_start = re.compile(rf"StartTopUp_{phase}_round(\d+)")
    pattern_stop = re.compile(rf"StopTopUp_{phase}_round(\d+)")
    pending = {}
    for ts, msg in zip(head_df["unity_timestamp"], head_df["messages"]):
        if not isinstance(msg, str) or msg == "":
            continue
        m = pattern_start.match(msg)
        if m:
            pending[int(m.group(1))] = float(ts)
            continue
        m = pattern_stop.match(msg)
        if m:
            idx = int(m.group(1))
            if idx in pending:
                intervals.append((idx, pending[idx], float(ts)))
                del pending[idx]
    intervals.sort(key=lambda x: x[1])
    return intervals


def compute_gain_for_segment(head_t, head_yaw, eye_t, eye_yaw, t_start, t_stop):
    """compute one gain value for a time segment using paired peak matching."""
    h_mask = (head_t >= t_start) & (head_t <= t_stop)
    e_mask = (eye_t >= t_start) & (eye_t <= t_stop)
    if h_mask.sum() < 50 or e_mask.sum() < 50:
        return np.nan, np.nan, np.nan

    h_seg_t = head_t[h_mask]
    h_seg_y = head_yaw[h_mask] - np.nanmedian(head_yaw[h_mask])
    e_seg_t = eye_t[e_mask]
    e_seg_y = eye_yaw[e_mask] - np.nanmedian(eye_yaw[e_mask])

    h_amp, e_amp = get_amplitudes_paired(h_seg_t, h_seg_y, e_seg_t, e_seg_y)
    if np.isnan(h_amp) or h_amp <= 0 or np.isnan(e_amp):
        return np.nan, np.nan, np.nan
    return h_amp, e_amp, e_amp / h_amp


def compute_sliding_gains(head_t, head_yaw, eye_t, eye_yaw, t_start, t_stop):
    """compute gain values within sliding windows in [t_start, t_stop]."""
    rows = []
    for t_center in np.arange(t_start + WINDOW_SECONDS / 2,
                              t_stop - WINDOW_SECONDS / 2 + 0.01,
                              STEP_SECONDS):
        h_amp, e_amp, gain = compute_gain_for_segment(
            head_t, head_yaw, eye_t, eye_yaw,
            t_center - WINDOW_SECONDS / 2,
            t_center + WINDOW_SECONDS / 2)
        if not np.isnan(gain):
            rows.append({"time_s": t_center, "head_amp": h_amp,
                         "eye_amp": e_amp, "gain": gain})
    return rows


# load all three phases

print("\nloading data...")
phase_data = {}  # phase -> dict with head_df, gaze_df, head_t, head_yaw, eye_t, eye_yaw, t_offset

for phase in ["baseline", "adaptation", "aftereffect"]:
    head_df = load_head(f"{data_folder}/{phase}_head.csv")
    gaze_df = load_gaze(f"{data_folder}/{phase}_gaze.csv")

    head_t_raw = head_df["unity_timestamp"].to_numpy()
    eye_t_raw = gaze_df["unity_timestamp"].to_numpy()
    t0 = min(head_t_raw[0], eye_t_raw[0])

    head_t = head_t_raw - t0
    eye_t = eye_t_raw - t0 + GLOBAL_DELAY_S

    h_yaw = smooth(head_yaw_deg(head_df))
    e_yaw = smooth(eye_yaw_deg(gaze_df))

    phase_data[phase] = {
        "head_df": head_df,
        "head_t": head_t,
        "head_yaw": h_yaw,
        "eye_t": eye_t,
        "eye_yaw": e_yaw,
        "t0": t0,
        "duration": head_t[-1],
    }
    print(f"  {phase}: {phase_data[phase]['duration']:.1f}s "
          f"({phase_data[phase]['duration']/60:.1f} min)")


# baseline & aftereffect: per-topup gains AND sliding-window gains within topups

records_per_topup = []     # one row per top-up
records_sliding = []       # rows from sliding windows

for phase in ["baseline", "aftereffect"]:
    pd_ = phase_data[phase]
    intervals = parse_topup_intervals(pd_["head_df"], phase)
    print(f"\n{phase}: {len(intervals)} top-ups found")

    for round_idx, t_start_abs, t_stop_abs in intervals:
        # convert to relative times
        t_start = t_start_abs - pd_["t0"]
        t_stop = t_stop_abs - pd_["t0"]

        # one gain for the whole top-up
        h_amp, e_amp, gain = compute_gain_for_segment(
            pd_["head_t"], pd_["head_yaw"], pd_["eye_t"], pd_["eye_yaw"],
            t_start, t_stop)
        if not np.isnan(gain):
            records_per_topup.append({
                "phase": phase,
                "round": round_idx,
                "t_start_s": t_start,
                "t_mid_s": (t_start + t_stop) / 2,
                "duration_s": t_stop - t_start,
                "head_amp": h_amp,
                "eye_amp": e_amp,
                "gain": gain,
            })

        # sliding-window gains within the top-up
        sliding = compute_sliding_gains(
            pd_["head_t"], pd_["head_yaw"], pd_["eye_t"], pd_["eye_yaw"],
            t_start, t_stop)
        for row in sliding:
            row["phase"] = phase
            row["round"] = round_idx
            records_sliding.append(row)


# adaptation: only sliding window, no top-ups

pd_ = phase_data["adaptation"]
adaptation_sliding = compute_sliding_gains(
    pd_["head_t"], pd_["head_yaw"], pd_["eye_t"], pd_["eye_yaw"],
    0, pd_["duration"])
for row in adaptation_sliding:
    row["phase"] = "adaptation"
    row["round"] = -1
    records_sliding.append(row)
print(f"adaptation: {len(adaptation_sliding)} sliding-window points")


# also compute one mean gain over the whole adaptation phase (for the per-topup plot)
adaptation_mean_gain = np.mean([r["gain"] for r in adaptation_sliding if not np.isnan(r["gain"])])

df_topup = pd.DataFrame(records_per_topup)
df_sliding = pd.DataFrame(records_sliding)
df_topup.to_csv(f"{data_folder}/gain_per_topup.csv", index=False)
df_sliding.to_csv(f"{data_folder}/gain_sliding.csv", index=False)
print(f"\nsaved gain_per_topup.csv ({len(df_topup)} rows)")
print(f"saved gain_sliding.csv ({len(df_sliding)} rows)")


# print summary

print(f"\n*** summary (per-topup means) ***")
for phase in ["baseline", "aftereffect"]:
    sub = df_topup[df_topup["phase"] == phase]
    if not sub.empty:
        print(f"  {phase} top-ups: gain = {sub['gain'].mean():.3f} "
              f"(n={len(sub)}, range {sub['gain'].min():.3f} to {sub['gain'].max():.3f})")
print(f"  adaptation:        gain = {adaptation_mean_gain:.3f} "
      f"(sliding-window mean, n={len(adaptation_sliding)})")


# plot: three subplots side by side, one per phase

fig, axes = plt.subplots(1, 3, figsize=(16, 5), sharey=True,
                         gridspec_kw={"width_ratios": [1, 2, 1]})

phase_color = {"baseline": "darkblue", "adaptation": "purple", "aftereffect": "orange"}
phase_titles = {
    "baseline": "Baseline top-ups\n(world unmanipulated, mag = 1.0)",
    "adaptation": "Adaptation phase\n(world magnified by 1.2x)",
    "aftereffect": "Aftereffect top-ups\n(world still magnified by 1.2x)",
}

for ax, phase in zip(axes, ["baseline", "adaptation", "aftereffect"]):
    c = phase_color[phase]

    # for baseline & aftereffect: plot both sliding-window points (small dots)
    # AND per-top-up means (big squares with line)
    if phase in ["baseline", "aftereffect"]:
        sliding = df_sliding[df_sliding["phase"] == phase]
        topup = df_topup[df_topup["phase"] == phase]

        ax.scatter(sliding["time_s"] / 60.0, sliding["gain"],
                   color=c, alpha=0.3, s=20, edgecolor="none",
                   label="sliding window (30s)")
        ax.plot(topup["t_mid_s"] / 60.0, topup["gain"],
                "s-", color=c, markersize=10, lw=1.5,
                label="mean per top-up", markeredgecolor="black")

    else:  # adaptation: only sliding-window
        sliding = df_sliding[df_sliding["phase"] == "adaptation"]
        ax.scatter(sliding["time_s"] / 60.0, sliding["gain"],
                   color=c, alpha=0.4, s=25, edgecolor="none",
                   label="sliding window (30s)")
        # smoothed trend line
        if len(sliding) >= 5:
            window = min(11, len(sliding) // 2 * 2 + 1)
            if window >= 5:
                trend = savgol_filter(sliding["gain"].to_numpy(), window, 2)
                ax.plot(sliding["time_s"] / 60.0, trend,
                        "-", color=c, lw=2.5, label="smoothed trend")

    ax.axhline(1.0, color="black", ls=":", lw=1, alpha=0.7)
    ax.set_title(phase_titles[phase])
    ax.set_xlabel("time within phase (min)")
    ax.grid(alpha=0.3)
    ax.legend(loc="best", fontsize=8)

axes[0].set_ylabel("VOR gain (eye / head)")
fig.suptitle("VOR gain across the three phases of the experiment", fontsize=12)
plt.tight_layout(rect=[0, 0, 1, 0.96])
plt.savefig(f"{data_folder}/gain_across_phases.png", dpi=150, bbox_inches="tight")
print("\nsaved gain_across_phases.png")
plt.show()