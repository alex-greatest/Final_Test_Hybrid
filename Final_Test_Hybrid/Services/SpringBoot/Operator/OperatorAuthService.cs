using System.Net;
using System.Net.Http.Json;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.SpringBoot.Operator;

public class OperatorAuthService(
    SpringBootHttpClient httpClient,
    AppSettingsService appSettingsService,
    OperatorState operatorState,
    ShiftState shiftState,
    ILogger<OperatorAuthService> logger)
{
    private const string AuthEndpoint = "/api/operator/auth";
    private const string LogoutEndpoint = "/api/operator/logout";

    public async Task<OperatorAuthResult> AuthenticateAsync(string login, string password, CancellationToken ct = default)
    {
        var request = CreateRequest(login, password);
        try
        {
            return await SendRequestAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Authentication request failed for {Login}", login);
            return OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false);
        }
    }

    private OperatorAuthRequest CreateRequest(string login, string password) => new()
    {
        Login = login,
        Password = password,
        StationName = appSettingsService.NameStation
    };

    private async Task<OperatorAuthResult> SendRequestAsync(OperatorAuthRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostWithResponseAsync(AuthEndpoint, request, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.NotFound => await HandleErrorAsync(response, request.Login, ct),
            _ => HandleUnexpectedStatus(response.StatusCode)
        };
    }

    private async Task<OperatorAuthResult> HandleSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var authResponse = await response.Content.ReadFromJsonAsync<OperatorAuthResponse>(ct);
        if (authResponse == null)
        {
            return OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false);
        }
        operatorState.SetAuthenticated(authResponse);
        return OperatorAuthResult.Ok();
    }

    private async Task<OperatorAuthResult> HandleErrorAsync(HttpResponseMessage response, string login, CancellationToken ct)
    {
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
        var message = errorResponse?.Message ?? "Неизвестная ошибка";
        logger.LogWarning("Authentication failed for {Login}: {Message}", login, message);
        return OperatorAuthResult.Fail(message, isKnownError: true);
    }

    private OperatorAuthResult HandleUnexpectedStatus(HttpStatusCode statusCode)
    {
        logger.LogError("Unexpected status code {StatusCode} for authentication", statusCode);
        return OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false);
    }

    public async Task<OperatorAuthResult> LogoutAsync(CancellationToken ct = default)
    {
        var request = CreateLogoutRequest();
        try
        {
            return await SendLogoutRequestAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logout request failed for {Username}", request.Username);
            return OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false);
        }
    }

    private OperatorLogoutRequest CreateLogoutRequest() => new()
    {
        Username = operatorState.Username ?? string.Empty,
        StationName = appSettingsService.NameStation
    };

    private async Task<OperatorAuthResult> SendLogoutRequestAsync(OperatorLogoutRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostWithResponseAsync(LogoutEndpoint, request, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => HandleLogoutSuccess(),
            HttpStatusCode.NotFound => await HandleLogoutErrorAsync(response, request.Username, ct),
            _ => HandleLogoutUnexpectedStatus(response.StatusCode)
        };
    }

    private OperatorAuthResult HandleLogoutSuccess()
    {
        operatorState.Logout();
        return OperatorAuthResult.Ok();
    }

    private async Task<OperatorAuthResult> HandleLogoutErrorAsync(HttpResponseMessage response, string username, CancellationToken ct)
    {
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
        var message = errorResponse?.Message ?? "Неизвестная ошибка";
        logger.LogWarning("Logout failed for {Username}: {Message}", username, message);
        return OperatorAuthResult.Fail(message, isKnownError: true);
    }

    private OperatorAuthResult HandleLogoutUnexpectedStatus(HttpStatusCode statusCode)
    {
        logger.LogError("Unexpected status code {StatusCode} for logout", statusCode);
        return OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false);
    }

    public void ManualLogout()
    {
        operatorState.Logout();
        shiftState.SetShiftNumber(null);
    }
}
