import sys
import re
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.signal import savgol_filter
from scipy.spatial.transform import Rotation as R

print("script start")

# path to data folder
# aftereffect_head.csv, aftereffect_gaze.csv 
if len(sys.argv) > 1:
    data_folder = sys.argv[1]
else:
    data_folder = input("path to data folder: ").strip().strip('"')

# mode selection: 'overview' shows one random trial per mag level (all plots),
# 'single' lets the user pick a specific mag + trial(s).
if len(sys.argv) > 2:
    chosen_mag_input = sys.argv[2]
else:
    chosen_mag_input = input(
        "magnification level (e.g. 1.04), or 'overview' for one random trial per mag: "
    ).strip()

if chosen_mag_input.lower() == "overview":
    overview_mode = True
    chosen_mag = None
    trial_selection = None
else:
    overview_mode = False
    chosen_mag = float(chosen_mag_input)

    # which trials within that mag level to show. accepted formats:
    #   "all"        -> every trial with that mag
    #   "1"          -> only the first trial
    #   "1-4"        -> trials 1, 2, 3, 4 (inclusive range, 1-based)
    #   "2,5"        -> trials 2 and 5
    #   "1-3,7"      -> ranges and single numbers can be combined
    if len(sys.argv) > 3:
        trial_selection = sys.argv[3]
    else:
        trial_selection = input("which trials? (e.g. 'all', '1', '1-4', '2,5'): ").strip()

def parse_trial_selection(text, n_available):
    """
    convert user's trial selection string into a list of 0-based trial indices to plot.
    """
    text = text.strip().lower()
    if text == "all" or text == "":
        return list(range(n_available))

    indices = set()
    # split by comma ("1-4" or "2")
    for chunk in text.split(","):
        chunk = chunk.strip()
        if "-" in chunk:
            # range like "1-4"
            a, b = chunk.split("-")
            start = int(a)
            end = int(b)
            for i in range(start, end + 1):
                indices.add(i - 1)  # convert from 1-based to 0-based
        else:
            # single number like "2"
            indices.add(int(chunk) - 1)

    # only valid indices (we have 8 trials)
    indices = sorted(i for i in indices if 0 <= i < n_available)
    return indices


# Savitzky-Golay smoothing settings 
SAVGOL_WINDOW = 15 # 167 ms at 90 Hz sampling rate
SAVGOL_POLY = 3 


# helpers 

def load_head(path):
    """
    we pick
    only the columns we need by their position.
    """
    df = pd.read_csv(path)
    df_clean = pd.DataFrame({
        "unity_timestamp": df.iloc[:, 0],
        "eye_timestamp": df.iloc[:, 1],
        "px": df.iloc[:, 2], # position and rotation of the head in unity world coordinates
        "py": df.iloc[:, 3],
        "pz": df.iloc[:, 4],
        "qx": df.iloc[:, 5], # quaternions for head rotation, in unity world coordinates 
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
    returns the delay in seconds. positive value = eye is delayed relative to head
    """
    # build a common time axis. both signals get resampled onto this grid.
    t_min = max(head_t[0], eye_t[0])
    t_max = min(head_t[-1], eye_t[-1])
    n_samples = 500
    t_common = np.linspace(t_min, t_max, n_samples)

    # interpolate. NaNs would break interp(), so we ignore them.
    head_finite = np.isfinite(head_yaw)
    eye_finite = np.isfinite(eye_yaw)
    if head_finite.sum() < 5 or eye_finite.sum() < 5:
        return 0.0

    head_interp = np.interp(t_common, head_t[head_finite], head_yaw[head_finite])
    eye_interp = np.interp(t_common, eye_t[eye_finite], eye_yaw[eye_finite])

    # cross-correlate head against -eye (negated because of VOR direction)
    corr = np.correlate(head_interp, -eye_interp, mode="full")
    lags = np.arange(-n_samples + 1, n_samples)

    # constrain the search to physiologically reasonable delays (-300ms to +300ms).
    # this prevents picking a wrong peak that lies a full oscillation period away.
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
    h_yaw = head_yaw_deg(h)
    h_yaw = smooth(h_yaw)
    h_yaw = h_yaw - np.nanmedian(h_yaw) # subtract per-trial median to align all trials to 0

    eye_t = g["unity_timestamp"].to_numpy() - t_start
    e_yaw = eye_yaw_deg(g)
    e_yaw = smooth(e_yaw)
    e_yaw = e_yaw - np.nanmedian(e_yaw) # subtract per-trial median to align all trials to 0

    return {
        "trial": trial,
        "head_t": head_t,
        "head_yaw": h_yaw,
        "eye_t": eye_t,
        "eye_yaw": e_yaw,
    }


# collect all trials for the chosen magnification 

# step A: compute global median delay across all trials and phases.
# one global value is the cleanest correction.
print("\n-delay from all trials -")
all_delays = []
loaded_data = {}  # cache loaded files so we don't read them twice

for phase in ["baseline", "aftereffect"]:
    head_df = load_head(f"{data_folder}/{phase}_head.csv")
    gaze_df = load_gaze(f"{data_folder}/{phase}_gaze.csv")
    loaded_data[phase] = (head_df, gaze_df)

    trials = parse_trials(head_df, phase)
    for tr in trials:
        s = get_trial_signals(head_df, gaze_df, tr)
        if s is None:
            continue
        d = measure_delay(s["head_t"], s["head_yaw"], s["eye_t"], s["eye_yaw"])
        all_delays.append(d)

if all_delays:
    global_median_delay = np.median(all_delays)
    global_mean_delay = np.mean(all_delays)
    print(f"total trials measured: {len(all_delays)}")
    print(f"global median delay: {global_median_delay*1000:.1f} ms")
    print(f"global mean delay:   {global_mean_delay*1000:.1f} ms")
else:
    global_median_delay = 0.0

import random
# determine which mag levels to loop over
if overview_mode:
    # find all unique mag levels across both phases
    all_mags = set()
    for phase in ["baseline", "aftereffect"]:
        head_df, _ = loaded_data[phase]
        for tr in parse_trials(head_df, phase):
            all_mags.add(tr["magnification"])
    mag_levels_to_plot = sorted(all_mags)
    print(f"\noverview mode: will plot one random trial per mag level "
          f"({len(mag_levels_to_plot)} mag levels)")
else:
    mag_levels_to_plot = [chosen_mag]

# step B: collect signals for all mag levels we want to plot
EYE_WARMUP_MS = 200
signals_per_mag = {}  # mag_level -> {phase: [list of signals]}

for current_mag in mag_levels_to_plot:
    all_signals_this_mag = {}
    for phase in ["baseline", "aftereffect"]:
        print(f"\n*** mag = {current_mag}, {phase} ***")
        head_df, gaze_df = loaded_data[phase]

        trials = parse_trials(head_df, phase)
        trials_for_mag = [t for t in trials if abs(t["magnification"] - current_mag) < 0.001]
        print(f"found {len(trials_for_mag)} trials with mag = {current_mag}")

        if overview_mode:
            # pick one random trial from this mag level
            if len(trials_for_mag) > 0:
                random_idx = random.randint(0, len(trials_for_mag) - 1)
                selected_indices = [random_idx]
            else:
                selected_indices = []
        else:
            selected_indices = parse_trial_selection(trial_selection, len(trials_for_mag))
        trials_selected = [trials_for_mag[i] for i in selected_indices]
        print(f"selected {len(trials_selected)} trials: {[i+1 for i in selected_indices]}")

        signals = []
        for tr in trials_selected:
            s = get_trial_signals(head_df, gaze_df, tr)
            if s is not None:
                signals.append(s)

        # apply the global median delay, then clip eye signal to valid range
        for s in signals:
            s["eye_t"] = s["eye_t"] + global_median_delay
            t_start_valid = s["head_t"][0] + EYE_WARMUP_MS / 1000.0
            valid = (s["eye_t"] >= t_start_valid) & (s["eye_t"] <= s["head_t"][-1])
            s["eye_t"] = s["eye_t"][valid]
            s["eye_yaw"] = s["eye_yaw"][valid]

        all_signals_this_mag[phase] = signals

    signals_per_mag[current_mag] = all_signals_this_mag


# step C: plot 

phase_color = {"baseline": "steelblue", "aftereffect": "crimson"}

if overview_mode:
    # one big figure: 2 columns, one row per mag level
    n_mags = len(mag_levels_to_plot)
    n_cols = 2
    n_rows = (n_mags + n_cols - 1) // n_cols  # ceiling division

    fig, axes = plt.subplots(n_rows, n_cols, figsize=(12, 2.0 * n_rows),
                             sharey=True)
    axes_flat = axes.flatten() if n_mags > 1 else [axes]

    for i, current_mag in enumerate(mag_levels_to_plot):
        ax = axes_flat[i]
        for phase in ["baseline", "aftereffect"]:
            signals = signals_per_mag[current_mag][phase]
            c = phase_color[phase]
            for s in signals:
                ax.plot(s["head_t"], s["head_yaw"], color=c, lw=1.5, alpha=0.9)
                ax.plot(s["eye_t"], -s["eye_yaw"], color=c, lw=1.0, ls="--", alpha=0.7)

        ax.axhline(0, color="gray", lw=0.5)
        ax.set_title(f"magnification = {current_mag}", fontsize=10)
        ax.grid(alpha=0.3)
        if i % n_cols == 0:
            ax.set_ylabel("yaw (deg)")
        if i >= n_mags - n_cols:
            ax.set_xlabel("time within trial (s)")

    # one legend for the whole figure, on the first subplot
    axes_flat[0].plot([], [], color=phase_color["baseline"], lw=1.5, label="head (baseline)")
    axes_flat[0].plot([], [], color=phase_color["baseline"], lw=1.0, ls="--", label="-eye-in-head (baseline)")
    axes_flat[0].plot([], [], color=phase_color["aftereffect"], lw=1.5, label="head (aftereffect)")
    axes_flat[0].plot([], [], color=phase_color["aftereffect"], lw=1.0, ls="--", label="-eye-in-head (aftereffect)")
    axes_flat[0].legend(fontsize=7, loc="upper right")

    # hide any extra empty subplots
    for j in range(n_mags, len(axes_flat)):
        axes_flat[j].axis("off")

    fig.suptitle("Head yaw and (negated) eye-in-head yaw per magnification\n"
                 "Savitzky-Golay smoothed, per-trial median offset removed",
                 fontsize=11)
    plt.tight_layout(rect=[0, 0, 1, 0.96])
    plt.savefig(f"{data_folder}/plot_overview_all_mags.png",
                dpi=150, bbox_inches="tight")
    plt.show()

else:
    # single-mag mode: one figure with two subplots (baseline + aftereffect)
    current_mag = chosen_mag
    fig, axes = plt.subplots(1, 2, figsize=(13, 5), sharey=True)

    for ax, phase in zip(axes, ["baseline", "aftereffect"]):
        signals = signals_per_mag[current_mag][phase]
        if len(signals) == 0:
            ax.set_title(f"{phase} (no trials)")
            continue

        c = phase_color[phase]
        for s in signals:
            ax.plot(s["head_t"], s["head_yaw"], color=c, lw=1.2, alpha=0.6)
            ax.plot(s["eye_t"], -s["eye_yaw"], color=c, lw=1.0, ls="--", alpha=0.5)

        ax.plot([], [], color=c, lw=1.5, label=f"head ({phase})")
        ax.plot([], [], color=c, lw=1.0, ls="--", label=f"-eye-in-head ({phase})")

        ax.axhline(0, color="gray", lw=0.5)
        ax.set_title(f"{phase}  ({len(signals)} trials)")
        ax.set_xlabel("time within trial (s)")
        ax.grid(alpha=0.3)
        ax.legend(fontsize=9, loc="upper right")

    axes[0].set_ylabel("yaw (deg)")

    selection_for_filename = trial_selection.replace(",", "_").replace("-", "to")
    title_text = f"Trials '{trial_selection}' at magnification = {current_mag}"
    filename = f"plot_mag{current_mag}_trials_{selection_for_filename}.png"

    fig.suptitle(title_text + "\nSavitzky-Golay smoothed, per-trial median offset removed",
                 fontsize=11)
    plt.tight_layout(rect=[0, 0, 1, 0.95])
    plt.savefig(f"{data_folder}/{filename}", dpi=150, bbox_inches="tight")
    plt.show()