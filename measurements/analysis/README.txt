# Analysis scripts

Python scripts for analysing eye tracking and head tracking data
from the swimtest / balloon game experiment.

## Scripts

- `timeseries_all_mag.py` — plot head and eye yaw over time per trial
- `swimtest_amplitudes.py` — scatter plot of eye vs head amplitude per trial,
  with regression lines for mag<1 and mag>1
- `delay_histogram.py` — histogram of cross-correlation delays across all trials
- `delay_vs_mag.py` — delay as a function of magnification level
  adaptation phase, and aftereffect top-ups
- `saccade_analysis.py` — saccade detection and amplitude comparison across phases

## Usage

Each script takes the data folder as a command-line argument:
python script_name.py path/to/data/folder


## Settings (consistent across scripts)

- Savitzky-Golay smoothing: window=15, polyorder=3 (≈167 ms at 90 Hz)
- Cross-correlation delay: global median = -77 ms (used to align eye and head)
- Sampling rate: 90 Hz (Vive Pro Eye + SRanipal SDK)
- Amplitude calculation: least square method