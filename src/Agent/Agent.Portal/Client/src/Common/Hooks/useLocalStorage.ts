import { useCallback, useEffect, useState } from 'react';

const readFromStorage = <T>(key: string, defaultValue: T): T => {
    try {
        const item = localStorage.getItem(key);
        return item ? (JSON.parse(item) as T) : defaultValue;
    } catch (error) {
        console.warn(`Error reading localStorage key "${key}":`, error);
        return defaultValue;
    }
};

/**
 * Generic hook for persisting state in localStorage with type safety.
 * Automatically handles JSON serialization/deserialization and syncs across tabs.
 *
 * @template T - The type of value to store
 * @param key - localStorage key
 * @param defaultValue - Default value if key doesn't exist
 * @returns Object with value and setValue
 */
export const useLocalStorage = <T>(key: string, defaultValue: T) => {
    const [value, setValue] = useState<T>(defaultValue);

    useEffect(() => {
        setValue(readFromStorage(key, defaultValue));
    }, [key, defaultValue]);

    const setStoredValue = useCallback(
        (newValue: T | ((prev: T) => T)) => {
            try {
                const valueToStore = newValue instanceof Function ? newValue(value) : newValue;

                setValue(valueToStore);
                localStorage.setItem(key, JSON.stringify(valueToStore));

                window.dispatchEvent(
                    new StorageEvent('storage', {
                        key,
                        newValue: JSON.stringify(valueToStore),
                        storageArea: localStorage,
                    })
                );
            } catch (error) {
                console.error(`Error setting localStorage key "${key}":`, error);
            }
        },
        [key, value]
    );

    useEffect(() => {
        const handleStorageChange = (e: StorageEvent) => {
            if (e.key === key && e.storageArea === localStorage) {
                try {
                    const newValue = e.newValue ? (JSON.parse(e.newValue) as T) : defaultValue;
                    setValue(newValue);
                } catch (error) {
                    console.warn(`Error parsing storage event for key "${key}":`, error);
                }
            }
        };

        window.addEventListener('storage', handleStorageChange);
        return () => window.removeEventListener('storage', handleStorageChange);
    }, [key, defaultValue]);

    return { value, setValue: setStoredValue };
};
