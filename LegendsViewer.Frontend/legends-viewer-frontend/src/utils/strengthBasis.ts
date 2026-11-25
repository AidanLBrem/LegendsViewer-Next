export function strengthBasisLabel(basis?: string | null): string {
  if (!basis) return 'Unavailable';
  const source = basis.replace('-skill-estimate', '');
  const label = source.startsWith('native') ? 'Native caste ratings' :
    source.startsWith('body-size') ? 'Adult body-size estimate' :
    source === 'numerical' ? 'Numerical fallback' : 'Unavailable';
  return label + (source.includes('placeholders') ? ' + placeholders' : '') +
    (source.includes('undead') ? ' (undead modifiers unknown)' : '') +
    (basis.includes('skill-estimate') ? ' + individual skill estimate' : '');
}

export const skillEstimateExplanation = 'Named combatants use exported skill points with verified native skill factors and an assumed 10-point equipment bonus for the training increment only. Equipment, age and profession effects are not reconstructed. Skills reflect export time, not the time of each battle; missing skills add no bonus. Anonymous squads retain caste ratings. Undead, transformed and unknown-rating combatants receive no skill adjustment. Rating coverage measures caste data, not skill completeness. These are strength estimates, not victory probabilities.';
