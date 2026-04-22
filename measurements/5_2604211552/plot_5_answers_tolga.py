"""
plot_answers.py
---------------
Plots psychometric functions for the swim-test adaptation experiment.

Usage:
    python plot_answers.py answers.csv

Requirements:
    pip install pandas numpy matplotlib scipy

The CSV must have columns: trial, timestamp, magnification, radial, response
where response = 1 means "stable", response = 0 means "unstable".

Assumes the first half of trials = baseline (pre-adaptation),
second half = aftereffect (post-adaptation). If your experiment
has a different structure, adjust the phase-split logic below.
"""
import sys
from pathlib import Path
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.optimize import curve_fit


def gaussian(x, mu, sigma, amp):
    """Gaussian bump, peak = mu (PSE), width = sigma, height = amp."""
    return amp * np.exp(-0.5 * ((x - mu) / sigma) ** 2)


def fit_psychometric(x, y):
    """Fit a Gaussian to (magnification, p_stable). Returns (mu, sigma, amp)."""
    try:
        popt, _ = curve_fit(
            gaussian, x, y,
            p0=[1.0, 0.1, 1.0],
            bounds=([0.7, 0.01, 0.3], [1.3, 0.5, 1.2]),
        )
        return popt
    except Exception as e:
        print(f"  fit failed: {e}")
        return None


def main(csv_path):
    df = pd.read_csv(csv_path)
    n = len(df)
    print(f"Loaded {n} trials from {csv_path}")

    # Split into baseline (first half) and aftereffect (second half).
    # If your experiment logs phase explicitly, replace this with that column.
    half = n // 2
    df["phase"] = ["baseline" if i < half else "aftereffect" for i in range(n)]

    # Aggregate: proportion of "stable" responses per magnification per phase
    agg = (
        df.groupby(["phase", "magnification"])["response"]
          .agg(["mean", "count", "sum"])
          .rename(columns={"mean": "p_stable", "sum": "n_stable"})
          .reset_index()
    )
    # Binomial standard error
    agg["se"] = np.sqrt(agg["p_stable"] * (1 - agg["p_stable"]) / agg["count"])

    # Fit Gaussians per phase
    fits = {}
    for phase in ["baseline", "aftereffect"]:
        sub = agg[agg["phase"] == phase].sort_values("magnification")
        fits[phase] = fit_psychometric(sub["magnification"].values,
                                        sub["p_stable"].values)
        if fits[phase] is not None:
            mu, sigma, amp = fits[phase]
            print(f"  {phase:12s} PSE={mu:.4f}  sigma={sigma:.4f}  amp={amp:.3f}")

    # --- Plot ---
    fig, axes = plt.subplots(1, 2, figsize=(13, 5))
    colors = {"baseline": "#2E86AB", "aftereffect": "#E63946"}
    markers = {"baseline": "o", "aftereffect": "s"}

    # Left: overlaid psychometric curves
    ax = axes[0]
    x_fit = np.linspace(0.78, 1.22, 300)
    for phase in ["baseline", "aftereffect"]:
        sub = agg[agg["phase"] == phase].sort_values("magnification")
        ax.errorbar(sub["magnification"], sub["p_stable"], yerr=sub["se"],
                    fmt=markers[phase], color=colors[phase], markersize=9,
                    capsize=4, label=f"{phase} (data)", zorder=3)
        if fits[phase] is not None:
            ax.plot(x_fit, gaussian(x_fit, *fits[phase]),
                    color=colors[phase], lw=2, alpha=0.7,
                    label=f"{phase} fit (PSE={fits[phase][0]:.3f})")
            ax.axvline(fits[phase][0], color=colors[phase], ls=":", alpha=0.5)

    ax.axvline(1.0, color="gray", ls="--", alpha=0.5, label="mag = 1.0 (veridical)")
    ax.set_xlabel("Magnification level")
    ax.set_ylabel('P(response = "stable")')
    ax.set_title("Psychometric function: stability judgments\nbefore vs. after adaptation")
    ax.legend(loc="upper right", fontsize=9)
    ax.grid(alpha=0.3)
    ax.set_ylim(-0.05, 1.1)

    # Right: difference between phases
    ax = axes[1]
    piv = agg.pivot(index="magnification", columns="phase", values="p_stable")
    piv["diff"] = piv["aftereffect"] - piv["baseline"]
    piv = piv.reset_index()
    ax.bar(piv["magnification"], piv["diff"], width=0.03,
           color=["#E63946" if d > 0 else "#2E86AB" for d in piv["diff"]],
           edgecolor="black", alpha=0.8)
    ax.axhline(0, color="black", lw=0.8)
    ax.axvline(1.0, color="gray", ls="--", alpha=0.5)
    ax.set_xlabel("Magnification level")
    ax.set_ylabel("Δ P(stable)  [aftereffect − baseline]")
    ax.set_title("Shift in stability perception\nafter adaptation")
    ax.grid(alpha=0.3, axis="y")

    plt.tight_layout()
    plt.show()


if __name__ == "__main__":
    csv_path = sys.argv[1] if len(sys.argv) > 1 else "answers.csv"
    if not Path(csv_path).exists():
        sys.exit(f"File not found: {csv_path}")
    main(csv_path)