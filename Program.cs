using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System.Net.NetworkInformation;

namespace GarbageCollector
{
    public class Program
    {
        internal readonly String version = "0.2.0";
        internal readonly String internalname = "AAAAAAAAAAAAAAAAAA";

        private string prefix = "gc";
        private string filepath = "LastSeen.json";

        public DiscordClient Client { get; set; }

        public Dictionary<ulong, DateTime> LastSeen;

        private static Program prog;
        static void Main(string[] args)
        {
            prog = new Program();
            prog.RunBotAsync().GetAwaiter().GetResult();
        }
        public async Task RunBotAsync()
        {

            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();


            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            var cfg = new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };

            this.Client = new DiscordClient(cfg);

            this.Client.Ready += this.Client_Ready;
            this.Client.GuildAvailable += this.Client_GuildAvailable;
            this.Client.ClientErrored += this.Client_ClientError;

            this.Client.MessageCreated += Client_MessageCreated;
            this.Client.MessageReactionAdded += Client_MessageReactionAdded;
            this.Client.VoiceStateUpdated += Client_VoiceStateUpdated;

            await this.Client.ConnectAsync(); 

            DiscordGuild wondergames = await Client.GetGuildAsync(ulong.Parse(cfgjson.WonderServer));

            if (!File.Exists(filepath))
            {
                IReadOnlyCollection<DiscordUser> userbase = await wondergames.GetAllMembersAsync();
                LastSeen = new Dictionary<ulong, DateTime>();
                foreach (DiscordUser user in userbase)
                {
                    LastSeen.Add(user.Id, DateTime.MinValue);
                }
                File.WriteAllText(filepath, JsonConvert.SerializeObject(LastSeen, Formatting.Indented));
            }
            else
            {
                LastSeen = JsonConvert.DeserializeObject<Dictionary<ulong, DateTime>>(File.ReadAllText(filepath));
            }

            await Task.Delay(-1);
        }

        private Task Client_VoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            Updateuser(e.User);
            return Task.CompletedTask;
        }

        private Task Client_MessageReactionAdded(MessageReactionAddEventArgs e)
        {
            Updateuser(e.User);
            return Task.CompletedTask;
        }

        private async Task<Task> Client_MessageCreated(MessageCreateEventArgs e)
        {
            Updateuser(e.Author);
            if (!e.Message.Content.StartsWith(prefix)) { return Task.CompletedTask; }

            string comando = e.Message.Content
                .Substring(prefix.Length + 1)
                .ToLower(); //+1 para el espacio
            try
            {
                DiscordMessage respuesta = null;
                if (comando.StartsWith("help")) generateHelp(e.Author);
                if (comando.StartsWith("ping")) await e.Channel.SendMessageAsync("Pong! - " + Client.Ping);
                if (comando.StartsWith("bot")) respuesta = await e.Channel.SendMessageAsync(getbottom(int.Parse(comando.Substring(3))));
                if (comando.StartsWith("top")) respuesta = await e.Channel.SendMessageAsync(gettop(int.Parse(comando.Substring(3))));
                if (comando.StartsWith("file"))
                {
                    DiscordMember member = (DiscordMember)e.Author;
                    await member.SendFileAsync(File.OpenRead(filepath));
                } 
            }
            catch (Exception ex)
            {
                DiscordMessage error = await e.Channel.SendMessageAsync(null, false, GenerateErrorEmbed(ex));
                Thread.Sleep(10000);
                await error.DeleteAsync();
            }

            return Task.CompletedTask;
        }

        private void generateHelp(DiscordUser author)
        {
            DiscordMember member = (DiscordMember)author;
            member.SendMessageAsync("Comandos disponibles" +
                "\nhelp" +
                "\nping" +
                "\ntop n" +
                "\nbot n" +
                "\nfile");
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Vaultbot", "Client is ready to process events.", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Vaultbot", $"Guild available: {e.Guild.Name}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "Vaultbot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

            while (!HasInternetConnection())
            {
                e.Client.DebugLogger.LogMessage(LogLevel.Error, "Vaultbot", $"Can't connect to the Discord Servers. Reconnecting", DateTime.Now);
                Thread.Sleep(5000);
            }

            prog.RunBotAsync().GetAwaiter().GetResult();
            return Task.CompletedTask;
        }
        private bool HasInternetConnection()
        {
            Ping sender = new Ping();
            PingReply respuesta = sender.Send("discordapp.com");
            return respuesta.Status.HasFlag(IPStatus.Success);
        }
        private void Updateuser(DiscordUser user)
        {
            LastSeen[user.Id] = DateTime.Now;
            File.WriteAllText(filepath, JsonConvert.SerializeObject(LastSeen, Formatting.Indented));
        }

        private DiscordEmbed GenerateQuickEmbed(string title, string desc)
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
            builder
                .WithTitle(title)
                .WithDescription(desc)
                .WithColor(new DiscordColor(16302848))
                .WithFooter(
                           "A Yoshi's Bot",
                           "https://i.imgur.com/rT9YocG.jpg"
                          );
            return builder.Build();
        }

        private DiscordEmbed GenerateErrorEmbed(Exception exception)
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
            builder
                .WithTitle("Algo Paso")
                .WithUrl(exception.HelpLink)
                .AddField("Mensaje", exception.Message)
                .AddField("StackTrace", exception.StackTrace)
                .WithColor(new DiscordColor("#FF0000"))
                .WithFooter(
                            "A Yoshi's Bot",
                            "https://i.imgur.com/rT9YocG.jpg"
                            );
            return builder.Build();
        }

        private string getbottom(int n)
        {
            IEnumerable<KeyValuePair<ulong, DateTime>> top5bot = (from valor in LastSeen orderby valor.Value ascending select valor).Take(n);
            String salida = "";
            foreach (var valor in top5bot)
            {
                salida += $"{Client.GetUserAsync(valor.Key).Result.Username} - {valor.Value.ToString("F")}\n";
            }
            return salida;
        }
        private string gettop(int n)
        {
            //LastSeen == Dictionary<ulong, Datetime>;
            IEnumerable<KeyValuePair<ulong, DateTime>> top5bot = (from valor in LastSeen orderby valor.Value descending select valor).Take(n);
            String salida = "";
            foreach (var valor in top5bot)
            {
                salida += $"{Client.GetUserAsync(valor.Key).Result.Username} - {valor.Value.ToString("F")}\n";
            }
            return salida;
        }
    }

    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("wonderServer")]
        public string WonderServer { get; private set; }
    }
}
