// Site-type visibility is shared by map rendering, history snapshots,
// tile selection and the count badges.
export interface SiteVisibilityOptions {
  showMonasteries: boolean;
  showCamps: boolean;
  showCaves: boolean;
}

export function isSiteTypeVisible(
  type: string | null | undefined,
  options: SiteVisibilityOptions
): boolean {
  if (type === 'Monastery') return options.showMonasteries;
  if (type === 'Camp') return options.showCamps;
  if (type === 'Cave') return options.showCaves;
  return true;
}
