// Keyed storage for the small pieces of state that sit beside the catalogue: the activity log,
// preferences, saved views and the pinned item lists.
//
// localStorage is reached through this module rather than through raw global calls so the app can
// enumerate and clear only its own keys. The published demo shares an origin with the rest of the
// house sites, so nothing here touches a key outside the application's own prefix.
//
// Every entry point is written to be safe when localStorage is unavailable (private browsing modes
// block it): the calls report failure by returning null or an empty list rather than throwing, and
// the C# side falls back to its in-memory store.
window.groceryStorage = (function () {
    const prefix = 'groceryapp.';

    function isAvailable() {
        try {
            const probe = '__groceryapp_probe__';
            window.localStorage.setItem(probe, '1');
            window.localStorage.removeItem(probe);
            return true;
        } catch {
            return false;
        }
    }

    return {
        available: isAvailable,

        get: function (key) {
            try {
                return window.localStorage.getItem(key);
            } catch {
                return null;
            }
        },

        set: function (key, value) {
            try {
                window.localStorage.setItem(key, value);
            } catch {
                // Quota exceeded or storage blocked. The caller keeps its in-memory copy.
            }
        },

        remove: function (key) {
            try {
                window.localStorage.removeItem(key);
            } catch {
                // Nothing to remove.
            }
        },

        // Keys the application owns, so diagnostics can report them and the demo reset can clear
        // them without disturbing anything the visitor stored elsewhere on the origin.
        keys: function () {
            try {
                const keys = [];
                for (let index = 0; index < window.localStorage.length; index++) {
                    const key = window.localStorage.key(index);
                    if (key && key.indexOf(prefix) === 0) {
                        keys.push(key);
                    }
                }
                return keys;
            } catch {
                return [];
            }
        },

        // Length of a stored payload in characters, used by the diagnostics page as a cheap stand-in
        // for the bytes each concern occupies.
        sizeOf: function (key) {
            try {
                const value = window.localStorage.getItem(key);
                return value ? value.length : 0;
            } catch {
                return 0;
            }
        }
    };
}());
