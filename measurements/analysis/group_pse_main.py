import csv
import numpy as np
import matplotlib.pyplot as plt
from scipy.stats import wilcoxon


# settings — adjust paths to where your files live
CSV_PATH = r"D:\TolgaDaniskan\PLACES_distortions\measurements\analysis\group_pse.csv"
OUT_DIR = r"D:\TolgaDaniskan\PLACES_distortions\measurements\analysis"
ADAPT_MAG = 1.2  # the magnification participants adapted to


# colors
BASE_COLOR = "steelblue"
AFT_COLOR = "darkorange"


def read_table(path):
    # read group_pse.csv into a list of dicts, one per participant.
    participants = []
    with open(path, newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            p = {"pid": row["pid"]}
            for key in row:
                if key != "pid" and row[key] != "":
                    p[key] = float(row[key])
            participants.append(p)
    participants.sort(key=lambda p: int(p["pid"].split("_")[1]))
    return participants


def main():
    participants = read_table(CSV_PATH)
    n = len(participants)

    # collect deltas for the stats panel
    d_centres = np.array([p["d_centre"] for p in participants])
    d_uppers = np.array([p["d_pse_hi"] for p in participants])
    d_lowers = np.array([p["d_pse_lo"] for p in participants])

    # group means (for reference) 
    base_lo_mean = np.mean([p["base_pse_lo"] for p in participants])
    base_hi_mean = np.mean([p["base_pse_hi"] for p in participants])
    base_centre_mean = np.mean([p["base_centre"] for p in participants])
    aft_lo_mean = np.mean([p["aft_pse_lo"] for p in participants])
    aft_hi_mean = np.mean([p["aft_pse_hi"] for p in participants])
    aft_centre_mean = np.mean([p["aft_centre"] for p in participants])

    # wilcoxon test on the centre shift
    w, p_val = wilcoxon(d_centres)
    n_positive_centre = int((d_centres > 0).sum())

    # build the plot
    fig, ax = plt.subplots(figsize=(12, 6.5))

    # positions on the x-axis: for each participant, base bar at x-0.2, aft bar at x+0.2
    # plus one extra column at the end for the group mean
    bar_half_width = 0.12
    offset = 0.22

    for i, p in enumerate(participants):
        x_base = i - offset
        x_aft = i + offset

        # baseline window (vertical line from lo to hi, with a marker at the centre)
        ax.plot([x_base, x_base], [p["base_pse_lo"], p["base_pse_hi"]],
                color=BASE_COLOR, lw=4, solid_capstyle="round", alpha=0.85)
        ax.plot(x_base, p["base_centre"], "o", color=BASE_COLOR,
                markersize=7, markeredgecolor="white", markeredgewidth=1.0, zorder=5)

        # aftereffect window
        ax.plot([x_aft, x_aft], [p["aft_pse_lo"], p["aft_pse_hi"]],
                color=AFT_COLOR, lw=4, solid_capstyle="round", alpha=0.85)
        ax.plot(x_aft, p["aft_centre"], "o", color=AFT_COLOR,
                markersize=7, markeredgecolor="white", markeredgewidth=1.0, zorder=5)

        # thin grey line connecting baseline centre to aftereffect centre
        ax.plot([x_base, x_aft], [p["base_centre"], p["aft_centre"]],
                color="grey", lw=0.8, alpha=0.6, zorder=2)

    # group mean as a separate column at the end
    x_group = n + 0.2
    x_group_base = x_group - offset
    x_group_aft = x_group + offset

    ax.plot([x_group_base, x_group_base], [base_lo_mean, base_hi_mean],
            color=BASE_COLOR, lw=8, solid_capstyle="round")
    ax.plot(x_group_base, base_centre_mean, "o", color=BASE_COLOR,
            markersize=10, markeredgecolor="black", markeredgewidth=1.2, zorder=5)

    ax.plot([x_group_aft, x_group_aft], [aft_lo_mean, aft_hi_mean],
            color=AFT_COLOR, lw=8, solid_capstyle="round")
    ax.plot(x_group_aft, aft_centre_mean, "o", color=AFT_COLOR,
            markersize=10, markeredgecolor="black", markeredgewidth=1.2, zorder=5)

    # connecting line for the group mean
    ax.plot([x_group_base, x_group_aft], [base_centre_mean, aft_centre_mean],
            color="black", lw=1.5, zorder=4)

    # reference lines
    ax.axhline(1.0, color="black", ls=":", lw=1, alpha=0.6, zorder=1)
    ax.text(-1.0, 1.0, "veridical", fontsize=8, color="black",
            va="bottom", ha="left", alpha=0.7)

    ax.axhline(ADAPT_MAG, color="green", ls=":", lw=1, alpha=0.7, zorder=1)
    ax.text(-1.0, ADAPT_MAG, f"adapt mag = {ADAPT_MAG}", fontsize=8,
            color="green", va="bottom", ha="left", alpha=0.8)

    # x-axis labels
    xtick_positions = list(range(n)) + [x_group]
    xtick_labels = [p["pid"] for p in participants] + ["GROUP\nMEAN"]
    ax.set_xticks(xtick_positions)
    ax.set_xticklabels(xtick_labels, fontsize=9)
    ax.set_xlim(-0.7, n + 0.9)

    # y-axis
    ax.set_ylabel("magnification")
    ax.set_ylim(0.85, 1.25)
    ax.grid(alpha=0.3, axis="y")

    # legend (dummy entries)
    ax.plot([], [], color=BASE_COLOR, lw=4, label="baseline window (low → high PSE)")
    ax.plot([], [], color=AFT_COLOR, lw=4, label="aftereffect window")
    ax.plot([], [], "o", color="grey", label="window centre")
    ax.legend(loc="upper left", fontsize=9, framealpha=0.95)

    # stats text box in upper right
    stats_text = (
        f"Group stats (n = {n}):\n"
        f"  $\\Delta$ centre: mean = {d_centres.mean():+.4f}\n"
        f"  $\\Delta$ lower PSE: mean = {d_lowers.mean():+.4f}\n"
        f"  $\\Delta$ upper PSE: mean = {d_uppers.mean():+.4f}\n"
        f"\n"
        f"  {n_positive_centre} of {n} show positive $\\Delta$ centre\n"
        f"  Wilcoxon p = {p_val:.3f}"
    )
    ax.text(0.99, 0.97, stats_text, transform=ax.transAxes,
            ha="right", va="top", fontsize=9,
            bbox=dict(boxstyle="round,pad=0.5", facecolor="white",
                      edgecolor="grey", alpha=0.95),
            family="monospace")

    # title
    ax.set_title("Stability window before vs after adaptation\n"
                 "bar = range from lower to upper PSE, dot = centre,  "
                 "thin line connects same participant",
                 fontsize=11)

    plt.tight_layout()
    plt.savefig(OUT_DIR + "/group_pse_main.png", dpi=150, bbox_inches="tight")
    print(f"saved {OUT_DIR}/group_pse_main.png")
    plt.show()


main()
