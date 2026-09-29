// Image upload straight to Cloudinary (spec §13a.4).
//
// Two paths, both of which keep the bytes away from this application entirely:
//
//  1. Cloudinary's official Upload Widget (spec §13a.1: prefer the ready-made component).
//     It offers drag-and-drop, camera and URL sources, and posts straight to Cloudinary.
//  2. A plain direct upload, used only if the widget script is unavailable. Same guarantee —
//     the browser POSTs to Cloudinary's REST endpoint with an *unsigned* preset — so a CDN
//     outage degrades the picker rather than breaking image attributes.
//
// In both cases only the returned URL reaches the server, and the API secret is never needed.

const UPLOAD_WIDGET_SCRIPT = "cloudinary"; // window.cloudinary, from upload-widget.cloudinary.com
const CLOUDINARY_UPLOAD_BASE = "https://api.cloudinary.com/v1_1";

/**
 * Wires a drop zone to Cloudinary. Returns false when the cloud target is not configured,
 * so the caller can show its "not configured" notice instead.
 */
export async function attach(host, dotNetRef, cloudName, uploadPreset) {
    if (!host || !cloudName || !uploadPreset) return false;

    const cloudinary = await waitForGlobal(UPLOAD_WIDGET_SCRIPT);
    if (cloudinary) {
        attachUploadWidget(host, dotNetRef, cloudinary, cloudName, uploadPreset);
        return true;
    }

    return attachDirectUpload(host, dotNetRef, cloudName, uploadPreset);
}

export function detach(host) {
    if (!host) return;

    host._widget?.close?.();
    host._widget = null;

    const input = host.querySelector("input[type=file]");
    if (host._onPick) host.removeEventListener("click", host._onPick);
    if (input && host._onChange) input.removeEventListener("change", host._onChange);
    if (host._onDragOver) host.removeEventListener("dragover", host._onDragOver);
    if (host._onDragLeave) host.removeEventListener("dragleave", host._onDragLeave);
    if (host._onDrop) host.removeEventListener("drop", host._onDrop);

    host._onPick = host._onChange = host._onDragOver = host._onDragLeave = host._onDrop = null;
}

// --- Cloudinary's own uploader -------------------------------------------------------------

function attachUploadWidget(host, dotNetRef, cloudinary, cloudName, uploadPreset) {
    const widget = cloudinary.createUploadWidget(
        {
            cloudName,
            uploadPreset,
            multiple: false,
            sources: ["local", "url", "camera"],
            clientAllowedFormats: ["png", "jpg", "jpeg", "gif", "webp"],
            cropping: false,
            language: "en"
        },
        async (error, result) => {
            if (error) {
                await notify(dotNetRef, "OnUploadFailed", "upload-failed");
                return;
            }

            // The callback also fires for open/queued/close; only a success carries a URL.
            if (result?.event === "success" && result.info?.secure_url) {
                await notify(dotNetRef, "OnUploaded", result.info.secure_url);
            }
        });

    host._widget = widget;
    host._onPick = () => widget.open();
    host.addEventListener("click", host._onPick);
}

// --- Fallback: post the file straight to Cloudinary ----------------------------------------

function attachDirectUpload(host, dotNetRef, cloudName, uploadPreset) {
    const input = host.querySelector("input[type=file]");
    if (!input) return false;

    const upload = async (file) => {
        if (!file) return;

        if (!file.type.startsWith("image/")) {
            await notify(dotNetRef, "OnUploadFailed", "not-an-image");
            return;
        }

        const form = new FormData();
        form.append("file", file);
        form.append("upload_preset", uploadPreset);

        host.classList.add("is-uploading");
        try {
            const response = await fetch(`${CLOUDINARY_UPLOAD_BASE}/${cloudName}/image/upload`, {
                method: "POST",
                body: form
            });

            if (!response.ok) {
                await notify(dotNetRef, "OnUploadFailed", "upload-failed");
                return;
            }

            const result = await response.json();
            await notify(dotNetRef, "OnUploaded", result.secure_url);
        } catch {
            await notify(dotNetRef, "OnUploadFailed", "upload-failed");
        } finally {
            host.classList.remove("is-uploading");
            input.value = "";
        }
    };

    host._onPick = () => input.click();
    host._onChange = () => upload(input.files[0]);
    host._onDragOver = (e) => { e.preventDefault(); host.classList.add("is-dragging"); };
    host._onDragLeave = () => host.classList.remove("is-dragging");
    host._onDrop = (e) => {
        e.preventDefault();
        host.classList.remove("is-dragging");
        upload(e.dataTransfer?.files?.[0]);
    };

    host.addEventListener("click", host._onPick);
    input.addEventListener("change", host._onChange);
    host.addEventListener("dragover", host._onDragOver);
    host.addEventListener("dragleave", host._onDragLeave);
    host.addEventListener("drop", host._onDrop);

    return true;
}

async function waitForGlobal(name, timeoutMs = 3000) {
    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline) {
        if (window[name]) return window[name];
        await new Promise(resolve => setTimeout(resolve, 50));
    }

    return null;
}

async function notify(dotNetRef, method, payload) {
    try {
        await dotNetRef.invokeMethodAsync(method, payload);
    } catch {
        // The circuit can already be gone when a slow upload finishes; nothing to do.
    }
}
