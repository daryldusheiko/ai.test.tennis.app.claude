// Registers the service worker that makes the app work offline, and reloads once when a new version takes over.
if ('serviceWorker' in navigator) {
    const hadController = navigator.serviceWorker.controller !== null;
    let reloading = false;

    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (hadController && !reloading) {
            reloading = true;
            location.reload();
        }
    });

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
}

document.querySelector('#blazor-error-ui .dismiss')?.addEventListener('click', () => {
    document.getElementById('blazor-error-ui').style.display = 'none';
});
