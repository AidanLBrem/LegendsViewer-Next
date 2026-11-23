# Civilization ethics

Open a civilization’s **Culture & National Character** card for its stance on slavery and 21 other practices. Select a second civilization to compare policies in both directions. A negative score represents potential friction on that issue. World history can supply other reasons for a conflict.

## Export the world's actual policies

1. Copy [export-civ-ethics.lua](../tools/dfhack/export-civ-ethics.lua) into the Dwarf Fortress installation’s `hack/scripts` directory.
2. In Legends mode, export the regular Legends files and run `export-civ-ethics` in the DFHack console on the same world date.
3. Keep the resulting `<save>-<date>-civ-ethics.tsv` beside the matching `-legends.xml` when opening or reloading the world in Legends Viewer.

Each row records the civilization ID, ethic, and response from the live entity’s `resources.ethic` array. **World export** uses these rows, including for modded civilizations. Without a matching file, **Vanilla estimate** uses the documented default for a recognized founding race. Other races show **Unknown**. The XML’s harsh-edict enactments and repeals have their own history.

Sources: [DFHack entity structures](https://github.com/DFHack/df-structures/blob/master/df.entity.xml) and [Dwarf Fortress ethics and directional reaction table](https://dwarffortresswiki.org/index.php/Ethic).
