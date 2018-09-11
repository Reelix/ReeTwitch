using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ReeTwitch
{
    class Program
    {
        readonly static string channelName = "ChannelNameHere"; // Twitch channel to join (All lower case)
        readonly static string botName = "BotNameHere"; // Your bots twitch account name (All lower case)
        readonly static string oauth = "oauth:PutYourKeyHere"; // Get the oauth key from https://twitchapps.com/tmi/ - Include the "oauth:" bit
        static readonly ConsoleColor userColor = ConsoleColor.Yellow;
        static readonly ConsoleColor streamerColor = ConsoleColor.Red;
        static StreamReader theReader;
        static StreamWriter theWriter;

        static int viewerCount = 0;

        static void Main(string[] args)
        {
            Console.Title = "Loading...";
            // Bit of validation
            if (channelName.Contains("#"))
            {
                Console.WriteLine("Remove the # from the channel name");
                Console.ReadLine();
            }
            else if (!oauth.StartsWith("oauth:"))
            {
                Console.WriteLine("Fix the oauth!");
                Console.ReadLine();
            }
            Console.Title = "Reelix's Twitch Bot - Running bot \"" + botName + "\" on channel \"" + channelName + "\"";
            Console.ForegroundColor = ConsoleColor.White;
            Thread twitchData = new Thread(ProcessStreamData);
            twitchData.Start();

            while (true)
            {
                string theLine = Console.ReadLine();
                if (theLine != "")
                {
                    // You can send stuff manually by typing!
                    SendToChannel(theLine);

                    // TODO: Special commands (Private messaging, banning, etc)
                }
            }
        }

        static int GetViewerCount()
        {
            // TODO: Split Mod Count, User count, etc
            WebClient webClient = new WebClient();
            string chatterData = webClient.DownloadString($"https://tmi.twitch.tv/group/user/{channelName}/chatters");
            string chatterCount = chatterData.Remove(0, chatterData.IndexOf("chatter_count\": ") + 16);
            chatterCount = chatterCount.Substring(0, chatterCount.IndexOf(","));
            return int.Parse(chatterCount);
        }

        static void ProcessStreamData()
        {
            Console.WriteLine($"Connecting bot \"{botName}\" to \"{channelName}\"...");
            int port = 6667; // No SSL according to https://help.twitch.tv/customer/portal/articles/1302780-twitch-irc
            TcpClient client = new TcpClient("irc.chat.twitch.tv", port);
            NetworkStream theStream = client.GetStream();
            theReader = new StreamReader(theStream);
            theWriter = new StreamWriter(theStream);
            string loginstring = "PASS " + oauth + Environment.NewLine + "NICK " + botName;
            theWriter.WriteLine("PASS " + oauth);
            theWriter.Flush();
            theWriter.WriteLine("NICK " + botName);
            theWriter.Flush();
            Console.WriteLine("Sent login - Waiting for reply...");
            string receivedData;
            while ((receivedData = theReader.ReadLine()) != null)
            {
                string[] dataParts = receivedData.Split(' ');
                try
                {
                    if (dataParts[0].Substring(0, 1) == ":")
                    {
                        dataParts[0] = dataParts[0].Remove(0, 1);
                    }
                    if (dataParts[0] == "tmi.twitch.tv")
                    {
                        string serverCommand = dataParts[1];
                        if (serverCommand == "001" || serverCommand == "002" || serverCommand == "003" || serverCommand == "004" || serverCommand == "375" || serverCommand == "372")
                        {
                            // Intro / MOTD stuff - Don't really care about that since it's just intro stuff
                            // Console.WriteLine(1);
                        }
                        else if (serverCommand == "421")
                        {
                            Console.WriteLine("Error - Invalid Command - Poke Reelix for being a n00b");
                        }
                        else if (serverCommand == "376")
                        {
                            // All done connecting - Now joing the channel
                            string joinstring = "JOIN " + "#" + channelName + "\r\n";
                            theWriter.WriteLine(joinstring);
                            theWriter.Flush();
                        }
                    }
                    else if (dataParts[0] == "PING")
                    {
                        try
                        {
                            theWriter.WriteLine(receivedData.Replace("PING", "PONG"));
                            theWriter.Flush();
                            Console.WriteLine("Ping? Pong!");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Fatal Error on PING: {e.Message}");
                        }
                    }
                    else
                    {
                        // User stuff
                        string userName = dataParts[0].Split('!')[0];
                        string command = dataParts[1];
                        if (command == "353" || command == "366")
                        {
                            // Names command stuff - Don't really care since it doesn't work in Twitch chat....
                        }
                        else if (command == "JOIN")
                        {
                            if (userName == botName)
                            {
                                Console.WriteLine($"Bot \"{botName}\" has joined the channel!");
                                viewerCount = GetViewerCount();
                                Console.WriteLine($"There are currently {viewerCount} users in the channel - Including yourself" + Environment.NewLine);
                            }
                            else
                            {
                                Console.WriteLine($"{userName} joined the channel");
                            }
                        }
                        else if (command == "PRIVMSG")
                        {
                            DateTime theTime = DateTime.Now;
                            Console.Write(theTime.ToString("[HH:mm:ss] "));
                            if (userName == channelName)
                            {
                                Console.ForegroundColor = streamerColor;
                            }
                            else
                            {
                                Console.ForegroundColor = userColor;
                            }
                            Console.Write(userName);
                            Console.ForegroundColor = ConsoleColor.White;
                            // ACK - Must be some better way to do this... Better parsing?
                            StringBuilder messageBuilder = new StringBuilder();
                            for (int j = 0; j < dataParts.Length; j++)
                            {
                                if (j >= 3)
                                {
                                    messageBuilder.Append(dataParts[j] + " ");
                                }
                            }
                            Console.WriteLine($": {messageBuilder.ToString().Remove(0, 1)}");
                        }
                        else
                        {
                            Console.WriteLine($"Unknown command -->{dataParts[0]} ++ {dataParts[1]}<--- : {receivedData}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Fatal Error on message receive: {e.Message} ---> {receivedData}");
                }
            }
        }

        static void SendToChannel(string text)
        {
            // Delete the line you just wrote from the display
            DeletePreviousConsoleLine();

            // And send it
            string sendText = "PRIVMSG #" + channelName + " :" + text;
            theWriter.WriteLine(sendText);
            theWriter.Flush();
            Console.WriteLine($"Sent: \"{text}\" to \"{channelName}\"");
        }

        // Thanks to https://stackoverflow.com/questions/46964727/hide-console-readline-input-from-console-after-submitting-c-sharp-console-app
        private static void DeletePreviousConsoleLine()
        {
            if (Console.CursorTop == 0) return;
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 1);
        }
    }
}
