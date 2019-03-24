// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.BotFramework;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.EurekaBot
{
	public class Startup
	{
		private ILoggerFactory _loggerFactory;
		private bool _isProduction = false;

		public Startup(IHostingEnvironment env)
		{
			_isProduction = env.IsProduction();

			var builder = new ConfigurationBuilder()
				.SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				.AddEnvironmentVariables();

			Configuration = builder.Build();
		}


		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			BotConfiguration botConfig = null;
			var secretKey = Configuration.GetSection("botFileSecret")?.Value;
			var botFilePath = Configuration.GetSection("botFilePath")?.Value;
			if (!File.Exists(botFilePath))
			{
				throw new FileNotFoundException($"The .bot configuration file was not found. botFilePath: {botFilePath}");
			}

			try
			{
				botConfig = BotConfiguration.Load(botFilePath ?? @".\EurekaChatBot.bot", secretKey);
				
				// Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
				services.AddSingleton(_ => botConfig);
			}
			catch
			{
				var msg = @"Error reading bot file. Please ensure you have valid botFilePath and botFileSecret set for your environment.
						- You can find the botFilePath and botFileSecret in the Azure App Service application settings.
						- If you are running this bot locally, consider adding a appsettings.json file with botFilePath and botFileSecret.
						- See https://aka.ms/about-bot-file to learn more about .bot file its use and bot configuration.";

				//logger.LogError(msg);
				throw new InvalidOperationException(msg);
			}

			services.AddBot<EurekaBot>(options =>
			{
				// Creates a logger for the application to use.
				ILogger logger = _loggerFactory.CreateLogger<EurekaBot>();

				// Retrieve current endpoint.
				var environment = _isProduction ? "production" : "development";
				var service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
				if (service == null && _isProduction)
				{
					// Attempt to load development environment
					service = botConfig.Services.Where(s => s.Type == "endpoint" && s.Name == "development").FirstOrDefault();
					logger.LogWarning("Attempting to load development endpoint in production environment.");
				}

				if (!(service is EndpointService endpointService))
				{
					throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
				}

				options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);
				options.ChannelProvider = new ConfigurationChannelProvider(Configuration);

				// Catches any errors that occur during a conversation turn and logs them.
				options.OnTurnError = async (context, exception) =>
				{
					logger.LogError($"Exception caught : {exception}");
					await context.SendActivityAsync($"Oops - {exception.Message}");
				};
			});
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			_loggerFactory = loggerFactory;

			app.UseDefaultFiles()
				.UseStaticFiles()
				.UseBotFramework();
		}
	}
}
