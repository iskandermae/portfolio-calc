using System.Text;
using PortfolioCalc.Core.Import.T212;

namespace PortfolioCalc.Core.Data.Import.T212;

/// <summary>Parses a Trading 212 "Transaction history" CSV export into raw rows, keyed by
/// the file's own header row so the parser never needs updating when a new column is
/// mapped. Handles RFC4180-style quoting (commas/quotes inside a quoted field) but not an
/// embedded newline inside a quoted field — no sample row needs that.</summary>
public class T212CsvImporter : IT212TransactionImporter
{
    public async Task<T212ImportData> ParseAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        // leaveOpen: true — the caller (the GUI's InputFile stream, or a test's own
        // stream) owns the stream's lifetime, same as IbkrFlexQueryImporter never closing
        // the stream it's handed.
        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: -1, leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidDataException("Not a recognizable Trading 212 export: file is empty.");
        var headers = ParseCsvLine(headerLine);

        var rows = new List<T212RawRow>();
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length == 0)
                continue;

            var values = ParseCsvLine(line);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Count && i < values.Count; i++)
                fields[headers[i]] = values[i];

            rows.Add(new T212RawRow(fields));
        }

        return new T212ImportData { Rows = rows };
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());

        return fields;
    }
}
