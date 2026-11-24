-- Run in DFHack with a loaded world:
-- export-creature-stats "C:/Exports/region1-00100-01-01-creature-stats.json"
local json = require('json')
local args = {...}
if not dfhack.isWorldLoaded() then qerror('No world loaded') end
local path = args[1]
if not path or not path:match('%-creature%-stats%.json$') then
    qerror('Provide the exact Legends export prefix followed by -creature-stats.json')
end
local result = {schema_version = 1, game_version = dfhack.getDFVersion(), creatures = {}}
for raceIndex, creature in ipairs(df.global.world.raws.creatures.all) do
    local entry = {creature_id = creature.creature_id, race_index = raceIndex, castes = {}}
    for casteIndex, caste in ipairs(creature.caste) do
        table.insert(entry.castes, {
            caste_id = caste.caste_id, caste_index = casteIndex,
            description = caste.description,
            adult_body_size = caste.misc.adult_size,
            native_army_strength = caste.misc.army_strength
        })
    end
    table.insert(result.creatures, entry)
end
local file, err = io.open(path, 'w')
if not file then qerror(tostring(err)) end
file:write(json.encode(result))
file:close()
print(('Exported %d creature raws to %s'):format(#result.creatures, path))
