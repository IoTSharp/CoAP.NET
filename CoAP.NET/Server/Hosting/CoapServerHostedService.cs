/*
 * Copyright (c) 2011-2015, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoAP.Server.Hosting
{
    internal sealed class CoapServerHostedService : IHostedService
    {
        private readonly IServer _server;
        private int _started;

        public CoapServerHostedService(IServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _started, 1) == 0)
            {
                _server.Start();
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _started, 0) == 1)
            {
                _server.Stop();
            }

            return Task.CompletedTask;
        }
    }
}
