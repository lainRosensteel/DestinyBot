﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace LittleSteve.Preconditions
{
    public class RequireOwnerOrAdminAttribute : PreconditionAttribute
    {
        //todo make this search id instead of name
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            CommandInfo command,
            IServiceProvider services)
        {
            var user = context.User as IGuildUser;
            if (user.GuildPermissions.Administrator ||
                (await context.Client.GetApplicationInfoAsync()).Owner.Username == user.Username)
            {
                return PreconditionResult.FromSuccess();
            }

            return PreconditionResult.FromError("");
        }
    }
}
