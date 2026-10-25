// Drag-to-reorder for the schedule cards. Pointer events are used because HTML drag and drop does not work with a finger on iOS.
// The page only reports the move; Blazor re-renders the new order.
export function attach(root, dotNet) {
    let drag = null;

    const itemSelector = kind => (kind === 'round' ? '.round-card' : '.game-row');

    const report = (kind, round, from, to) => {
        if (to !== from) {
            dotNet.invokeMethodAsync('Reorder', kind, round, from, to);
        }
    };

    const onDown = event => {
        const handle = event.target.closest('[data-drag]');
        if (!handle || (event.pointerType === 'mouse' && event.button !== 0)) {
            return;
        }

        event.preventDefault();
        const kind = handle.dataset.drag;
        const item = handle.closest(itemSelector(kind));
        const items = [...item.parentElement.children].filter(child => child.matches(itemSelector(kind)));
        const from = items.indexOf(item);
        drag = {
            kind,
            item,
            handle,
            from,
            to: from,
            pointerId: event.pointerId,
            startY: event.clientY,
            round: kind === 'game' ? Number(item.closest('.round-card').dataset.index) : -1,
            centres: items.map(child => {
                const box = child.getBoundingClientRect();
                return box.top + box.height / 2;
            }),
        };
        handle.setPointerCapture(event.pointerId);
        item.classList.add('dragging');
    };

    const onMove = event => {
        if (!drag || event.pointerId !== drag.pointerId) {
            return;
        }

        const offset = event.clientY - drag.startY;
        drag.item.style.transform = `translateY(${offset}px)`;
        const position = drag.centres[drag.from] + offset;
        let nearest = 0;
        drag.centres.forEach((centre, index) => {
            if (Math.abs(centre - position) < Math.abs(drag.centres[nearest] - position)) {
                nearest = index;
            }
        });
        drag.to = nearest;
    };

    const onUp = event => {
        if (!drag || event.pointerId !== drag.pointerId) {
            return;
        }

        const finished = drag;
        drag = null;
        finished.item.style.transform = '';
        finished.item.classList.remove('dragging');
        if (event.type === 'pointerup') {
            report(finished.kind, finished.round, finished.from, finished.to);
        }
    };

    const onKey = event => {
        const handle = event.target.closest('[data-drag]');
        const step = event.key === 'ArrowUp' ? -1 : event.key === 'ArrowDown' ? 1 : 0;
        if (!handle || step === 0) {
            return;
        }

        event.preventDefault();
        const kind = handle.dataset.drag;
        const item = handle.closest(itemSelector(kind));
        const items = [...item.parentElement.children].filter(child => child.matches(itemSelector(kind)));
        const from = items.indexOf(item);
        const round = kind === 'game' ? Number(item.closest('.round-card').dataset.index) : -1;
        report(kind, round, from, from + step);
    };

    root.addEventListener('pointerdown', onDown);
    root.addEventListener('pointermove', onMove);
    root.addEventListener('pointerup', onUp);
    root.addEventListener('pointercancel', onUp);
    root.addEventListener('keydown', onKey);

    return {
        dispose() {
            root.removeEventListener('pointerdown', onDown);
            root.removeEventListener('pointermove', onMove);
            root.removeEventListener('pointerup', onUp);
            root.removeEventListener('pointercancel', onUp);
            root.removeEventListener('keydown', onKey);
        },
    };
}
