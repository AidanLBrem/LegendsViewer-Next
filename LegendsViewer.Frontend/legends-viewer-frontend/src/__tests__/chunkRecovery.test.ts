import { describe, expect, it, vi } from 'vitest';
import {
  clearSuccessfulChunkRetry,
  isLazyChunkLoadFailure,
  retryLazyChunkOnce,
} from '../utils/chunkRecovery';

function fakeStorage() {
  const records = new Map<string, string>();
  return {
    getItem: (key: string) => records.get(key) ?? null,
    setItem: (key: string, value: string) => { records.set(key, value); },
    removeItem: (key: string) => { records.delete(key); },
  };
}

describe('lazy route chunk recovery', () => {
  it('recognizes browser dynamic imports and Vite CSS preload errors', () => {
    expect(isLazyChunkLoadFailure(new TypeError(
      'error loading dynamically imported module: http://localhost:15422/assets/Entities-DYpFbTDq.js'
    ))).toBe(true);
    expect(isLazyChunkLoadFailure(new Error(
      'Unable to preload CSS for /assets/Entities-C4Vwu0Kh.css'
    ))).toBe(true);
    expect(isLazyChunkLoadFailure(new Error('Cannot read properties of undefined'))).toBe(false);
  });

  it('reloads only once per failed route to avoid an infinite refresh loop', () => {
    const storage = fakeStorage();
    const reload = vi.fn();
    const error = new TypeError(
      'error loading dynamically imported module: http://localhost:15422/assets/Entities-DYpFbTDq.js'
    );

    expect(retryLazyChunkOnce(error, '/entity', storage, reload)).toBe(true);
    expect(retryLazyChunkOnce(error, '/entity', storage, reload)).toBe(false);
    expect(reload).toHaveBeenCalledTimes(1);

    // A successful later navigation allows another recovery if a new
    // frontend version replaces the hashed chunks during the same tab.
    clearSuccessfulChunkRetry('/entity', storage);
    expect(retryLazyChunkOnce(error, '/entity', storage, reload)).toBe(true);
    expect(reload).toHaveBeenCalledTimes(2);
  });

  it('does not reload on unrelated errors or when session storage is restricted', () => {
    const storage = fakeStorage();
    const reload = vi.fn();
    expect(retryLazyChunkOnce(new Error('Bad response'), '/hf', storage, reload)).toBe(false);

    const blockedStorage = {
      getItem: () => { throw new Error('Storage blocked'); },
      setItem: vi.fn(),
      removeItem: vi.fn(),
    };
    expect(retryLazyChunkOnce(
      new TypeError('Failed to fetch dynamically imported module'),
      '/hf', blockedStorage, reload
    )).toBe(false);
    expect(reload).not.toHaveBeenCalled();
  });

  it('tracks retries separately for different routes', () => {
    const storage = fakeStorage();
    const reload = vi.fn();
    const error = new Error('Importing a module script failed.');
    expect(retryLazyChunkOnce(error, '/entity', storage, reload)).toBe(true);
    expect(retryLazyChunkOnce(error, '/hf', storage, reload)).toBe(true);
    expect(reload).toHaveBeenCalledTimes(2);
  });
});
