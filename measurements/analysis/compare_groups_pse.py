import csv
import numpy as np
import matplotlib.pyplot as plt
from scipy.stats import wilcoxon, ttest_rel


CSV_PATH = r"D:\TolgaDaniskan\PLACES_distortions\measurements\analysis\group_pse.csv"   # path to the table exported from MATLAB
OUT_DIR = r"D:\TolgaDaniskan\PLACES_distortions\measurements\analysis"                # where the plots get saved
ADAPT_MAG = 1.2              # magnification used during adaptation


blue = "blue"
orange = "orange"


def read_table(path):
    # read group_pse.csv into a list of dicts, one per participant
    participants = []
    with open(path, newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            p = {"pid": row["pid"]}
            for key in row:
                if key != "pid" and row[key] != "":
                    p[key] = float(row[key])
            participants.append(p)

    # if the csv only has the PSEs, work out centre and width myself
    for p in participants:
        for phase in ["base", "aft"]:
            if phase + "_centre" not in p:
                p[phase + "_centre"] = (p[phase + "_pse_lo"] + p[phase + "_pse_hi"]) / 2
            if phase + "_width" not in p:
                p[phase + "_width"] = p[phase + "_pse_hi"] - p[phase + "_pse_lo"]
        # aftereffect minus baseline
        for q in ["pse_lo", "pse_hi", "centre", "width"]:
            p["d_" + q] = p["aft_" + q] - p["base_" + q]

    participants.sort(key=lambda p: int(p["pid"].split("_")[1]))
    return participants


def run_stats(name, deltas):
    # print mean, CI and tests for one delta measure, return mean + CI for plotting
    deltas = np.array(deltas)
    n = len(deltas)
    mean = deltas.mean()
    sd = deltas.std(ddof=1)
    se = sd / np.sqrt(n)
    ci_low = mean - 1.96 * se
    ci_high = mean + 1.96 * se
    n_positive = np.sum(deltas > 0)

    print(name)
    print("  n =", n)
    print("  mean delta = %+.4f  (95%% CI %+.4f to %+.4f)" % (mean, ci_low, ci_high))
    print("  %d of %d participants positive (we expect positive)" % (n_positive, n))

    # n is tiny, so Wilcoxon is the safer test, t-test just for comparison
    w, p_wilcoxon = wilcoxon(deltas)
    t, p_ttest = ttest_rel(deltas, np.zeros(n))
    print("  Wilcoxon p = %.4f" % p_wilcoxon)
    print("  t-test   p = %.4f" % p_ttest)
    print()
    return mean, ci_low, ci_high


def plot_shift(participants):
    # one line per participant: baseline centre -> aftereffect centre
    fig, ax = plt.subplots(figsize=(6.5, 5.2))

    for p in participants:
        ax.plot([0, 1], [p["base_centre"], p["aft_centre"]], color="0.6", lw=1.3)
        ax.scatter(0, p["base_centre"], color=blue, s=55, zorder=3)
        ax.scatter(1, p["aft_centre"], color=orange, s=55, zorder=3)
        ax.annotate(p["pid"], (1, p["aft_centre"]), fontsize=7.5,
                    xytext=(5, 0), textcoords="offset points", va="center")

    base_mean = np.mean([p["base_centre"] for p in participants])
    aft_mean = np.mean([p["aft_centre"] for p in participants])
    ax.plot([0, 1], [base_mean, aft_mean], color="black", lw=3, zorder=4,
            label="group mean (%.3f to %.3f)" % (base_mean, aft_mean))

    ax.axhline(1.0, color="grey", ls="--", lw=1, label="mag = 1.0 (veridical)")
    ax.axhline(ADAPT_MAG, color="green", ls=":", lw=1, label="adapt mag = %.1f" % ADAPT_MAG)

    ax.set_xticks([0, 1])
    ax.set_xticklabels(["baseline", "aftereffect"])
    ax.set_ylabel("Stability window centre (magnification)")
    ax.set_title("Window centre before vs after adaptation")
    ax.legend(fontsize=8, loc="upper left")
    ax.grid(alpha=0.25, axis="y")

    fig.tight_layout()
    fig.savefig(OUT_DIR + "/group_shift.png", dpi=140)
    plt.close(fig)


def plot_delta(participants, mean_centre, ci_low, ci_high):
    pids = [p["pid"] for p in participants]
    d_centre = [p["d_centre"] for p in participants]
    d_upper = [p["d_pse_hi"] for p in participants]
    x = np.arange(len(participants))

    fig, ax = plt.subplots(figsize=(max(6.5, 1.15 * len(participants) + 3), 5.2))
    ax.bar(x - 0.19, d_centre, 0.36, color=blue, label="delta window centre")
    ax.bar(x + 0.19, d_upper, 0.36, color=orange, label="delta upper PSE")
    ax.axhline(0, color="black", lw=0.8)
    ax.axhline(mean_centre, color="green", ls="--", lw=1.3,
               label="mean delta centre = %+.3f" % mean_centre)

    ax.set_xticks(x)
    ax.set_xticklabels(pids, rotation=25, ha="right")
    ax.set_ylabel("Aftereffect minus baseline (magnification)")
    ax.set_title("Adaptation effect per participant (positive = expected direction)")
    ax.legend(fontsize=8)
    ax.grid(alpha=0.25, axis="y")

    fig.tight_layout()
    fig.savefig(OUT_DIR + "/group_delta.png", dpi=140)
    plt.close(fig)


def main():
    participants = read_table(CSV_PATH)

    print("Participants:", ", ".join(p["pid"] for p in participants))
    print("Adaptation magnification:", ADAPT_MAG)
    print()

    # the upper PSE is the interesting edge because we adapted to mag > 1
    mean_c, lo_c, hi_c = run_stats("delta window centre",
                                   [p["d_centre"] for p in participants])
    run_stats("delta upper PSE", [p["d_pse_hi"] for p in participants])
    run_stats("delta lower PSE", [p["d_pse_lo"] for p in participants])
    run_stats("delta window width", [p["d_width"] for p in participants])

    plot_shift(participants)
    plot_delta(participants, mean_c, lo_c, hi_c)
    print("saved group_shift.png and group_delta.png")


main()