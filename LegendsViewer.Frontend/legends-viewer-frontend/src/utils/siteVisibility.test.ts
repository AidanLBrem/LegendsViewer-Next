import { describe, expect, it } from 'vitest';
import { isSiteTypeVisible } from './siteVisibility';

describe('site type visibility', () => {
  const allVisible = { showMonasteries: true, showCamps: true };

  it('shows all site types by default', () => {
    expect(isSiteTypeVisible('Monastery', allVisible)).toBe(true);
    expect(isSiteTypeVisible('Camp', allVisible)).toBe(true);
    expect(isSiteTypeVisible('Tower', allVisible)).toBe(true);
  });

  it('hides monasteries without hiding camps or necromancer towers', () => {
    const options = { showMonasteries: false, showCamps: true };
    expect(isSiteTypeVisible('Monastery', options)).toBe(false);
    expect(isSiteTypeVisible('Camp', options)).toBe(true);
    expect(isSiteTypeVisible('Tower', options)).toBe(true);
  });

  it('hides camps without hiding monasteries', () => {
    const options = { showMonasteries: true, showCamps: false };
    expect(isSiteTypeVisible('Camp', options)).toBe(false);
    expect(isSiteTypeVisible('Monastery', options)).toBe(true);
    expect(isSiteTypeVisible('Town', options)).toBe(true);
  });

  it('can hide both while leaving other and unknown types visible', () => {
    const options = { showMonasteries: false, showCamps: false };
    expect(isSiteTypeVisible('Monastery', options)).toBe(false);
    expect(isSiteTypeVisible('Camp', options)).toBe(false);
    expect(isSiteTypeVisible('Fortress', options)).toBe(true);
    expect(isSiteTypeVisible(undefined, options)).toBe(true);
  });
});
