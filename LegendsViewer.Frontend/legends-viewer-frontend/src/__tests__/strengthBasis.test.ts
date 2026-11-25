import { describe, expect, it } from 'vitest';
import { strengthBasisLabel } from '../utils/strengthBasis';

describe('strength basis disclosure', () => {
  it('labels the native base separately from the skill approximation', () => {
    expect(strengthBasisLabel('native')).toBe('Native caste ratings');
    expect(strengthBasisLabel('native-skill-estimate')).toBe('Native caste ratings + individual skill estimate');
  });
  it('retains placeholder and undead caveats alongside skill estimates', () => {
    expect(strengthBasisLabel('body-size-undead-placeholders-skill-estimate')).toBe(
      'Adult body-size estimate + placeholders (undead modifiers unknown) + individual skill estimate');
    expect(strengthBasisLabel(null)).toBe('Unavailable');
  });
});
