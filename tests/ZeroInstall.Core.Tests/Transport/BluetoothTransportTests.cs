using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class BluetoothTransportTests
{
    private readonly IBluetoothAdapter _adapter = Substitute.For<IBluetoothAdapter>();
    private readonly NullLogger<BluetoothTransport> _logger = NullLogger<BluetoothTransport>.Instance;

    [Fact]
    public void Constructor_NullAdapter_Throws()
    {
        var act = () => new BluetoothTransport(null!, 0, true, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("adapter");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new BluetoothTransport(_adapter, 0, true, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task TestConnectionAsync_Server_CallsAcceptConnection()
    {
        var stream = new MemoryStream();
        _adapter.AcceptConnectionAsync(BluetoothTransport.ServiceGuid, Arg.Any<CancellationToken>())
            .Returns(stream);

        var sut = new BluetoothTransport(_adapter, 0, isServer: true, _logger);
        var result = await sut.TestConnectionAsync();

        result.Should().BeTrue();
        await _adapter.Received(1).AcceptConnectionAsync(
            BluetoothTransport.ServiceGuid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionAsync_Client_CallsConnect()
    {
        ulong address = 0xAABBCCDDEEFF;
        var stream = new MemoryStream();
        _adapter.ConnectAsync(address, BluetoothTransport.ServiceGuid, Arg.Any<CancellationToken>())
            .Returns(stream);

        var sut = new BluetoothTransport(_adapter, address, isServer: false, _logger);
        var result = await sut.TestConnectionAsync();

        result.Should().BeTrue();
        await _adapter.Received(1).ConnectAsync(
            address, BluetoothTransport.ServiceGuid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionAsync_WhenAdapterThrows_ReturnsFalse()
    {
        _adapter.AcceptConnectionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Stream>(_ => throw new InvalidOperationException("No Bluetooth adapter"));

        var sut = new BluetoothTransport(_adapter, 0, isServer: true, _logger);
        var result = await sut.TestConnectionAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithoutConnect_Throws()
    {
        var sut = new BluetoothTransport(_adapter, 0, true, _logger);
        var data = new MemoryStream([1, 2, 3]);
        var metadata = new TransferMetadata { RelativePath = "test.txt", SizeBytes = 3 };

        var act = () => sut.SendAsync(data, metadata);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TestConnectionAsync*");
    }

    [Fact]
    public async Task ReceiveAsync_WithoutConnect_Throws()
    {
        var sut = new BluetoothTransport(_adapter, 0, true, _logger);
        var metadata = new TransferMetadata { RelativePath = "test.txt" };

        var act = () => sut.ReceiveAsync(metadata);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TestConnectionAsync*");
    }

    [Fact]
    public async Task SendManifestAsync_WithoutConnect_Throws()
    {
        var sut = new BluetoothTransport(_adapter, 0, true, _logger);
        var manifest = new TransferManifest();

        var act = () => sut.SendManifestAsync(manifest);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveManifestAsync_WithoutConnect_Throws()
    {
        var sut = new BluetoothTransport(_adapter, 0, true, _logger);

        var act = () => sut.ReceiveManifestAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAndReceive_ManifestRoundTrip()
    {
        var pipe = new DuplexMemoryStream();

        var serverAdapter = Substitute.For<IBluetoothAdapter>();
        serverAdapter.AcceptConnectionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ServerStream);

        var clientAdapter = Substitute.For<IBluetoothAdapter>();
        clientAdapter.ConnectAsync(Arg.Any<ulong>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ClientStream);

        var server = new BluetoothTransport(serverAdapter, 0, isServer: true, _logger);
        var client = new BluetoothTransport(clientAdapter, 1, isServer: false, _logger);

        await server.TestConnectionAsync();
        await client.TestConnectionAsync();

        var manifest = new TransferManifest
        {
            SourceHostname = "TestPC",
            Items = [new MigrationItem { DisplayName = "data.zip", EstimatedSizeBytes = 1024 }]
        };

        var sendTask = client.SendManifestAsync(manifest);
        var receiveTask = server.ReceiveManifestAsync();

        await Task.WhenAll(sendTask, receiveTask);
        var received = await receiveTask;

        received.SourceHostname.Should().Be("TestPC");
        received.Items.Should().HaveCount(1);
        received.Items[0].DisplayName.Should().Be("data.zip");
        received.Items[0].EstimatedSizeBytes.Should().Be(1024);
    }

    [Fact]
    public async Task SendAndReceive_DataRoundTrip()
    {
        var pipe = new DuplexMemoryStream();

        var serverAdapter = Substitute.For<IBluetoothAdapter>();
        serverAdapter.AcceptConnectionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ServerStream);

        var clientAdapter = Substitute.For<IBluetoothAdapter>();
        clientAdapter.ConnectAsync(Arg.Any<ulong>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ClientStream);

        var server = new BluetoothTransport(serverAdapter, 0, isServer: true, _logger);
        var client = new BluetoothTransport(clientAdapter, 1, isServer: false, _logger);

        await server.TestConnectionAsync();
        await client.TestConnectionAsync();

        var payload = Encoding.UTF8.GetBytes("Hello, Bluetooth!");
        var metadata = new TransferMetadata
        {
            RelativePath = "hello.txt",
            SizeBytes = payload.Length
        };

        var sendTask = client.SendAsync(new MemoryStream(payload), metadata);
        var receiveTask = server.ReceiveAsync(metadata);

        await Task.WhenAll(sendTask, receiveTask);
        var receivedStream = await receiveTask;

        using var ms = new MemoryStream();
        await receivedStream.CopyToAsync(ms);
        var receivedData = Encoding.UTF8.GetString(ms.ToArray());
        receivedData.Should().Be("Hello, Bluetooth!");
    }

    [Fact]
    public async Task DiscoverDevicesAsync_DelegatesToAdapter()
    {
        var expectedDevices = new List<DiscoveredBluetoothDevice>
        {
            new() { DeviceName = "PC1", Address = 1 },
            new() { DeviceName = "PC2", Address = 2 }
        };

        _adapter.DiscoverDevicesAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(expectedDevices);

        var result = await BluetoothTransport.DiscoverDevicesAsync(
            _adapter, TimeSpan.FromSeconds(5));

        result.Should().HaveCount(2);
        result[0].DeviceName.Should().Be("PC1");
        result[1].DeviceName.Should().Be("PC2");
    }

    [Fact]
    public void EstimateTransferTime_ZeroBytes_ReturnsZero()
    {
        BluetoothTransport.EstimateTransferTime(0).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void EstimateTransferTime_NegativeBytes_ReturnsZero()
    {
        BluetoothTransport.EstimateTransferTime(-100).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void EstimateTransferTime_OneMB_ReturnsAboutFourSeconds()
    {
        var estimate = BluetoothTransport.EstimateTransferTime(1024 * 1024);
        estimate.TotalSeconds.Should().BeApproximately(4.0, 0.5);
    }

    [Fact]
    public void EstimateTransferTime_OneGB_ReturnsAboutOneHour()
    {
        var estimate = BluetoothTransport.EstimateTransferTime(1024L * 1024 * 1024);
        // 1 GB / 256000 B/s = ~69.9 minutes
        estimate.TotalMinutes.Should().BeApproximately(69.9, 2.0);
    }

    [Fact]
    public void ServiceGuid_IsNotEmpty()
    {
        BluetoothTransport.ServiceGuid.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void EstimatedMaxBytesPerSecond_Is256KB()
    {
        BluetoothTransport.EstimatedMaxBytesPerSecond.Should().Be(250 * 1024);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAdapter()
    {
        var sut = new BluetoothTransport(_adapter, 0, true, _logger);
        await sut.DisposeAsync();

        _adapter.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_WithStream_DisposesStream()
    {
        var stream = new MemoryStream();
        _adapter.AcceptConnectionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(stream);

        var sut = new BluetoothTransport(_adapter, 0, isServer: true, _logger);
        await sut.TestConnectionAsync();

        await sut.DisposeAsync();

        // MemoryStream.Dispose() doesn't throw, but we verify adapter was disposed
        _adapter.Received(1).Dispose();
    }

    [Fact]
    public async Task SendAndReceive_LargePayload_RoundTrip()
    {
        var pipe = new DuplexMemoryStream();

        var serverAdapter = Substitute.For<IBluetoothAdapter>();
        serverAdapter.AcceptConnectionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ServerStream);

        var clientAdapter = Substitute.For<IBluetoothAdapter>();
        clientAdapter.ConnectAsync(Arg.Any<ulong>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ClientStream);

        var server = new BluetoothTransport(serverAdapter, 0, isServer: true, _logger);
        var client = new BluetoothTransport(clientAdapter, 1, isServer: false, _logger);

        await server.TestConnectionAsync();
        await client.TestConnectionAsync();

        var payload = new byte[100 * 1024]; // 100 KB
        new Random(42).NextBytes(payload);
        var metadata = new TransferMetadata
        {
            RelativePath = "large.bin",
            SizeBytes = payload.Length
        };

        var sendTask = client.SendAsync(new MemoryStream(payload), metadata);
        var receiveTask = server.ReceiveAsync(metadata);

        await Task.WhenAll(sendTask, receiveTask);
        var receivedStream = await receiveTask;

        using var ms = new MemoryStream();
        await receivedStream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task SendAndReceive_MultipleSequentialTransfers()
    {
        var pipe = new DuplexMemoryStream();

        var serverAdapter = Substitute.For<IBluetoothAdapter>();
        serverAdapter.AcceptConnectionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ServerStream);

        var clientAdapter = Substitute.For<IBluetoothAdapter>();
        clientAdapter.ConnectAsync(Arg.Any<ulong>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pipe.ClientStream);

        var server = new BluetoothTransport(serverAdapter, 0, isServer: true, _logger);
        var client = new BluetoothTransport(clientAdapter, 1, isServer: false, _logger);

        await server.TestConnectionAsync();
        await client.TestConnectionAsync();

        for (int i = 0; i < 3; i++)
        {
            var payload = Encoding.UTF8.GetBytes($"File {i} contents");
            var metadata = new TransferMetadata
            {
                RelativePath = $"file{i}.txt",
                SizeBytes = payload.Length,
                ChunkIndex = 0,
                TotalChunks = 1
            };

            var sendTask = client.SendAsync(new MemoryStream(payload), metadata);
            var receiveTask = server.ReceiveAsync(metadata);

            await Task.WhenAll(sendTask, receiveTask);
            var receivedStream = await receiveTask;

            using var ms = new MemoryStream();
            await receivedStream.CopyToAsync(ms);
            Encoding.UTF8.GetString(ms.ToArray()).Should().Be($"File {i} contents");
        }
    }

    /// <summary>
    /// A pair of streams connected together for testing bidirectional communication.
    /// Writes to ClientStream can be read from ServerStream and vice versa.
    /// </summary>
    private sealed class DuplexMemoryStream
    {
        private readonly PipeStream _clientToServer = new();
        private readonly PipeStream _serverToClient = new();

        public Stream ClientStream => new CombinedStream(_clientToServer, _serverToClient);
        public Stream ServerStream => new CombinedStream(_serverToClient, _clientToServer);

        private sealed class CombinedStream(PipeStream writeTarget, PipeStream readSource) : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
                => readSource.Read(buffer, offset, count);

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
                => await readSource.ReadAsync(buffer.AsMemory(offset, count), ct);

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
                => await readSource.ReadAsync(buffer, ct);

            public override void Write(byte[] buffer, int offset, int count)
                => writeTarget.Write(buffer, offset, count);

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
                => await writeTarget.WriteAsync(buffer.AsMemory(offset, count), ct);

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
                => await writeTarget.WriteAsync(buffer, ct);

            public override void Flush() => writeTarget.Flush();
            public override Task FlushAsync(CancellationToken ct) => writeTarget.FlushAsync(ct);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        /// <summary>
        /// A simple in-memory pipe: writes are buffered and can be read by another consumer.
        /// Thread-safe for single producer / single consumer.
        /// </summary>
        private sealed class PipeStream : Stream
        {
            private readonly SemaphoreSlim _dataAvailable = new(0);
            private readonly Queue<byte[]> _buffers = new();
            private byte[]? _currentBuffer;
            private int _currentOffset;
            private readonly object _lock = new();

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                var copy = new byte[count];
                Array.Copy(buffer, offset, copy, 0, count);
                lock (_lock)
                {
                    _buffers.Enqueue(copy);
                }
                _dataAvailable.Release();
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
            {
                var copy = buffer.ToArray();
                lock (_lock)
                {
                    _buffers.Enqueue(copy);
                }
                _dataAvailable.Release();
                await Task.CompletedTask;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                _dataAvailable.Wait();
                return ReadFromBuffer(buffer, offset, count);
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            {
                await _dataAvailable.WaitAsync(ct);
                var tempBuffer = new byte[buffer.Length];
                var bytesRead = ReadFromBuffer(tempBuffer, 0, buffer.Length);
                tempBuffer.AsMemory(0, bytesRead).CopyTo(buffer);
                return bytesRead;
            }

            private int ReadFromBuffer(byte[] buffer, int offset, int count)
            {
                if (_currentBuffer is null || _currentOffset >= _currentBuffer.Length)
                {
                    lock (_lock)
                    {
                        if (_buffers.Count == 0) return 0;
                        _currentBuffer = _buffers.Dequeue();
                        _currentOffset = 0;
                    }
                }

                var available = _currentBuffer!.Length - _currentOffset;
                var toCopy = Math.Min(available, count);
                Array.Copy(_currentBuffer, _currentOffset, buffer, offset, toCopy);
                _currentOffset += toCopy;

                // If we have leftover data, put the semaphore back
                if (_currentOffset < _currentBuffer.Length)
                {
                    _dataAvailable.Release();
                }

                return toCopy;
            }

            public override void Flush() { }
            public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
