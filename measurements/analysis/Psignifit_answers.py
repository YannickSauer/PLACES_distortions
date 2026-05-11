import sys
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import psignifit as ps
import psignifit.psigniplot as psp

print("script started")

# path to csv
if len(sys.argv) > 1:
    csv_path = sys.argv[1]
else:
    csv_path = input("path to answers.csv: ").strip().strip('"')

df = pd.read_csv(csv_path)
print(f"trials: {len(df)}")

# first = baseline, second part = aftereffect measurements
half = len(df) // 2
df["phase"] = ["baseline"] * half + ["aftereffect"] * (len(df) - half)


# getting data in psignifit format (arrays): columns [level, n_correct, n_total]
# for side upper we need to count "unstable"-responses as "correct" 
def make_psignifit_data(df_phase, side):
    """
    side lower: only mag <= 1.0, y = P(stable)  (0 to 1)
    side upper: only mag >= 1.0, y = P(unstable) (0 to 1)
        mirrored x-axys
    """
    if side == "lower":
        sub = df_phase[df_phase["magnification"] <= 1.0]
        data = []
        for mag in sorted(sub["magnification"].unique()):
            resp = sub[sub["magnification"] == mag]["response"]
            n_stable = int(resp.sum())
            n_total = len(resp)
            data.append([mag, n_stable, n_total])
        return np.array(data)
    else:
        sub = df_phase[df_phase["magnification"] >= 1.0]
        data = []
        for mag in sorted(sub["magnification"].unique()):
            resp = sub[sub["magnification"] == mag]["response"]
            # now unstable = "correct" response
            n_unstable = int((resp == 0).sum()) # count how many are 0 (unstable)
            n_total = len(resp)
            data.append([mag, n_unstable, n_total])
        return np.array(data)


def fit_both_sides(df_phase, label):
    print(f"\n=== {label} ===")

    # side lower: sigmoid-fit
    data_lower = make_psignifit_data(df_phase, "lower")
    print(f"lower data:\n{data_lower}")

    result_lower = ps.psignifit(
        data_lower,
        experiment_type="yes/no", #or "2AFC", but we have yes/no data
        sigmoid="norm", # normal distribution as sigmoid function (cumulative normal)
    )
    lower_thresh = result_lower.parameter_estimate["threshold"] # parameter estimate are the fitted parameters, threshold is the 50% point
    # 95% confidence interval for the lower threshold
    ci_lower = result_lower.confidence_intervals["threshold"]["0.95"]
    print(f"lower PSE: {lower_thresh:.4f}" 
          f" (95% CI: {ci_lower[0]:.4f} - {ci_lower[1]:.4f})")

    # side upper: sigmoid-fit on P(unstable)
    data_upper = make_psignifit_data(df_phase, "upper")
    print(f"upper data:\n{data_upper}")

    result_upper = ps.psignifit(
        data_upper,
        experiment_type="yes/no",
        sigmoid="norm",
    )
    upper_thresh = result_upper.parameter_estimate["threshold"]
    ci_upper = result_upper.confidence_intervals["threshold"]["0.95"]
    print(f"upper PSE: {upper_thresh:.4f} " 
          f"(95% CI: {ci_upper[0]:.4f} - {ci_upper[1]:.4f})")

    # PSE (point of subjective equality), middle between lower and upper threshold
    pse = (lower_thresh + upper_thresh) / 2.0
    # JND (just noticeable difference), half the distance between lower and upper threshold
    jnd = (upper_thresh - lower_thresh) / 2.0
    print(f"midpoint = {pse:.4f}, JND = {jnd:.4f}")

    return {
        "result_lower": result_lower, # baseline
        "result_upper": result_upper, # baseline
        "pse": pse,
        "jnd": jnd,
        "lower_thresh": lower_thresh, # aftereffect
        "upper_thresh": upper_thresh, # aftereffect
        "ci_lower": ci_lower,
        "ci_upper": ci_upper,
    }


# fit for both sides and both phases
baseline_fit = fit_both_sides(df[df["phase"] == "baseline"], "Baseline")
aftereffect_fit = fit_both_sides(df[df["phase"] == "aftereffect"], "Aftereffect")


# plot
fig, ax = plt.subplots(1, 1, figsize=(9, 6))

x_smooth = np.linspace(0.78, 1.22, 300)


def plot_combined_sigmoid(result_lower, result_upper, ax, color):
    # lower sigmoid (left): P(stable) as function of magnification <= 1.0
    x_lower = np.linspace(0.78, 1.0, 150)
    sigmoid_lower = result_lower.configuration.make_sigmoid()
    y_lower = sigmoid_lower(
        x_lower,
        result_lower.parameter_estimate["threshold"],
        result_lower.parameter_estimate["width"],
    )
    # scale y_lower to account for lambda and gamma (lapse and guess rate)
    lam = result_lower.parameter_estimate["lambda"]
    gam = result_lower.parameter_estimate["gamma"]
    y_lower = gam + (1 - lam - gam) * y_lower
    ax.plot(x_lower, y_lower, color=color, lw=2, alpha=0.7)

    # upper sigmoid (right): we fit p(unstable), but we want to plot p(stable) = 1 - p(unstable)
    x_upper = np.linspace(1.0, 1.22, 150)
    sigmoid_upper = result_upper.configuration.make_sigmoid()
    y_upper_unstable = sigmoid_upper(
        x_upper,
        result_upper.parameter_estimate["threshold"],
        result_upper.parameter_estimate["width"],
    )
    lam_u = result_upper.parameter_estimate["lambda"]
    gam_u = result_upper.parameter_estimate["gamma"]
    y_upper_unstable = gam_u + (1 - lam_u - gam_u) * y_upper_unstable
    y_upper_stable = 1 - y_upper_unstable
    ax.plot(x_upper, y_upper_stable, color=color, lw=2, alpha=0.7)


# plot raw data points with error bars (standard error of proportion)
def plot_raw(df_phase, ax, color, marker, label):
    for mag in sorted(df_phase["magnification"].unique()):
        resp = df_phase[df_phase["magnification"] == mag]["response"]
        p = resp.mean()
        ax.plot(mag, p, marker=marker, color=color, markersize=9, linestyle="None", zorder=3)
        # n = len(resp)
        # se = np.sqrt(p * (1 - p) / n) if n > 0 else 0
        # ax.errorbar(mag, p, yerr=se, fmt=marker, color=color, markersize=9,
        #            capsize=4, zorder=3)
    # dummy points for legend
    ax.plot([], [], marker=marker, color=color, markersize=9, linestyle="None",
            label=f"{label} (data)")


plot_raw(df[df["phase"] == "baseline"], ax, "darkblue", "o", "baseline")
plot_combined_sigmoid(baseline_fit["result_lower"], baseline_fit["result_upper"],
                      ax, "darkblue")
ax.axvline(baseline_fit["lower_thresh"], color="darkblue", ls=":", alpha=0.5)
ax.axvline(baseline_fit["upper_thresh"], color="darkblue", ls=":", alpha=0.5)
ax.plot([], [], "-", color="darkblue", lw=2,
        label=f"baseline (PSE_lo={baseline_fit['lower_thresh']:.3f}, "
              f"PSE_hi={baseline_fit['upper_thresh']:.3f})")

plot_raw(df[df["phase"] == "aftereffect"], ax, "orange", "s", "aftereffect")
plot_combined_sigmoid(aftereffect_fit["result_lower"], aftereffect_fit["result_upper"],
                      ax, "orange")
ax.axvline(aftereffect_fit["lower_thresh"], color="orange", ls=":", alpha=0.5)
ax.axvline(aftereffect_fit["upper_thresh"], color="orange", ls=":", alpha=0.5)
ax.plot([], [], "-", color="orange", lw=2,
        label=f"aftereffect (PSE_lo={aftereffect_fit['lower_thresh']:.3f}, "
              f"PSE_hi={aftereffect_fit['upper_thresh']:.3f})")

ax.axvline(1.0, color="gray", ls="--", alpha=0.5, label="mag = 1.0 (veridical)")
ax.set_xlabel("Magnification level")
ax.set_ylabel("P(response = 'stable')")
ax.set_title("Psychometric function (psignifit fit, both sides)\n"
             "before vs. after adaptation")
ax.set_ylim(-0.05, 1.1)
ax.legend(loc="upper right", fontsize=9)
ax.grid(alpha=0.3)

plt.tight_layout()
plt.savefig("psychometric_plot_psignifit.png", dpi=150, bbox_inches="tight")
plt.show()