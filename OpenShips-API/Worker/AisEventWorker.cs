using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenShipsAPI.Domain.Events;
using OpenShipsAPI.Infrastructure.Database;
using OpenShipsAPI.Infrastructure.Database.Models;
using OpenShipsAPI.Infrastructure.Queues;

namespace OpenShipsAPI.Worker;

public sealed class AisEventWorker : BackgroundService
{
    private const int BatchSize = 500;

    private readonly AisEventQueue _queue;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<AisEventWorker> _logger;

    public AisEventWorker(AisEventQueue queue, IDbContextFactory<AppDbContext> dbFactory, ILogger<AisEventWorker> logger)
    {
        _queue = queue;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AisEvent>(BatchSize);

        while (await _queue.Reader.WaitToReadAsync(stoppingToken))
        {
            batch.Clear();

            while (batch.Count < BatchSize &&
                   _queue.Reader.TryRead(out var item))
            {
                batch.Add(item);
            }

            if (batch.Count > 0)
            {
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    await ProcessBatchAsync(batch, stoppingToken);
                }
                finally
                {
                    stopwatch.Stop();

                    _logger.LogInformation(
                        "AIS batch processed: Count={Count}, DurationMs={DurationMs}",
                        batch.Count,
                        stopwatch.ElapsedMilliseconds);
                    
                    _logger.LogInformation(
                        "AIS queue status: Queued={Queued}, BatchSize={BatchSize}",
                        _queue.Count,
                        batch.Count);
                }
            }
        }
    }

    private async Task ProcessBatchAsync(List<AisEvent> events, CancellationToken cancellationToken)
    {
        var positions = events
            .OfType<AisPositionEvent>()
            .ToList();

        if (positions.Count > 0)
        {
            await ProcessPositionsAsync(
                positions,
                cancellationToken);
        }
        
        var staticData = events.OfType<AisStaticShipDataEvent>().ToList();
        if (staticData.Count > 0)
        {
            await ProcessStaticDataAsync(staticData, cancellationToken);
        }
        
        // var classB = events.OfType<AisClassBReport>();
        // await ProcessClassBAsync(...);
    }

    private async Task ProcessStaticDataAsync(List<AisStaticShipDataEvent> events, CancellationToken cancellationToken)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var latestStaticData = events
            .GroupBy(x => x.Mmsi)
            .Select(g =>
                g.OrderByDescending(x => x.EventTimestamp)
                    .First()
            )
            .ToList();

        var mmsis = latestStaticData
            .Select(x => x.Mmsi)
            .Distinct()
            .ToArray();
        
        var currentStaticData = await db.StaticShipData
            .Where(x => mmsis.Contains(x.Mmsi))
            .ToDictionaryAsync(
                x => x.Mmsi,
                cancellationToken);

        var added = 0;
        var updated = 0;
        
        foreach (var data in latestStaticData)
        {
            if (currentStaticData.TryGetValue(data.Mmsi, out var current))
            {
                if (data.EventTimestamp <= current.EventTimestamp)
                    continue;
                
                if (data.EventTimestamp > current.EventTimestamp)
                {
                    // Update the existing static data
                    current.Source = data.Source;
                    current.SourceMessageId = data.SourceMessageId;
                    current.License = data.License;
                    current.MessageType = data.MessageType;
                    current.ReceivedAt = data.ReceivedAt;
                    current.EventTimestamp = data.EventTimestamp;
                    
                    current.ShipName = data.ShipName;
                    current.ShipType = data.ShipType;
                    current.ImoNumber = data.ImoNumber;
                    current.CallSign = data.CallSign;
                    current.RawDestination = data.RawDestination;
                    current.Draught = data.Draught;
                    current.Eta = data.Eta;
                    current.Dim_A = data.Dim_A;
                    current.Dim_B = data.Dim_B;
                    current.Dim_C = data.Dim_C;
                    current.Dim_D = data.Dim_D;
                    current.Length = data.Length;
                    current.Beam = data.Beam;

                    updated++;
                }
            }
            else
            {
                // Add the new static data
                var newStaticData =
                    CreateStaticData(data);

                db.StaticShipData.Add(
                    newStaticData);

                currentStaticData[data.Mmsi] =
                    newStaticData;
                
                added++;
            }
        }
        
        await db.SaveChangesAsync(cancellationToken);
        
        /*
        _logger.LogInformation(
            "Processed {StaticData} static data events, added {Added}, updated {Updated}",
            latestStaticData.Count,
            added,
            updated);
            */
    }
    
    private async Task ProcessPositionsAsync(List<AisPositionEvent> positions, CancellationToken cancellationToken)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);
        
        var latestPositions = positions
            .GroupBy(x => x.Mmsi)
            .Select(g =>
                g.OrderByDescending(x => x.EventTimestamp)
                    .First())
            .ToList();

        var mmsis = latestPositions
            .Select(x => x.Mmsi)
            .Distinct()
            .ToArray();
        
        var currentPositions = await db.CurrentAisPositions
            .Where(x => mmsis.Contains(x.Mmsi))
            .ToDictionaryAsync(
                x => x.Mmsi,
                cancellationToken);
        
        // var stopwatch = Stopwatch.StartNew();

        var lastHistoryPositions = await GetLastHistoryPositionsAsync(
            db,
            mmsis,
            cancellationToken);

        /*
        stopwatch.Stop();

        _logger.LogInformation(
            "Loading latest AIS history: MmsiCount={MmsiCount}, RowsReturned={RowsReturned}, DurationMs={DurationMs}",
            mmsis.Length,
            lastHistoryPositions.Count,
            stopwatch.ElapsedMilliseconds);
        */

        var history = new List<HistoricalAisPosition>();

        foreach (var position in latestPositions)
        {
            /*
             * ------------------------------------------------
             * CURRENT
             * ------------------------------------------------
             */

            if (currentPositions.TryGetValue(
                    position.Mmsi,
                    out var current))
            {
                if (position.EventTimestamp <= current.EventTimestamp)
                    continue;

                UpdateCurrent(
                    current,
                    position);
            }
            else
            {
                var newCurrent =
                    CreateCurrent(position);

                db.CurrentAisPositions.Add(
                    newCurrent);

                currentPositions[position.Mmsi] =
                    newCurrent;
            }

            /*
             * ------------------------------------------------
             * HISTORY
             * ------------------------------------------------
             */

            if (lastHistoryPositions.TryGetValue(
                    position.Mmsi,
                    out var lastHistory))
            {
                var valuesChanged =
                    Math.Round(position.Latitude, 5) != Math.Round(lastHistory.Latitude, 5) ||
                    Math.Round(position.Longitude, 5) != Math.Round(lastHistory.Longitude, 5) ||
                    position.Sog != lastHistory.Sog ||
                    position.Cog != lastHistory.Cog ||
                    position.Heading != lastHistory.Heading ||
                    position.NavigationStatus != lastHistory.NavigationStatus ||
                    position.RateOfTurn != lastHistory.RateOfTurn ||
                    position.SpecialManoeuvreIndicator != lastHistory.SpecialManoeuvreIndicator ||
                    position.CommunicationState != lastHistory.CommunicationState;
                
                var enoughTimePassed =
                    position.EventTimestamp -
                    lastHistory.EventTimestamp >=
                    TimeSpan.FromMinutes(1);

                if (valuesChanged && enoughTimePassed)
                {
                    var newHistory =
                        CreateHistory(position);

                    history.Add(newHistory);
                    
                    lastHistoryPositions[position.Mmsi] =
                        newHistory;
                }
            }
            else
            {
                var newHistory =
                    CreateHistory(position);

                history.Add(newHistory);

                lastHistoryPositions[position.Mmsi] =
                    newHistory;
            }
        }

        if (history.Count > 0)
        {
            await db.HistoricalAisPositions.AddRangeAsync(
                history,
                cancellationToken);
        }

        await db.SaveChangesAsync(
            cancellationToken);

       /*
        _logger.LogInformation(
            "Processed {Positions} positions, saved {History} to history",
            latestPositions.Count,
            history.Count);
            */
    }

    private static CurrentAisPosition CreateCurrent(AisPositionEvent position)
    {
        return new CurrentAisPosition
        {
            Source = position.Source,
            SourceMessageId = position.SourceMessageId,
            License = position.License,

            MessageType = position.MessageType,
            Mmsi = position.Mmsi,

            ReceivedAt = position.ReceivedAt,
            EventTimestamp = position.EventTimestamp,

            Latitude = position.Latitude,
            Longitude = position.Longitude,

            Sog = position.Sog,
            Cog = position.Cog,
            Heading = position.Heading,
            NavigationStatus = position.NavigationStatus,
            RateOfTurn = position.RateOfTurn,
            PositionAccuracy = position.PositionAccuracy,

            RepeatIndicator = position.RepeatIndicator,
            Valid = position.Valid,
            SpecialManoeuvreIndicator = position.SpecialManoeuvreIndicator,
            Spare = position.Spare,
            Raim = position.Raim,
            CommunicationState = position.CommunicationState
        };
    }

    private static void UpdateCurrent(CurrentAisPosition current, AisPositionEvent position)
    {
        current.Source =
            position.Source;

        current.SourceMessageId =
            position.SourceMessageId;

        current.License =
            position.License;

        current.MessageType =
            position.MessageType;

        current.EventTimestamp =
            position.EventTimestamp;

        current.ReceivedAt =
            position.ReceivedAt;

        current.Latitude =
            position.Latitude;

        current.Longitude =
            position.Longitude;

        current.Sog =
            position.Sog;

        current.Cog =
            position.Cog;

        current.Heading =
            position.Heading;

        current.NavigationStatus =
            position.NavigationStatus;

        current.RateOfTurn =
            position.RateOfTurn;

        current.PositionAccuracy =
            position.PositionAccuracy;

        current.RepeatIndicator =
            position.RepeatIndicator;

        current.Valid =
            position.Valid;

        current.SpecialManoeuvreIndicator =
            position.SpecialManoeuvreIndicator;

        current.Spare =
            position.Spare;

        current.Raim =
            position.Raim;

        current.CommunicationState =
            position.CommunicationState;
    }

    private static HistoricalAisPosition CreateHistory(AisPositionEvent position)
    {
        return new HistoricalAisPosition
        {
            Source = position.Source,
            SourceMessageId = position.SourceMessageId,
            License = position.License,

            MessageType = position.MessageType,
            Mmsi = position.Mmsi,

            ReceivedAt = position.ReceivedAt,
            EventTimestamp = position.EventTimestamp,

            Latitude = position.Latitude,
            Longitude = position.Longitude,

            Sog = position.Sog,
            Cog = position.Cog,
            Heading = position.Heading,
            NavigationStatus = position.NavigationStatus,
            RateOfTurn = position.RateOfTurn,
            PositionAccuracy = position.PositionAccuracy,

            RepeatIndicator = position.RepeatIndicator,
            Valid = position.Valid,
            SpecialManoeuvreIndicator = position.SpecialManoeuvreIndicator,
            Spare = position.Spare,
            Raim = position.Raim,
            CommunicationState = position.CommunicationState
        };
    }
    
    private static StaticShipDataAis CreateStaticData(AisStaticShipDataEvent data)
    {
        return new StaticShipDataAis
        {
            Mmsi = data.Mmsi,
            Source = data.Source,
            SourceMessageId = data.SourceMessageId,
            License = data.License,
            MessageType = data.MessageType,
            ReceivedAt = data.ReceivedAt,
            EventTimestamp = data.EventTimestamp,
            ShipName = data.ShipName,
            ShipType = data.ShipType,
            ImoNumber = data.ImoNumber,
            CallSign = data.CallSign,
            RawDestination = data.RawDestination,
            Draught = data.Draught,
            Eta = data.Eta,
            Dim_A = data.Dim_A,
            Dim_B = data.Dim_B,
            Dim_C = data.Dim_C,
            Dim_D = data.Dim_D,
            Length = data.Length,
            Beam = data.Beam,
        };
    }
    
    private async Task<Dictionary<int, HistoricalAisPosition>>
        GetLastHistoryPositionsAsync(
            AppDbContext db,
            int[] mmsis,
            CancellationToken cancellationToken)
    {
        if (mmsis.Length == 0)
        {
            return new Dictionary<int, HistoricalAisPosition>();
        }

        var mmsiParameter = new Npgsql.NpgsqlParameter<int[]>(
            "mmsis",
            mmsis);

        var positions = await db.HistoricalAisPositions
            .FromSqlRaw(
                """
                SELECT latest.*
                FROM unnest(@mmsis) AS ships("Mmsi")
                CROSS JOIN LATERAL (
                    SELECT h.*
                    FROM public."HistoricalAisPositions" AS h
                    WHERE h."Mmsi" = ships."Mmsi"
                    ORDER BY h."EventTimestamp" DESC
                    LIMIT 1
                ) AS latest
                """,
                mmsiParameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return positions.ToDictionary(x => x.Mmsi);
    }
}