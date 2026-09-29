// Drag-and-drop image upload straight to Cloudinary (spec §13a.4).
//
// The file never touches this application: the browser POSTs it to Cloudinary's upload
// endpoint with an *unsigned* upload preset, and only the returned URL is handed back to
// the server to store. The API secret is therefore not needed here at all.

const CLOUDINARY_UPLOAD_BASE = "https://api.cloudinary.com/v1_1";

/**
 * Wires a drop zone and its hidden file input to Cloudinary.
 * Returns false when the cloud target is not configured, so the caller can degrade.
 */
export function attach(host, dotNetRef, cloudName, uploadPreset) {
    if (!host || !cloudName || !uploadPreset) return false;

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

export function detach(host) {
    if (!host) return;

    const input = host.querySelector("input[type=file]");
    if (host._onPick) host.removeEventListener("click", host._onPick);
    if (input && host._onChange) input.removeEventListener("change", host._onChange);
    if (host._onDragOver) host.removeEventListener("dragover", host._onDragOver);
    if (host._onDragLeave) host.removeEventListener("dragleave", host._onDragLeave);
    if (host._onDrop) host.removeEventListener("drop", host._onDrop);

    host._onPick = host._onChange = host._onDragOver = host._onDragLeave = host._onDrop = null;
}

async function notify(dotNetRef, method, payload) {
    try {
        await dotNetRef.invokeMethodAsync(method, payload);
    } catch {
        // The circuit can already be gone when a slow upload finishes; nothing to do.
    }
}
