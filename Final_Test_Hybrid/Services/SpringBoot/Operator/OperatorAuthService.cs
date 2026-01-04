using System.Net;
using System.Net.Http.Json;
using Final_Test_Hybrid.Services.Common.Logging;
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
    ILogger<OperatorAuthService> logger,
    ISpringBootLogger sbLogger)
{
    private const string AuthEndpoint = "/api/operator/auth";
    private const string QrAuthEndpoint = "/api/operator/auth/Qr";
    private const string LogoutEndpoint = "/api/operator/logout";

    public Task<OperatorAuthResult> AuthenticateAsync(string login, string password, CancellationToken ct = default)
    {
        var request = CreateRequest(login, password);
        return ExecuteAsync(() => SendRequestAsync(request, ct), "Authentication", login, ct);
    }

    public Task<OperatorAuthResult> AuthenticateByQrAsync(string qrCode, CancellationToken ct = default)
    {
        var request = CreateQrRequest(qrCode);
        return ExecuteAsync(() => SendQrRequestAsync(request, ct), "QR authentication", null, ct);
    }

    public Task<OperatorAuthResult> LogoutAsync(CancellationToken ct = default)
    {
        var request = CreateLogoutRequest();
        return ExecuteAsync(() => SendLogoutRequestAsync(request, ct), "Logout", request.Username, ct);
    }

    private async Task<OperatorAuthResult> ExecuteAsync(
        Func<Task<OperatorAuthResult>> action,
        string operation,
        string? context,
        CancellationToken ct)
    {
        try
        {
            return await action();
        }
        catch (TaskCanceledException)
        {
            return OperatorAuthResult.Fail("Нет ответа от сервера", isKnownError: false);
        }
        catch (HttpRequestException)
        {
            return OperatorAuthResult.Fail("Нет соединения с сервером", isKnownError: false);
        }
        catch (Exception ex)
        {
            LogError(ex, operation, context);
            return OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false);
        }
    }

    private void LogError(Exception ex, string operation, string? context)
    {
        var contextPart = context != null ? $" for {context}" : "";
        logger.LogError(ex, "{Operation} request failed{Context}", operation, contextPart);
        sbLogger.LogError(ex, "Ошибка запроса {Operation}{Context}", operation, contextPart);
    }

    private OperatorAuthRequest CreateRequest(string login, string password) => new()
    {
        Login = login,
        Password = password,
        StationName = appSettingsService.NameStation
    };

    private OperatorQrAuthRequest CreateQrRequest(string qrCode) => new()
    {
        Login = qrCode,
        Station = appSettingsService.NameStation
    };

    private OperatorLogoutRequest CreateLogoutRequest() => new()
    {
        Username = operatorState.Username ?? string.Empty,
        StationName = appSettingsService.NameStation
    };

    private async Task<OperatorAuthResult> SendRequestAsync(OperatorAuthRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostWithResponseAsync(AuthEndpoint, request, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, "Authentication", request.Login, ct),
            _ => HandleUnexpectedStatus(response.StatusCode, "authentication")
        };
    }

    private async Task<OperatorAuthResult> SendQrRequestAsync(OperatorQrAuthRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostWithResponseAsync(QrAuthEndpoint, request, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await HandleSuccessAsync(response, ct),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, "QR authentication", null, ct),
            _ => HandleUnexpectedStatus(response.StatusCode, "authentication")
        };
    }

    private async Task<OperatorAuthResult> SendLogoutRequestAsync(OperatorLogoutRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostWithResponseAsync(LogoutEndpoint, request, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => HandleLogoutSuccess(),
            HttpStatusCode.NotFound => await HandleNotFoundAsync(response, "Logout", request.Username, ct),
            _ => HandleUnexpectedStatus(response.StatusCode, "logout")
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
        return OperatorAuthResult.Ok(authResponse.Username);
    }

    private OperatorAuthResult HandleLogoutSuccess()
    {
        operatorState.Logout();
        return OperatorAuthResult.Ok();
    }

    private async Task<OperatorAuthResult> HandleNotFoundAsync(
        HttpResponseMessage response,
        string operation,
        string? context,
        CancellationToken ct)
    {
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
        var message = errorResponse?.Message ?? "Неизвестная ошибка";
        var contextPart = context != null ? $" for {context}" : "";
        logger.LogWarning("{Operation} failed{Context}: {Message}", operation, contextPart, message);
        sbLogger.LogWarning("{Operation} не удалась{Context}: {Message}", operation, contextPart, message);
        return OperatorAuthResult.Fail(message, isKnownError: true);
    }

    private OperatorAuthResult HandleUnexpectedStatus(HttpStatusCode statusCode, string operation)
    {
        logger.LogError("Unexpected status code {StatusCode} for {Operation}", statusCode, operation);
        sbLogger.LogError(null, "Неожиданный код статуса {StatusCode} при {Operation}", statusCode, operation);
        return OperatorAuthResult.Fail("Неизвестная ошибка", isKnownError: false);
    }

    public void ManualLogout()
    {
        operatorState.Logout();
        shiftState.SetShiftNumber(null);
    }
}
