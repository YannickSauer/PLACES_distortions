import sys
import re
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.signal import savgol_filter, find_peaks
from scipy.spatial.transform import Rotation as R

print("script start")

# path to data folder. expects files: baseline_head.csv, baseline_gaze.csv,
# aftereffect_head.csv, aftereffect_gaze.csv inside this folder
if len(sys.argv) > 1:
    data_folder = sys.argv[1]
else:
    data_folder = input("path to data folder: ").strip().strip('"')


# Savitzky-Golay smoothing settings 
SAVGOL_WINDOW = 15  # 167 ms at 90 Hz sampling rate
SAVGOL_POLY = 3

# peak detection settings
# head moves ~10 deg amplitude at ~1 Hz, so peaks are well separated
PEAK_MIN_DISTANCE_S = 0.3      # minimum 300 ms between peaks
PEAK_MIN_PROMINENCE_DEG = 3.0  # ignore tiny wiggles below 3 deg


# helpers 

def load_head(path):
    df = pd.read_csv(path)
    df = df.loc[:, ~df.columns.duplicated()]
    df = df.rename(columns={
        "Main Camera_rotation.x": "qx",
        "Main Camera_rotation.y": "qy",
        "Main Camera_rotation.z": "qz",
        "Main Camera_rotation.w": "qw",
    })
    return df


def load_gaze(path):
    df = pd.read_csv(path)
    df = df.loc[:, ~df.columns.str.match(r"^\s*$")]
    return df


def parse_trials(head_df, phase):
    trials = []
    pattern = re.compile(
        r"StartTrial_(\w+?)_t(\d+)_mag([\d.]+)_rad(\d+)"
    )
    pending = {}

    for ts, msg in zip(head_df["unity_timestamp"], head_df["messages"]):
        if not isinstance(msg, str) or msg == "":
            continue
        match = pattern.match(msg)
        if match and match.group(1) == phase:
            trial_idx = int(match.group(2))
            mag = float(match.group(3))
            rad = int(match.group(4))
            pending[trial_idx] = {
                "trial_idx": trial_idx,
                "magnification": mag,
                "radial": rad,
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
    """
    measure the time delay between head and eye signals using cross-correlation.
    returns the delay in seconds.
    """
    t_min = max(head_t[0], eye_t[0])
    t_max = min(head_t[-1], eye_t[-1])
    n_samples = 500
    t_common = np.linspace(t_min, t_max, n_samples)

    head_finite = np.isfinite(head_yaw)
    eye_finite = np.isfinite(eye_yaw)
    if head_finite.sum() < 5 or eye_finite.sum() < 5:
        return 0.0

    head_interp = np.interp(t_common, head_t[head_finite], head_yaw[head_finite])
    eye_interp = np.interp(t_common, eye_t[eye_finite], eye_yaw[eye_finite])

    corr = np.correlate(head_interp, -eye_interp, mode="full")
    lags = np.arange(-n_samples + 1, n_samples)

    dt = t_common[1] - t_common[0]
    max_delay_seconds = 0.3
    max_lag = int(max_delay_seconds / dt)
    valid = (lags >= -max_lag) & (lags <= max_lag)

    best_lag = lags[valid][np.argmax(corr[valid])]
    delay_seconds = best_lag * dt

    return delay_seconds


def get_trial_signals(head_df, gaze_df, trial):
    t_start = trial["t_start"]
    t_stop = trial["t_stop"]

    head_mask = (head_df["unity_timestamp"] >= t_start) & (head_df["unity_timestamp"] <= t_stop)
    h = head_df[head_mask]
    gaze_mask = (gaze_df["unity_timestamp"] >= t_start) & (gaze_df["unity_timestamp"] <= t_stop)
    g = gaze_df[gaze_mask]

    if len(h) < 30 or len(g) < 30:
        return None

    head_t = h["unity_timestamp"].to_numpy() - t_start
    h_yaw_raw = head_yaw_deg(h)
    h_yaw_raw = smooth(h_yaw_raw)  # smoothed, but NOT median-subtracted
    h_yaw = h_yaw_raw - np.nanmedian(h_yaw_raw)  # median-subtracted for plotting

    eye_t = g["unity_timestamp"].to_numpy() - t_start
    e_yaw_raw = eye_yaw_deg(g)
    e_yaw_raw = smooth(e_yaw_raw)  # smoothed, but NOT median-subtracted
    e_yaw = e_yaw_raw - np.nanmedian(e_yaw_raw)  # median-subtracted for plotting

    return {
        "trial": trial,
        "head_t": head_t,
        "head_yaw": h_yaw,             # median-subtracted (for plots, peak detection)
        "head_yaw_raw": h_yaw_raw,     # NOT median-subtracted (for lstsq fit)
        "eye_t": eye_t,
        "eye_yaw": e_yaw,
        "eye_yaw_raw": e_yaw_raw,
    }


# amplitude extraction 

def get_amplitude(t, x):
    """
    detect all peaks (maxima and minima) in the signal and return the mean
    of their absolute values.
    one number per trial = one point in the scatter plot.
    """
    if len(t) < 5 or np.all(np.isnan(x)):
        return []

    # estimate sampling rate to convert min-distance from seconds to samples
    fs = 1.0 / np.median(np.diff(t))
    distance = max(1, int(PEAK_MIN_DISTANCE_S * fs))

    # remove NaNs for peak detection
    finite = np.isfinite(x)
    if finite.sum() < 5:
        return []
    xs = x[finite]
    ts = t[finite]

    # positive peaks (maxima) and negative peaks (minima, found by flipping sign)
    pos_peaks, _ = find_peaks(xs, distance=distance, prominence=PEAK_MIN_PROMINENCE_DEG)
    neg_peaks, _ = find_peaks(-xs, distance=distance, prominence=PEAK_MIN_PROMINENCE_DEG)

    # combine into one sorted list of time, signed amplitude pairs
    peaks = []
    for i in pos_peaks:
        peaks.append((ts[i], xs[i])) # positive peak: keep sign
    for i in neg_peaks:
        peaks.append((ts[i], x[finite][i])) # negative peak: still negative value
    peaks.sort(key=lambda p: p[0]) # sort by time
    return peaks

def get_amplitudes_paired(head_t, head_yaw, eye_t, eye_yaw, max_pair_distance_s=0.3):
    """ match head and eye peaks pairwise by time, return mean head amp, 
    mean eye amp, and per-pair gain. for each head peak, find the closest eye peak within 
    max_pair_distance_s, and form a pair. unmatched head peaks are ignored. 
    this guarantees head and eye amp are based on the same set of oscillation cycles.
    """
    head_peaks = get_amplitude(head_t, head_yaw)
    eye_peaks = get_amplitude(eye_t, eye_yaw)

    if not head_peaks or not eye_peaks:
        return np.nan, np.nan
    
    head_amps = []
    eye_amps = []

    # for each head peak, find the closest eye peak with opposite sign (because of VOR) within max_pair_distance_s
    for h_t, h_amp in head_peaks:
        same_sign_eye = [(et, ea) for (et, ea) in eye_peaks if np.sign(ea) == -np.sign(h_amp)]
        if not same_sign_eye:
            continue

        # closest in time
        closest = min(same_sign_eye, key=lambda p: abs(p[0] - h_t))
        if abs(closest[0] - h_t) > max_pair_distance_s:
            continue

        head_amps.append(abs(h_amp))
        eye_amps.append(abs(closest[1]))

    if not head_amps:
        return np.nan, np.nan
    
    return float(np.mean(head_amps)), float(np.mean(eye_amps))

def get_gain_lstsq(head_t, head_yaw, eye_t, eye_yaw, subtract_median=False):
    """compute gain using a least-squares fit over all samples (not just peaks).
    model: -y_eye(t) = g * y_head(t) + s
    where g is the gain (slope) and s is the spatial offset (intercept).
    we fit g and s simultaneously so both are optimal.

    if subtract_median=True: medians are removed before the fit (legacy behaviour).
    if subtract_median=False: s is estimated entirely by the fit (cleaner).
    """
    if len(head_t) < 5 or len(eye_t) < 5:
        return np.nan, np.nan

    h_yaw = head_yaw.copy()
    e_yaw = eye_yaw.copy()
    if subtract_median:
        h_yaw = h_yaw - np.nanmedian(h_yaw)
        e_yaw = e_yaw - np.nanmedian(e_yaw)

    # safety: make sure arrays have matching lengths (can drift if signals
    # were clipped after smoothing)
    n_head = min(len(head_t), len(h_yaw))
    n_eye = min(len(eye_t), len(e_yaw))
    head_t = head_t[:n_head]
    h_yaw = h_yaw[:n_head]
    eye_t = eye_t[:n_eye]
    e_yaw = e_yaw[:n_eye]

    head_finite = np.isfinite(h_yaw)
    eye_finite = np.isfinite(e_yaw)
    if head_finite.sum() < 5 or eye_finite.sum() < 5:
        return np.nan, np.nan

    valid = (head_t >= eye_t[eye_finite][0]) & (head_t <= eye_t[eye_finite][-1])
    if valid.sum() < 5:
        return np.nan, np.nan

    h_t_valid = head_t[valid]
    h_yaw_valid = h_yaw[valid]
    e_yaw_on_head = np.interp(h_t_valid, eye_t[eye_finite], e_yaw[eye_finite])

    # filter to samples where head movement is big enough to be reliable.
    # we look at "deviation from median" so this filter still makes sense
    # whether or not we subtracted the median earlier.
    h_centered = h_yaw_valid - np.nanmedian(h_yaw_valid)
    big_enough = np.abs(h_centered) > 1.0
    if big_enough.sum() < 5:
        return np.nan, np.nan

    h = h_yaw_valid[big_enough]
    e = -e_yaw_on_head[big_enough]

    A = np.vstack([h, np.ones_like(h)]).T
    result, *_ = np.linalg.lstsq(A, e, rcond=None)
    g, s = result[0], result[1]

    # compute R² (coefficient of determination) to quantify fit quality.
    # R² = 1 - SS_residual / SS_total
    # close to 1 = points lie nicely on the line, close to 0 = fit is poor
    e_predicted = g * h + s
    ss_residual = np.sum((e - e_predicted) ** 2)
    ss_total = np.sum((e - np.mean(e)) ** 2)
    r_squared = 1.0 - (ss_residual / ss_total) if ss_total > 0 else np.nan

    return float(g), float(s), float(r_squared)
           


# load data and compute amplitudes per trial 

amplitude_rows = []  # list of dicts, one per trial

# step A: load ALL trials from BOTH phases first, so we can compute one global delay
print("\n--- loading all trials ---")
all_trial_signals = []  # list of (phase, signal_dict) tuples

for phase in ["baseline", "aftereffect"]:
    head_df = load_head(f"{data_folder}/{phase}_head.csv")
    gaze_df = load_gaze(f"{data_folder}/{phase}_gaze.csv")
    trials = parse_trials(head_df, phase)
    print(f"{phase}: found {len(trials)} trials")
    for tr in trials:
        s = get_trial_signals(head_df, gaze_df, tr)
        if s is not None:
            all_trial_signals.append((phase, s))

# step B: measure delay for every trial, then take ONE global median across all
# trials and both phases. the eye-tracker delay is a hardware property and
# doesn't depend on magnification or phase, so a single global value is cleanest.
print("\n--- computing global delay ---")
all_delays = []
for phase, s in all_trial_signals:
    d = measure_delay(s["head_t"], s["head_yaw"], s["eye_t"], s["eye_yaw"])
    all_delays.append(d)

global_median_delay = np.median(all_delays)
global_mean_delay = np.mean(all_delays)
print(f"global median delay: {global_median_delay*1000:.1f} ms (from {len(all_delays)} trials)")
print(f"global mean delay:   {global_mean_delay*1000:.1f} ms")

# step C: apply that one global delay to every trial, clip to valid time range,
# then compute amplitudes
EYE_WARMUP_MS = 200
for phase, s in all_trial_signals:
    s["eye_t"] = s["eye_t"] + global_median_delay

    # clip eye signal to a valid time window. skip the first 200 ms because
    # the eye-tracker often produces artifacts right after trial start.
    t_start_valid = s["head_t"][0] + EYE_WARMUP_MS / 1000.0
    valid = (s["eye_t"] >= t_start_valid) & (s["eye_t"] <= s["head_t"][-1])
    s["eye_t"] = s["eye_t"][valid]
    s["eye_yaw"] = s["eye_yaw"][valid]
    s["eye_yaw_raw"] = s["eye_yaw_raw"][valid]  # apply same clipping to raw

    # method 1 (paired peak matching): one head-amp and one eye-amp per trial
    head_amp, eye_amp = get_amplitudes_paired(s["head_t"], s["head_yaw"], s["eye_t"], s["eye_yaw"])

    # method 2 (least-squares): fit -y_eye = g * y_head + s over all samples.
    # we use the raw signals (without median-subtraction) so that the offset s
    # is fully estimated by the fit. R² tells us how well the fit matches.
    gain_lstsq, offset_lstsq, r_squared_lstsq = get_gain_lstsq(
        s["head_t"], s["head_yaw_raw"], s["eye_t"], s["eye_yaw_raw"],
        subtract_median=False)

    amplitude_rows.append({
        "phase": phase,
        "trial": s["trial"]["trial_idx"],
        "magnification": s["trial"]["magnification"],
        "head_amp": head_amp,
        "eye_amp": eye_amp,
        "gain_paired": eye_amp / head_amp if head_amp and head_amp > 0 else np.nan,
        "gain_lstsq": gain_lstsq,
        "offset_lstsq": offset_lstsq,
        "r_squared": r_squared_lstsq,
    })

# put everything in a dataframe and save it for later use
amp_df = pd.DataFrame(amplitude_rows)
amp_df.to_csv(f"{data_folder}/amplitudes.csv", index=False)
print(f"\nsaved {data_folder}/amplitudes.csv with {len(amp_df)} rows")

# Plot 1: example trial showing the lstsq fit on sample-pairs.
# change these three lines to pick a different example trial:
EXAMPLE_PHASE = "baseline"       
EXAMPLE_MAG = 1                
EXAMPLE_TRIAL_NUMBER = 2          

# collect all trials matching phase + mag (in order)
matching_trials = [sig for phase, sig in all_trial_signals
                   if phase == EXAMPLE_PHASE
                   and abs(sig["trial"]["magnification"] - EXAMPLE_MAG) < 0.001]

example_signal = None
if len(matching_trials) == 0:
    print(f"\nWARNING: no trials found for {EXAMPLE_PHASE}, mag={EXAMPLE_MAG}")
elif EXAMPLE_TRIAL_NUMBER < 1 or EXAMPLE_TRIAL_NUMBER > len(matching_trials):
    print(f"\nWARNING: only {len(matching_trials)} trials available "
          f"for {EXAMPLE_PHASE}, mag={EXAMPLE_MAG}. "
          f"Pick EXAMPLE_TRIAL_NUMBER between 1 and {len(matching_trials)}.")
else:
    example_signal = matching_trials[EXAMPLE_TRIAL_NUMBER - 1]
    print(f"\nPlot 1: using trial #{EXAMPLE_TRIAL_NUMBER} of {len(matching_trials)} "
          f"matching trials ({EXAMPLE_PHASE}, mag={EXAMPLE_MAG})")

if example_signal is not None:
    # compute the fit for this example using raw signals (no median subtraction)
    g_fit, s_fit, r2_fit = get_gain_lstsq(
    example_signal["head_t"], example_signal["head_yaw_raw"],
    example_signal["eye_t"], example_signal["eye_yaw_raw"],
    subtract_median=False)

    # build the same sample-pair scatter the fit uses
    h_t = example_signal["head_t"]
    h_yaw = example_signal["head_yaw_raw"]
    e_t = example_signal["eye_t"]
    e_yaw = example_signal["eye_yaw_raw"]

    # safety: arrays may have drifted in length due to earlier clipping
    n_head = min(len(h_t), len(h_yaw))
    n_eye = min(len(e_t), len(e_yaw))
    h_t = h_t[:n_head]
    h_yaw = h_yaw[:n_head]
    e_t = e_t[:n_eye]
    e_yaw = e_yaw[:n_eye]

    eye_finite = np.isfinite(e_yaw)
    valid_h = (h_t >= e_t[eye_finite][0]) & (h_t <= e_t[eye_finite][-1])
    h_t_v = h_t[valid_h]
    h_yaw_v = h_yaw[valid_h]
    e_on_h = np.interp(h_t_v, e_t[eye_finite], e_yaw[eye_finite])

    # we plot ALL points (no filter on |h| > 1), but the fit uses only the
    # filtered ones - the fit line is identical either way
    fig_ex, ax_ex = plt.subplots(figsize=(7, 7))
    ax_ex.scatter(h_yaw_v, -e_on_h, alpha=0.5, s=25, color="purple",
                  edgecolor="none", label=f"sample-pairs (n={len(h_yaw_v)})")

    # the fit line: y = g_fit * x + s_fit
    x_range = np.array([h_yaw_v.min() - 2, h_yaw_v.max() + 2])
    y_line = g_fit * x_range + s_fit
    ax_ex.plot(x_range, y_line, color="red", lw=2.5,
               label=f"lstsq fit: y = {g_fit:.3f} · x + {s_fit:.2f}")

    # reference: diagonal y=x (perfect VOR)
    diag_range = np.array([min(x_range[0], -25), max(x_range[1], 25)])
    ax_ex.plot(diag_range, diag_range, color="black", ls=":", lw=1,
               label="perfect VOR (gain = 1, offset = 0)")

    ax_ex.axhline(0, color="gray", lw=0.5)
    ax_ex.axvline(0, color="gray", lw=0.5)
    ax_ex.set_xlabel("head yaw (deg)")
    ax_ex.set_ylabel("-eye yaw (deg)")
    ax_ex.set_title(f"Plot 1: example trial — sample-pairs and lstsq fit\n"
                f"({EXAMPLE_PHASE}, mag = {example_signal['trial']['magnification']}, "
                f"trial #{EXAMPLE_TRIAL_NUMBER} of {len(matching_trials)})\n"
                f"gain = {g_fit:.3f}, offset = {s_fit:.2f}, R² = {r2_fit:.3f}")
    ax_ex.legend(loc="upper left", fontsize=9)
    ax_ex.grid(alpha=0.3)
    ax_ex.set_aspect("equal", adjustable="box")
    plt.tight_layout()
    plt.savefig(f"{data_folder}/plot1_lstsq_example.png", dpi=150, bbox_inches="tight")
    print(f"saved {data_folder}/plot1_lstsq_example.png")
    plt.show()

# print comparison of the two gain methods
print(f"\n*** gain comparison (paired-peaks vs least-squares) ***")
print(f"{'phase':>12}  {'gain_paired':>12}  {'gain_lstsq':>12}  {'difference':>12}")
print("-" * 55)
for phase in ["baseline", "aftereffect"]:
    sub = amp_df[amp_df["phase"] == phase]
    g_paired = sub["gain_paired"].mean()
    g_lstsq = sub["gain_lstsq"].mean()
    diff = g_lstsq - g_paired
    print(f"{phase:>12}  {g_paired:>12.3f}  {g_lstsq:>12.3f}  {diff:>+12.3f}")

# plot 2: gain vs magnification
# we filter out trials with poor fit quality (low R²) — these are usually
# trials where the participant didn't fixate properly, so the lstsq fit
# can't find a meaningful relationship between head and eye.
R_SQUARED_THRESHOLD = 0.9   # trials below this are excluded, can be adjusted

# report how many trials are excluded
n_total = len(amp_df.dropna(subset=["gain_lstsq", "r_squared"]))
n_excluded = (amp_df["r_squared"] < R_SQUARED_THRESHOLD).sum()
print(f"\nfiltering trials with R² < {R_SQUARED_THRESHOLD}: "
      f"excluding {n_excluded} of {n_total} trials")

fig, ax = plt.subplots(figsize=(9, 6))

phase_color = {"baseline": "darkblue", "aftereffect": "orange"}

for phase in ["baseline", "aftereffect"]:
    sub = amp_df[amp_df["phase"] == phase].dropna(subset=["gain_lstsq"])
    sub = sub[sub["r_squared"] >= R_SQUARED_THRESHOLD]
    c = phase_color[phase]

    # compute mean gain for mag<1 and mag>1 subgroups
    mean_low = sub[sub["magnification"] < 1.0]["gain_lstsq"].mean()
    mean_high = sub[sub["magnification"] > 1.0]["gain_lstsq"].mean()

    # individual trials as transparent dots
    ax.scatter(sub["magnification"], sub["gain_lstsq"],
               color=c, alpha=0.4, s=40, edgecolor="none",
               label=f"individual trials ({phase})")

    # mean per mag level, connected with a line.
    # include mag<1 and mag>1 means in the legend label for quick reference.
    mags = sorted(sub["magnification"].unique())
    means = [sub[sub["magnification"] == mag]["gain_lstsq"].mean() for mag in mags]
    ax.plot(mags, means, color=c, lw=2,
            marker="o", markersize=8,
            label=f"mean per mag ({phase}) - "
                  f"mag<1: {mean_low:.3f}, mag>1: {mean_high:.3f}")

# reference lines
ax.axhline(1.0, color="black", ls=":", lw=1, label="gain = 1 (perfect VOR)")
ax.axvline(1.0, color="gray", ls=":", lw=0.5)

ax.set_xlabel("magnification")
ax.set_ylabel("VOR gain (lstsq)")
ax.set_title("VOR gain (least-squares method) vs magnification level\n"
             "baseline (blue) vs aftereffect (orange)",
             fontsize=11)
ax.grid(alpha=0.3)
ax.legend(loc="best", fontsize=9)

plt.tight_layout()
plt.savefig(f"{data_folder}/plot2_gain_vs_mag.png", dpi=150, bbox_inches="tight")
plt.show()
