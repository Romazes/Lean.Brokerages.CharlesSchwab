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
using System.Linq;
using NUnit.Framework;
using System.Net.Http;
using System.Threading;
using QuantConnect.Api;
using QuantConnect.Util;
using QuantConnect.Tests;
using System.Threading.Tasks;
using QuantConnect.Interfaces;
using QuantConnect.Configuration;
using System.Collections.Generic;
using QuantConnect.Brokerages.CharlesSchwab.Api;

namespace QuantConnect.Brokerages.CharlesSchwab.Tests;

[TestFixture]
public class CharlesSchwabBrokerageAdditionalTests
{
    [Test]
    public void ParameterlessConstructorComposerUsage()
    {
        var brokerage = Composer.Instance.GetExportedValueByTypeName<IDataQueueHandler>("CharlesSchwabBrokerage");
        Assert.IsNotNull(brokerage);
    }

    private static IEnumerable<TestCaseData> LookUpSymbolsTestParameters
    {
        get
        {
            yield return new TestCaseData(Symbols.AAPL, false);
            yield return new TestCaseData(Symbols.SPY, false);
            yield return new TestCaseData(Symbol.Create("VIX", SecurityType.Index, Market.USA), false);
            yield return new TestCaseData(Symbol.Create("DJI", SecurityType.Index, Market.USA), true);
        }
    }

    [Test, TestCaseSource(nameof(LookUpSymbolsTestParameters))]
    public void LookUpSymbols(Symbol symbol, bool isEmptyResult)
    {
        var option = Symbol.CreateCanonicalOption(symbol);

        var dataQueueUniverseProvider = TestSetup.CreateBrokerage(null, null);

        var optionChain = dataQueueUniverseProvider.LookupSymbols(option, false).ToList();

        Assert.IsNotNull(optionChain);

        if (isEmptyResult)
        {
            Assert.IsEmpty(optionChain);
        }
        else
        {
            Assert.True(optionChain.Any());
            Assert.Greater(optionChain.Count, 0);
            Assert.That(optionChain.Distinct().ToList().Count, Is.EqualTo(optionChain.Count));
        }

        dataQueueUniverseProvider.DisposeSafely();
    }

    [Test]
    public void GetAuthorizationUrl()
    {
        var baseUrl = Config.Get("charles-schwab-api-url");
        var redirectUrl = Config.Get("charles-schwab-redirect-url");

        var appKey = Config.Get("charles-schwab-app-key");

        var tokenRefreshHandler = new CharlesSchwabTokenRefreshHandler(new HttpClientHandler(), baseUrl, appKey, string.Empty, redirectUrl, string.Empty, string.Empty);

        var authorizationUrl = tokenRefreshHandler.GetAuthorizationUrl();

        Assert.IsNotNull(authorizationUrl);
        Assert.IsNotEmpty(authorizationUrl);

        Assert.Pass($"Charles Schwab, Authorization URL: {authorizationUrl}");
    }

    [Test]
    public async Task TestSendSignInQuantConnectAsync()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var accountNumber = Config.Get("charles-schwab-account-number");
        var leanApiClient = new ApiConnection(Globals.UserId, Globals.UserToken);

        if (!leanApiClient.Connected)
        {
            throw new ArgumentException("Invalid api user id or token, cannot authenticate subscription.");
        }

        var leanTokenHandler = new CharlesSchwabLeanTokenHandler(new HttpClientHandler(), leanApiClient, "CharlesSchwab", "L-test", Globals.ProjectId, accountNumber);

        var result = await leanTokenHandler.GetAccessToken(cancellationTokenSource.Token);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result);
    }
}