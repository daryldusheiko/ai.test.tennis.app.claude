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

    // 'light' and 'dark' override the phone's setting; anything else follows it. index.html applies the stored choice before the page is drawn.
    applyTheme(choice) {
        if (choice === 'light' || choice === 'dark') {
            document.documentElement.setAttribute('data-theme', choice);
        } else {
            document.documentElement.removeAttribute('data-theme');
        }
    },

    // Keeps the squiggle layer behind a text box scrolled with it.
    linkScroll(textArea, mirror) {
        textArea.addEventListener('scroll', () => {
            mirror.scrollTop = textArea.scrollTop;
        });
    },
};
