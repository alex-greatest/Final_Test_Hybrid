using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public record BrowseResult(IReadOnlyList<string> Addresses, string? Error)
{
    public bool Success => Error == null;
}

public class OpcUaBrowseService(
    OpcUaConnectionService connectionService,
    ILogger<OpcUaBrowseService> logger,
    ISubscriptionLogger subscriptionLogger)
{
    private ISession? Session => connectionService.Session;

    public async Task<BrowseResult> BrowseChildTagsAsync(string parentNodeId, CancellationToken ct = default)
    {
        if (Session == null)
        {
            const string error = "PLC не подключен";
            logger.LogError(error);
            subscriptionLogger.LogError(null, error);
            return new BrowseResult([], error);
        }
        try
        {
            var nodeId = new NodeId(parentNodeId);
            var browseDescription = new BrowseDescriptionCollection
            {
                new BrowseDescription
                {
                    NodeId = nodeId,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)NodeClass.Variable,
                    ResultMask = (uint)BrowseResultMask.All
                }
            };
            var response = await Session.BrowseAsync(null, null, 0, browseDescription, ct);
            var addresses = new List<string>();
            foreach (var result in response.Results.Where(result => !StatusCode.IsBad(result.StatusCode)))
            {
                addresses.AddRange(result.References.Select(ExtractAddress));
            }
            await BrowseContinuationPointsAsync(response.Results, addresses, ct);
            logger.LogInformation("Browse завершён: {Count} тегов из {NodeId}", addresses.Count, parentNodeId);
            subscriptionLogger.LogInformation("Browse завершён: {Count} тегов из {NodeId}", addresses.Count, parentNodeId);
            return new BrowseResult(addresses, null);
        }
        catch (ServiceResultException ex)
        {
            var error = OpcUaErrorMapper.ToHumanReadable(ex.StatusCode);
            logger.LogError(ex, "Ошибка browse {NodeId}: {Error}", parentNodeId, error);
            subscriptionLogger.LogError(ex, "Ошибка browse {NodeId}: {Error}", parentNodeId, error);
            return new BrowseResult([], error);
        }
        catch (Exception ex)
        {
            var error = $"Ошибка browse: {ex.Message}";
            logger.LogError(ex, "Ошибка browse {NodeId}", parentNodeId);
            subscriptionLogger.LogError(ex, "Ошибка browse {NodeId}", parentNodeId);
            return new BrowseResult([], error);
        }
    }

    private async Task BrowseContinuationPointsAsync(
        BrowseResultCollection results,
        List<string> addresses,
        CancellationToken ct)
    {
        var continuationPoints = results
            .Where(r => r.ContinuationPoint is { Length: > 0 })
            .Select(r => r.ContinuationPoint)
            .ToList();
        while (continuationPoints.Count > 0)
        {
            var nextResponse = await Session!.BrowseNextAsync(null, false, new ByteStringCollection(continuationPoints), ct);
            continuationPoints.Clear();
            foreach (var result in nextResponse.Results.Where(result => !StatusCode.IsBad(result.StatusCode)))
            {
                addresses.AddRange(result.References.Select(ExtractAddress));
                if (result.ContinuationPoint != null && result.ContinuationPoint.Length > 0)
                {
                    continuationPoints.Add(result.ContinuationPoint);
                }
            }
        }
    }

    private static string ExtractAddress(ReferenceDescription reference)
    {
        return reference.NodeId.ToString();
    }
}
