(() => {
    'use strict';
    const toggle = document.querySelector('[data-dashboard-amount-toggle]');
    const dashboard = toggle?.closest('main');
    if (!dashboard) return;

    const amounts = Array.from(dashboard.querySelectorAll('[data-dashboard-amount]'), element => ({
        element,
        visible: element.textContent,
        masked: `•••• ${element.dataset.dashboardAmount}`
    }));
    const tooltips = Array.from(dashboard.querySelectorAll('[data-dashboard-tooltip]'), element => ({
        element,
        visible: element.title,
        masked: element.dataset.dashboardTooltip
    }));
    const eye = toggle.querySelector('[data-dashboard-eye]');
    const eyeOff = toggle.querySelector('[data-dashboard-eye-off]');
    let masked = false;

    toggle.addEventListener('click', () => {
        masked = !masked;
        for (const amount of amounts) amount.element.textContent = masked ? amount.masked : amount.visible;
        for (const tooltip of tooltips) tooltip.element.title = masked ? tooltip.masked : tooltip.visible;
        eye.hidden = masked;
        eyeOff.hidden = !masked;
        const action = masked ? 'Afficher les montants' : 'Masquer les montants';
        toggle.setAttribute('aria-label', action);
        toggle.title = action;
    });
    toggle.hidden = false;
})();
