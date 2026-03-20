using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class AccessLevelManagerTests
{
    [Fact]
    public async Task ResetToNormalModeAsync_ReturnsFalse_WhenRegisterWriteFails()
    {
        var client = new WriteOnlyModbusClient(new TimeoutException("write timeout"));
        var manager = CreateManager(client);

        var success = await manager.ResetToNormalModeAsync();

        Assert.False(success);
        Assert.Equal(AccessLevel.Normal, manager.CurrentLevel);
    }

    [Fact]
    public async Task ResetToNormalModeAsync_ReturnsTrue_WhenRegisterWriteSucceeds()
    {
        var client = new WriteOnlyModbusClient();
        var manager = CreateManager(client);

        var success = await manager.ResetToNormalModeAsync();

        Assert.True(success);
        Assert.Equal(AccessLevel.Normal, manager.CurrentLevel);
        var write = Assert.Single(client.Writes);
        Assert.Equal((ushort)999, write.Address);
        Assert.Equal([(ushort)0, (ushort)0], write.Values);
    }

    private static AccessLevelManager CreateManager(WriteOnlyModbusClient client)
    {
        var writer = new RegisterWriter(
            client,
            TestInfrastructure.CreateLogger<RegisterWriter>(),
            new TestStepLoggerStub());

        return new AccessLevelManager(
            writer,
            Options.Create(new DiagnosticSettings { BaseAddressOffset = 1 }),
            TestInfrastructure.CreateLogger<AccessLevelManager>());
    }

    private sealed class WriteOnlyModbusClient(Exception? writeException = null) : IModbusClient
    {
        private readonly Exception? _writeException = writeException;

        public List<(ushort Address, ushort[] Values)> Writes { get; } = [];

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task<ushort[]> ReadHoldingRegistersAsync(
            ushort address,
            ushort count,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task WriteSingleRegisterAsync(
            ushort address,
            ushort value,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task WriteMultipleRegistersAsync(
            ushort address,
            ushort[] values,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (_writeException != null)
            {
                throw _writeException;
            }

            Writes.Add((address, values.ToArray()));
            return Task.CompletedTask;
        }
    }
}
