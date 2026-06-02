import sys
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy import stats 

print("script start")

# path to data folder (where amplitudes.csv lives, written by swimtest_amplitudes.py)
if len(sys.argv) > 1:
    data_folder = sys.argv[1]
else:
    data_folder = input("path to data folder: ").strip().strip('"')


# different threshold candidates to mark on the histogram.
# we'll draw vertical lines at these positions and print how many
# trials would survive each one.
THRESHOLDS_TO_SHOW = [0.6, 0.7, 0.8, 0.9]

# load the amplitudes csv (must be produced by swimtest_amplitudes.py first!!)
df = pd.read_csv(f"{data_folder}/amplitudes.csv")
df = df.dropna(subset=["r_squared"])

# extreme mag levels (0.8 and 1.2) had very few trials and often poor fits, so we exclude them here
df = df[~df["magnification"].round(2).isin([0.8, 0.85, 1.16, 1.2])]
print(f"loaded {len(df)} trials from amplitudes.csv (without mag 0.8 and 1.2)")


# summary table

print(f"\n*** R² statistics per phase ***")
for phase in ["baseline", "aftereffect"]:
    sub = df[df["phase"] == phase]
    if sub.empty:
        continue
    r = sub["r_squared"]
    print(f"  {phase}: n={len(sub)}, median={r.median():.3f}, "
          f"mean={r.mean():.3f}, min={r.min():.3f}, max={r.max():.3f}")

print(f"\n*** trials surviving each threshold ***")
print(f"{'threshold':>10} {'baseline':>15} {'aftereffect':>15}")
print("-" * 45)
for thr in THRESHOLDS_TO_SHOW:
    line = f"{thr:>10.2f}"
    for phase in ["baseline", "aftereffect"]:
        sub = df[df["phase"] == phase]
        kept = (sub["r_squared"] >= thr).sum()
        total = len(sub)
        line += f"  {kept:>3} of {total:>3} ({100*kept/total:>4.0f}%) "
    print(line)


# plot histogram

fig, axes = plt.subplots(2, 2, figsize=(12, 10))

phase_color = {"baseline": "steelblue", "aftereffect": "darkorange"}

# bin edges from 0 to 1 in steps of 0.05
bins = np.arange(0, 1.01, 0.05)

for i, phase in enumerate(["baseline", "aftereffect"]):
    # ax for histogram (top row) and scatter (bottom row)
    ax_hist = axes[0, i]
    ax_scatter = axes[1, i]
    
    sub = df[df["phase"] == phase]
    if sub.empty:
        continue
    c = phase_color[phase]
    n_trials = len(sub)

    # A: histogram of R² values
    ax_hist.hist(sub["r_squared"], bins=bins, color=c, edgecolor="black", alpha=0.7)
    
    line_colors = ["red", "darkgreen", "purple", "navy"]
    for thr, lc in zip(THRESHOLDS_TO_SHOW, line_colors):
        kept = (sub["r_squared"] >= thr).sum()
        ax_hist.axvline(thr, color=lc, lw=1.5, ls="--",
                        label=f"R² ≥ {thr}: {kept}/{n_trials}")

    ax_hist.set_title(f"{phase.capitalize()} (n = {n_trials} trials)")
    ax_hist.set_xlabel("R² (fit quality)")
    ax_hist.set_ylabel("number of trials")
    ax_hist.legend(loc="upper left", fontsize=9)
    ax_hist.set_xlim(0, 1.05)
    ax_hist.grid(alpha=0.3, axis="y")

    #  B: new -> R² vs. Mag + regression line
    ax_scatter.scatter(sub["magnification"], sub["r_squared"], 
                       color=c, alpha=0.4, edgecolor='none', s=50)
    
    # mean per magnification level as a line plot
    mags = sorted(sub["magnification"].unique())
    means = [sub[sub["magnification"] == m]["r_squared"].mean() for m in mags]
    ax_scatter.plot(mags, means, color="black", marker="o", ls="--", 
                    markersize=6, label="mean R² per mag")
    
    # linear regression line
    x = sub["magnification"]
    y = sub["r_squared"]
    slope, intercept, r_value, p_value, std_err = stats.linregress(x, y)

    # plot regression line across the range of magnifications
    x_fit = np.array([x.min(), x.max()])
    y_fit = intercept + slope * x_fit
    ax_scatter.plot(x_fit, y_fit, color="gray", lw=1.5, ls="-")
    
    # stats text box
    stats_text = (f"linear fit: {slope:.4f}\n"
                  f"p-value: {p_value:.4f}\n"
                  f"Correlation r: {r_value:.3f}")
    
    ax_scatter.text(0.7, 0.2, stats_text, transform=ax_scatter.transAxes, fontsize=9, 
                    verticalalignment='top', bbox=dict(boxstyle='round,pad=0.4', facecolor='white', edgecolor='#cccccc', alpha=0.8))
    ax_scatter.set_xlabel("Magnification Level")
    ax_scatter.set_ylabel("R² (fit quality)")
    ax_scatter.set_ylim(0, 1.05) # R² from 0 to 1
    ax_scatter.grid(alpha=0.3)
    ax_scatter.legend(loc="lower left", fontsize=9)


fig.suptitle("Distribution of R² values across trials\n"
             "lower R² = the lstsq fit doesn't match the data well\n"
             "vertical lines show how many trials each threshold would keep",
             fontsize=11)
plt.tight_layout(rect=[0, 0, 1, 0.95])
plt.savefig(f"{data_folder}/r_squared_histogram.png",
            dpi=150, bbox_inches="tight")
print(f"\nsaved {data_folder}/r_squared_histogram.png")
plt.show()