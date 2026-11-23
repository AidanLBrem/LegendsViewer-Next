-- Export live civilization ethics alongside Legends XML for Legends Viewer.
if not dfhack.isWorldLoaded() then qerror('Load the world before exporting ethics.') end
local world = df.global.world
local filename = ('%s-%05d-%02d-%02d-civ-ethics.tsv'):format(
    world.cur_savegame.save_dir, df.global.cur_year,
    dfhack.world.ReadCurrentMonth() + 1, dfhack.world.ReadCurrentDay())
local rows = {'entity_id\tethic\tresponse\n'}
for _, civ in ipairs(world.entities.all) do
    if civ.type == df.historical_entity_type.Civilization then
        for index = 0, df.ethic_type._last_item do
            local response = df.ethic_response[civ.resources.ethic[index]]
            if response then
                rows[#rows + 1] = ('%d\t%s\t%s\n'):format(civ.id, df.ethic_type[index], response)
            end
        end
    end
end
local file = assert(io.open(filename, 'wb'))
assert(file:write(table.concat(rows)))
assert(file:close())
print('Wrote ' .. filename)
