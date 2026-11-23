import { describe, expect, it, vi } from 'vitest';
import {
  isRouteAssetLoadError,
  retryRouteAssetOnce,
  ROUTE_ASSET_RETRY_KEY,
} from '../utils/routeAssetRecovery';

function memoryStorage(): Pick<Storage, 'getItem' | 'setItem'> {
  const values = new Map<string, string>();
  return {
    getItem(key: string) { return values.get(key) ?? null; },
    setItem(key: string, value: string) { values.set(key, value); },
  };
}

describe('route asset recovery', () => {
  it('recognizes missing dynamic-import JS and CSS chunks', () => {
    expect(isRouteAssetLoadError(new TypeError(
      'error loading dynamically imported module: http://localhost:15422/assets/Entities-old.js'
    ))).toBe(true);
    expect(isRouteAssetLoadError('Failed to fetch dynamically imported module')).toBe(true);
    expect(isRouteAssetLoadError(new Error('Unable to preload CSS for /assets/Entities-old.css'))).toBe(true);
    expect(isRouteAssetLoadError(new Error('Importing a module script failed.'))).toBe(true);
    expect(isRouteAssetLoadError(new Error('A component threw while rendering'))).toBe(false);
  });

  it('reloads at most once when a chunk stays unavailable', () => {
    const storage = memoryStorage();
    const reload = vi.fn();
    expect(retryRouteAssetOnce(storage, reload)).toBe(true);
    expect(storage.getItem(ROUTE_ASSET_RETRY_KEY)).toBe('1');
    expect(retryRouteAssetOnce(storage, reload)).toBe(false);
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('can reload again after successful navigation clears the retry flag', () => {
    const values = new Map<string, string>();
    const storage = {
      getItem(key: string) { return values.get(key) ?? null; },
      setItem(key: string, value: string) { values.set(key, value); },
      removeItem(key: string) { values.delete(key); },
    };
    const reload = vi.fn();
    expect(retryRouteAssetOnce(storage, reload)).toBe(true);
    storage.removeItem(ROUTE_ASSET_RETRY_KEY);
    expect(retryRouteAssetOnce(storage, reload)).toBe(true);
    expect(reload).toHaveBeenCalledTimes(2);
  });
});
