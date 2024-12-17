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

using Newtonsoft.Json;

namespace QuantConnect.Brokerages.CharlesSchwab.Models;

/// <summary>
/// Represents a request for an access token in Lean Brokerage.
/// </summary>
public readonly struct LeanAccessTokenMetaDataRequest
{
    /// <summary>
    /// The brokerage associated with the access token request.
    /// </summary>
    public string Brokerage { get; }

    /// <summary>
    /// The deployment identifier for the project.
    /// </summary>
    public string DeployId { get; }

    /// <summary>
    /// The project identifier for the access token request.
    /// </summary>
    public int ProjectId { get; }

    /// <summary>
    /// The Charles Schwab account number associated with the request.
    /// </summary>
    [JsonProperty("accountId")]
    public string AccountNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeanAccessTokenMetaDataRequest"/> struct with the specified parameters.
    /// </summary>
    /// <param name="brokerage">The name of the brokerage.</param>
    /// <param name="deployId">The deployment identifier.</param>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="accountNumber">The Charles Schwab account number.</param>
    public LeanAccessTokenMetaDataRequest(string brokerage, string deployId, int projectId, string accountNumber)
        => (Brokerage, DeployId, ProjectId, AccountNumber) = (brokerage.ToLowerInvariant(), deployId, projectId, accountNumber);
}