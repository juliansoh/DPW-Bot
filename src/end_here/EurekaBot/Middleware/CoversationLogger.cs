// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Configuration;

namespace Microsoft.EurekaBot
{
	public class ConversationLogger : IMiddleware
	{
		readonly BotConfiguration _botConfiguration;
		static DocumentClient _cosmosClient;
		static Uri _collectionLink;

		public ConversationLogger(BotConfiguration botConfiguration)
		{
			_botConfiguration = botConfiguration;
		}

		async public Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default(CancellationToken))
		{
			turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
			{
				var responses = await nextSend().ConfigureAwait(false);

				if (!string.IsNullOrWhiteSpace(turnContext.Activity.Text))
				{
					//Make sure our Cosmos DB is all ready to go
					await EnsureDatabaseConfigured();

					foreach (var activity in activities)
					{
						if (string.IsNullOrWhiteSpace(activity.Text))
							continue;

						dynamic log = new
						{
							question = turnContext.Activity.Text,
							answer = activity.Text,
							score = activity.Properties["qna_score"],
							userId = activity.ReplyToId,
						};

						await RecordLogEntry(log);
					}

					return responses;
				}

				return null;
			});

			await next(cancellationToken);
		}
		async Task RecordLogEntry(dynamic item)
		{
			try
			{
				await _cosmosClient.CreateDocumentAsync(_collectionLink, item);
			}
			catch (DocumentClientException dce)
			{
				Console.WriteLine($"Unable to save to Cosmos: {dce.GetBaseException()}");
			}
		}

		//Ensures the Cosmos database, collection and client are all created and assigned
		async Task EnsureDatabaseConfigured()
		{
			var service = _botConfiguration.Services.FirstOrDefault(s => s.Type == ServiceTypes.CosmosDB) as CosmosDbService;

			if (_cosmosClient == null)
			{
				_collectionLink = UriFactory.CreateDocumentCollectionUri(service.Database, service.Collection);
				_cosmosClient = new DocumentClient(new Uri(service.Endpoint), service.Key, ConnectionPolicy.Default);
			}

			var db = new Database { Id = service.Database };
			var collection = new DocumentCollection { Id = service.Collection };

			//Create the database
			var result = await _cosmosClient.CreateDatabaseIfNotExistsAsync(db);

			if (result.StatusCode == HttpStatusCode.Created || result.StatusCode == HttpStatusCode.OK)
			{
				//Create the collection
				var dbLink = UriFactory.CreateDatabaseUri(service.Database);
				await _cosmosClient.CreateDocumentCollectionIfNotExistsAsync(dbLink, collection);
			}
		}
	}
}