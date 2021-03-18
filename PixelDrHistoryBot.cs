// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace PixelDrHistoryBot
{
    /// <summary>
    /// Main entry point and orchestration for bot.
    /// </summary>
    public class PixelDrHistoryBot : IBot
    {
        private readonly BotServices _services;
        private readonly ConversationState _conversationState;
        private readonly UserState _userState;
        private IStatePropertyAccessor<UserHistoryState> _userStateAccessor;
        private UserHistoryState _userHistoryState;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelDrHistoryBot"/> class.
        /// </summary>
        /// <param name="botServices">Bot services.</param>
        /// <param name="conversationState">Bot conversation state.</param>
        /// <param name="userState">Bot user state.</param>
        public PixelDrHistoryBot(BotServices botServices, ConversationState conversationState, UserState userState)
        {
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            _userState = userState ?? throw new ArgumentNullException(nameof(userState));
            _services = botServices ?? throw new ArgumentNullException(nameof(botServices));
            _userStateAccessor = _userState.CreateProperty<UserHistoryState>(nameof(UserHistoryState));
        }

        /// <summary>
        /// Run every turn of the conversation. Handles orchestration of messages.
        /// </summary>
        /// <param name="turnContext">Bot Turn Context.</param>
        /// <param name="cancellationToken">Task CancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Update the current user's history-taking state
            _userHistoryState = await _userStateAccessor.GetAsync(turnContext, () => new UserHistoryState());

            if (turnContext.Activity.Type == ActivityTypes.Message && !turnContext.Responded)
            {
                var luisService = _services.LuisServices["PixelDrHistoryBot_General"];
                var result = await luisService.RecognizeAsync(turnContext, CancellationToken.None);

                string response = "{\"sentiment\":" + result.Properties.Values.FirstOrDefault().ToString() + ", ";

                if (result.GetTopScoringIntent().score > 0.75)
                {
                    // Return appropriate dialog depending on intent detected
                    await ProcessLuisIntent(turnContext, result, response);
                }
                else
                {
                    await ProcessQnAMakerResponse(turnContext, response);
                }
            }
        }

        private async Task ProcessLuisIntent(ITurnContext turnContext, RecognizerResult result, string response)
        {
            // Match top LUIS intent to appropriate action
            switch (result.GetTopScoringIntent().intent)
            {
                // AMTS: Intent to give patient an address to remember later on - extracts address entity from sentence and stores for later retrieval
                case "AMTSRememberAddress":
                    {
                        if (result.Entities.Count > 1)
                        {
                            response += "\"text\":\"Okay Doctor, " + result.Entities.GetValue("AMTSAddress")[0] + ", I'll remember it.\", \"type\":\"AMTSRememberAddress\"}";

                            // Save the address into state and recall later
                            _userHistoryState.patientAMTSAddress = result.Entities.GetValue("AMTSAddress")[0].ToString();
                            await _userStateAccessor.SetAsync(turnContext, _userHistoryState);
                        }
                        else
                        {
                            response += "\"text\":\"Sure Doctor, what's the address?\", \"type\":\"AMTSRememberAddress\"}";
                            // Prompt user for address
                        }
                        await turnContext.SendActivityAsync(response);
                        break;
                    }

                // AMTS: Intent to recall an address that was specified earlier by the user
                case "AMTSRecallAddress":
                    {
                        if (_userHistoryState.patientAMTSAddress != null)
                        {
                            response += "\"text\":\"I think it was " + _userHistoryState.patientAMTSAddress + ".\", \"type\":\"AMTSRecallAddress\"}";
                        }
                        else
                        {
                            response += "\"text\":\"I don't think you told me an address Doctor.\", \"type\":\"AMTSRememberAddress\"}";
                        }
                        await turnContext.SendActivityAsync(response);
                        break;
                    }

                // If intent isn't matched for some reason, dispatch to QnAMaker
                default:
                    {
                        await ProcessQnAMakerResponse(turnContext, response);
                        break;
                    }
            }
        }
        private async Task ProcessQnAMakerResponse(ITurnContext turnContext, string response)
        {
            var qnaService = _services.QnAServices["PixelDrHistoryBot"];
            var answers = await qnaService.GetAnswersAsync(turnContext);

            if (answers.Any())
            {
                string metadata = "";
                foreach (var metadataPair in answers.First().Metadata)
                {
                    metadata += "\"" + metadataPair.Name + "\": \"" + metadataPair.Value + "\",";
                }
                response += "\"text\":\"" + answers.First().Answer + "\", \"type\":\"QnA\", \"metadata\": {" + metadata + "}}";
            }
            else
            {
                response += "\"text\":\"Sorry Doctor, I'm not sure what you mean.\", \"type\":\"NoMatchFound\"}";
            }
            await turnContext.SendActivityAsync(response);
        }
    }
}
