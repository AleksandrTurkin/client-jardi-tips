const listeners = new Map();

export function getIsOnline() {
    return navigator.onLine;
}

export function subscribe(dotNetReference) {
    if (listeners.has(dotNetReference)) {
        return getIsOnline();
    }

    const onOnline = () => notify(dotNetReference, true);
    const onOffline = () => notify(dotNetReference, false);

    window.addEventListener('online', onOnline);
    window.addEventListener('offline', onOffline);
    listeners.set(dotNetReference, { onOnline, onOffline });

    return getIsOnline();
}

export function unsubscribe(dotNetReference) {
    const entry = listeners.get(dotNetReference);
    if (!entry) {
        return;
    }

    window.removeEventListener('online', entry.onOnline);
    window.removeEventListener('offline', entry.onOffline);
    listeners.delete(dotNetReference);
}

function notify(dotNetReference, isOnline) {
    dotNetReference.invokeMethodAsync('OnConnectivityChanged', isOnline);
}
