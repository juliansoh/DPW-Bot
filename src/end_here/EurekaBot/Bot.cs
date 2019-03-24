// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.EurekaBot
{
	public class EurekaBot : IBot
	{
		readonly BotConfiguration _botConfiguration;
		readonly IConfiguration _configuration;
		readonly ILogger _logger;

		static QnAMaker _qnaMakerService;

		public EurekaBot(IConfiguration configuration, ILoggerFactory loggerFactory, BotConfiguration botConfiguration)
		{
			if (loggerFactory == null)
			{
				throw new System.ArgumentNullException(nameof(loggerFactory));
			}

			_configuration = configuration;
			_botConfiguration = botConfiguration;
			_logger = loggerFactory.CreateLogger<EurekaBot>();
			_logger.LogTrace("EurekaBot turn start.");
		}

		public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
		{
			switch (turnContext.Activity.Type)
			{
				case ActivityTypes.ConversationUpdate:

					if (turnContext.Activity.MembersAdded != null)
					{
						var reply = turnContext.Activity.CreateReply();
						reply.Attachments.Add(new WelcomeCard(_configuration).ToAttachment());

						foreach (var newMember in turnContext.Activity.MembersAdded)
						{
							//Only send a notification to new members other than the bot
							if (newMember.Id != turnContext.Activity.Recipient.Id)
							{
								await turnContext.SendActivityAsync(reply);
							}
						}
					}

					break;

				case ActivityTypes.Message:

					await LookupAnswerInKnowledgeBase(turnContext, cancellationToken);
					break;
			}
		}

		async Task LookupAnswerInKnowledgeBase(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (string.IsNullOrEmpty(turnContext.Activity.Text))
				return;

			//Make sure we have a valid QnAMaker service to use
			EnsureQnAMakerService();

			//Get any possible answers to the question typed
			var results = await _qnaMakerService.GetAnswersAsync(turnContext);
			if (results != null && results.Any())
			{
				var result = results.First();
				var reply = turnContext.Activity.CreateReply(result.Answer);

				//We want to track tag the score too so Middleware has access to it
				reply.Properties.Add("qna_score", result.Score);

				//Return the first result (you could also ensure the result.Score is of a minimum threshold)
				await turnContext.SendActivityAsync(reply);
			}
			else
			{
				await turnContext.SendActivityAsync(_configuration["noAnswerMessage"], cancellationToken: cancellationToken);
			}
		}

		void EnsureQnAMakerService()
		{
			if(_qnaMakerService != null)
				return;

			//Iterate through all services in the .bot file and get the first one with 'qna' as the type
			var service = _botConfiguration.Services.FirstOrDefault(s => s.Type == ServiceTypes.QnA) as QnAMakerService;

			var qnaEndpoint = new QnAMakerEndpoint()
			{
				KnowledgeBaseId = service.KbId,
				EndpointKey = service.EndpointKey,
				Host = service.Hostname,
			};

			_qnaMakerService = new QnAMaker(qnaEndpoint);
		}
	}
}
