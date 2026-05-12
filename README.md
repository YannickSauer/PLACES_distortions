# Visuo-Vestibular Adaptation Experiment (PLACEs)

A VR experiment in Unity that measures perceptual adaptation to magnified distorted vision.
Participants play a balloon-shooting game while wearing a VR Headset (simulated in VR), then judge the perceived stability of random-dot patterns during rhythmic head movements.

---

## Table of contents

1. [Requirements]
2. [Installation]
3. [Project structure]
4. [Running the experiment]
5. [Inspector settings reference]
6. [Output data]

---

## Requirements

### Hardware

- **VR Headset**: HTC Vive Pro Eye 
- **Controllers**: 1x Vive controller (for trigger / trackpad input)
- **PC**: VR-ready

### Software

- **Unity**: `Unity 2022.3.62f3`
- **SteamVR**: installed and configured for the Vive
- **SRanipal Runtime**: required for eye tracking on the Vive Pro Eye
- **Vive SRanipal SDK** 

### Unity packages used 

- XR Plugin Management
- OpenXR Plugin
- XR Interaction Toolkit
- TextMeshPro
- Input System


## Installation

1. **Clone the repository**

2. **Install SteamVR and the SRanipal runtime** 
   Confirm in the SRanipal status icon (Windows tray) that the eye tracker is detected and calibrated.

3. **Open the project in Unity Hub**
   - Click **Add → Add project from disk**, select the cloned folder.
   - Use the Unity version specified above. 

4. **Confirm settings:**
   - `Edit → Project Settings → Editor → Asset Serialization Mode` → Force Text 
   - `Edit → Project Settings → XR Plugin Management` → ensure OpenXR is checked for the standalone target. Render Mode: Multi-pass

5. **Open the main scene:**
   `Assets/Scenes/Adaptation.unity`

---

## Project structure

```
Assets/
├── Scenes/
│   ├── Adaptation.unity               # Balloon-shooting 
│   └── SwimTest.unity                 # Head-Movement stability test 
├── Scripts/
│   ├── ExperimentManager.cs           # Orchestrates the full experiment flow
│   ├── AdaptationTask.cs              # Balloon game logic
│   ├── SwimTest.cs                    # Head-Movement stability test
│   ├── Distortions.cs                 # Magnification / radial distortion shader 
│   └── ...
controller
│   ├── DotManager.cs                  # Random-dot generation & projection
│   ├── EyeTrackingToolbox.cs          # Eye-tracking interface
│   ├── ViveEyeTracker.cs              # Vive-specific implementation
│   └── ...
├── Instructions/                       # On-screen instruction texts (.txt)
└── Audio/                              # Voice-over clips for instructions & sounds
measurements/                           # Output folder
```

---

## Running the experiment

### Before the participant arrives

1. **Power on the Vive headset and controllers**, confirm tracking in SteamVR.
2. **Run SRanipal Eye Calibration** (right-click the SRanipal tray icon → Eye Calibration).
   This must be done **with the headset on the participant's head**, so do this once they're seated.
3. **Open the main scene** in Unity
4. **Select the `ExperimentManager` GameObject** in the Hierarchy.
5. **Set the `Subject ID`** in the Inspector ( `1`, `2`, `3` …).
   This determines the output folder name: `measurements/<subjectID>_<timestamp>/`.
6. **Verify other settings match the protocol** — see [Inspector settings reference] below.

### Starting the experiment

The project uses two keyboard shortcuts in the Unity Editor (Play Mode):

| Key       | Action                                                                  |
| --------- | ----------------------------------------------------------------------- |
| **`T`**   | Start **Training**: balloon-game tutorial → head-movement tutorial → full experiment |
| **`Space`** | Start the **full experiment** directly (skips training)              |

> **Standard protocol: press `T`** so the participant goes through both training phases first.
> Use `Space` only when re-running on an experienced pilot, or when resuming after a tech issue.

### What the participant goes through

1. **Welcome** — instruction text on a wall in VR (when pressing `T` or `Space`)
2. **Training** — practice the adaptation task and learn the rhythmic head-shake timing
4. **Pre-baseline** — adaptation scene *without* distortions
5. **Baseline blocks** — head-movement stability judgments under various distortion levels, with top-ups in between (no distortions) 
6. **Adaptation phase** — balloon game *with* distortions enabled
7. **Aftereffect blocks** — head-movement stability judgments under various distortion levels, with top-ups in between (distortions)
8. **End screen** 

Total duration: approx. **7 minutes**.

### Controls (Vive controller, during participant tasks)

| Input                  | Action                                                |
| ---------------------- | ----------------------------------------------------- |
| Trackpad               | Advance instruction screen / "stable" judgement (test) |
| Trigger                | Shoot balloon (adaptation) / "unstable" judgment (test) |

## Inspector settings reference

> **All Inspector values are saved in the scene file and are committed to git.**
> When you clone the repo, you should see exactly the values listed below.
> **If anything is missing, check that you opened the right scene and that all packages imported correctly.**

### `ExperimentManager` GameObject

| Field                          | Value / notes                                                 |
| ------------------------------ | ------------------------------------------------------------- |
| **Subject ID**                 | Set per participant before starting                           |
| **Adaptation Magnification**   | `1.2` — magnification factor during adaptation                |
| **Adaptation Radial**          | `0`                                                           |
| **Adaptation Phase Settings**  |                                                               |
| └─ Pre-baseline Duration       | `45`                                                          |
| └─ Adaptation Duration         | `1080`                                                        |
| └─ Top-up Duration             | `30`                                                          |
| └─ Scene Name                  | `Adaptation`                                                  |
| **Aftereffect Settings**       |                                                               |
| └─ Magnification Stimulus Levels | `[0.8, 0.85, 0.9, 0.95, 1.0, 1.04, 1.08, 1.12, 1.16, 1.2]`  |
|    └─ `in total` 1x0.8, 1x0.85, 4x0.9, 4x0.95, 4x1, 4x1.04, 4x1.08, 4x1.12, 1x1.16, 1x1.2
| └─ Radial Stimulus Levels      | `[1]` (Element 0 = 0)                                         |
| └─ Sampling Frequency          | `2` (trials per stimulus level)                               |
| └─ Top-up Frequency            | `5`                                                           |
| └─ Scene Name                  | `SwimTest`                                                    |
| **Instruction Audio clips**    | Drag from `Assets/Audio/` - should already be assigned        |
| **Output Directory**           | Set automatically at runtime - leave as default               |

### `Distortions` component (on Main Camera)

| Field            | Value                                          |
| ---------------- | ---------------------------------------------- |
| Active           | `true` (toggled by ExperimentManager)          |
| Magn             | Set by ExperimentManager at runtime            |
| Radial           | Set by ExperimentManager at runtime            |
| Multiplier       | `1.0`                                          |
| Displacement X/Y | `0`                                            |

### `DotManager` component (on Main Camera)

| Field           | Value                                                 |
| --------------- | ----------------------------------------------------- |
| Size            | `0.7`                                                 |
| Dot Color       | Red                                                   |
| Alpha           | Set by code; leave at `1` in editor                   |
| Mesh            | Circle16                                              |
| Dotspace        | `sphere`                                              |
| Background Color | Gray                                                 |

### `SwimTest` component

| Field                    | Value                                |
| ------------------------ | ------------------------------------ |
| Wall Distance            | `4`                                  |
| Head Rotation Threshold  | `10` (degrees)                       |
| Timing Threshold         | `0.25` (seconds)                     |
| Center Threshold         | `5.0`                                |
| BPM                      | `94`    (metronome frequency)        |
| Min Good Trials          | `10`                                 |
| Audio clips & paths      | All drag-assigned in inspector       |


## Output data

After each session, a folder is created at:

```
<project-root>/measurements/
```

Containing:

| File                          | Contents                                                              |
| ----------------------------- | --------------------------------------------------------------------- |
| `answers.csv`                 | Trial-by-trial responses                                              |
| `preBaseline_gaze.csv`        | Eye-tracking samples during pre-baseline                              |
| `preBaseline_head.csv`        | Head pose samples during pre-baseline                                 |
| `baseline_gaze.csv` / `_head.csv`   | Same, for baseline aftereffect blocks                           |
| `adaptation_gaze.csv` / `_head.csv` | Same, for the adaptation phase                                  |
| `aftereffect_gaze.csv` / `_head.csv` | Same, for the aftereffect blocks                               |

Each gaze CSV contains 28 columns: timestamps, eye openness, pupil diameter, eye origin (xyz), gaze direction (xyz), per eye + combined, plus gaze distance.

---

