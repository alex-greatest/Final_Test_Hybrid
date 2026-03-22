using System.Net;
using System.Net.Http.Json;
namespace Final_Test_Hybrid.Services.SpringBoot.Operator;

internal static class AdminAuthResponseParser
{
    public static async Task<OperatorAuthResult> ParseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await ParseSuccessAsync(response, ct),
            HttpStatusCode.NotFound => await ParseNotFoundAsync(response, ct),
            _ => OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false)
        };
    }

    private static async Task<OperatorAuthResult> ParseSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var authResponse = await response.Content.ReadFromJsonAsync<OperatorAuthResponse>(ct);
        return authResponse == null
            ? OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false)
            : OperatorAuthResult.Ok(authResponse.Username);
    }

    private static async Task<OperatorAuthResult> ParseNotFoundAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
        var message = errorResponse?.Message ?? "Неизвестная ошибка";
        return OperatorAuthResult.Fail(message, isKnownError: true);
    }
}
