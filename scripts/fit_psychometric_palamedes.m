% Difference of two cumulative Gaussians fit using Palamedes Toolbox
% data csv: trial, timestamp, magnification, radial, response (1=stable, 0=unstable)

clear; close all; clc;


%% load data
csv_path = 'D:\TolgaDaniskan\PLACES_distortions\measurements\6_2604231156\answers.csv';
T = readtable(csv_path);
fprintf('trials: %d\n', height(T));

half = floor(height(T)/2);
phase = strings(height(T),1);
phase(1:half) = "baseline";
phase(half+1:end) = "aftereffect";
T.phase = phase;


%% custom function
% PSI(x) = Phi(x; mu_lo, sigma_lo) - Phi(x; mu_hi, sigma_hi)
% params = [mu_lo, sigma_lo, mu_hi, sigma_hi]
PF = @diffOfTwoNormals;


%% fit baseline and aftereffect
fprintf('\n--- Baseline ---\n');
base_params = fitPF(T(T.phase=="baseline",:), PF);

fprintf('\n--- Aftereffect ---\n');
after_params = fitPF(T(T.phase=="aftereffect",:), PF);





%% print results
fprintf('\n----\n');
fprintf('Results\n');
fprintf('----\n');
fprintf('\nBaseline:\n');
fprintf('  mu_lo (PSE lower) = %.4f\n', base_params(1));
fprintf('  sigma_lo          = %.4f\n', base_params(2));
fprintf('  mu_hi (PSE upper) = %.4f\n', base_params(3));
fprintf('  sigma_hi          = %.4f\n', base_params(4));
fprintf('  lapse             = %.4f\n', base_params(5));
fprintf('\nAftereffect:\n');
fprintf('  mu_lo (PSE lower) = %.4f\n', after_params(1));
fprintf('  sigma_lo          = %.4f\n', after_params(2));
fprintf('  mu_hi (PSE upper) = %.4f\n', after_params(3));
fprintf('  sigma_hi          = %.4f\n', after_params(4));
fprintf('  lapse             = %.4f\n', after_params(5));


%% plot
figure('Position', [100 100 900 600]);
hold on;

x_smooth = linspace(0.78, 1.22, 300);

% baseline
[mags_b, props_b, ~] = aggregate(T(T.phase=="baseline",:));
plot(mags_b, props_b, 'o', 'Color', [0 0 0.55], 'MarkerSize', 9, ...
    'MarkerFaceColor', [0 0 0.55]);
y_b = PF(base_params, x_smooth);
plot(x_smooth, y_b, '-', 'Color', [0 0 0.55], 'LineWidth', 2);
xline(base_params(1), ':', 'Color', [0 0 0.55]);
xline(base_params(3), ':', 'Color', [0 0 0.55]);

% aftereffect
[mags_a, props_a, ~] = aggregate(T(T.phase=="aftereffect",:));
plot(mags_a, props_a, 's', 'Color', [1 0.5 0], 'MarkerSize', 9, ...
    'MarkerFaceColor', [1 0.5 0]);
y_a = PF(after_params, x_smooth);
plot(x_smooth, y_a, '-', 'Color', [1 0.5 0], 'LineWidth', 2);
xline(after_params(1), ':', 'Color', [1 0.5 0]);
xline(after_params(3), ':', 'Color', [1 0.5 0]);

xline(1.0, '--', 'Color', [0.5 0.5 0.5]);

xlabel('Magnification level');
ylabel('P(response = stable)');
title({'Difference of two cumulative Gaussians (Palamedes)', ...
       'before vs. after adaptation'});
ylim([-0.05 1.1]);
grid on;
grid minor;

legend({...
    'baseline (data)', ...
    sprintf('baseline fit (PSE_{lo}=%.3f, PSE_{hi}=%.3f)', base_params(1), base_params(3)), ...
    '', '', ...
    'aftereffect (data)', ...
    sprintf('aftereffect fit (PSE_{lo}=%.3f, PSE_{hi}=%.3f)', after_params(1), after_params(3)), ...
    '', '', ...
    'mag = 1.0 (veridical)'}, ...
    'Location', 'northeast', 'FontSize', 9);

saveas(gcf, 'psychometric_palamedes.png');


%% --- helper functions ---

function y = diffOfTwoNormals(params, x)
    % difference of two cumulative normals with lapse rate
    % params = [mu_lo, sigma_lo, mu_hi, sigma_hi, lapse]
    mu_lo = params(1);
    sigma_lo = params(2);
    mu_hi = params(3);
    sigma_hi = params(4);
    lapse = params(5);
    
    lower = normcdf(x, mu_lo, sigma_lo);
    upper = normcdf(x, mu_hi, sigma_hi); % cumulative distribution = normcdf
    p = lower - upper;
    % apply lapse: curve goes from lapse to 1-lapse instead of 0 to 1
    y = lapse + (1 - 2*lapse) * p;
    
    y = max(min(y, 1-eps), eps);
end


function negLL = negLogLikelihood(params, mags, nCorrect, ns, PF)
    % negative log-likelihood of binomial data given PF parameters
    
    % constraint: sigma > 0 and mu_lo < mu_hi (otherwise nonsense)
    if params(2) < 0.005 || params(4) < 0.005 || params(1) >= params(3) || params(5) < 0 || params(5) > 0.1
        negLL = Inf;
        return;
    end
    
    p = PF(params, mags);
    LL = sum(nCorrect .* log(p) + (ns - nCorrect) .* log(1-p));
    negLL = -LL;
end


function params = fitPF(T_phase, PF)
    [mags, props, ns] = aggregate(T_phase); % aggregate counts for each mag how many trials there have been (ns)
    nCorrect = round(props .* ns); % and what amount was stable (props)
    
    fprintf('aggregated data:\n');
    for k = 1:length(mags)
        fprintf('  mag=%.2f  stable=%d/%d  P=%.3f\n', mags(k), nCorrect(k), ns(k), props(k));
    end
    
    % starting values
    bestParams = [0.93 0.03 1.06 0.03 0.02];
    
    % PAL_minimize 
    options = PAL_minimize('options');
    options.MaxIter = 5000;
    options.MaxFunEvals = 5000;
    options.TolX = 1e-8;
    options.TolFun = 1e-8;
    
    objfun = @(p) negLogLikelihood(p, mags, nCorrect, ns, PF);
    [params, fval, exitflag] = PAL_minimize(objfun, bestParams, options);
    
    if exitflag ~= 1
        warning('optimizer did not fully converge (exitflag=%d)', exitflag);
    end
    
    fprintf('\nfitted parameters:\n');
    fprintf('  mu_lo  (PSE lower) = %.4f\n', params(1));
    fprintf('  sigma_lo           = %.4f\n', params(2));
    fprintf('  mu_hi  (PSE upper) = %.4f\n', params(3));
    fprintf('  sigma_hi           = %.4f\n', params(4));
    fprintf('  lapse              = %.4f\n', params(5));
    fprintf('  -log-likelihood    = %.4f\n', fval);
    
end



function [mags, props, ns] = aggregate(T_phase)
    mags = unique(T_phase.magnification);
    props = nan(size(mags));
    ns = nan(size(mags));
    for k = 1:length(mags)
        sub = T_phase(T_phase.magnification == mags(k), :);
        props(k) = mean(sub.response);
        ns(k) = height(sub);
    end
end
