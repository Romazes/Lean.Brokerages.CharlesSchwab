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
using RestSharp;
using System.Net.Http;
using Newtonsoft.Json;
using QuantConnect.Api;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using QuantConnect.Brokerages.CharlesSchwab.Models;

namespace QuantConnect.Brokerages.CharlesSchwab.Api;

/// <summary>
/// Handles the retrieval and refreshing of access tokens for Charles Schwab integration using Lean API.
/// </summary>
public class CharlesSchwabLeanTokenHandler : TokenHandler
{
    /// <summary>
    /// Stores metadata about the Lean access token and its expiration details.
    /// </summary>
    private LeanAccessTokenMetaDataResponse _accessTokenMetaData;

    /// <summary>
    /// API client for communicating with the Lean platform.
    /// </summary>
    private readonly ApiConnection _leanApiClient;

    /// <summary>
    /// The JSON body request used for API metadata operations.
    /// </summary>
    private readonly string _jsonBodyRequest;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharlesSchwabLeanTokenHandler"/> class.
    /// </summary>
    /// <param name="innerHandler">The inner HTTP message handler for processing HTTP requests.</param>
    /// <param name="leanApiClient">The API connection to communicate with the Lean platform.</param>
    /// <param name="brokerageName">The name of the brokerage associated with the token.</param>
    /// <param name="deployId">The deployment ID of the associated project.</param>
    /// <param name="projectId">The project ID associated with the deployment.</param>
    /// <param name="accountNumber">The account number associated with the brokerage account.</param>
    public CharlesSchwabLeanTokenHandler(HttpMessageHandler innerHandler, ApiConnection leanApiClient, string brokerageName, string deployId, int projectId, string accountNumber)
        : base(innerHandler)
    {
        _leanApiClient = leanApiClient ?? throw new ArgumentNullException(nameof(leanApiClient));

        _jsonBodyRequest = JsonConvert.SerializeObject(
                new LeanAccessTokenMetaDataRequest(brokerageName, deployId, projectId, accountNumber),
                new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver(), Formatting = Formatting.None }
                );

    }

    /// <summary>
    /// Retrieves a valid access token. Refreshes the token if it has expired.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The valid access token as a string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the token cannot be refreshed or retrieved.</exception>
    public override async Task<string> GetAccessToken(CancellationToken cancellationToken)
    {
        if (_accessTokenMetaData != null && DateTime.UtcNow < _accessTokenMetaData?.AccessTokenExpires)
        {
            return _accessTokenMetaData.AccessToken;
        }

        try
        {
            var request = new RestRequest("live/auth0/refresh", Method.POST);
            request.AddJsonBody(_jsonBodyRequest);

            var response = await _leanApiClient.TryRequestAsync<LeanAccessTokenMetaDataResponse>(request);

            if (response.Item1)
            {
                _accessTokenMetaData = response.Item2;
                return response.Item2.AccessToken;
            }

            throw new InvalidOperationException($"{nameof(CharlesSchwabTokenRefreshHandler)}.{nameof(GetAccessToken)}: {string.Join(",", response.Item2.Errors)}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{nameof(CharlesSchwabTokenRefreshHandler)}.{nameof(GetAccessToken)}: {ex.Message}");
        }
    }
}
