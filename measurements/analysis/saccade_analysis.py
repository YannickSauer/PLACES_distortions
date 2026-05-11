import sys
import re
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.signal import savgol_filter
from scipy.spatial.transform import Rotation as R

print("script start")

# path to data folder
if len(sys.argv) > 1:
    data_folder = sys.argv[1]
else:
    data_folder = input("path to data folder: ").strip().strip('"')


# eye-tracker delay (same global value as in the other scripts)
GLOBAL_DELAY_S = -0.077

# saccade detection settings
# a saccade is a fast eye movement above a velocity threshold.
# typical saccade velocity in humans: 30 to 700 deg/s.
# we use 80 deg/s as a conservative threshold so we don't pick up smooth pursuit
# or noise. minimum duration 20 ms to avoid micro-saccades, and minimum amplitude 2 deg to avoid noise.
SACCADE_VELOCITY_THRESHOLD = 80   # deg/s, samples above this count as saccade
SACCADE_MIN_DURATION_MS = 20      # minimum duration to count as saccade
SACCADE_MIN_AMPLITUDE_DEG = 2.0   # minimum amplitude to count as real saccade

# additional filter for the analysis: we focus on "target-directed" saccades.
# small saccades (<5 deg) are mostly corrective micro-saccades.
# very large ones (>30 deg) are likely tracker glitches or extreme head turns.
# the interesting range for the balloon game is 5-30 deg (looking from one
# balloon to another).
TARGET_SACCADE_MIN_DEG = 5.0
TARGET_SACCADE_MAX_DEG = 30.0


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


def detect_saccades(t, yaw):
    """detect saccades in an eye-yaw signal using a velocity threshold.
    returns a list of dicts: {t_start, t_end, amplitude_deg, peak_velocity}.
    we don't care about direction"""
    # compute angular velocity (deg/s), pad with 0 to keep the same length
    dt = np.diff(t)
    dyaw = np.diff(yaw)
    velocity = np.zeros_like(yaw)
    valid = (dt > 0) & np.isfinite(dyaw)
    velocity[1:][valid] = dyaw[valid] / dt[valid]

    # find samples that exceed the velocity threshold
    above = np.abs(velocity) > SACCADE_VELOCITY_THRESHOLD

    # group consecutive above-threshold samples into saccade events
    saccades = []
    in_saccade = False
    sacc_start_idx = None

    for i in range(len(above)):
        if above[i] and not in_saccade:
            in_saccade = True
            sacc_start_idx = i
        elif not above[i] and in_saccade:
            in_saccade = False
            sacc_end_idx = i - 1

            t_start = t[sacc_start_idx]
            t_end = t[sacc_end_idx]
            duration_ms = (t_end - t_start) * 1000

            # amplitude = how far the eye moved during the saccade
            yaw_start = yaw[sacc_start_idx]
            yaw_end = yaw[sacc_end_idx]
            amplitude = abs(yaw_end - yaw_start)

            # peak velocity within the saccade
            peak_vel = np.nanmax(np.abs(velocity[sacc_start_idx:sacc_end_idx + 1]))

            # filter out things that are too short or too small (noise)
            if duration_ms >= SACCADE_MIN_DURATION_MS and amplitude >= SACCADE_MIN_AMPLITUDE_DEG:
                saccades.append({
                    "t_start": t_start,
                    "t_end": t_end,
                    "duration_ms": duration_ms,
                    "amplitude_deg": amplitude,
                    "peak_velocity": peak_vel,
                })

    return saccades


def parse_topup_intervals(head_df, phase):
    """find StartTopUp/StopTopUp markers."""
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


# load all three phases and detect saccades

print("\nloading data and detecting saccades...")
phase_data = {}

for phase in ["baseline", "adaptation", "aftereffect"]:
    head_df = load_head(f"{data_folder}/{phase}_head.csv")
    gaze_df = load_gaze(f"{data_folder}/{phase}_gaze.csv")

    head_t_raw = head_df["unity_timestamp"].to_numpy()
    eye_t_raw = gaze_df["unity_timestamp"].to_numpy()
    t0 = min(head_t_raw[0], eye_t_raw[0])

    head_t = head_t_raw - t0
    eye_t = eye_t_raw - t0 + GLOBAL_DELAY_S
    head_yaw = head_yaw_deg(head_df)
    eye_yaw = eye_yaw_deg(gaze_df)

    # detect saccades on the raw eye signal (no smoothing!).
    # smoothing would flatten the velocity peaks and we'd miss saccades.
    saccades = detect_saccades(eye_t, eye_yaw)

    phase_data[phase] = {
        "head_df": head_df,
        "head_t": head_t,
        "head_yaw": head_yaw,
        "eye_t": eye_t,
        "eye_yaw": eye_yaw,
        "saccades": saccades,
        "t0": t0,
        "duration": head_t[-1],
    }
    print(f"  {phase}: {len(saccades)} saccades found in {head_t[-1]/60:.1f} min "
          f"({len(saccades)/(head_t[-1]/60):.1f} per minute)")


# for baseline & aftereffect: keep only saccades that fall within top-up intervals
# (no saccades from swimtest trials)

records = []  # list of all saccades with phase + interval info

for phase in ["baseline", "aftereffect"]:
    pd_ = phase_data[phase]
    intervals = parse_topup_intervals(pd_["head_df"], phase)

    for round_idx, t_start_abs, t_stop_abs in intervals:
        t_start = t_start_abs - pd_["t0"]
        t_stop = t_stop_abs - pd_["t0"]
        # saccades within this top-up
        for s in pd_["saccades"]:
            if t_start <= s["t_start"] <= t_stop:
                records.append({
                    "phase": phase,
                    "topup_round": round_idx,
                    "t_within_phase": s["t_start"],
                    "amplitude_deg": s["amplitude_deg"],
                    "duration_ms": s["duration_ms"],
                    "peak_velocity": s["peak_velocity"],
                })

# adaptation: use all saccades 
for s in phase_data["adaptation"]["saccades"]:
    records.append({
        "phase": "adaptation",
        "topup_round": -1,
        "t_within_phase": s["t_start"],
        "amplitude_deg": s["amplitude_deg"],
        "duration_ms": s["duration_ms"],
        "peak_velocity": s["peak_velocity"],
    })

df = pd.DataFrame(records)
df.to_csv(f"{data_folder}/saccades.csv", index=False)
print(f"\nsaved saccades.csv with {len(df)} saccades")


# print summary

print(f"\n*** saccade summary ***")
for phase in ["baseline", "adaptation", "aftereffect"]:
    sub = df[df["phase"] == phase]
    if not sub.empty:
        print(f"  {phase}: n = {len(sub):>4}, "
              f"mean amplitude = {sub['amplitude_deg'].mean():5.2f} deg, "
              f"median = {sub['amplitude_deg'].median():5.2f} deg")


# filter to target-directed saccades for the analysis
df_target = df[(df["amplitude_deg"] >= TARGET_SACCADE_MIN_DEG) &
               (df["amplitude_deg"] <= TARGET_SACCADE_MAX_DEG)].copy()

print(f"\n*** target-directed saccades only "
      f"({TARGET_SACCADE_MIN_DEG}-{TARGET_SACCADE_MAX_DEG} deg) ***")
for phase in ["baseline", "adaptation", "aftereffect"]:
    sub = df_target[df_target["phase"] == phase]
    if not sub.empty:
        print(f"  {phase}: n = {len(sub):>4}, "
              f"mean amplitude = {sub['amplitude_deg'].mean():5.2f} deg, "
              f"median = {sub['amplitude_deg'].median():5.2f} deg")


# plot: three subplots, one per phase, saccade amplitude over time

fig, axes = plt.subplots(1, 3, figsize=(16, 5), sharey=True,
                         gridspec_kw={"width_ratios": [1, 2, 1]})

phase_color = {"baseline": "darkblue", "adaptation": "purple", "aftereffect": "orange"}
phase_titles = {
    "baseline": "Baseline top-ups\n(world unmanipulated, mag = 1.0)",
    "adaptation": "Adaptation phase\n(world magnified by 1.2x)",
    "aftereffect": "Aftereffect top-ups\n(world still magnified by 1.2x)",
}

# get baseline mean for reference line
baseline_mean = df_target[df_target["phase"] == "baseline"]["amplitude_deg"].mean()

for ax, phase in zip(axes, ["baseline", "adaptation", "aftereffect"]):
    sub = df_target[df_target["phase"] == phase].sort_values("t_within_phase")
    if sub.empty:
        continue

    c = phase_color[phase]

    # individual saccades as transparent points
    ax.scatter(sub["t_within_phase"] / 60.0, sub["amplitude_deg"],
               color=c, alpha=0.3, s=15, edgecolor="none",
               label=f"individual saccades (n={len(sub)})")

    # smoothed trend line over time (moving average over consecutive saccades)
    if len(sub) >= 20:
        # N = max(20, len(sub) // 30) #(about 30 points in the moving average window, but at least 20)
        N = 20 # same window for all phases for easier comparison
        times = sub["t_within_phase"].to_numpy() / 60.0
        amps = sub["amplitude_deg"].to_numpy()

        smooth_times, smooth_amps = [], []
        for i in range(0, len(sub) - N + 1, max(1, N // 2)):
            smooth_times.append(np.mean(times[i:i + N]))
            smooth_amps.append(np.mean(amps[i:i + N]))
        ax.plot(smooth_times, smooth_amps, "-", color=c, lw=2.5,
                label=f"moving average (over {N} saccades)")

    # phase mean and baseline reference
    mean_amp = sub["amplitude_deg"].mean()
    ax.axhline(mean_amp, color=c, ls="--", lw=1.5, alpha=0.8,
               label=f"phase mean ({mean_amp:.2f} deg)")
    if phase != "baseline":
        ax.axhline(baseline_mean, color="gray", ls=":", lw=1.5,
                   label=f"baseline mean ({baseline_mean:.2f} deg)")

    ax.set_title(phase_titles[phase])
    ax.set_xlabel("time within phase (min)")
    ax.grid(alpha=0.3)
    ax.legend(loc="upper right", fontsize=8)

axes[0].set_ylabel("saccade amplitude (deg)")
axes[0].set_ylim(TARGET_SACCADE_MIN_DEG - 1, TARGET_SACCADE_MAX_DEG + 1)
fig.suptitle(f"Target-directed saccade amplitudes "
             f"({TARGET_SACCADE_MIN_DEG}-{TARGET_SACCADE_MAX_DEG} deg) "
             f"across the three phases\n"
             f"saccades are larger when the world is magnified",
             fontsize=12)
plt.tight_layout(rect=[0, 0, 1, 0.94])
plt.savefig(f"{data_folder}/saccade_amplitudes.png", dpi=150, bbox_inches="tight")
print("\nsaved saccade_amplitudes.png")
plt.show()