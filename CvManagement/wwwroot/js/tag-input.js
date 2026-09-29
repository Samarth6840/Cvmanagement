// Tagify wrapper (spec §5.3 / §13a.1): a ready-made tag input with autocomplete from the
// tags already entered elsewhere in the system.
//
// Tagify builds its own input inside the host element and the host has no Blazor children,
// so Blazor and Tagify never manage the same nodes.

const inputs = new Map();

export async function init(host, initialTags, whitelist, placeholder, dotNetRef) {
    if (!host) return false;

    const Tagify = await waitForGlobal("Tagify");
    if (!Tagify) return false;

    destroy(host);

    const input = document.createElement("input");
    input.type = "text";
    input.className = "form-control";
    input.setAttribute("placeholder", placeholder || "");
    host.appendChild(input);

    const tagify = new Tagify(input, {
        whitelist: whitelist || [],
        delimiters: ",",
        maxTags: 50,
        dropdown: {
            enabled: 1,
            maxItems: 20,
            closeOnSelect: false,
            highlightFirst: true
        }
    });

    if (initialTags && initialTags.length) {
        tagify.addTags(initialTags.filter(tag => tag && tag.trim().length));
    }

    const notify = () => notifyTags(dotNetRef, tagify.value.map(tag => tag.value));

    tagify.on("add", notify);
    tagify.on("remove", notify);

    inputs.set(host.id, tagify);
    return true;
}

export function destroy(hostOrId) {
    const host = typeof hostOrId === "string" ? document.getElementById(hostOrId) : hostOrId;
    const id = typeof hostOrId === "string" ? hostOrId : hostOrId?.id;
    if (!id) return;

    const tagify = inputs.get(id);
    if (tagify) {
        tagify.destroy();
        inputs.delete(id);
    }

    if (host) host.innerHTML = "";
}

async function waitForGlobal(name, timeoutMs = 3000) {
    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline) {
        if (window[name]) return window[name];
        await new Promise(resolve => setTimeout(resolve, 50));
    }

    return null;
}

async function notifyTags(dotNetRef, tags) {
    try {
        await dotNetRef.invokeMethodAsync("OnTagsChangedJs", tags);
    } catch {
        // Circuit disposed; the tags live in the parent's state anyway.
    }
}
