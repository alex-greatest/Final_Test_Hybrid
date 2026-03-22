using System.Net;
using System.Net.Http.Json;
using Final_Test_Hybrid.Services.SpringBoot;
using Final_Test_Hybrid.Services.SpringBoot.Operator;

namespace Final_Test_Hybrid.Tests.Runtime;

public class AdminAuthResponseParserTests
{
    [Fact]
    public async Task ParseAsync_WhenStatusOk_ReturnsSuccessWithUsername()
    {
        using var response = CreateResponse(
            HttpStatusCode.OK,
            new OperatorAuthResponse
            {
                Username = "admin01",
                Role = "ADMIN"
            });

        var result = await AdminAuthResponseParser.ParseAsync(response, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("admin01", result.Username);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ParseAsync_WhenStatusNotFound_ReturnsKnownErrorWithServerMessage()
    {
        using var response = CreateResponse(
            HttpStatusCode.NotFound,
            new ErrorResponse
            {
                Message = "Пользователь не найден"
            });

        var result = await AdminAuthResponseParser.ParseAsync(response, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsKnownError);
        Assert.Equal("Пользователь не найден", result.ErrorMessage);
    }

    [Fact]
    public async Task ParseAsync_WhenStatusUnexpected_ReturnsUnknownError()
    {
        using var response = CreateResponse(
            HttpStatusCode.Forbidden,
            new ErrorResponse
            {
                Message = "Не должен использоваться"
            });

        var result = await AdminAuthResponseParser.ParseAsync(response, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.IsKnownError);
        Assert.Equal("Неизвестная ошибка", result.ErrorMessage);
    }

    private static HttpResponseMessage CreateResponse<T>(HttpStatusCode statusCode, T payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(payload)
        };
    }
}
