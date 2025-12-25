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
            return CreateNotConnectedError();
        }
        try
        {
            var addresses = await BrowseNodeAsync(parentNodeId, ct);
            logger.LogInformation("Browse завершён: {Count} тегов из {NodeId}", addresses.Count, parentNodeId);
            subscriptionLogger.LogInformation("Browse завершён: {Count} тегов из {NodeId}", addresses.Count, parentNodeId);
            return new BrowseResult(addresses, null);
        }
        catch (ServiceResultException ex)
        {
            return HandleServiceResultException(ex, parentNodeId);
        }
        catch (Exception ex)
        {
            return HandleGenericException(ex, parentNodeId);
        }
    }

    private BrowseResult CreateNotConnectedError()
    {
        const string error = "PLC не подключен";
        logger.LogError(error);
        subscriptionLogger.LogError(null, error);
        return new BrowseResult([], error);
    }

    private async Task<List<string>> BrowseNodeAsync(string parentNodeId, CancellationToken ct)
    {
        var nodeId = new NodeId(parentNodeId);
        var browseDescription = CreateBrowseDescription(nodeId);
        var response = await Session!.BrowseAsync(null, null, 0, browseDescription, ct);
        var addresses = ExtractAddressesFromResults(response.Results);
        await ProcessContinuationPointsAsync(response.Results, addresses, ct);
        return addresses;
    }

    private static BrowseDescriptionCollection CreateBrowseDescription(NodeId nodeId)
    {
        return
        [
            new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = (uint)NodeClass.Variable,
                ResultMask = (uint)BrowseResultMask.All
            }
        ];
    }

    private static List<string> ExtractAddressesFromResults(BrowseResultCollection results)
    {
        return results
            .Where(r => !StatusCode.IsBad(r.StatusCode))
            .SelectMany(r => r.References)
            .Select(ExtractAddress)
            .ToList();
    }

    private async Task ProcessContinuationPointsAsync(
        BrowseResultCollection results,
        List<string> addresses,
        CancellationToken ct)
    {
        var continuationPoints = GetContinuationPoints(results);
        while (continuationPoints.Count > 0)
        {
            var nextResponse = await Session!.BrowseNextAsync(null, false, new ByteStringCollection(continuationPoints), ct);
            addresses.AddRange(ExtractAddressesFromResults(nextResponse.Results));
            continuationPoints = GetContinuationPoints(nextResponse.Results);
        }
    }

    private static List<byte[]> GetContinuationPoints(BrowseResultCollection results)
    {
        return results
            .Where(r => !StatusCode.IsBad(r.StatusCode))
            .Where(r => r.ContinuationPoint is { Length: > 0 })
            .Select(r => r.ContinuationPoint)
            .ToList();
    }

    private BrowseResult HandleServiceResultException(ServiceResultException ex, string parentNodeId)
    {
        var error = OpcUaErrorMapper.ToHumanReadable(ex.StatusCode);
        logger.LogError(ex, "Ошибка browse {NodeId}: {Error}", parentNodeId, error);
        subscriptionLogger.LogError(ex, "Ошибка browse {NodeId}: {Error}", parentNodeId, error);
        return new BrowseResult([], error);
    }

    private BrowseResult HandleGenericException(Exception ex, string parentNodeId)
    {
        var error = $"Ошибка browse: {ex.Message}";
        logger.LogError(ex, "Ошибка browse {NodeId}", parentNodeId);
        subscriptionLogger.LogError(ex, "Ошибка browse {NodeId}", parentNodeId);
        return new BrowseResult([], error);
    }

    private static string ExtractAddress(ReferenceDescription reference)
    {
        return reference.NodeId.ToString();
    }
}
