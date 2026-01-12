using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public record BrowseResult(IReadOnlyList<string> Addresses, string? Error)
{
    public bool Success => Error == null;
}

public class OpcUaBrowseService(
    OpcUaConnectionService connectionService,
    DualLogger<OpcUaBrowseService> logger)
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
            return new BrowseResult(addresses, null);
        }
        catch (Exception ex)
        {
            return HandleException(ex, parentNodeId);
        }
    }

    public async Task<BrowseResult> BrowseAllTagsRecursiveAsync(string parentNodeId, CancellationToken ct = default)
    {
        if (Session == null)
        {
            return CreateNotConnectedError();
        }
        try
        {
            var addresses = new List<string>();
            await BrowseNodeRecursiveAsync(parentNodeId, addresses, ct);
            logger.LogInformation("Browse рекурсивный завершён: {Count} тегов из {NodeId}", addresses.Count, parentNodeId);
            return new BrowseResult(addresses, null);
        }
        catch (Exception ex)
        {
            return HandleException(ex, parentNodeId);
        }
    }

    private async Task BrowseNodeRecursiveAsync(string parentNodeId, List<string> addresses, CancellationToken ct)
    {
        var references = await BrowseAllReferencesAsync(parentNodeId, ct);
        foreach (var reference in references)
        {
            await ProcessReferenceAsync(reference, addresses, ct);
        }
    }

    private async Task ProcessReferenceAsync(ReferenceDescription reference, List<string> addresses, CancellationToken ct)
    {
        var nodeIdString = reference.NodeId.ToString();
        var children = await BrowseAllReferencesAsync(nodeIdString, ct);
        if (children.Count == 0)
        {
            if (reference.NodeClass == NodeClass.Variable && IsS7Tag(nodeIdString))
            {
                addresses.Add(nodeIdString);
            }
            return;
        }
        foreach (var child in children)
        {
            await ProcessReferenceAsync(child, addresses, ct);
        }
    }

    private static bool IsS7Tag(string nodeId)
    {
        return nodeId.Contains("DB_Recipe", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<ReferenceDescription>> BrowseAllReferencesAsync(string parentNodeId, CancellationToken ct)
    {
        var nodeId = new NodeId(parentNodeId);
        var browseDescription = CreateBrowseDescriptionAll(nodeId);
        var response = await Session!.BrowseAsync(null, null, 0, browseDescription, ct);
        var references = ExtractReferencesFromResults(response.Results);
        await ProcessContinuationPointsForReferencesAsync(response.Results, references, ct);
        return references;
    }

    private static BrowseDescriptionCollection CreateBrowseDescriptionAll(NodeId nodeId)
    {
        return
        [
            new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = 0,
                ResultMask = (uint)BrowseResultMask.All
            }
        ];
    }

    private static List<ReferenceDescription> ExtractReferencesFromResults(BrowseResultCollection results)
    {
        return results
            .Where(r => !StatusCode.IsBad(r.StatusCode))
            .SelectMany(r => r.References)
            .ToList();
    }

    private async Task ProcessContinuationPointsForReferencesAsync(
        BrowseResultCollection results,
        List<ReferenceDescription> references,
        CancellationToken ct)
    {
        var continuationPoints = GetContinuationPoints(results);
        while (continuationPoints.Count > 0)
        {
            var nextResponse = await Session!.BrowseNextAsync(null, false, new ByteStringCollection(continuationPoints), ct);
            references.AddRange(ExtractReferencesFromResults(nextResponse.Results));
            continuationPoints = GetContinuationPoints(nextResponse.Results);
        }
    }

    private BrowseResult CreateNotConnectedError()
    {
        const string error = "PLC не подключен";
        logger.LogError(error);
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
            .Where(r => !StatusCode.IsBad(r.StatusCode) && r.ContinuationPoint is { Length: > 0 })
            .Select(r => r.ContinuationPoint)
            .ToList();
    }

    private BrowseResult HandleException(Exception ex, string parentNodeId)
    {
        var error = ex is ServiceResultException sre
            ? OpcUaErrorMapper.ToHumanReadable(sre.StatusCode)
            : $"Ошибка browse: {ex.Message}";
        logger.LogError(ex, "Ошибка browse {NodeId}: {Error}", parentNodeId, error);
        return new BrowseResult([], error);
    }

    private static string ExtractAddress(ReferenceDescription reference)
    {
        return reference.NodeId.ToString();
    }
}
