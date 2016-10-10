﻿/*
 * Copyright 2016 Google Inc. All Rights Reserved.
 * Use of this source code is governed by a BSD-style
 * license that can be found in the LICENSE file or at
 * https://developers.google.com/open-source/licenses/bsd
 */

using Google.Protobuf;
using Grpc.Core;
using System;
using System.Threading.Tasks;

namespace Google.Api.Gax
{
    internal static class ApiCall
    {
        internal static ApiCall<TRequest, TResponse> Create<TRequest, TResponse>(
            Func<TRequest, CallOptions, AsyncUnaryCall<TResponse>> asyncGrpcCall,
            Func<TRequest, CallOptions, TResponse> syncGrpcCall,
            CallSettings baseCallSettings,
            IClock clock)
            where TRequest : class, IMessage<TRequest>
            where TResponse : class, IMessage<TResponse>
        {
            return new ApiCall<TRequest, TResponse>(
                asyncGrpcCall.WithTaskTransform().MapArg((CallSettings cs) => cs.ToCallOptions(clock)),
                syncGrpcCall.MapArg((CallSettings cs) => cs.ToCallOptions(clock)),
                baseCallSettings);
        }
    }

    /// <summary>
    /// Bridge between an RPC method (with synchronous and asynchronous variants) and higher level
    /// abstractions, applying call settings as required.
    /// </summary>
    /// <typeparam name="TRequest">RPC request type</typeparam>
    /// <typeparam name="TResponse">RPC response type</typeparam>
    public sealed class ApiCall<TRequest, TResponse>
        where TRequest : class, IMessage<TRequest>
        where TResponse : class, IMessage<TResponse>
    {
        internal ApiCall(
            Func<TRequest, CallSettings, Task<TResponse>> asyncCall,
            Func<TRequest, CallSettings, TResponse> syncCall,
            CallSettings baseCallSettings)
        {
            _asyncCall = GaxPreconditions.CheckNotNull(asyncCall, nameof(asyncCall));
            _syncCall = GaxPreconditions.CheckNotNull(syncCall, nameof(syncCall));
            _baseCallSettings = GaxPreconditions.CheckNotNull(baseCallSettings, nameof(baseCallSettings));
        }

        private readonly Func<TRequest, CallSettings, Task<TResponse>> _asyncCall;
        private readonly Func<TRequest, CallSettings, TResponse> _syncCall;
        private readonly CallSettings _baseCallSettings;

        private T Call<T>(TRequest request, CallSettings perCallCallSettings, Func<CallSettings, T> fn)
        {
            CallSettings callSettings = _baseCallSettings
                .Clone()
                .Merge(perCallCallSettings);
            return fn(callSettings);
        }

        /// <summary>
        /// Performs an RPC call asynchronously.
        /// </summary>
        /// <param name="request">The RPC request.</param>
        /// <param name="perCallCallSettings">The call settings to apply to this specific call,
        /// overriding defaults where necessary.</param>
        /// <returns>A task representing the asynchronous operation. The result of the completed task
        /// will be the RPC response.</returns>
        public Task<TResponse> Async(TRequest request, CallSettings perCallCallSettings) =>
            Call(request, perCallCallSettings, callSettings => _asyncCall(request, callSettings));

        /// <summary>
        /// Performs an RPC call synchronously.
        /// </summary>
        /// <param name="request">The RPC request.</param>
        /// <param name="perCallCallSettings">The call settings to apply to this specific call,
        /// overriding defaults where necessary.</param>
        /// <returns>The RPC response.</returns>
        public TResponse Sync(TRequest request, CallSettings perCallCallSettings) =>
            Call(request, perCallCallSettings, callSettings => _syncCall(request, callSettings));

        internal ApiCall<TRequest, TResponse> WithUserAgent(string userAgent)
        {
            return new ApiCall<TRequest, TResponse>(
                _asyncCall.MapArg(callSettings => callSettings.AddUserAgent(userAgent)),
                _syncCall.MapArg(callSettings => callSettings.AddUserAgent(userAgent)),
                _baseCallSettings);
        }

        internal ApiCall<TRequest, TResponse> WithRetry(IClock clock, IScheduler scheduler)
        {
            return new ApiCall<TRequest, TResponse>(
                _asyncCall.WithRetry(clock, scheduler),
                _syncCall.WithRetry(clock, scheduler),
                _baseCallSettings);
        }
    }
}
