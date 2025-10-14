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

    // Required properties from the native IntersectionObserver interface
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

    // Helper method to trigger intersection changes in tests
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

// Alternative more comprehensive mock
Object.defineProperty(window, 'CSS', {
    value: {
        supports: (property: string, value?: string) => {
            // Mock common CSS properties that your app uses
            const supportedProperties = [
                'display: flex',
                'gap',
                'grid-template-columns',
                'container-queries',
                // Add other properties your components check for
            ];

            const query = value ? `${property}: ${value}` : property;
            return supportedProperties.some(prop => query.includes(prop));
        },
        escape: (value: string) => value,
        // Add other CSS methods your code might use
    },
    writable: true,
    configurable: true,
});
