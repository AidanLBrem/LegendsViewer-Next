using LegendsViewer.Backend.Legends.Interfaces;
using System.Text;
using System.Globalization;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using LegendsViewer.Backend.Utilities;

namespace LegendsViewer.Backend.Legends.Parser;

public class HistoryParser : IDisposable
{
    private readonly IWorld _world;
    private readonly StreamReader _history;
    private readonly StringBuilder _log;

    private string _currentLine = string.Empty;
    private Entity? _currentCiv;

    public HistoryParser(IWorld world, string historyFile)
    {
        _world = world;
        _history = new StreamReader(historyFile, Encoding.GetEncoding("windows-1252"));
        _log = new StringBuilder();
    }

    private bool CivStart()
    {
        return !_currentLine.StartsWith(" ");
    }

    private void SkipAnimalPeople()
    {
        while (!_currentLine.Contains(",") || _currentLine.StartsWith(" "))
        {
            ReadLine();
        }
    }

    private void SkipToNextCiv()
    {
        while (!_history.EndOfStream && !CivStart())
        {
            ReadLine();
        }
    }

    private void ReadLine()
    {
        _currentLine = _history.ReadLine() ?? string.Empty;
    }

    private bool ReadCiv()
    {
        string civName = _currentLine.Substring(0, _currentLine.IndexOf(",", StringComparison.Ordinal));
        CreatureInfo civRace = _world.GetCreatureInfo(_currentLine.Substring(_currentLine.IndexOf(",", StringComparison.Ordinal) + 2,
            _currentLine.Length - _currentLine.IndexOf(",", StringComparison.Ordinal) - 2).ToLower());
        var entities = _world.Entities
            .Where(entity => string.Equals(entity.Name, civName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (entities.Count == 1)
        {
            _currentCiv = entities[0];
        }
        else if (entities.Count == 0)
        {
            _world.ParsingErrors.Report($"Couldn\'t Find Entity by Name:\n{civName}");
        }
        else
        {
            var possibleEntities = entities.Where(entity => entity.Race == civRace).ToList();
            if (possibleEntities.Count == 1)
            {
                _currentCiv = possibleEntities[0];
            }
            else if (possibleEntities.Count == 0)
            {
                _world.ParsingErrors.Report($"Couldn\'t Find Entity by Name and Race:\n{civName}");
            }
            else
            {
                var possibleCivilizations = possibleEntities.Where(entity => entity.EntityType == EntityType.Civilization).ToList();
                if (possibleCivilizations.Count == 1)
                {
                    _currentCiv = possibleEntities[0];
                }
                else
                {
#if DEBUG
                    _world.ParsingErrors.Report($"Ambiguous Civilizations ({entities.Count}) Name:\n{civName}");
#endif
                }
            }
        }
        if (_currentCiv != null)
        {
            _currentCiv.Race = civRace;
            foreach (Entity group in _currentCiv.Groups)
            {
                group.Race = _currentCiv.Race;
            }

            _currentCiv.IsCiv = true;
            _currentCiv.EntityType = EntityType.Civilization;
        }
        ReadLine();
        return true;
    }

    private void ReadWorships()
    {
        if (_currentLine.Contains("Worship List"))
        {
            ReadLine();
            while (_currentLine?.StartsWith("  ") == true)
            {
                if (_currentCiv != null)
                {
                    string deityName = Formatting.InitCaps(Formatting.ReplaceNonAscii(_currentLine.Substring(2,
                        _currentLine.IndexOf(",", StringComparison.Ordinal) - 2)));
                    var deities = _world.HistoricalFigures.Where(h => h.Name.Equals(deityName.Replace("'", "`"), StringComparison.OrdinalIgnoreCase) && (h.IsDeity || h.Force)).ToList();
                    if (deities.Count == 1)
                    {
                        var deity = deities[0];
                        deity.WorshippedBy = _currentCiv;
                        if (!_currentCiv.Worshipped.Contains(deity))
                        {
                            _currentCiv.Worshipped.Add(deity);
                        }
                    }
                    else if (deities.Count == 0)
                    {
                        _world.ParsingErrors.Report($"Couldn\'t Find Deity:\n{deityName}, Deity of {_currentCiv.Name}");
                    }
#if DEBUG
                    else
                    {
                        _world.ParsingErrors.Report($"Ambiguous ({deities.Count}) Deity Name:\n{deityName}, Deity of {_currentCiv.Name}");
                    }
#endif
                }
                ReadLine();
            }
        }
    }

    private bool LeaderStart()
    {
        return !_history.EndOfStream && _currentLine.Contains(" List") && !_currentLine.Contains("[*]") && !_currentLine.Contains("%");
    }

    // History exports do not always include a comma after the closing reign
    // parenthesis. Never use an unchecked IndexOf result as a substring length.
    private static bool TryParseLeaderRecord(string line, out string leaderName, out int reignBegan)
    {
        const string leaderMarker = "[*]";
        const string reignField = "Reign Began:";
        leaderName = string.Empty;
        reignBegan = 0;

        int markerIndex = line.IndexOf(leaderMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        int nameStart = markerIndex + leaderMarker.Length;
        int birthIndex = line.IndexOf("(b", nameStart, StringComparison.Ordinal);
        if (birthIndex <= nameStart)
        {
            return false;
        }

        leaderName = Formatting.ReplaceNonAscii(line.Substring(nameStart, birthIndex - nameStart).Trim());
        int fieldIndex = line.IndexOf(reignField, birthIndex, StringComparison.OrdinalIgnoreCase);
        if (leaderName.Length == 0 || fieldIndex < 0)
        {
            return false;
        }

        int yearStart = fieldIndex + reignField.Length;
        int yearEnd = line.IndexOf(')', yearStart);
        return yearEnd > yearStart &&
            int.TryParse(line.AsSpan(yearStart, yearEnd - yearStart),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out reignBegan);
    }

    private void ReadLeaders()
    {
        while (LeaderStart())
        {
            int listIndex = _currentLine.IndexOf(" List", StringComparison.Ordinal);
            if (listIndex <= 1)
            {
                _world.ParsingErrors.Report("Skipped malformed leader list heading", _currentLine);
                ReadLine();
                continue;
            }

            string leaderTypeString = _currentLine.Substring(1, listIndex - 1).Trim();
            string leaderType = Formatting.InitCaps(leaderTypeString);
            _currentCiv?.LeaderTypes.Add(leaderType);
            _currentCiv?.Leaders.Add([]);
            ReadLine();
            while (_currentLine?.StartsWith("  ") == true)
            {
                if (_currentCiv != null && _currentLine.Contains("[*]"))
                {
                    if (!TryParseLeaderRecord(_currentLine, out string leaderName, out int reignBegan))
                    {
                        _world.ParsingErrors.Report("Skipped malformed leader entry", _currentLine);
                        ReadLine();
                        continue;
                    }

                    var leaders = _world.HistoricalFigures.Where(hf =>
                            string.Equals(hf.Name, leaderName.Replace("'", "`"), StringComparison.OrdinalIgnoreCase)).ToList();
                    if (leaders.Count == 1)
                    {
                        var leader = leaders[0];
                        var profile = _currentCiv.EntityPositions.FirstOrDefault(p =>
                            string.Equals(p.Name, leaderType, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.NameMale, leaderType, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.NameFemale, leaderType, StringComparison.OrdinalIgnoreCase));
                        int positionId = profile?.Id ?? 0;
                        var lastLeader = _currentCiv.Leaders[_currentCiv.LeaderTypes.Count - 1].LastOrDefault();
                        var previousPosition = lastLeader?.Positions?.LastOrDefault(p =>
                            p.Entity == _currentCiv &&
                            string.Equals(p.Title, leaderType, StringComparison.OrdinalIgnoreCase) &&
                            (!p.StartYear.HasValue || p.StartYear.Value <= reignBegan));
                        if (previousPosition != null &&
                            (!previousPosition.EndYear.HasValue || previousPosition.EndYear.Value > reignBegan))
                        {
                            previousPosition.EndYear = reignBegan;
                        }

                        leader.StartPositionAssignment(_currentCiv, reignBegan, positionId, leaderType);
                        _currentCiv.Leaders[_currentCiv.LeaderTypes.Count - 1].Add(leader);
                    }
                    else if (leaders.Count == 0)
                    {
                        _world.ParsingErrors.Report($"Couldn\'t Find Leader:\n{leaderName}, Leader of {_currentCiv.Name}");
                    }
#if DEBUG
                    else
                    {
                        _world.ParsingErrors.Report($"Ambiguous ({leaders.Count}) Leader Name:\n{leaderName}, Leader of {_currentCiv.Name}");
                    }
#endif
                }
                ReadLine();
            }
        }
    }

    public string Parse()
    {
        string? name = _history.ReadLine();
        _world.Name = Formatting.ReplaceNonAscii(name ?? "Unknown World");
        _world.AlternativeName = _history.ReadLine() ?? "";
        ReadLine();
        SkipAnimalPeople();

        while (!_history.EndOfStream)
        {
            if (ReadCiv())
            {
                ReadWorships();
                ReadLeaders();
            }
            else
            {
                SkipToNextCiv();
            }
        }
        if (!string.IsNullOrEmpty(_currentLine) && CivStart())
        {
            ReadCiv();
        }

        _history.Close();

        return _log.ToString();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _history.Dispose();
        }
    }
}

