(() => {
    'use strict';
    const editor = document.querySelector('[data-staff-access]');
    if (!editor) return;
    const inputs = [...editor.querySelectorAll('[data-staff-permission]')];
    const presets = [...editor.querySelectorAll('[data-staff-preset]')];
    const history = inputs.find(input => input.value === editor.dataset.historyCode);
    const payments = inputs.find(input => input.value === editor.dataset.paymentsCode);
    const dashboard = inputs.find(input => input.value === editor.dataset.dashboardCode);
    const reports = inputs.find(input => input.value === editor.dataset.reportsCode);
    const profit = inputs.find(input => input.value === editor.dataset.profitCode);
    const names = editor.querySelector('[data-staff-profile]');
    const count = editor.querySelector('[data-staff-count]');
    const codesOf = codes => new Set(codes.split(/\s+/).filter(Boolean));
    const equals = (left, right) => left.size === right.size && [...left].every(code => right.has(code));
    const legacy = codesOf(editor.dataset.legacyCodes);
    const profiles = presets.map(button => ({ button, codes: codesOf(button.dataset.codes) }));

    function refresh() {
        payments.disabled = !history.checked;
        if (payments.disabled) payments.checked = false;
        profit.disabled = !history.checked && !dashboard.checked && !reports.checked;
        if (profit.disabled) profit.checked = false;
        const selected = new Set(inputs.filter(input => input.checked).map(input => input.value));
        const profile = profiles.find(option => equals(option.codes, selected));
        for (const option of profiles) option.button.setAttribute('aria-pressed', String(option === profile));
        names.textContent = profile?.button.dataset.staffPreset ?? (!selected.size ? 'Aucun accès métier'
            : equals(legacy, selected) ? 'Équipe polyvalente' : 'Sur mesure');
        count.textContent = `${selected.size} accès`;
    }

    for (const { button, codes } of profiles) {
        button.addEventListener('click', () => {
            inputs.forEach(input => { input.checked = codes.has(input.value); });
            refresh();
        });
    }
    editor.addEventListener('change', refresh);
    window.addEventListener('pageshow', refresh);
    refresh();
    editor.querySelector('[data-staff-presets]').hidden = false;
    editor.querySelector('[data-staff-summary]').hidden = false;
})();
