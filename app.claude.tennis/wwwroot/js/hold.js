// Press-and-hold on a player's name. Pointer events are used so a finger works on iOS; the page only hears that a hold happened.
// Enter on a focused name does the same, for keyboards. A hold swallows the click that follows it, so it never also counts as a tap.
export function attach(root, dotNet, delay) {
    const moveTolerance = 10;
    let press = null;
    let swallowClick = false;

    const target = event => event.target.closest('[data-player]');

    const fire = element => dotNet.invokeMethodAsync('PlayerHeld', Number(element.dataset.round), element.dataset.player);

    const cancel = () => {
        if (press) {
            clearTimeout(press.timer);
            press = null;
        }
    };

    const onDown = event => {
        const element = target(event);
        if (!element || (event.pointerType === 'mouse' && event.button !== 0)) {
            return;
        }

        cancel();
        press = {
            id: event.pointerId,
            x: event.clientX,
            y: event.clientY,
            timer: setTimeout(() => {
                press = null;
                swallowClick = true;
                document.addEventListener('pointerup', endHold, { once: true, capture: true });
                fire(element);
            }, delay),
        };
    };

    const onMove = event => {
        if (press && event.pointerId === press.id && Math.hypot(event.clientX - press.x, event.clientY - press.y) > moveTolerance) {
            cancel();
        }
    };

    const onEnd = event => {
        if (press && event.pointerId === press.id) {
            cancel();
        }
    };

    // The click that ends a hold arrives straight after the pointer is released, but the dialog opens under the pointer, so the
    // release may land outside this list. Listening on the document makes sure the flag never waits for the next tap.
    const endHold = () => {
        setTimeout(() => {
            swallowClick = false;
        }, 50);
    };

    const onClick = event => {
        if (swallowClick && target(event)) {
            event.stopPropagation();
            event.preventDefault();
        }

        swallowClick = false;
    };

    const onKey = event => {
        const element = target(event);
        // While a swap is waiting for its second player, Enter is a normal tap.
        if (element && event.key === 'Enter' && root.dataset.swapping !== 'true') {
            event.preventDefault();
            fire(element);
        }
    };

    const onContextMenu = event => {
        if (target(event)) {
            event.preventDefault();
        }
    };

    root.addEventListener('pointerdown', onDown);
    root.addEventListener('pointermove', onMove);
    root.addEventListener('pointerup', onEnd);
    root.addEventListener('pointercancel', onEnd);
    root.addEventListener('click', onClick, true);
    root.addEventListener('keydown', onKey);
    root.addEventListener('contextmenu', onContextMenu);

    return {
        dispose() {
            cancel();
            root.removeEventListener('pointerdown', onDown);
            root.removeEventListener('pointermove', onMove);
            root.removeEventListener('pointerup', onEnd);
            root.removeEventListener('pointercancel', onEnd);
            root.removeEventListener('click', onClick, true);
            root.removeEventListener('keydown', onKey);
            root.removeEventListener('contextmenu', onContextMenu);
        },
    };
}
