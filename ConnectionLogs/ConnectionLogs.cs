﻿using System.Linq;
using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Nexd.MySQL;

namespace ConnectionLogs;

public class ConnectionLogs : BasePlugin, IPluginConfig<StandardConfig>
{
    public override string ModuleName => "Connection logs";

    public override string ModuleVersion => "0.4";

    public override string ModuleAuthor => "WidovV";
    public override string ModuleDescription => "Logs client connections to a database and discord.";

    private MySqlDb? _db;
    private string? _serverName;

    public required StandardConfig Config { get; set; }

    public override void Load(bool hotReload)
    {
        _db = new(Config.DatabaseHost ?? string.Empty, Config.DatabaseUser ?? string.Empty, Config.DatabasePassword ?? string.Empty, Config.DatabaseName ?? string.Empty, Config.DatabasePort);
        if (Config.StoreInDatabase)
        {
            Queries.CreateTable(_db);
        }

        RegisterListener<Listeners.OnClientDisconnect>(Listener_OnClientDisconnectHandler);
        RegisterListener<Listeners.OnClientPutInServer>(Listener_OnClientPutInServerHandler);
        RegisterListener<Listeners.OnMapStart>(Listener_OnMapStartHandler);
    }

    private void Listener_OnMapStartHandler(string mapName) => _serverName = ConVar.Find(("hostname")).StringValue ?? "Server";

    private void Listener_OnClientPutInServerHandler(int playerSlot)
    {
        CCSPlayerController player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player == null || !player.IsValid || player.IsBot)
        {
            return;
        }

        if (Config.StoreInDatabase)
        {
            Queries.InsertNewClient(_db, player);
        }

        if (Config.SendMessageToDiscord)
        {
            DiscordClass.SendMessage(Config, true, player, _serverName);
        }
    }


    public void Listener_OnClientDisconnectHandler(int playerSlot)
    {
        CCSPlayerController player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player == null || !player.IsValid || player.IsBot)
        {
            return;
        }

        if (Config.SendMessageToDiscord)
        {
            DiscordClass.SendMessage(Config, false, player, _serverName);
        }
    }

    
    [ConsoleCommand("css_connectedplayers", "get every connected player")]
    [CommandHelper(usage: "css_connectedplayers", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void ConnectedPlayers(CCSPlayerController player, CommandInfo info)
    {
        if (!Config.StoreInDatabase)
        {
            player.PrintToChat($"{Config.ChatPrefix} This command is disabled");
            return;
        }

        if (info.ArgCount != 1)
        {
            return;
        }

        Task.Run(async () =>
        {
            IEnumerable<User> users = await Queries.GetConnectedPlayers(_db);

            if (!users.Any())
            {
                player.PrintToChat($"{Config.ChatPrefix} No connected players");
                return;
            }

            Server.NextFrame(() =>
            {
                bool validPlayer = player != null;

                foreach (User p in users)
                {
                    if (!validPlayer)
                    {
                        Server.PrintToConsole($"{p.ClientName} ({p.SteamId}) First joined: {p.ConnectedAt} | Last seen: {p.LastSeen}");
                        continue;
                    }

                    player?.PrintToChat($"{Config.ChatPrefix} {p.ClientName} ({p.SteamId}) First joined: {p.ConnectedAt} | last seen {p.LastSeen}");
                }
            });
        });
    }

    public void OnConfigParsed(StandardConfig standardConfig)
    {
        foreach (PropertyInfo property in typeof(StandardConfig).GetProperties())
        {
            if (property.GetValue(standardConfig) == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now}] {property.Name} is null, please fix this in the standardConfig file.");
                Console.ResetColor();
                continue;
            }

            // Check if the property is a string
            if (property.PropertyType == typeof(string))
            {
                // Check if the property is empty
                if (string.IsNullOrEmpty(property.GetValue(standardConfig).ToString()))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{DateTime.Now}] {property.Name} is empty, please fix this in the standardConfig file.");
                    Console.ResetColor();
                    continue;
                }
                
                property.SetValue(standardConfig, Cfg.ModifyColorValue(property.GetValue(standardConfig).ToString()));
            }
        }

        Config = standardConfig;
    }
}
