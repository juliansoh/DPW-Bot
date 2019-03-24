// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;

namespace Microsoft.EurekaBot
{
	public class WelcomeCard : VideoCard
	{
		public WelcomeCard(IConfiguration config)
		{
			Title = config["welcomeCard:title"];
			Text = config["welcomeCard:description"];

			Media = new List<MediaUrl> { new MediaUrl(config["welcomeCard:videoUrl"]) };
			Buttons = new List<CardAction>
			{
				new CardAction(ActionTypes.OpenUrl, "Learn More", value: config["welcomeCard:learnMoreUrl"])
			};
		}
	}
}