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
using System.Threading;
using QuantConnect.Orders;
using System.Collections.Generic;

namespace QuantConnect.Brokerages.CharlesSchwab.Tests;

public partial class CharlesSchwabBrokerageTests
{
    //public record ComboLimitPriceByOptionContracts(decimal ComboLimitPrice, IReadOnlyCollection<OptionContractByQuantity> OptionContracts, decimal groupOrderManagerQuantity);
    //public record OptionContractByQuantity(Symbol Symbol, decimal Quantity);

    private static IEnumerable<TestCaseData> ComboOrderTestParameters
    {
        get
        {
            var F_Equity = Symbol.Create("F", SecurityType.Equity, Market.USA);
            var options = new[]
            {
                Symbol.CreateOption(F_Equity, Market.USA, SecurityType.Option.DefaultOptionStyle(), OptionRight.Call, 11m, new DateTime(2024, 12, 20)),
                Symbol.CreateOption(F_Equity, Market.USA, SecurityType.Option.DefaultOptionStyle(), OptionRight.Call, 21.82m, new DateTime(2024, 12, 20))
            };
            yield return new(0.01m, 1m, options);
            yield return new(0.01m, -1m, options);


            var VIX_Index = Symbol.Create("VIX", SecurityType.Index, Market.USA);
            var indexOptions = new[]
                {
                    Symbol.CreateOption(VIX_Index, Market.USA, SecurityType.IndexOption.DefaultOptionStyle(), OptionRight.Call, 12m, new DateTime(2024, 12, 18)),
                    Symbol.CreateOption(VIX_Index, Market.USA, SecurityType.IndexOption.DefaultOptionStyle(), OptionRight.Put, 15m, new DateTime(2024, 12, 18))
                };

            yield return new(0.01m, 1m, indexOptions);
        }
    }

    [Test]
    public void PlaceComboMarketOrder()
    {
        var F_Equity = Symbol.Create("F", SecurityType.Equity, Market.USA);
        var options = new[]
            {
                Symbol.CreateOption(F_Equity, Market.USA, SecurityType.Option.DefaultOptionStyle(), OptionRight.Call, 11m, new DateTime(2024, 12, 20)),
                Symbol.CreateOption(F_Equity, Market.USA, SecurityType.Option.DefaultOptionStyle(), OptionRight.Put, 10m, new DateTime(2024, 12, 20))
            };

        var groupOrderManager = new GroupOrderManager(1, legCount: options.Length, quantity: 2);

        var comboOrders = PlaceComboOrder(
            options,
            null,
            (optionContract, quantity, price, groupOrderManager) =>
            new ComboMarketOrder(optionContract, quantity, DateTime.UtcNow, groupOrderManager),
            groupOrderManager);

        AssertComboOrderPlacedSuccessfully(comboOrders);
    }

    [TestCaseSource(nameof(ComboOrderTestParameters))]
    public void PlaceComboLimitOrder(decimal limitPrice, decimal groupOrderManagerQuantity, Symbol[] optionContracts)
    {
        var groupOrderManager = new GroupOrderManager(1, legCount: optionContracts.Length, groupOrderManagerQuantity, limitPrice);

        var comboOrders = PlaceComboOrder(
            optionContracts,
            limitPrice,
            (optionContract, quantity, price, groupOrderManager) =>
                new ComboLimitOrder(optionContract, quantity.GetOrderLegGroupQuantity(groupOrderManager), price.Value, DateTime.UtcNow, groupOrderManager),
            groupOrderManager);

        AssertComboOrderPlacedSuccessfully(comboOrders);
        CancelComboOpenOrders(comboOrders);
    }

    private void AssertComboOrderPlacedSuccessfully<T>(IReadOnlyCollection<T> comboOrders) where T : ComboOrder
    {
        Assert.IsTrue(comboOrders.All(o => o.Status.IsClosed() || o.Status == OrderStatus.Submitted));
    }

    private IReadOnlyCollection<T> PlaceComboOrder<T>(
    Symbol[] legs,
    decimal? orderLimitPrice,
    Func<Symbol, decimal, decimal?, GroupOrderManager, T> orderType, GroupOrderManager groupOrderManager) where T : ComboOrder
    {
        var comboOrders = legs
            .Select(optionContract => orderType(optionContract, 1m, orderLimitPrice, groupOrderManager))
            .ToList().AsReadOnly();

        var manualResetEvent = new ManualResetEvent(false);
        var orderStatusCallback = HandleComboOrderStatusChange(comboOrders, manualResetEvent, OrderStatus.Submitted);

        Brokerage.OrdersStatusChanged += orderStatusCallback;

        foreach (var comboOrder in comboOrders)
        {
            OrderProvider.Add(comboOrder);
            groupOrderManager.OrderIds.Add(comboOrder.Id);
            Assert.IsTrue(Brokerage.PlaceOrder(comboOrder));
        }

        Assert.IsTrue(manualResetEvent.WaitOne(TimeSpan.FromSeconds(60)));

        Brokerage.OrdersStatusChanged -= orderStatusCallback;

        return comboOrders;
    }

    private void CancelComboOpenOrders(IReadOnlyCollection<ComboLimitOrder> comboLimitOrders)
    {
        using var manualResetEvent = new ManualResetEvent(false);

        var orderStatusCallback = HandleComboOrderStatusChange(comboLimitOrders, manualResetEvent, OrderStatus.Canceled);

        Brokerage.OrdersStatusChanged += orderStatusCallback;

        var openOrders = OrderProvider.GetOpenOrders(order => order.Type == OrderType.ComboLimit);
        foreach (var openOrder in openOrders)
        {
            Assert.IsTrue(Brokerage.CancelOrder(openOrder));
        }

        if (openOrders.Count > 0)
        {
            Assert.IsTrue(manualResetEvent.WaitOne(TimeSpan.FromSeconds(60)));
        }

        Brokerage.OrdersStatusChanged -= orderStatusCallback;
    }

    private static EventHandler<List<OrderEvent>> HandleComboOrderStatusChange<T>(
    IReadOnlyCollection<T> comboOrders,
    ManualResetEvent manualResetEvent,
    OrderStatus expectedOrderStatus) where T : ComboOrder
    {
        return (_, orderEvents) =>
        {

            foreach (var order in comboOrders)
            {
                foreach (var orderEvent in orderEvents)
                {
                    if (orderEvent.OrderId == order.Id)
                    {
                        order.Status = orderEvent.Status;
                    }
                }

                if (comboOrders.All(o => o.Status.IsClosed()) || comboOrders.All(o => o.Status == expectedOrderStatus))
                {
                    manualResetEvent.Set();
                }
            }
        };
    }
}
