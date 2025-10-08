class ResizeObserver {
    callback: ResizeObserverCallback;

    constructor(callback: ResizeObserverCallback) {
        this.callback = callback;
    }

    observe() {}
    unobserve() {}
    disconnect() {}
}

(globalThis as any).ResizeObserver = ResizeObserver;
