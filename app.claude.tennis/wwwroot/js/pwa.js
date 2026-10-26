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

// A new step of a flow starts at the top of the page, not wherever the previous step was scrolled to.
window.socialTennis = {
    scrollToTop() {
        document.querySelector('.app-content')?.scrollTo(0, 0);
    },
};
