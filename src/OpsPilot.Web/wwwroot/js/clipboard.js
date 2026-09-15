// The only custom JavaScript: a browser API unavailable directly from C#.
export async function copyText(text) {
    if (!window.isSecureContext || !navigator.clipboard) {
        throw new Error("Clipboard requires HTTPS or localhost.");
    }
    await navigator.clipboard.writeText(text);
}
