// Downloads a file streamed from .NET (see Logs.razor ExportAsync).
export async function downloadFileFromStream(fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName ?? '';
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}
