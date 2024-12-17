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

using QuantConnect.Api;
using System;

namespace QuantConnect.Brokerages.CharlesSchwab.Models;

/// <summary>
/// Represents the response for an access token request in Lean Brokerage.
/// </summary>
public class LeanAccessTokenMetaDataResponse : RestResponse
{
    /// <summary>
    /// The access token returned in the response.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// The expiration date and time of the access token.
    /// </summary>
    public DateTime AccessTokenExpires { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeanAccessTokenMetaDataResponse"/> class with the specified access token.
    /// </summary>
    /// <param name="accessToken">The access token returned by the request.</param>
    public LeanAccessTokenMetaDataResponse(string accessToken)
    {
        AccessToken = accessToken;
        // A Trader API access token is valid for 30 minutes after creation.
        AccessTokenExpires = DateTime.UtcNow.AddMinutes(29); // The 1 minute buffer for expiration
    }
}
