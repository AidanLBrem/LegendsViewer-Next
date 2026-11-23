/**
 * A tab can retain an older Vite entry bundle after a frontend update.
 * Its lazy-import URLs then refer to chunks from the previous build.
 * Recover once per route, without changing how assets are built or served.
 */
type RetryStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

const retryPrefix = 'legendsViewer:lazy-chunk-retry:';
const failedChunkPattern =
  /(?:failed to fetch dynamically imported module|error loading dynamically imported module|importing a module script failed|failed to load module script|unable to preload css|loading chunk [^ ]+ failed|chunkloaderror)/i;

function failureMessage(error: unknown): string {
  if (typeof error === 'string') return error;
  if (error instanceof Error) return error.message;
  if (error && typeof error === 'object' && 'message' in error) {
    const value = (error as { message?: unknown }).message;
    if (typeof value === 'string') return value;
  }
  return '';
}

export function isLazyChunkLoadFailure(error: unknown): boolean {
  return failedChunkPattern.test(failureMessage(error));
}

/**
 * Return true only when a refresh was actually initiated.
 * Persist the attempt BEFORE reloading, otherwise a genuinely missing
 * chunk would cause an endless refresh loop.
 */
export function retryLazyChunkOnce(
  error: unknown,
  routePath: string,
  storage: RetryStorage,
  reload: () => void,
): boolean {
  if (!isLazyChunkLoadFailure(error)) return false;
  const key = retryPrefix + routePath;
  try {
    if (storage.getItem(key) === '1') return false;
    storage.setItem(key, '1');
  } catch {
    // Some browsers restrict storage. Avoid an unbounded retry in that case.
    return false;
  }
  reload();
  return true;
}

export function clearSuccessfulChunkRetry(routePath: string, storage: RetryStorage): void {
  try {
    storage.removeItem(retryPrefix + routePath);
  } catch {
    // A disabled storage feature should not affect routing.
  }
}
