// EasyMDE wrapper (spec §13a.1: use a ready-made Markdown editor rather than a hand-rolled
// toolbar). EasyMDE builds its own DOM inside the host element, which Blazor never renders
// into, so Blazor's diffing never fights the editor for the same nodes.

const editors = new Map();

export async function init(host, initialValue, dotNetRef) {
    if (!host) return false;

    const EasyMDE = await waitForGlobal("EasyMDE");
    if (!EasyMDE) return false;

    destroy(host);

    const textarea = document.createElement("textarea");
    host.appendChild(textarea);

    const editor = new EasyMDE({
        element: textarea,
        initialValue: initialValue ?? "",
        spellChecker: false,
        status: false,
        minHeight: "200px",
        toolbar: [
            "bold", "italic", "heading", "|",
            "quote", "unordered-list", "ordered-list", "|",
            "link", "image", "code", "|",
            "preview", "side-by-side", "fullscreen"
        ]
    });

    editor.codemirror.on("change", () => {
        notify(dotNetRef, editor.value());
    });

    editors.set(host.id, editor);
    return true;
}

export function setValue(hostId, value) {
    const editor = editors.get(hostId);
    if (!editor) return;

    const next = value ?? "";
    if (editor.value() !== next) {
        editor.value(next);
    }
}

export function destroy(hostOrId) {
    const host = typeof hostOrId === "string" ? document.getElementById(hostOrId) : hostOrId;
    const id = typeof hostOrId === "string" ? hostOrId : hostOrId?.id;
    if (!id) return;

    const editor = editors.get(id);
    if (editor) {
        editor.toTextArea();
        editors.delete(id);
    }

    if (host) host.innerHTML = "";
}

// The CDN script may still be in flight when the first component renders; poll briefly
// instead of silently leaving the user with a plain textarea.
async function waitForGlobal(name, timeoutMs = 3000) {
    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline) {
        if (window[name]) return window[name];
        await new Promise(resolve => setTimeout(resolve, 50));
    }

    return null;
}

async function notify(dotNetRef, value) {
    try {
        await dotNetRef.invokeMethodAsync("OnEditorChanged", value);
    } catch {
        // Circuit disposed mid-typing; dropping the update is correct here.
    }
}
