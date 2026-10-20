// Theme control: light, dark, or follow the operating system.
//
// The inline script in index.html applies the stored preference before first paint (so there is
// no light flash for a dark preference). This module owns everything after that: changing the
// preference, persisting it, and re-resolving "system" when the OS setting changes.
window.groceryTheme = (function () {
    const storageKey = 'groceryapp.theme';
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    let preference = read();

    function read() {
        try {
            const stored = localStorage.getItem(storageKey);
            return stored === 'light' || stored === 'dark' ? stored : 'system';
        } catch {
            return 'system';
        }
    }

    function resolved() {
        return preference === 'system' ? (media.matches ? 'dark' : 'light') : preference;
    }

    function apply() {
        const root = document.documentElement;
        root.setAttribute('data-theme', resolved());
        root.setAttribute('data-theme-preference', preference);
    }

    function set(next) {
        preference = next === 'light' || next === 'dark' ? next : 'system';

        try {
            if (preference === 'system') {
                localStorage.removeItem(storageKey);
            } else {
                localStorage.setItem(storageKey, preference);
            }
        } catch {
            // Storage can be blocked (private browsing); the choice still applies for this session.
        }

        apply();
        return preference;
    }

    media.addEventListener('change', function () {
        if (preference === 'system') {
            apply();
        }
    });

    apply();

    return {
        get: function () { return preference; },
        resolved: resolved,
        set: set
    };
}());
