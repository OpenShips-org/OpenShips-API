using System.Text.RegularExpressions;
using OpenShipsAPI.Domain.Destination;

namespace OpenShipsAPI.Infrastructure.Streams.Common;

public static class DestinationParser
{
    private static readonly string[] RoundTripSeparators =
    [
        "<->",
        "<>"
    ];
    
    private static readonly string[] DirectedSeparators =
    [
        ">>>",
        ">>",
        "=>",
        ">"
    ];
    
    public static ShipDestination? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var raw = Normalize(value);

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Round trip
        foreach (var separator in RoundTripSeparators)
        {
            if (!raw.Contains(separator, StringComparison.Ordinal))
                continue;

            var parts = raw.Split(
                separator,
                2,
                StringSplitOptions.TrimEntries
            );

            if (parts.Length != 2 ||
                string.IsNullOrWhiteSpace(parts[0]) ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                return null;
            }

            return new ShipDestination
            {
                Raw = raw,
                RouteType = DestinationType.RoundTrip,
                From = parts[0],
                To = parts[1]
            };
        }

        if (TryParseUnLocodeRoute(raw, out var from2, out var to2))
        {
            return new ShipDestination
            {
                Raw = raw,
                RouteType = DestinationType.Directed,
                From = from2,
                To = to2
            };
        }


        // Directed route
        foreach (var separator in DirectedSeparators)
        {
            if (!raw.Contains(separator, StringComparison.Ordinal))
                continue;

            var parts = raw.Split(
                separator,
                2,
                StringSplitOptions.TrimEntries
            );

            var from = string.IsNullOrWhiteSpace(parts[0])
                ? null
                : parts[0];

            var to = string.IsNullOrWhiteSpace(parts[1])
                ? null
                : parts[1];

            // Kein sinnvoller Zielteil
            if (to is null)
                return null;

            return new ShipDestination
            {
                Raw = raw,
                RouteType = DestinationType.Directed,
                From = from,
                To = to
            };
        }

        // Keine Routing-Syntax
        return new ShipDestination
        {
            Raw = raw,
            RouteType = DestinationType.Single,
            To = raw
        };
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(
            value.Trim().ToUpperInvariant(),
            @"\s+",
            " "
        );
    }
    
    private static bool TryParseUnLocodeRoute(
        string value,
        out string? from,
        out string? to)
    {
        from = null;
        to = null;

        var parts = value.Split(
            '-',
            2,
            StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
            return false;

        if (!IsUnLocode(parts[0]) || !IsUnLocode(parts[1]))
            return false;

        from = parts[0];
        to = parts[1];

        return true;
    }
    
    private static bool IsUnLocode(string value)
    {
        return value.Length == 5 &&
               value[..2].All(char.IsLetter) &&
               value[2..].All(char.IsLetterOrDigit);
    }
}