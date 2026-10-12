/**
 * AcxiomCRM Enterprise Landing Page JavaScript
 * Handles interactive product preview tabs, ROI calculator, and demo launcher
 */

document.addEventListener('DOMContentLoaded', function () {
    // 1. Mockup Tab Switcher
    const tabButtons = document.querySelectorAll('.mockup-tab-btn');
    const tabPanes = document.querySelectorAll('.mockup-pane');

    tabButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            tabButtons.forEach(b => b.classList.remove('active'));
            tabPanes.forEach(p => p.classList.add('d-none'));

            this.classList.add('active');
            const targetId = this.getAttribute('data-target');
            const targetPane = document.getElementById(targetId);
            if (targetPane) {
                targetPane.classList.remove('d-none');
            }
        });
    });

    // 2. Interactive Pipeline ROI Calculator
    const dealVolumeInput = document.getElementById('calcDeals');
    const dealVolumeVal = document.getElementById('calcDealsVal');
    const acvInput = document.getElementById('calcAcv');
    const acvVal = document.getElementById('calcAcvVal');
    const winRateInput = document.getElementById('calcWinRate');
    const winRateVal = document.getElementById('calcWinRateVal');

    const outAnnualRev = document.getElementById('outAnnualRev');
    const outWeightedPipe = document.getElementById('outWeightedPipe');
    const outHoursSaved = document.getElementById('outHoursSaved');

    function updateCalculator() {
        if (!dealVolumeInput || !acvInput || !winRateInput) return;

        const deals = parseInt(dealVolumeInput.value, 10);
        const acv = parseInt(acvInput.value, 10);
        const winRate = parseInt(winRateInput.value, 10);

        dealVolumeVal.textContent = deals;
        acvVal.textContent = '$' + acv.toLocaleString();
        winRateVal.textContent = winRate + '%';

        // Calculations
        const annualDeals = deals * 12;
        const totalPipeline = annualDeals * acv;
        const wonDealsRevenue = Math.round(totalPipeline * (winRate / 100));
        const weightedPipe = Math.round((deals * acv) * (winRate / 100));
        const hoursSaved = Math.round(deals * 4.2); // ~4.2 hours saved per deal cycle

        outAnnualRev.textContent = '$' + wonDealsRevenue.toLocaleString();
        outWeightedPipe.textContent = '$' + weightedPipe.toLocaleString() + ' /mo';
        outHoursSaved.textContent = hoursSaved + ' hrs /mo';
    }

    if (dealVolumeInput && acvInput && winRateInput) {
        dealVolumeInput.addEventListener('input', updateCalculator);
        acvInput.addEventListener('input', updateCalculator);
        winRateInput.addEventListener('input', updateCalculator);
        updateCalculator();
    }

    // 3. Demo Quick Login Modal Fill & Submit
    window.launchDemo = function (email, pass) {
        // Redirect to Login page with prefilled param or direct action
        const loginUrl = `/Account/Login`;
        // Create a temporary form to post directly to Login or navigate to Login page
        window.location.href = `/Account/Login`;
    };
});
