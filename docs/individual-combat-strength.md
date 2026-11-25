# Individual combat strength: evidence and estimate

## Verified on DF 53.17 Windows x64

Executable SHA-256: `3436f0553d6fa17b25716f8b1f034b52e3d9dc30be7f3ddac67cd0ff731d7b16`.
Historical-figure routine: `0x140BDC3A0` through returns ending at `0x140BDCB05`.
These addresses are build-specific. The executable is not included.

`tools/verify-historical-figure-strength.py` executes the original bytes in Unicorn with synthetic structures and a cached caste rating. Run with Python, `pefile==2024.8.26`, `unicorn==2.1.4`, and the matching executable path. **1,250 checks passed**: all skill IDs 0–149 individually, seeded combinations, threshold/cap boundaries, absent/generic inventory, quality, the two profession-ID divisors, and 200 specific-item inventory cases. This is isolated native-code execution, not a live-game battle test.

The dispatch tables at `0x140BDCB08` and `0x140BDCB14` route these skill IDs:

| Group | IDs / tokens |
|---|---|
| Material | 37–45: AXE, SWORD, DAGGER, MACE, HAMMER, SPEAR, CROSSBOW, SHIELD, ARMOR; 49–52: PIKE, WHIP, BOW, BLOWGUN |
| Defensive | 86 DISCIPLINE, 87 SITUATIONAL_AWARENESS, 99 WRESTLING, 100 BITE, 101 GRASP_STRIKE, 102 STANCE_STRIKE, 103 DODGING |

Other skills, including MINING, THROW and general melee/ranged combat, do not enter these two sums in this routine. Token names were matched against the DFHack structure definitions from the preceding investigation; the executable tests independently verify the numeric dispatch. The application's `Skill.Points` already reads `total_ip`. No conversion from displayed rank or extra residual-XP addition is needed.

For nonnegative accumulated points `p`, the integer factor is:

`F(p) = min(80, 40 + floor(p / 500))`

This yields 40 untrained, 76 at 18,000 IP (displayed Legendary), and 80 at 20,000 IP. Further points do not increase that group's factor. Material skills sum at figure offset `+0x170`, defensive skills at `+0x174`, except that specific-item inventory routes material skills into separate inventory slots.

For generic inventory, the verified subset is:

`floor((caste + floor(materialBonus * F(materialIP) / 40)) * F(defensiveIP) / 40)`

Material scaling is at `0x140BDC9E1`; defensive scaling at `0x140BDCA6E`. Generic quality values 1–5 apply `floor(max(10, materialBonus) * (5 + quality) / 5)` first. Profession IDs 103 and 104 then divide the result by 10 and 100 respectively. The no-inventory/no-entity fixture has no material bonus, even with weapon XP. Weapon XP therefore does **not** universally multiply body strength.

The specific-item branch (`0x140BDC73D`–`0x140BDC979`) selects the maximum of the eleven weapon-slot contributions and adds shield and armor contributions. Each slot contributes `floor(slotMaterialStrength * F(slotIP) / 40)`; the defensive factor then scales the combined caste/equipment result. The synthetic checks verify this path too. Unlike generic inventory, weapon IP is not summed across slots here. Entity fallback, the age-dependent caste call, and special profession semantics remain unvalidated here. Neither inventory branch is implemented as native-equivalent behavior in the viewer because the required inventory data is missing.

## Application approximation

Legends exports do not provide all equipment inputs or a history of skills at each battle. The viewer deliberately retains the existing caste baseline, adds only an estimated equipment-training increment, then applies the defensive factor:

`trainingIncrement = floor(10 * F(materialIP) / 40) - 10`

`estimatedStrength = floor((casteBaseline + trainingIncrement) * F(defensiveIP) / 40)`

**10 is an assumed equipment bonus, not a recovered universal default.** Subtracting its untrained contribution preserves existing untrained/no-skill ratings. This differs from the native absolute rating by construction. No actual weapons, armor, material quality, special profession or age effects are claimed. Duplicate skill tokens use their maximum points; negative points are ignored; wide sums avoid overflow. These import safeguards are also application behavior, not claims about native overflow handling.

For a baseline of 120: no skills = 120; 18,000 axe IP = 129; 18,000 axe plus 18,000 dodging IP = 245; both groups capped = 260. A legendary weapon user alone need not exceed a large monster. Defensive training has the much larger effect in this model.

Only named, ordinary figures with a known base rating receive adjustments. Anonymous squads retain the existing caste/race calculation. Unknown ratings keep the 1,200 placeholder; transformed and undead figures retain existing fallbacks. Both armies still switch together from native caste ratings to body-size estimates when native coverage is incomplete. Historical battles now scale accumulated skill-group IP linearly from assumed adulthood (12 years for dwarf, human, elf, goblin and kobold) to death or the latest recorded world year, before applying native factor caps. Unknown races or dates receive no historical skill bonus. These ages are modeling defaults, not recovered caste maturity data; the current world year is a history-derived proxy for export date. Hall of Fame retains export-time IP. The estimate uses later career information and is not a historical snapshot. Missing skills mean no known bonus, not proven absence of training.

Battle and commander basis strings append `-skill-estimate` when eligible named figures have combat points; this describes the model, not data completeness. Existing rating coverage continues to measure caste coverage. Commanders consume the same battle totals, so matchup categories and command scores incorporate individual estimates without treating a commander's combat skill as a separate leadership multiplier.

Hall of Fame adds a native-base individual estimate ranking (known native bases only, excluding transformed/undead figures) and a combat-IP ranking independent of body size. The IP ranking is raw experience, not native combat power, and can grow above the native factor cap. Existing caste-only rankings remain available. No deployment configuration changes are required.
