﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using FluentScheduler;
using LittleSteve.Data;
using LittleSteve.Data.Entities;
using LittleSteve.Jobs;
using LittleSteve.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LittleSteve.Modules
{
    [Group("youtube")]
    [RequireContext(ContextType.Guild)]
    public class YoutubeModule : ModuleBase<SteveBotCommandContext>
    {
        private readonly SteveBotContext _botContext;

        public YoutubeModule(SteveBotContext botContext)
        {
            _botContext = botContext;
        }
        [Command]
        public async Task Youtube()
        {
            if (string.IsNullOrWhiteSpace(Context.GuildOwner?.YoutuberId))
            {
                return;
            }

            var video = await YoutubeFeedReader.GetLastestVideoFromFeed(Context.GuildOwner.YoutuberId);
            await ReplyAsync(video.Url);
        }
        [Command("add")]
        public async Task AddTwitch(string youtubeId, IGuildChannel guildChannel)
        {
          
            var video = await YoutubeFeedReader.GetLastestVideoFromFeed(youtubeId);
            if (video is null)
            {
                await ReplyAsync("I am unable to find information for that channel");
                return;
            }

            var youtuber = await _botContext.Youtubers.Include(x => x.YoutubeAlertSubscriptions)
                .FirstOrDefaultAsync(x => x.Id == youtubeId);


            if (youtuber is null)
            {
                youtuber = new Youtuber
                {
                    Id = youtubeId,
                    Name = video.User,
                    YoutubeAlertSubscriptions = new List<YoutubeAlertSubscription>()
                };
                _botContext.Youtubers.Add(youtuber);
                JobManager.AddJob(
                    () => new YoutubeMonitoringJob(youtuber.Id, Context.Provider.GetService<SteveBotContext>(),
                        Context.Client).Execute(), s => s.WithName(youtubeId).ToRunEvery(60).Seconds());
            }

            if (youtuber.YoutubeAlertSubscriptions.Any(x => x.DiscordChannelId == (long) guildChannel.Id))
            {
                await ReplyAsync($"You already subscribed to {youtuber.Name} in {guildChannel.Name}");
                return;
            }

            youtuber.YoutubeAlertSubscriptions.Add(new YoutubeAlertSubscription
            {
                DiscordChannelId = (long) guildChannel.Id,
                YoutuberId = youtuber.Id
            });
            var changes = _botContext.SaveChanges();

            if (changes > 0)
            {
                await ReplyAsync($"Alert for {youtuber.Name} added to {guildChannel.Name}");
            }
            else
            {
                await ReplyAsync($"Unable to create Alert for {youtuber.Name}");
            }
        }

        [Command("remove")]
        public async Task RemoveYoutube(string youtubeName, IGuildChannel guildChannel)
        {
            var youtuber = await _botContext.Youtubers.Include(x=> x.YoutubeAlertSubscriptions).FirstOrDefaultAsync(x =>
                x.Name.Equals(youtubeName, StringComparison.CurrentCultureIgnoreCase));

            if (youtuber is null)
            {
                await ReplyAsync("Channel Not Found");
                return;
            }

            var alert = youtuber.YoutubeAlertSubscriptions.FirstOrDefault(x =>
                x.DiscordChannelId == (long) guildChannel.Id);
            if (alert is null)
            {
                await ReplyAsync($"This channel doesnt contain an alert for {youtuber.Name}");
                return;
            }
            youtuber.YoutubeAlertSubscriptions.Remove(alert);
            if (Context.GuildOwner.YoutuberId == youtuber.Id)
            {
                var owner = await _botContext.GuildOwners.FindAsync(Context.GuildOwner.DiscordId, Context.GuildOwner.GuildId);
                owner.TwitchStreamerId = 0;
            }

            var changes = _botContext.SaveChanges();

            if (changes > 0)
            {
                await ReplyAsync($"Alert for {youtuber.Name} removed from {guildChannel.Name}");
            }
            else
            {
                await ReplyAsync($"Unable to remove Alert for {youtuber.Name}");
            }

        }
    }
}