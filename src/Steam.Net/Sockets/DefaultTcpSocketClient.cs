﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Steam.Net.Sockets
{
    internal class DefaultTcpSocketClient : ISocketClient
    {
        private readonly AsyncEvent<Func<byte[], Task>> _receiveDataEvent = new AsyncEvent<Func<byte[], Task>>();
        private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new AsyncEvent<Func<Exception, Task>>();

        private readonly SemaphoreSlim _lock;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken, _parentToken = default;
        private Task _runTask;
        private bool _isDisconnecting;
        const int _tcpMagic = 0x31305456;

        public event Func<byte[], Task> MessageReceived
        {
            add => _receiveDataEvent.Add(value);
            remove => _receiveDataEvent.Remove(value);
        }

        public event Func<Exception, Task> Disconnected
        {
            add => _disconnectedEvent.Add(value);
            remove => _disconnectedEvent.Remove(value);
        }

        public DefaultTcpSocketClient()
        {
            _lock = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public IPAddress LocalIp => (_tcpClient.Client.LocalEndPoint as IPEndPoint).Address;
        
        public async Task ConnectAsync(IPEndPoint endpoint)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await ConnectInternalAsync(endpoint).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task ConnectInternalAsync(IPEndPoint endpoint)
        {
            await DisconnectInternalAsync().ConfigureAwait(false);

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _cancellationTokenSource.Token).Token;
            
            _tcpClient = new TcpClient();
            await Task.Run(() => _tcpClient.ConnectAsync(endpoint.Address, endpoint.Port), _cancellationToken);

            _networkStream = _tcpClient.GetStream();
            _runTask = ReceiveAsync(_cancellationToken);
        }

        public async Task DisconnectAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task DisconnectInternalAsync(bool disposing = false)
        {
            try
            {
                _cancellationTokenSource.Cancel(false);
            }
            catch { }

            _isDisconnecting = true;
            try
            {
                if (!disposing)
                    await (_runTask ?? Task.Delay(0)).ConfigureAwait(false);
            }
            finally { _isDisconnecting = false; }

            if(_tcpClient != null)
            {
                try
                {
                    _tcpClient.Dispose();
                    _networkStream.Dispose();
                }
                catch { }
                _tcpClient = null;
                _networkStream = null;
            }
        }

        public async Task SendAsync(byte[] data)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _networkStream.WriteAsync(BitConverter.GetBytes(data.Length), 0, 4).ConfigureAwait(false);
                await _networkStream.WriteAsync(BitConverter.GetBytes(_tcpMagic), 0, 4).ConfigureAwait(false);
                await _networkStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _cancellationTokenSource.Token).Token;
        }
        
        private async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            Task _sentData = Task.CompletedTask;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!_networkStream.CanRead)
                    {
                        continue;
                    }

                    byte[] bytes = new byte[8];
                    if (await _networkStream.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false) == 0)
                        throw new SocketException((int)SocketError.ConnectionReset);

                    uint length = BitConverter.ToUInt32(bytes, 0);
                    uint magic = BitConverter.ToUInt32(bytes, 4);

                    if (magic != _tcpMagic)
                    {
                        throw new InvalidDataException("The provided packet does not have the correct magic value");
                    }

                    bytes = new byte[length];
                    await ReceiveDataAsync(bytes, bytes.Length, cancellationToken).ConfigureAwait(false);

                    await _sentData.ConfigureAwait(false);

                    _sentData = _receiveDataEvent.InvokeAsync(bytes);
                }
            }
            catch (Exception ex)
            {
                var _ = OnClosed(ex);
            }
        }

        private async Task OnClosed(Exception ex)
        {
            if (_isDisconnecting)
                return;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectInternalAsync(false);
            }
            finally
            {
                _lock.Release();
            }
            await _disconnectedEvent.InvokeAsync(ex);
        }

        private async Task ReceiveDataAsync(byte[] buffer, int length, CancellationToken token) // sometimes Steam sends data in batches and ReadAsync doesn't return that amount, so we need to loop until we get the whole packet
        {
            for (int remainingData = length, receivedData = 0; remainingData > 0 && !token.IsCancellationRequested; remainingData -= receivedData)
                receivedData = await _networkStream.ReadAsync(buffer, receivedData, remainingData).ConfigureAwait(false);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisconnectInternalAsync(true).GetAwaiter().GetResult();
                    _lock.Dispose();
                    _cancellationTokenSource.Dispose();
                }
                
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
