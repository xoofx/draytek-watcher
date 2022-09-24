namespace DrayTekWatcher.Core.DrayTek;

using System.Text.RegularExpressions;

public class DrayTekCliParser
{
    private static readonly Regex RegexWanStatusStart = new Regex(@"^(\w*):\s+(\w+),");

    private static bool IsMore(string line) => line.StartsWith("--- MORE ---");

    public static List<WANInfo> ParseWanStatus(string wanStatusText)
    {
        var reader = new StringReader(wanStatusText);
        string? line;

        var list = new List<WANInfo>();
        WANInfo? currentStatus = null;
        while ((line = reader.ReadLine()) != null)
        {
            if (IsMore(line) || string.IsNullOrWhiteSpace(line)) continue;

            var match = RegexWanStatusStart.Match(line);
            if (match.Success)
            {
                // Create the status
                currentStatus = null;
            }

            line = line.Trim();

            var keyValues = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var keyValue in keyValues)
            {
                var keyValueSplit = keyValue.Split(new[] { ':', '=' });
                if (keyValueSplit.Length == 2)
                {
                    var key = keyValueSplit[0].Trim();
                    var value = keyValueSplit[1].Trim();

                    if (currentStatus == null)
                    {
                        currentStatus = new WANInfo(key)
                        {
                            Status = string.Compare("offline", value, StringComparison.InvariantCultureIgnoreCase) == 0
                                ? WANStatus.Offline
                                : string.Compare("online", value, StringComparison.InvariantCultureIgnoreCase) == 0
                                    ? WANStatus.Online
                                    : WANStatus.Undefined
                        };
                        list.Add(currentStatus);
                    }
                    else
                    {
                        bool processed = true;
                        switch (key)
                        {
                            case "IP":
                                currentStatus.IPAddress = value;
                                break;
                            case "GW IP":
                                currentStatus.GatewayIPAddress = value;
                                break;
                            default:
                                processed = false;
                                break;
                        }

                        if (!processed)
                        {
                            currentStatus.Add(key, value);
                        }
                    }
                }
            }
        }

        return list;
    }
}