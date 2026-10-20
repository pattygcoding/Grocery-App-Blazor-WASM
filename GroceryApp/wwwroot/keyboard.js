// Document level keyboard shortcuts, reported into Blazor.
//
// Shortcuts have to be recognised wherever focus happens to be, which means listening on the document rather
// than on an element, and that in turn means going through a small module instead of a Blazor event handler.
// This module only names the shortcut; what it means is decided on the C# side, so there is one place where
// the application says what Ctrl+Z does.
//
// Like the other modules here it reports failure rather than throwing, so a page served from a cache without
// this file still works: it just does not answer to the keyboard.
window.groceryKeyboard = (function () {
    let reference = null;
    let handler = null;

    function isTyping(target) {
        if (!target) { return false; }

        const tag = (target.tagName || '').toLowerCase();

        return tag === 'input' || tag === 'textarea' || tag === 'select' || target.isContentEditable === true;
    }

    function nameFor(event) {
        const key = event.key;
        const modifier = event.ctrlKey || event.metaKey;

        if (modifier && (key === 'k' || key === 'K')) { return 'palette'; }
        if (modifier && event.shiftKey && (key === 'z' || key === 'Z')) { return 'redo'; }
        if (modifier && (key === 'y' || key === 'Y')) { return 'redo'; }
        if (modifier && (key === 'z' || key === 'Z')) { return 'undo'; }
        if (key === 'Escape') { return 'escape'; }
        if (key === '?') { return 'help'; }
        if (key === '/') { return 'search'; }

        return null;
    }

    function onKeyDown(event) {
        const name = nameFor(event);

        if (!name) { return; }

        // Escape and the palette key work from inside a field as well: Escape is how a visitor gets out of one,
        // and Ctrl+K is expected to work anywhere. Everything else stays out of the way while text is being
        // typed, which also leaves the browser's own undo alone in a text field.
        const worksWhileTyping = name === 'escape' || name === 'palette';

        if (!worksWhileTyping && isTyping(event.target)) { return; }

        // These combinations have browser or operating system meanings of their own; the application needs them.
        event.preventDefault();

        if (reference) {
            reference.invokeMethodAsync('OnShortcut', name);
        }
    }

    return {
        register: function (dotNetReference) {
            reference = dotNetReference;

            if (!handler) {
                handler = onKeyDown;
                document.addEventListener('keydown', handler, true);
            }
        },

        unregister: function () {
            reference = null;

            if (handler) {
                document.removeEventListener('keydown', handler, true);
                handler = null;
            }
        },

        // Puts the cursor in the catalogue's search field. Retries briefly, because the shortcut may have
        // navigated to the catalogue and the field will not be in the document until it has rendered.
        focusSearch: function () {
            let attempts = 15;

            function tryFocus() {
                const field = document.getElementById('catalogue-search');

                if (field) {
                    field.focus();
                    field.select();
                    return;
                }

                attempts -= 1;

                if (attempts > 0) {
                    window.setTimeout(tryFocus, 60);
                }
            }

            tryFocus();
        }
    };
}());
