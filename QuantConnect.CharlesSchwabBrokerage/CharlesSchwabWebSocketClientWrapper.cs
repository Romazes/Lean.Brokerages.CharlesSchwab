﻿/*
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
using Newtonsoft.Json;
using System.Threading;
using Newtonsoft.Json.Linq;
using QuantConnect.Logging;
using System.Threading.Tasks;
using QuantConnect.Brokerages.CharlesSchwab.Api;
using QuantConnect.Brokerages.CharlesSchwab.Models;
using QuantConnect.Brokerages.CharlesSchwab.Models.Stream;
using QuantConnect.Brokerages.CharlesSchwab.Models.Enums.Stream;

namespace QuantConnect.Brokerages.CharlesSchwab;

/// <summary>
/// A wrapper for the Charles Schwab WebSocket client that facilitates streaming market data and account updates through a WebSocket connection.
/// </summary>
public class CharlesSchwabWebSocketClientWrapper : WebSocketClientWrapper
{
    /// <summary>
    /// Represents a way of tracking streaming requests made.
    /// The field should be increased for each new stream request made. 
    /// </summary>
    private int _idRequestCount = 0;

    /// <summary>
    /// JSON serializer settings used for serializing and deserializing messages.
    /// </summary>
    private JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

    /// <summary>
    /// The streamer information retrieved from the Charles Schwab API.
    /// </summary>
    private readonly StreamerInfo _streamInfo;

    /// <summary>
    /// The API client used to interact with the Charles Schwab services.
    /// </summary>
    private readonly CharlesSchwabApiClient _charlesSchwabApiClient;

    /// <summary>
    /// A cancellation token source used to manage cancellation of asynchronous operations.
    /// </summary>
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private event EventHandler<AccountContent> OrderUpdate;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharlesSchwabWebSocketClientWrapper"/> class.
    /// </summary>
    /// <param name="charlesSchwabApiClient">The API client used to fetch user preferences and access tokens.</param>
    public CharlesSchwabWebSocketClientWrapper(CharlesSchwabApiClient charlesSchwabApiClient, EventHandler<AccountContent> orderUpdateHandler)
    {
        _charlesSchwabApiClient = charlesSchwabApiClient;
        _streamInfo = charlesSchwabApiClient.GetUserPreference().SynchronouslyAwaitTaskResult().StreamerInfo.First();

        Initialize(_streamInfo.StreamerSocketUrl);

        Open += async (_, _) => await LoginRequest(_cancellationTokenSource.Token);
        Message += HandleWebSocketMessage;
        OrderUpdate += orderUpdateHandler;
    }

    private void CharlesSchwabWebSocketClientWrapper_OrderUpdate(object sender, AccountContent e)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Handles incoming WebSocket messages from the Charles Schwab API.
    /// This method parses the received message and processes the response based on its type.
    /// </summary>
    /// <param name="_">The source of the event, typically the WebSocket client.</param>
    /// <param name="webSocketMessage">The WebSocket message received, containing JSON data to be processed.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the response contains an unexpected service type,
    /// or if the Admin service's content code is non-zero, indicating an error.
    /// </exception>
    private void HandleWebSocketMessage(object _, WebSocketMessage webSocketMessage)
    {
        var parsedResponse = ParseResponse(webSocketMessage);

        switch (parsedResponse)
        {
            case NotifyResponse:
                break;
            case StreamResponse streamResponse:
                HandleStreamResponse(streamResponse);
                break;
            case DataResponse dataResponse:
                HandleDataResponse(dataResponse);
                break;
            default:
                throw new InvalidOperationException($"{nameof(CharlesSchwabWebSocketClientWrapper)}.{nameof(HandleWebSocketMessage)}: Unknown stream response type. {(webSocketMessage.Data as TextMessage).Message}");

        }
    }

    /// <summary>
    /// Handles data responses from the Charles Schwab API.
    /// </summary>
    /// <param name="dataResponse">The data response containing a collection of data items to process.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the service type of the data is unexpected or unsupported.
    /// </exception>
    private void HandleDataResponse(DataResponse dataResponse)
    {
        foreach (var data in dataResponse.Data)
        {
            switch (data.Service)
            {
                case Service.Account:
                    foreach (var content in data.Content)
                    {
                        OrderUpdate?.Invoke(this, content);
                    }
                    break;
                default:
                    throw new NotSupportedException($"{nameof(CharlesSchwabWebSocketClientWrapper)}.{nameof(HandleDataResponse)}: Unsupported service type {data.Service}.");
            }
        }
    }

    /// <summary>
    /// Handles the incoming stream response by processing each individual response.
    /// This method checks the service type and performs actions based on the response content.
    /// </summary>
    /// <param name="streamResponse">The stream response containing a collection of individual responses to process.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an unexpected service type is encountered or if the response code indicates an error.
    /// </exception>
    private void HandleStreamResponse(StreamResponse streamResponse)
    {
        foreach (var response in streamResponse.Responses)
        {
            switch (response.Service)
            {
                case Service.Admin when response.Content.Code == 0:
                    SendMessage(new AccountStreamRequest(_idRequestCount, _streamInfo.SchwabClientCustomerId, _streamInfo.SchwabClientCorrelId));
                    break;
                case Service.Account:
                    continue;
                default:
                    throw new NotSupportedException($"{nameof(CharlesSchwabWebSocketClientWrapper)}.{nameof(HandleStreamResponse)}: {response.Content.Code} - {response.Content.Message}");
            }
        }
    }

    /// <summary>
    /// Parses a WebSocket message and deserializes it into a specific response type based on the JSON structure.
    /// </summary>
    /// <param name="webSocketMessage">The WebSocket message containing the JSON data to be parsed.</param>
    /// <returns>
    /// An instance of <see cref="IStreamBaseResponse"/> representing the parsed message as a 
    /// <see cref="NotifyResponse"/>, <see cref="StreamResponse"/>, or <see cref="DataResponse"/> based on the message type.
    /// </returns>
    /// <exception cref="JsonException">
    /// Thrown if the message cannot be parsed into any known response type.
    /// </exception>
    private IStreamBaseResponse ParseResponse(WebSocketMessage webSocketMessage)
    {
        var jsonWebSocketMessage = (webSocketMessage.Data as TextMessage).Message;

        if (Log.DebuggingEnabled)
        {
            Log.Debug($"{nameof(CharlesSchwabWebSocketClientWrapper)}.{nameof(HandleWebSocketMessage)}.WS.JSON: {jsonWebSocketMessage}");
        }

        var jOjbect = JObject.Parse(jsonWebSocketMessage);

        if (jOjbect.ContainsKey("notify"))
        {
            return JsonConvert.DeserializeObject<NotifyResponse>(jsonWebSocketMessage);
        }
        else if (jOjbect.ContainsKey("response"))
        {
            return JsonConvert.DeserializeObject<StreamResponse>(jsonWebSocketMessage);
        }
        else if (jOjbect.ContainsKey("data"))
        {
            return JsonConvert.DeserializeObject<DataResponse>(jsonWebSocketMessage);
        }

        throw new JsonException($"{nameof(CharlesSchwabWebSocketClientWrapper)}.{nameof(ParseResponse)}: Unable to parse response:");
    }

    /// <summary>
    /// Asynchronously sends a login request to the Charles Schwab API using the provided stream information and access token.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task LoginRequest(CancellationToken cancellationToken)
    {
        var accessToken = await _charlesSchwabApiClient.GetAccessToken(cancellationToken);

        var adminLoginRequest = new AdminStreamRequest(
            _idRequestCount,
            Command.Login,
            _streamInfo.SchwabClientCustomerId,
            _streamInfo.SchwabClientCorrelId,
            accessToken,
            _streamInfo.SchwabClientChannel,
            _streamInfo.SchwabClientFunctionId);

        SendMessage(adminLoginRequest);
    }

    /// <summary>
    /// Sends a JSON message over the WebSocket connection and increments the request count.
    /// </summary>
    /// <param name="streamRequest">The request object containing message data to be serialized and sent.</param>
    private void SendMessage(StreamRequest streamRequest)
    {
        var jsonMessage = JsonConvert.SerializeObject(streamRequest, _jsonSerializerSettings);

        if (Log.DebuggingEnabled)
        {
            Log.Debug($"{nameof(CharlesSchwabWebSocketClientWrapper)}.{nameof(SendMessage)}: {jsonMessage}");
        }

        Send(jsonMessage);
        Interlocked.Increment(ref _idRequestCount);
    }
}