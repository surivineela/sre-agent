class ResizeObserver {
    callback: ResizeObserverCallback;

    constructor(callback: ResizeObserverCallback) {
        this.callback = callback;
    }

    observe() {}
    unobserve() {}
    disconnect() {}
}

class IntersectionObserver {
    private callback: IntersectionObserverCallback;

    root: Element | Document | null = null;
    rootMargin: string = '0px';
    thresholds: ReadonlyArray<number> = [0];

    constructor(callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
        this.callback = callback;
        this.root = options?.root || null;
        this.rootMargin = options?.rootMargin || '0px';
        this.thresholds = options?.threshold ? (Array.isArray(options.threshold) ? options.threshold : [options.threshold]) : [0];
    }

    observe() {}
    unobserve() {}
    disconnect() {}

    takeRecords(): IntersectionObserverEntry[] {
        return [];
    }

    triggerIntersection(entries: Partial<IntersectionObserverEntry>[]) {
        const fullEntries = entries.map(entry => ({
            target: entry.target || document.createElement('div'),
            isIntersecting: entry.isIntersecting || false,
            intersectionRatio: entry.intersectionRatio || 0,
            intersectionRect: entry.intersectionRect || new DOMRect(),
            boundingClientRect: entry.boundingClientRect || new DOMRect(),
            rootBounds: entry.rootBounds || new DOMRect(),
            time: entry.time || Date.now(),
            ...entry,
        })) as IntersectionObserverEntry[];

        this.callback(fullEntries, this as unknown as IntersectionObserver);
    }
}

(globalThis as any).ResizeObserver = ResizeObserver;
(globalThis as any).IntersectionObserver = IntersectionObserver;

Object.defineProperty(window, 'CSS', {
    value: {
        supports: (property: string, value?: string) => {
            const supportedProperties = ['display: flex', 'gap', 'grid-template-columns', 'container-queries'];

            const query = value ? `${property}: ${value}` : property;
            return supportedProperties.some(prop => query.includes(prop));
        },
        escape: (value: string) => value,
    },
    writable: true,
    configurable: true,
});
