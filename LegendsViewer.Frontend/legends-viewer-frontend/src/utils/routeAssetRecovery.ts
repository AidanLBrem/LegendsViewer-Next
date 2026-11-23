/**
 * Vite hashes lazy-loaded route chunks. A browser tab left open across a
 * frontend update may still reference chunks that no longer exist.
 * Recover once with a fresh page load; never enter a reload loop.
 */
export const ROUTE_ASSET_RETRY_KEY = 'legends-viewer:route-asset-retry';

export function isRouteAssetLoadError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '');
  return /failed to fetch dynamically imported module|error loading dynamically imported module|importing a module script failed|unable to preload css|loading (?:css )?chunk .+ failed/i.test(message);
}

export function retryRouteAssetOnce(
  storage: Pick<Storage, 'getItem' | 'setItem'>,
  reload: () => void
): boolean {
  if (storage.getItem(ROUTE_ASSET_RETRY_KEY) === '1') return false;
  storage.setItem(ROUTE_ASSET_RETRY_KEY, '1');
  reload();
  return true;
}
