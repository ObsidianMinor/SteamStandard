﻿using Steam.Common;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Steam.Net.Sockets
{
    internal class DefaultTcpSocketClient : ISocketClient
    {
        private readonly SemaphoreSlim _lock;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken, _parentToken;
        private Task _runTask;
        const int _tcpMagic = 0x31305456;

        public DefaultTcpSocketClient()
        {
            _lock = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public IPAddress LocalIp => (_tcpClient.Client.LocalEndPoint as IPEndPoint).Address;

        public event EventHandler<Exception> Disconnected;
        public event EventHandler Connected;
        public event EventHandler<byte[]> ReceivedData;

        public async Task ConnectAsync(IPEndPoint endpoint, int timeout)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await ConnectInternalAsync(endpoint, timeout, _cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task ConnectInternalAsync(IPEndPoint endpoint, int timeout, CancellationToken token)
        {
            await DisconnectInternalAsync().ConfigureAwait(false);

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _cancellationTokenSource.Token).Token;
            
            _tcpClient = new TcpClient();

            Task connectTask = _tcpClient.ConnectAsync(endpoint.Address, endpoint.Port);
            Task timeoutTask = Task.Delay(timeout);
            Task resultTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

            if (resultTask == timeoutTask)
                throw new TimeoutException($"The connect task timed out in {timeout} ms");

            await resultTask;
            _networkStream = _tcpClient.GetStream();
            _runTask = ReceiveAsync(_cancellationToken).ContinueWith(t => Disconnected?.Invoke(this, t.Exception?.InnerException));

            Connected?.Invoke(this, EventArgs.Empty);
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

            if (!disposing)
                await (_runTask ?? Task.Delay(0)).ConfigureAwait(false);

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

        public void SetCancellationTtoken(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _cancellationTokenSource.Token).Token;
        }
        
        private async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100).ConfigureAwait(false);
                if (!_networkStream.CanRead)
                {
                    continue;
                }

                byte[] bytes = new byte[8];
                await _networkStream.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                
                uint length = BitConverter.ToUInt32(bytes, 0);
                uint magic = BitConverter.ToUInt32(bytes, 4);

                if (magic != _tcpMagic)
                {
                    return;
                }

                bytes = new byte[length];
                await ReceiveDataAsync(bytes, bytes.Length, cancellationToken).ConfigureAwait(false);
                
                ReceivedData?.Invoke(this, bytes);
            }
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
