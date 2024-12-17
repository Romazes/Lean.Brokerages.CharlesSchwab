/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using QuantConnect.Brokerages.CharlesSchwab.Models;

namespace QuantConnect.Brokerages.CharlesSchwab.Api;

/// <summary>
/// Provides base functionality for token handling, including retries and token refresh.
/// </summary>
public abstract class TokenHandler : DelegatingHandler, ITokenRefreshHandler
{
    /// <summary>
    /// Represents the maximum number of retry attempts for an authenticated request.
    /// </summary>
    private int _maxRetryCount = 3;

    /// <summary>
    /// Represents the time interval between retry attempts for an authenticated request.
    /// </summary>
    private TimeSpan _retryInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenHandler"/> class.
    /// </summary>
    /// <param name="innerHandler">The inner HTTP message handler for processing HTTP requests.</param>
    protected TokenHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    /// <summary>
    /// Retrieves an access token. Must be implemented by derived classes.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The valid access token as a string.</returns>
    public abstract Task<string> GetAccessToken(CancellationToken cancellationToken);

    /// <summary>
    /// Sends an HTTP request with automatic retries and token refresh on authorization failure.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The HTTP response message.</returns>
    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = default(HttpResponseMessage);
        var accessToken = await GetAccessToken(cancellationToken);
        for (var retryCount = 0; retryCount < _maxRetryCount; retryCount++)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            response = await base.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                break;
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                accessToken = await GetAccessToken(cancellationToken);
            }
            else
            {
                break;
            }

            // Wait for retry interval or cancellation request
            if (cancellationToken.WaitHandle.WaitOne(_retryInterval))
            {
                break;
            }
        }

        return response;
    }
}
