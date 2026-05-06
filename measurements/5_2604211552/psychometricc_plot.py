import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.optimize import curve_fit

print("Skript startet")
# pfad zur csv
csv_path = r"D:\TolgaDaniskan\PLACES_distortions\measurements\5_2604211552\answers.csv"

df = pd.read_csv(csv_path)
print(f"trials: {len(df)}")

# erste haelfte = baseline, zweite haelfte = aftereffect
half = len(df) // 2
df["phase"] = ["baseline"] * half + ["aftereffect"] * (len(df) - half)

# fuer jede magnification: anteil stabil + standardfehler
def summarize(sub):
    out = []
    for mag in sorted(sub["magnification"].unique()):
        resp = sub[sub["magnification"] == mag]["response"]
        p = resp.mean()
        n = len(resp)
        se = np.sqrt(p * (1 - p) / n)
        out.append((mag, p, se, n))
    return np.array(out)

baseline = summarize(df[df["phase"] == "baseline"])
aftereffect = summarize(df[df["phase"] == "aftereffect"])


# gaussian fuer den fit
def gauss(x, mu, sigma, amp):
    return amp * np.exp(-0.5 * ((x - mu) / sigma) ** 2)


# fit fuer beide phasen
p0 = [1.0, 0.1, 1.0]
bounds = ([0.7, 0.01, 0.3], [1.3, 0.5, 1.0])

popt_base, _ = curve_fit(gauss, baseline[:, 0], baseline[:, 1], p0=p0, bounds=bounds)
popt_after, _ = curve_fit(gauss, aftereffect[:, 0], aftereffect[:, 1], p0=p0, bounds=bounds)

print(f"baseline:    PSE={popt_base[0]:.3f}, sigma={popt_base[1]:.3f}")
print(f"aftereffect: PSE={popt_after[0]:.3f}, sigma={popt_after[1]:.3f}")

# plot
fig, ax1 = plt.subplots(1, 1, figsize=(7, 5))

# linker plot: psychometrische kurven
x_smooth = np.linspace(0.78, 1.22, 300)

ax1.errorbar(baseline[:, 0], baseline[:, 1], yerr=baseline[:, 2],
             fmt="o", color="#2E86AB", markersize=9, capsize=4,
             label="baseline (data)", zorder=3)
ax1.plot(x_smooth, gauss(x_smooth, *popt_base), color="#2E86AB", lw=2, alpha=0.7,
         label=f"baseline fit (PSE={popt_base[0]:.3f})")
ax1.axvline(popt_base[0], color="#2E86AB", ls=":", alpha=0.5)

ax1.errorbar(aftereffect[:, 0], aftereffect[:, 1], yerr=aftereffect[:, 2],
             fmt="s", color="#E63946", markersize=9, capsize=4,
             label="aftereffect (data)", zorder=3)
ax1.plot(x_smooth, gauss(x_smooth, *popt_after), color="#E63946", lw=2, alpha=0.7,
         label=f"aftereffect fit (PSE={popt_after[0]:.3f})")
ax1.axvline(popt_after[0], color="#E63946", ls=":", alpha=0.5)

ax1.axvline(1.0, color="gray", ls="--", alpha=0.5, label="mag = 1.0 (veridical)")
ax1.set_xlabel("Magnification level")
ax1.set_ylabel("P(response = 'stable')")
ax1.set_title("Psychometric function: stability judgments\nbefore vs. after adaptation")
ax1.set_ylim(-0.05, 1.1)
ax1.legend(loc="upper right", fontsize=9)
ax1.grid(alpha=0.3)

# rechter plot: differenz
# diff = aftereffect[:, 1] - baseline[:, 1]
# colors = ["#E63946" if d > 0 else "#2E86AB" for d in diff]

# ax2.bar(baseline[:, 0], diff, width=0.03, color=colors, edgecolor="black", alpha=0.8)
# ax2.axhline(0, color="black", lw=0.8)
# ax2.axvline(1.0, color="gray", ls="--", alpha=0.5)
# ax2.set_xlabel("Magnification level")
# ax2.set_ylabel("Δ P(stable) [aftereffect − baseline]")
# ax2.set_title("Shift in stability perception\nafter adaptation")
# ax2.grid(alpha=0.3, axis="y")

plt.tight_layout()
plt.savefig("psychometric_plot.png", dpi=150, bbox_inches="tight")
plt.show()