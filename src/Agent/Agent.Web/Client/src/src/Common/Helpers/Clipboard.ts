export const copyToClipboard = async (textToCopy: string): Promise<void> => {
    try {
        // This seems to be blocked in portal iframes
        await navigator.clipboard.writeText(textToCopy);
    } catch (error) {
        // Fallback method
        const textArea = document.createElement('textarea');
        textArea.value = textToCopy;
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        document.execCommand('copy');
        document.body.removeChild(textArea);
    }
};
