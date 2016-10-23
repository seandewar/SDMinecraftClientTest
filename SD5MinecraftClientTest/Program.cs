using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace SD5MinecraftClientTest
{
    class Program
    {
        static byte[] entid = null;
        static int entityid = 0;
        static string leveltype = null;
        static byte[] gamemode = null;
        static byte[] dimension = null;
        static byte[] difficulty = null;
        static byte[] maxplayers = null;

        static int spawnx = 0;
        static int spawny = 0;
        static int spawnz = 0;

        static long ageOfWorld = 0;
        static long timeOfDay = 0;

        static bool autoDisableRain = true;
        static bool isRaining = false;

        static byte[] EndiannessUnicode(byte[] textbytes)
        {
            byte[] ret = new byte[textbytes.Length];

            for (int i = 0; i < textbytes.Length; i += 2)
            {
                ret[i] = textbytes[i + 1];
                ret[i + 1] = textbytes[i];
            }

            return ret;
        }

        static byte[] ProtocolString(string text)
        {
            byte[] len = BitConverter.GetBytes((short)text.Length);
            Array.Reverse(len);

            byte[] tex = Encoding.Unicode.GetBytes(text);
            tex = EndiannessUnicode(tex);

            byte[] ret = new byte[len.Length + tex.Length];
            Array.Copy(len, ret, len.Length);
            Array.Copy(tex, 0, ret, len.Length, tex.Length);

            return ret;
        }

        static string ReadProtocolString(NetworkStream ns)
        {
            byte[] len = Read(ns, 2);
            Array.Reverse(len);
            short length = BitConverter.ToInt16(len, 0);

            byte[] text = Read(ns, (int)length * 2);
            text = EndiannessUnicode(text);
            string ret = Encoding.Unicode.GetString(text);

            return ret;
        }

        static byte[] Read(NetworkStream ns, int bytesToRead)
        {
            int bytesRead = 0;
            int bytesReadNow = 0;
            byte[] ret = new byte[bytesToRead];

            while (bytesRead < bytesToRead)
            {
                bytesReadNow = ns.Read(ret, bytesRead, bytesToRead - bytesRead);

                if (bytesReadNow == 0)
                    throw new Exception("Connection lost");

                bytesRead += bytesReadNow;
                bytesReadNow = 0;
            }

            return ret;
        }

        static short ReadShort(NetworkStream ns)
        {

            byte[] d = Read(ns, 2);
            Array.Reverse(d);
            
            return BitConverter.ToInt16(d, 0);
        }

        static int ReadInt(NetworkStream ns)
        {

            byte[] d = Read(ns, 4);
            Array.Reverse(d);

            return BitConverter.ToInt32(d, 0);
        }

        static float ReadFloat(NetworkStream ns)
        {

            byte[] d = Read(ns, 4);
            Array.Reverse(d);

            return BitConverter.ToSingle(d, 0);
        }

        static double ReadDouble(NetworkStream ns)
        {

            byte[] d = Read(ns, 8);
            Array.Reverse(d);

            return BitConverter.ToDouble(d, 0);
        }

        static byte ReadByte(NetworkStream ns)
        {
            byte[] d = Read(ns, 1);

            return d[0];
        }

        static void SkipSlot(NetworkStream ns)
        {
            byte[] slotitmid = Read(ns, 2);   //The slot item ID.
            Array.Reverse(slotitmid);
            short slotitemid = BitConverter.ToInt16(slotitmid, 0);
            if (slotitemid == -1)    //If no item (ID -1), no more data follows, can continue;
                return;

            Read(ns, 1); //Item count
            Read(ns, 2); //Item damage

            byte[] slotlennbt = Read(ns, 2); //The length of optional NBT data.
            Array.Reverse(slotlennbt);
            short slotlengthnbt = BitConverter.ToInt16(slotlennbt, 0);
            if (slotlengthnbt == -1)    //If no extra nbt data (length of -1), no more data follows, can continue;
                return;

            Read(ns, (int)slotlengthnbt); //Optional NBT Data
        }

        static void SkipEntityMetadata(NetworkStream ns)
        {
            while (true)
            {
                byte m = ReadByte(ns);

                if (m == 0x7F) //If 0x7F, stop reading.
                    break;

                int index = m & 0x1F;
                int type = m >> 5;

                if (type == 0)
                    ReadByte(ns);
                else if (type == 1)
                    ReadShort(ns);
                else if (type == 2)
                    ReadInt(ns);
                else if (type == 3)
                    Read(ns, 4);
                else if (type == 4)
                    ReadProtocolString(ns);
                else if (type == 5)
                    SkipSlot(ns);
                else if (type == 6)
                {
                    ReadInt(ns);
                    ReadInt(ns);
                    ReadInt(ns);
                }
            }
        }

        static void SendChatMessage(NetworkStream ns, string message)
        {
            Thread.Sleep(50);

            List<byte> chat = new List<byte>();
            chat.Add(0x03);     //Chat message.

            byte[] msg = ProtocolString(message);
            for (int i = 0; i < msg.Length; i++)
                chat.Add(msg[i]);         //Add the chat string.

            ns.Write(chat.ToArray(), 0, chat.Count);

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Sent chat message: " + message);
        }

        static void SendInfoInChat(NetworkStream ns)
        {
            SendChatMessage(ns, entityid + ", " + leveltype + ", " + gamemode[0] + ", " + dimension[0] + ", " + difficulty[0] + ", " + maxplayers[0]);
        }

        static void Main(string[] args)
        {
            Console.Title = "SD5 Minecraft Protocol Test - BOT";

            Console.WriteLine("Username?");
            string username = Console.ReadLine();

            Console.WriteLine("IP?");
            string ip = Console.ReadLine();

            Console.WriteLine("Port?");
            string prt = Console.ReadLine();

            Console.WriteLine("");

            IPAddress ipaddress = null;
            int port = 0;

            try
            {
                if (username == "")
                    username = "SD5Bot"; //+ new Random().Next(1000, 10000);

                if (ip == "")
                    ipaddress = IPAddress.Loopback;
                else
                    ipaddress = IPAddress.Parse(ip);

                if (prt == "")
                    port = 25565;
                else
                    port = int.Parse(prt);
            }
            catch
            {
                Console.WriteLine("Invalid IP or Port!");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Connecting to " + ipaddress.ToString() + ":" + port + "...");
            TcpClient tcpc = new TcpClient();
            tcpc.NoDelay = true;

            try
            {
                tcpc.Connect(ipaddress, port);
            }
            catch
            {
                tcpc.Close();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to connect!");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Connected!");
            Console.WriteLine("");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Sending handshake with username: " + username + " ...");
            List<byte> handshake = new List<byte>();

            handshake.Add(0x02);        //ID - 0x02 Handshake
            handshake.Add(51);          //Protocol version - 51 for 1.4.6/1.4.7

            byte[] usernamebytes = ProtocolString(username);

            for (int i = 0; i < usernamebytes.Length; i++)
                handshake.Add(usernamebytes[i]);         //Add the username string.

            byte[] host = ProtocolString(ipaddress.ToString());

            for (int i = 0; i < host.Length; i++)
                handshake.Add(host[i]);             //Add the host string.

            byte[] portbytes = BitConverter.GetBytes(port);
            Array.Reverse(portbytes);

            for (int i = 0; i < 4; i++)
                handshake.Add(portbytes[i]);        //Add the port.

            try
            {
                tcpc.GetStream().Write(handshake.ToArray(), 0, handshake.Count);
            }
            catch
            {
                tcpc.Close();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to send handshake!");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Handshake sent!");
            Console.WriteLine("");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Waiting for encryption request...");

            try
            {
                byte[] id = Read(tcpc.GetStream(), 1);      //Encryption request ID 0xFD

                if (id[0] != 0xFD)
                {
                    tcpc.Close();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unexpected response from server: " + id[0]);
                    if (id[0] == 0xFF)
                        Console.WriteLine("Reason: " + ReadProtocolString(tcpc.GetStream()));
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Received encryption request :");

                string serverid = ReadProtocolString(tcpc.GetStream()); //Gets the serverid.
                Console.WriteLine("     Server ID: " + serverid);

                byte[] pubkeylen = Read(tcpc.GetStream(), 2);           //Gets the public key length
                Array.Reverse(pubkeylen);
                short pubkeylength = BitConverter.ToInt16(pubkeylen, 0);
                Console.WriteLine("     Public Key Length: " + pubkeylength);

                byte[] pubkey = Read(tcpc.GetStream(), (int)pubkeylength); //Gets the public key, which is a set of bytes.
                Console.WriteLine("     Public Key received!");

                byte[] verifylen = Read(tcpc.GetStream(), 2);           //Gets the verify token length
                Array.Reverse(verifylen);
                short verifylength = BitConverter.ToInt16(verifylen, 0);
                Console.WriteLine("     Verify Token Length: " + verifylength);

                byte[] verify = Read(tcpc.GetStream(), (int)verifylength); //Gets the verify token, which is a set of bytes.
                Console.WriteLine("     Verify Token received!");
            }
            catch
            {
                tcpc.Close();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Connection lost while getting encryption request!");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Assuming offline mode, will skip encryption and pass client status update!");
            Console.WriteLine("");

            //Console.WriteLine("Sending encryption key response...");
            //List<byte> encryptionkeyresponse = new List<byte>();

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Sending client status update...");
            List<byte> clientstatus = new List<byte>();

            clientstatus.Add(0xCD);        //ID - 0xCD Client Statuses
            clientstatus.Add(0x0);         //Payload - Initial spawn, all bits are 0.

            try
            {
                tcpc.GetStream().Write(clientstatus.ToArray(), 0, clientstatus.Count);
            }
            catch
            {
                tcpc.Close();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to send client status update!");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Client status update sent!");
            Console.WriteLine("");

            try
            {
                byte[] loggedinid = Read(tcpc.GetStream(), 1); //Login request 0x01

                if (loggedinid[0] != 0x01)
                {
                    tcpc.Close();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unexpected response from server: " + loggedinid[0]);
                    if (loggedinid[0] == 0xFF)
                        Console.WriteLine("Reason: " + ReadProtocolString(tcpc.GetStream()));
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Login request received!");

                entid = Read(tcpc.GetStream(), 4); //Entity ID
                Array.Reverse(entid);
                entityid = BitConverter.ToInt32(entid, 0);
                Console.WriteLine("     Entity ID: " + entityid);

                leveltype = ReadProtocolString(tcpc.GetStream());    //Level type
                Console.WriteLine("     Level type: " + leveltype);

                gamemode = Read(tcpc.GetStream(), 1);    //Gamemode: 0 is survival, 1 is creative, 2 is adventure, bit 3 (0x8) is the hardcore flag
                Console.WriteLine("     Gamemode: " + gamemode[0]);

                dimension = Read(tcpc.GetStream(), 1);    //Dimension: -1 nether, 0 overworld, 1 the end
                Console.WriteLine("     Dimension: " + dimension[0]);

                difficulty = Read(tcpc.GetStream(), 1);  //Difficulty: 0 - 3 : Peaceful, Easy, Normal, Hard
                Console.WriteLine("     Difficulty: " + difficulty[0]);

                Read(tcpc.GetStream(), 1);  //Unused byte, previous world height

                maxplayers = Read(tcpc.GetStream(), 1);  //Max players
                Console.WriteLine("     Max players: " + maxplayers[0]);
            }
            catch
            {
                tcpc.Close();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Connection lost while handling login request!");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Logged in!");
            Console.WriteLine("");

            Thread.Sleep(1000);

            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Ready for tasking!");

                Thread.Sleep(2000);

                while (true)
                {

                    byte[] id = Read(tcpc.GetStream(), 1); //Read packet ID

                    //Console.ForegroundColor = ConsoleColor.White;
                    //Console.WriteLine(id[0]);

                    switch (id[0])
                    {
                        case 0x00: //Keep-alive packet 0x00 - PORT TO OWN THREAD
                            byte[] keepalve = Read(tcpc.GetStream(), 4); //Receive random keep alive value, and respond with same packet.

                            byte[] keepaliveresponse = new byte[5];
                            keepaliveresponse[0] = 0x00;
                            keepaliveresponse[1] = keepalve[0];
                            keepaliveresponse[2] = keepalve[1];
                            keepaliveresponse[3] = keepalve[2];
                            keepaliveresponse[4] = keepalve[3];

                            tcpc.GetStream().Write(keepaliveresponse, 0, 5);
                            break;

                        case 0x03:  //Chat message 0x03
                            string chatmessage = ReadProtocolString(tcpc.GetStream()); //message

                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine("Chat message received: " + chatmessage);

                            //CHAT COMMANDS
                            string chatactualsender = "";
                            int poss1 = chatmessage.IndexOf("<");
                            int poss2 = chatmessage.IndexOf(">");
                            int tos = poss2 - poss1;
                            if (poss1 != -1 && poss2 != -1)
                                chatactualsender = chatmessage.Substring(poss1 + 1, tos - 1);

                            string chatactualmessage = "";
                            int pos = chatmessage.IndexOf(">");
                            if (pos != -1)
                                chatactualmessage = chatmessage.Substring(pos + 2, chatmessage.Length - (pos + 2));

                            if (chatactualsender.ToLower() == username.ToLower())
                                break;

                            if (chatactualmessage == "!help")
                            {
                                SendChatMessage(tcpc.GetStream(), "Commands:");
                                SendChatMessage(tcpc.GetStream(), "---------");
                                SendChatMessage(tcpc.GetStream(), "!help - Shows help.");
                                SendChatMessage(tcpc.GetStream(), "!info - Shows bot information.");
                                SendChatMessage(tcpc.GetStream(), "!botname - Shows bot's name.");
                                SendChatMessage(tcpc.GetStream(), "!time - Shows current server map timings since last update.");
                                SendChatMessage(tcpc.GetStream(), "!say <message> - Forces the bot to say something after the !say message.");
                                SendChatMessage(tcpc.GetStream(), "!tele <to> - Teleports you to the player requested.");
                                SendChatMessage(tcpc.GetStream(), "!tself [to] - Teleports SD5Bot to you.");
                                SendChatMessage(tcpc.GetStream(), "!autodisablerain - Toggles the automatic rain disabler.");
                            }
                            else if (chatactualmessage == "!info")
                            {
                                SendInfoInChat(tcpc.GetStream());
                            }
                            else if (chatactualmessage == "!botname")
                            {
                                SendChatMessage(tcpc.GetStream(), "My name is " + username);
                            }
                            else if (chatactualmessage == "!time")
                            {
                                SendChatMessage(tcpc.GetStream(), "Current map time - total age: " + ageOfWorld + ", time of day: " + timeOfDay);
                            }
                            else if (chatactualmessage.StartsWith("!say ") && chatactualmessage.Length > 5)
                            {
                                SendChatMessage(tcpc.GetStream(), chatactualmessage.Substring(5, chatactualmessage.Length - 5));
                            }
                            else if (chatactualmessage.StartsWith("!tele ") && chatactualmessage.Length > 6)
                            {
                                string to = chatactualmessage.Substring(6, chatactualmessage.Length - 6);

                                if (to.ToLower() == chatactualsender.ToLower() || to.ToLower() == username.ToLower())
                                {
                                    SendChatMessage(tcpc.GetStream(), "/tell " + chatactualsender + " You cannot teleport there!");
                                    break;
                                }

                                SendChatMessage(tcpc.GetStream(), "/tp " + chatactualsender + " " + to);
                            }
                            else if (chatactualmessage.StartsWith("!tself ") && chatactualmessage.Length > 7)
                            {
                                string to = chatactualmessage.Substring(7, chatactualmessage.Length - 7);

                                if (to.ToLower() == username.ToLower())
                                {
                                    SendChatMessage(tcpc.GetStream(), "/tell " + chatactualsender + " I may not teleport to myself!");
                                    break;
                                }

                                SendChatMessage(tcpc.GetStream(), "/tp " + username + " " + to);
                            }
                            else if (chatactualmessage == "!tself")
                            {
                                SendChatMessage(tcpc.GetStream(), "/tp " + username + " " + chatactualsender);
                            }
                            else if (chatactualmessage == "!autodisablerain")
                            {
                                if (autoDisableRain)
                                {
                                    autoDisableRain = false;
                                    SendChatMessage(tcpc.GetStream(), "Automatic rain disabler OFF.");
                                }
                                else
                                {
                                    autoDisableRain = true;
                                    SendChatMessage(tcpc.GetStream(), "Automatic rain disabler ON.");

                                    if (isRaining)
                                    {
                                        SendChatMessage(tcpc.GetStream(), "Turning off the rain...");
                                        SendChatMessage(tcpc.GetStream(), "/weather clear");
                                    }
                                }
                            }
                            break;
                        
                        case 0x04: //Time of day update 0x04
                            byte[] ageofwrld = Read(tcpc.GetStream(), 8);
                            Array.Reverse(ageofwrld);
                            ageOfWorld = BitConverter.ToInt64(ageofwrld, 0);

                            byte[] timeofdy = Read(tcpc.GetStream(), 8);
                            Array.Reverse(timeofdy);
                            timeOfDay = BitConverter.ToInt64(timeofdy, 0);
                            break;

                        case 0x05: //Entity Equipment 0x05 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            SkipSlot(tcpc.GetStream());
                            break;

                        case 0x06: //Spawn position 0x06
                            byte[] spwnx = Read(tcpc.GetStream(), 4); //x
                            Array.Reverse(spwnx);
                            spawnx = BitConverter.ToInt32(spwnx, 0);

                            byte[] spwny = Read(tcpc.GetStream(), 4); //y
                            Array.Reverse(spwny);
                            spawny = BitConverter.ToInt32(spwny, 0);

                            byte[] spwnz = Read(tcpc.GetStream(), 4); //z
                            Array.Reverse(spwnz);
                            spawnz = BitConverter.ToInt32(spwnz, 0);

                            Console.ForegroundColor = ConsoleColor.DarkCyan;
                            Console.WriteLine("Spawn position update: " + spawnx + ", " + spawny + ", " + spawnz);
                            break;

                        case 0x0D: //Player Position and Look update 0x0D - TODO
                            Read(tcpc.GetStream(), 8);
                            Read(tcpc.GetStream(), 8);
                            Read(tcpc.GetStream(), 8);
                            Read(tcpc.GetStream(), 8);
                            Read(tcpc.GetStream(), 4);
                            Read(tcpc.GetStream(), 4);
                            Read(tcpc.GetStream(), 1);
                            break;

                        case 0x10: //Held item change 0x10 - TODO
                            Read(tcpc.GetStream(), 2);
                            break;

                        case 0x12: //Animation 0x12 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x14: //Spawn named entity 0x14 - TODO
                            int spawnnamedeid = ReadInt(tcpc.GetStream());
                            string spawnnamedname = ReadProtocolString(tcpc.GetStream());
                            int spawnnamedx = ReadInt(tcpc.GetStream());
                            int spawnnamedy = ReadInt(tcpc.GetStream());
                            int spawnnamedz = ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            SkipEntityMetadata(tcpc.GetStream());

                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine("Player nearby: " + spawnnamedname + " with entity id " + spawnnamedeid + " at " + spawnnamedx + ", " + spawnnamedy + ", " + spawnnamedz);
                            break;

                        case 0x16: //Pickup item 0x16 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            break;

                        case 0x17: //Spawn object/vehicle 0x17 - TODO
                            ReadInt(tcpc.GetStream());
                            Read(tcpc.GetStream(), 1);
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), 1);

                            int spawnobjectthrower = ReadInt(tcpc.GetStream());
                            if (spawnobjectthrower > 0)
                            {
                                ReadShort(tcpc.GetStream());
                                ReadShort(tcpc.GetStream());
                                ReadShort(tcpc.GetStream());
                            }
                            break;

                        case 0x18: //Spawn mob 0x18 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            SkipEntityMetadata(tcpc.GetStream());
                            break;

                        case 0x1A: //Spawn Experience Orb 0x1A - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            break;

                        case 0x1C: //Entity Velocity 0x1C - TODO
                            ReadInt(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            break;

                        case 0x1D: //Destroy Entity 0x1D - TODO
                            byte destroyentlen = ReadByte(tcpc.GetStream()); //Amount of ent ids
                            Read(tcpc.GetStream(), (int)destroyentlen * 4);
                            break;

                        case 0x1F: //Entity Relative Move 0x1F - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x20: //Entity Look 0x20 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x21: //Entity relative move and look 0x21 - TODO
                            ReadInt(tcpc.GetStream());
                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), 1);
                            break;

                        case 0x22: //Entity Teleport 0x22 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x23: //Entity Head Look 0x23 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x26: //Entity Status Update 0x26 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x27: //Entity Attach 0x27 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            break;

                        case 0x28: //Entity metadata 0x28 - TODO
                            ReadInt(tcpc.GetStream());
                            SkipEntityMetadata(tcpc.GetStream());
                            break;

                        case 0x29: //Entity effect 0x29 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            break;

                        case 0x2A: //Remove entity effect 0x2A - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x33: //Chunk Data 0x33 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadShort(tcpc.GetStream()); //Both are ushorts
                            ReadShort(tcpc.GetStream());
                            int chunkdatasize = ReadInt(tcpc.GetStream());
                            Read(tcpc.GetStream(), chunkdatasize);
                            break;

                        case 0x34: //Multi-block change 0x34 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            int multiblockdatalen = ReadInt(tcpc.GetStream());
                            Read(tcpc.GetStream(), multiblockdatalen);
                            break;

                        case 0x35: //Block Change 0x35 - TODO
                            ReadInt(tcpc.GetStream()); //X
                            ReadByte(tcpc.GetStream()); //Y (is a byte)
                            ReadInt(tcpc.GetStream()); //Z
                            ReadShort(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x36: //Block Action 0x36 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            break;

                        case 0x37: //Block break update 0x37 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x38: //Map chunk bulk 0x38 - TODO
                            byte[] chunkcolcnt = Read(tcpc.GetStream(), 2);   //The number of chunk columns.
                            Array.Reverse(chunkcolcnt);
                            short chunkcolcount = BitConverter.ToInt16(chunkcolcnt, 0);

                            byte[] datalen = Read(tcpc.GetStream(), 4);   //The length of chunk data.
                            Array.Reverse(datalen);
                            int datalength = BitConverter.ToInt32(datalen, 0);

                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), datalength); //Byte array containing gzipped chunk data.
                            Read(tcpc.GetStream(), 12 * (int)chunkcolcount); //12 bytes joined to packet for each chunk col sent - metadata for each chunk.
                            break;

                        case 0x3C: //Explosion 0x3C - TODO
                            double explosionx = ReadDouble(tcpc.GetStream());
                            double explosiony = ReadDouble(tcpc.GetStream());
                            double explosionz = ReadDouble(tcpc.GetStream());
                            float explosionrad = ReadFloat(tcpc.GetStream());
                            int explosionreccnt = ReadInt(tcpc.GetStream());
                            int explosionsize = 3 * explosionreccnt;
                            Read(tcpc.GetStream(), explosionreccnt * 3);
                            ReadFloat(tcpc.GetStream());
                            ReadFloat(tcpc.GetStream());
                            ReadFloat(tcpc.GetStream());

                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine("Explosion at: " + explosionx + ", " + explosiony + ", " + explosionz + " with size " + explosionsize);
                            break;

                        case 0x3D: //Sound or Particle Effect 0x3D - TODO
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x3E: //Named sound effect 0x3E - TODO
                            ReadProtocolString(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            Read(tcpc.GetStream(), 4);
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0x46: //Game state change 0x46 - TODO
                            byte gamestatereason = ReadByte(tcpc.GetStream());
                            byte gamestatenewgamemode = ReadByte(tcpc.GetStream()); //Only used when reason = 3

                            Console.ForegroundColor = ConsoleColor.DarkMagenta;
                            Console.Write("Game state update: New game state reason: " + gamestatereason + " ");
                            if (gamestatereason == 0)
                                Console.Write("(Invalid bed)");
                            else if (gamestatereason == 1)
                            {
                                isRaining = true;
                                Console.Write("(Begin raining)");

                                if(autoDisableRain)
                                {
                                    SendChatMessage(tcpc.GetStream(), "Turning off the rain...");
                                    SendChatMessage(tcpc.GetStream(), "/weather clear");
                                }
                            }
                            else if (gamestatereason == 2)
                            {
                                isRaining = false;
                                Console.Write("(End raining)");
                            }
                            else if (gamestatereason == 3)
                                Console.Write("(Gamemode changed to " + gamestatenewgamemode + ")");
                            else if (gamestatereason == 4)
                                Console.Write("(Enter credits)");
                            Console.WriteLine("");
                            break;

                        case 0x47: //Spawn global entity 0x47 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            break;

                        case 0x65: //In-game GUI Window Close request 0x65 - TODO
                            Read(tcpc.GetStream(), 1);
                            break;

                        case 0x67: //Set slot 0x67 - TODO, handled to an extent.
                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), 2);

                            SkipSlot(tcpc.GetStream());
                            break;

                        case 0x68: //Set In-game GUI Window Items 0x68 - TODO
                            Read(tcpc.GetStream(), 1);

                            byte[] setwinitemscnt = Read(tcpc.GetStream(), 2);  //Get the count for number of slots.
                            Array.Reverse(setwinitemscnt);
                            short setwinitemscount = BitConverter.ToInt16(setwinitemscnt, 0);

                            for (int i = 0; i < (int)setwinitemscount; i++)
                            {
                                SkipSlot(tcpc.GetStream());
                            }
                            break;

                        case 0x82: //Sign update packet 0x82 - TODO
                            int signx = ReadInt(tcpc.GetStream()); //X
                            short signy = ReadShort(tcpc.GetStream()); //Y (is a short)
                            int signz = ReadInt(tcpc.GetStream()); //Z

                            string signl1 = ReadProtocolString(tcpc.GetStream()); //Line 1
                            string signl2 = ReadProtocolString(tcpc.GetStream()); //Line 2
                            string signl3 = ReadProtocolString(tcpc.GetStream()); //Line 3
                            string signl4 = ReadProtocolString(tcpc.GetStream()); //Line 4

                            string signtext = signl1 + Environment.NewLine + signl2 + Environment.NewLine + signl3 + Environment.NewLine + signl4;

                            Console.ForegroundColor = ConsoleColor.DarkBlue;
                            Console.WriteLine("Sign block update: " + signx + ", " + signy + ", " + signz);
                            //Console.WriteLine("Sign block text:");
                            //Console.WriteLine(signtext);
                            break;

                        case 0x83: //Item data 0x83 - TODO
                            ReadShort(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            short itemdatalen = ReadShort(tcpc.GetStream());
                            Read(tcpc.GetStream(), (int)itemdatalen);
                            break;

                        case 0x84: //Update Tile Entity 0x84 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadShort(tcpc.GetStream());
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            short updatetileentitydata = ReadShort(tcpc.GetStream());
                            if (updatetileentitydata > 0)   //If updatetileentitydata is higher then 0, then read NBT byte array.
                                Read(tcpc.GetStream(), (int)updatetileentitydata);
                            break;

                        case 0xC8: //Increment Statistic 0xC8 - TODO
                            ReadInt(tcpc.GetStream());
                            ReadByte(tcpc.GetStream());
                            break;

                        case 0xC9: //Player List Update (1 item) 0xC9 - TODO
                            ReadProtocolString(tcpc.GetStream());
                            Read(tcpc.GetStream(), 1);
                            Read(tcpc.GetStream(), 2);
                            break;

                        case 0xCA: //Player Abilities 0xCA - TODO
                            Read(tcpc.GetStream(), 3);
                            break;

                        case 0xFA: //Plugin Message 0xFA - TODO
                            string pluginchannel = ReadProtocolString(tcpc.GetStream());
                            short pluginlen = ReadShort(tcpc.GetStream());
                            Read(tcpc.GetStream(), (int)pluginlen);
                            break;

                        case 0xFF: //Disconnect/Kick 0xFF
                            string kickmessage = ReadProtocolString(tcpc.GetStream());

                            tcpc.Close();
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Disconnect/Kick packet from server, message: " + kickmessage);
                            Console.ReadKey();
                            Environment.Exit(0);
                            break;

                        default:    //UNKNOWN PACKET
                            tcpc.Close();
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Unrecognised packet ID " + id[0] + ", disconnecting.");
                            Console.ReadKey();
                            Environment.Exit(0);
                            break;
                    }
                }
            }
            catch
            {
                tcpc.Close();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Connection lost!");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }
    }
}
