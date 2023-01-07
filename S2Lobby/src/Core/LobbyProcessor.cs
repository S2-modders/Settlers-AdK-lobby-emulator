using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic.Logging;
using S2Library.Protocol;

namespace S2Lobby
{
    public class LobbyProcessor : ServerProcessor
    {
        //private readonly byte[] _userData = Crypto.BytesFromHexString("00000000040000000000000000000000000000000000000006000000");
        private readonly byte[] _nicknameData = Crypto.BytesFromHexString("000000003900000000000000000000000000000000000000a2000000785edbc9c8800cd880b8842195a1184c1631e000ff819891094508668e438300886265604788beb8ce644b0c8d0d1c05004a9b0ff3");

        private Server _server;
        private uint _joinedServerId = 0;
        private static readonly ConcurrentDictionary<uint, uint> ServerUpdateReceivers = new ConcurrentDictionary<uint, uint>();
        private static readonly ConcurrentDictionary<uint, uint> GlobalChatReceivers = new ConcurrentDictionary<uint, uint>();

        public LobbyProcessor(Program program, uint connection) : base(program, connection)
        {
        }

        public override void Close()
        {
            base.Close();

            if (_server != null)
            {
                Program.Servers.Remove(_server.Id);
                NotifyUnlistServer(_server.Id, _server.Running);
                _server = null;
            }
        }

        protected sealed override bool HandlePayloadType(Payloads.Types payloadType, PayloadPrefix payload, PayloadWriter writer)
        {
            if (base.HandlePayloadType(payloadType, payload, writer))
            {
                return true;
            }

            switch (payloadType)
            {
                case Payloads.Types.RegisterNickname:
                    HandleRegisterNickname((RegisterNickname)payload, writer);
                    return true;
                case Payloads.Types.ConfirmNickname:
                    HandleConfirmNickname((ConfirmNickname)payload, writer);
                    return true;
                case Payloads.Types.GetCDKeys:
                    HandleGetCdKeys((GetCDKeys)payload, writer);
                    return true;
                case Payloads.Types.RequestMOTD:
                    HandleGetMOTD((RequestMOTD)payload, writer);
                    return true;
                case Payloads.Types.GetUserInfo:
                    HandleGetUserInfo((GetUserInfo)payload, writer);
                    return true;
                case Payloads.Types.GetPlayerInfo:
                    HandleGetCharacterInfo((GetPlayerInfo)payload, writer);
                    return true;
                case Payloads.Types.GetChatServer:
                    HandleGetChatServer((GetChatServer)payload, writer);
                    return true;
                case Payloads.Types.SelectNickname:
                    HandleSelectNickname((SelectNickname)payload, writer);
                    return true;
                case Payloads.Types.RegisterServer:
                    HandleRegisterServer((RegisterServer)payload, writer);
                    return true;
                case Payloads.Types.GetServers:
                    HandleGetServers((GetServers)payload, writer);
                    return true;
                case Payloads.Types.StopServerUpdates:
                    HandleStopServerUpdates((StopServerUpdates)payload, writer);
                    return true;
                case Payloads.Types.RequestUserBuddyList:
                    HandleRequestUserBuddyList((RequestUserBuddyList)payload, writer);
                    return true;
                case Payloads.Types.RegObserverUserLogin:
                    HandleRegObserverUserLogin((RegObserverUserLogin)payload, writer);
                    return true;
                case Payloads.Types.DeregObserverUserLogin:
                    HandleDeregObserverUserLogin((DeregObserverUserLogin)payload, writer);
                    return true;
                case Payloads.Types.RequestUserIgnoreList:
                    HandleRequestUserIgnoreList((RequestUserIgnoreList)payload, writer);
                    return true;
                case Payloads.Types.ConnectToServer:
                    HandleConnectToServer((ConnectToServer)payload, writer);
                    return true;
                case Payloads.Types.UpdateServerInfo:
                    HandleUpdateServerInfo((UpdateServerInfo)payload, writer);
                    return true;
                case Payloads.Types.UnlistServer:
                    HandleUnlistServer((UnlistServer) payload, writer);
                    return true;
                case Payloads.Types.PlayerJoinedServer:
                    HandlePlayerJoinedServer((PlayerJoinedServer)payload, writer);
                    return true;
                case Payloads.Types.PlayerLeftServer:
                    HandlePlayerLeftServer((PlayerLeftServer)payload, writer);
                    return true;
                case Payloads.Types.JoinServer:
                    HandleJoinServer((JoinServer) payload, writer);
                    return true;
                case Payloads.Types.LeaveServer:
                    HandleLeaveServer((LeaveServer) payload, writer);
                    return true;
                
                case Payloads.Types.PropertyGet:
                    HandlePropertyGet((PropertyGet)payload, writer);
                    return true;
                
                // Character related messages
                // TODO add Character info blabla
                case Payloads.Types.RemoveCharacter:
                    HandleRemoveCharacter((RemoveCharacter)payload, writer);
                    return true;

                case Payloads.Types.AddCharacter:
                    HandleAddCharacter((AddCharacter)payload, writer);
                    return true;
                
                // Chat related packages
                case Payloads.Types.RegObserverGlobalChat:
                    HandleRegObserverGlobalChat((RegObserverGlobalChat)payload, writer);
                    return true;
                case Payloads.Types.DeregObserverGlobalChat:
                    HandleDeregObserverGlobalChat((DeregObserverGlobalChat)payload, writer);
                    return true;
                case Payloads.Types.ChatPayload:
                    HandleChatPayload((ChatPayload) payload, writer);
                    return true;
                
                
                default:
                    Logger.LogError($"{payloadType} not implemented!");
                    return false;
            }
        }

        private void HandleRegisterNickname(RegisterNickname payload, PayloadWriter writer)
        {
            uint accountId = payload.OwnerId;
            string nickname = payload.Name;

            if (accountId != 0)
            {
                StatusWithId resultPayload1 = Payloads.CreatePayload<StatusWithId>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Incorrect account";
                resultPayload1.Id = payload.OwnerId;
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            Program.Accounts.SetNickname(Database.Connection, Account.Id, nickname);
            Account.PlayerName = nickname;

            StatusWithId resultPayload2 = Payloads.CreatePayload<StatusWithId>();
            resultPayload2.Errorcode = 0;
            resultPayload2.Errormsg = null;
            resultPayload2.Id = Account.Id;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);
        }

        private void HandleConfirmNickname(ConfirmNickname payload, PayloadWriter writer)
        {
            uint accountId = payload.UserId;
            if (accountId != Account.Id)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Incorrect account";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            string mail = payload.Mail;
            if (mail != null)
            {
                Program.Accounts.SetEmail(Database.Connection, accountId, mail);
                Account.Email = mail;
            }

            byte[] nicknameData = payload.Data;
            if (nicknameData != null)
            {
                Program.Accounts.SetUserData(Database.Connection, accountId, nicknameData);
                Account.UserData = nicknameData;
            }

            ResultStatusMsg resultPayload2 = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload2.Errorcode = 0;
            resultPayload2.Errormsg = null;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);
        }

        private void HandleGetCdKeys(GetCDKeys payload, PayloadWriter writer)
        {
            uint accountId = payload.UserId;
            if (accountId != Account.Id)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Incorrect account";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            SendCDKey resultPayload2 = Payloads.CreatePayload<SendCDKey>();
            resultPayload2.UserId = accountId;
            resultPayload2.CdKey = "0000000000000000";
            resultPayload2.Keypool = 1;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);

            ResultStatusMsg resultPayload3 = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload3.Errorcode = 0;
            resultPayload3.Errormsg = null;
            resultPayload3.TicketId = payload.TicketId;
            SendReply(writer, resultPayload3);
        }

        private void HandleGetMOTD(RequestMOTD payload, PayloadWriter writer)
        {
            // For whatever reason MOTD is encoded as UTF-8
            byte[] motd = Encoding.UTF8.GetBytes(
                Constants.MOTD.Replace("%name%", Account.UserName));
            
            SendMOTD resultPayload = Payloads.CreatePayload<SendMOTD>();
            resultPayload.Txt = motd;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);
        }

        private void HandleGetUserInfo(GetUserInfo payload, PayloadWriter writer)
        {
            uint accountId = payload.UserId;
            if (accountId != Account.Id)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Incorrect account";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            SendUserInfo resultPayload2 = Payloads.CreatePayload<SendUserInfo>();
            resultPayload2.UserId = accountId;
            resultPayload2.Name = Account.UserName;
            resultPayload2.Password = null;
            resultPayload2.Mail = Account.Email;
            resultPayload2.Banned = false;
            resultPayload2.Active = true;
            resultPayload2.Status = 2;
            resultPayload2.Data = Account.UserData;
            resultPayload2.Created = "2012-04-02 00:00:00+0:00";
            resultPayload2.LastLogin = "2012-04-02 00:00:00+0:00";
            resultPayload2.TotalLogins = 1;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);

            ResultStatusMsg resultPayload3 = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload3.Errorcode = 0;
            resultPayload3.Errormsg = null;
            resultPayload3.TicketId = payload.TicketId;
            SendReply(writer, resultPayload3);
        }

        private void HandleGetCharacterInfo(GetPlayerInfo payload, PayloadWriter writer)
        {
            uint accountId = payload.UserId;
            if (accountId != Account.Id)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Incorrect account";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            SendPlayerInfo resultPayload2 = Payloads.CreatePayload<SendPlayerInfo>();
            resultPayload2.CharId = Account.Id;
            resultPayload2.Name = Account.PlayerName;
            resultPayload2.OwnerId = Account.Id;
            resultPayload2.OwnerName = Account.UserName;
            resultPayload2.GuildId = 0;
            resultPayload2.GuildName = null;
            resultPayload2.GuildRole = 0;
            resultPayload2.Status = 1;
            resultPayload2.ServerId = 0;
            resultPayload2.ServerName = null;
            resultPayload2.Data = _nicknameData;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);

            ResultStatusMsg resultPayload3 = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload3.Errorcode = 0;
            resultPayload3.Errormsg = null;
            resultPayload3.TicketId = payload.TicketId;
            SendReply(writer, resultPayload3);
        }

        private void HandleGetChatServer(GetChatServer payload, PayloadWriter writer)
        {
            SendChatServerInfo resultPayload = Payloads.CreatePayload<SendChatServerInfo>();
            resultPayload.ServerId = 1;
            resultPayload.Ip = Config.Get("chat/ip");
            resultPayload.Port = Config.GetInt("chat/port");
            resultPayload.ServerType = payload.ServerType;
            resultPayload.Version = null;
            resultPayload.Data = null;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);
        }

        private void HandleSelectNickname(SelectNickname payload, PayloadWriter writer)
        {
            uint playerId = payload.CharId;

            Account account = Program.Accounts.Get(Database.Connection, playerId);
            if (account == null)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Incorrect account";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            SelectNicknameReply resultPayload2 = Payloads.CreatePayload<SelectNicknameReply>();
            resultPayload2.CharId = account.Id;
            resultPayload2.Name = account.PlayerName;
            resultPayload2.OwnerId = account.Id;
            resultPayload2.OwnerName = account.UserName;
            resultPayload2.GuildId = 0;
            resultPayload2.GuildName = null;
            resultPayload2.GuildRole = 0;
            resultPayload2.Status = 1;
            resultPayload2.ServerId = 0;
            resultPayload2.ServerName = null;
            resultPayload2.Data = _nicknameData;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);

            ResultStatusMsg resultPayload3 = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload3.Errorcode = 0;
            resultPayload3.Errormsg = null;
            resultPayload3.TicketId = payload.TicketId;
            SendReply(writer, resultPayload3);
        }

        private void HandleRegisterServer(RegisterServer payload, PayloadWriter writer)
        {
            uint serverId = Program.Servers.Register(payload.Name);
            if (serverId == 0)
            {
                // TODO implement error codes if available
                SendReply(writer, Payloads.CreateStatusFailMsg("Failed to register server", payload.TicketId));
                return;
            }

            _server = Program.Servers.Get(serverId);
            if (_server == null)
            {
                Program.Servers.Remove(serverId);
                SendReply(writer, Payloads.CreateStatusFailMsg("Failed to register server", payload.TicketId));
                return;
            }

            _server.Players.TryAdd(Account.Id, Connection);
            
            _server.ConnectionId = Connection;
            _server.OwnerId = Account.Id;
            _server.Description = payload.Description;
            _server.Ip = Config.Get("host/ip");
            _server.Port = payload.Port;
            //_server.Port = Config.GetInt("host/port");
            _server.ServerType = payload.ServerType;
            _server.LobbyId = payload.LobbyId;
            _server.Version = payload.Version;
            _server.MaxPlayers = payload.MaxPlayers;
            _server.AiPlayers = payload.AiPlayers;
            _server.Level = payload.Level;
            _server.GameMode = payload.GameMode;
            _server.Hardcore = payload.Hardcore;
            _server.Map = payload.Map;
            //_server.Map = "MP_2P_Storm_Coast\vfr_11888";
            _server.AutomaticJoin = payload.AutomaticJoin;
            _server.Data = payload.Data;
            _server.Running = false;
            
            var resultPayload = Payloads.CreatePayload<StatusWithId>();
            resultPayload.Errorcode = 0;
            resultPayload.Errormsg = null;
            resultPayload.Id = _server.Id;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);
            
            SendServerUpdates(payload.TicketId);

            Logger.Log($"User {Account.UserName} created a new lobby as {payload.Name}");
        }
        private void HandleRegisterServerOld(RegisterServer payload, PayloadWriter writer)
        {
            string name = payload.Name;

            uint serverId = Program.Servers.Register(name);
            if (serverId == 0)
            {
                StatusWithId resultPayload1 = Payloads.CreatePayload<StatusWithId>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "Can not register server";
                resultPayload1.Id = 0;
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            _server = Program.Servers.Get(serverId);
            if (_server == null)
            {
                Program.Servers.Remove(serverId);

                StatusWithId resultPayload1 = Payloads.CreatePayload<StatusWithId>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "Can not register server";
                resultPayload1.Id = 0;
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            /*
            _server.ConnectionId = Connection;
            _server.OwnerId = Account.Id;
            _server.Description = payload.Description;
            _server.Ip = payload.Ip ?? Config.Get("lobby/ip");
            _server.Port = payload.Port;
            _server.Type = payload.ServerType;
            _server.SubType = payload.ServerSubtype;
            _server.MaxPlayers = payload.MaxPlayers;
            _server.RoomId = payload.RoomId;
            _server.Level = payload.Level;
            _server.GameMode = payload.GameMode;
            _server.Hardcore = payload.Hardcore;
            _server.Running = payload.Running;
            _server.LockedConfig = payload.LockedConfig;
            _server.Data = payload.Data;
            if (payload.Cipher != null)
            {
                byte[] serverPassword = Crypto.HandleCipher(payload.Cipher, SessionKey);
                int length = System.Array.FindIndex(serverPassword, b => b == 0);
                string password = System.Text.Encoding.ASCII.GetString(serverPassword, 0, length);
                Logger.LogDebug($" Server password is: {password}");

                _server.NeedsPassword = true;
                _server.Password = password;
            }
            */
            SendServerUpdates();

            StatusWithId resultPayload2 = Payloads.CreatePayload<StatusWithId>();
            resultPayload2.Errorcode = 0;
            resultPayload2.Errormsg = null;
            resultPayload2.Id = _server.Id;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);
        }

        private void HandleGetServers(GetServers payload, PayloadWriter writer)
        {
            if (!ServerUpdateReceivers.TryAdd(Connection, Account.Id))
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "Can not get server list";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            List<Server> servers = Program.Servers.GetServers();

            uint ticketId = payload.TicketId;
            foreach (Server server in servers)
            {
                SendReply(writer, CreateServerInfoPayload(server, ticketId));
            }

            //ServerListTest(writer, payload.TicketId);
            
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }

        private void ServerListTest(PayloadWriter writer, uint ticketId)
        {
            GameServerData resultPayload = Payloads.CreatePayload<GameServerData>();

            resultPayload.debug = Crypto.BytesFromHexString("05000000 7465737400" +
                                                           "05000000" +
                                                           "01000000 00" +
                                                           "0D000000 3139322E3136382E382E323000" +
                                                           "67150000" +
                                                           "00 00 00 00 00" +
                                                           "06000000313137353700" +
                                                           "06" +
                                                           "01" +
                                                           "04" +
                                                           "00 00 00" +
                                                           "1B000000 4D505F32505F53746F726D5F436F6173740B64655F313137353700" +
                                                           "00" +
                                                           "00 00 00 00" +
                                                           "0A 00 00 00");
                
            resultPayload.ServerId = 54;
            resultPayload.OwnerId = 20;
            resultPayload.Name = "testname";
            resultPayload.Description = "description";
            resultPayload.Ip = "192.168.8.20";
            resultPayload.Port = 5479;
            resultPayload.MaxPlayers = 6;
            resultPayload.CurPlayers = 2;
            resultPayload.AiPlayers = 1;
            resultPayload.ServerType = 5;
            resultPayload.LobbyId = 9212;
            //resultPayload.Map = "MP_2P_Storm_Coast\vde_11757";
            resultPayload.Map = "MP_2P_Storm_Coast\vde_9212";
            resultPayload.Running = false;
            //resultPayload.Data = Crypto.BytesFromHexString("01 01 01 01 3131373537");
            resultPayload.TicketId = ticketId;

            SendReply(writer, resultPayload);

            //Program.Servers.Register(resultPayload.Name);
            
            var chatPayload = Payloads.CreatePayload<Chat>();
            chatPayload.Txt = $"Created fake game: {resultPayload.Name}";
            chatPayload.FromId = 0;
            SendReply(writer, chatPayload);
        }
        
        private void ServerListTest(PayloadWriter writer, uint ticketId, string name, byte[] data)
        {
            var rnd = new Random();
            GameServerData resultPayload = Payloads.CreatePayload<GameServerData>();

            resultPayload.ServerId = (uint) rnd.Next(100, 1000);
            resultPayload.OwnerId = (uint) rnd.Next(1001, 2000);
            resultPayload.Name = name;
            resultPayload.Description = "description";
            resultPayload.Ip = "192.168.8.20";
            resultPayload.Port = 5479;
            resultPayload.MaxPlayers = 6;
            resultPayload.CurPlayers = 2;
            resultPayload.AiPlayers = 1;
            resultPayload.Map = "MP_2P_Storm_Coast\vde_11757";
            resultPayload.Running = false;
            resultPayload.Data = data;
            resultPayload.TicketId = ticketId;

            SendReply(writer, resultPayload);

            var chatPayload = Payloads.CreatePayload<Chat>();
            chatPayload.Txt = $"Created fake game: {resultPayload.Name}";
            chatPayload.FromId = 0;
            SendReply(writer, chatPayload);
        }
        
        private static GameServerData CreateServerInfoPayload(Server server, uint ticketId)
        {
            var resultPayload = Payloads.CreatePayload<GameServerData>();
            resultPayload.ServerId = server.Id;
            resultPayload.Name = server.Name;
            resultPayload.OwnerId = server.OwnerId;
            resultPayload.Description = server.Description;
            resultPayload.Ip = server.Ip;
            resultPayload.Port = server.Port;
            resultPayload.ServerType = server.ServerType;
            resultPayload.LobbyId = server.LobbyId;
            resultPayload.Version = server.Version;
            //resultPayload.Version = "11757";
            resultPayload.MaxPlayers = server.MaxPlayers;
            resultPayload.CurPlayers = server.GetPlayerCount();
            resultPayload.AiPlayers = server.AiPlayers;
            resultPayload.Level = server.Level;
            resultPayload.GameMode = server.GameMode;
            resultPayload.Hardcore = server.Hardcore;
            resultPayload.Map = server.Map;
            //resultPayload.Map = "MP_2P_Storm_Coast\vde_9212";
            resultPayload.Running = server.Running;
            resultPayload.Data = server.Data;
            //resultPayload.Data = Encoding.Default.GetBytes("9898");
            resultPayload.TicketId = ticketId;

            return resultPayload;
        }

        private void HandleStopServerUpdates(StopServerUpdates payload, PayloadWriter writer)
        {
            uint accountId;
            ServerUpdateReceivers.TryRemove(Connection, out accountId);

            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }

        private void HandleRequestUserBuddyList(RequestUserBuddyList payload, PayloadWriter writer)
        {
            // TODO
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }

        private void HandleRegObserverUserLogin(RegObserverUserLogin payload, PayloadWriter writer)
        {
            //TODO: better implementation?
            if (GlobalLoginReceivers.TryAdd(Connection, Account.Id))
            {
                SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
            }
            else
            {
                Logger.LogError($"RegObserverUserLogin failed for {Account.UserName}");
            }

            // notify new user about all logged in users
            foreach (KeyValuePair<uint, Account> userOnline in GlobalUsersOnline.ToArray())
            {
                var resultPayload = Payloads.CreatePayload<UserLoggedIn>();
                resultPayload.UserId = userOnline.Value.Id;
                resultPayload.Name = userOnline.Value.UserName;
                SendReply(writer, resultPayload);
            }
        }
        
        private void HandleDeregObserverUserLogin(DeregObserverUserLogin payload, PayloadWriter writer)
        {
            uint accountId;
            if (GlobalLoginReceivers.TryRemove(Connection, out accountId))
            {
                SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));   
            }
            else
            {
                Logger.LogError($"DeregObserverUserLogin failed for {accountId}");
            }

            /*
            // notify all users
            foreach (KeyValuePair<uint, uint> loginObserver in GlobalLoginReceivers.ToArray())
            {
                var resultPayload = Payloads.CreatePayload<UserLoggedOut>();
                resultPayload.UserId = accountId;
                SendToLobbyConnection(loginObserver.Key, resultPayload);
            }
            */
        }

        private void HandleJoinServer(JoinServer payload, PayloadWriter writer)
        {
            /*
             * Error IDs:
             * 0x84 (132): GameServer not found
             * 0x87 (135): GameServer full
             */
            Server server = Program.Servers.Get(payload.ServerId);
            if (server == null)
            {
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x84, "GameServer could not be found", payload.TicketId));
                return;
            }

            if (server.IsFull())
            {
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x87, "GameServer is already full", payload.TicketId));
            }
            else
            {
                if (server.Players.TryAdd(Account.Id, Connection))
                {
                    _joinedServerId = server.Id;
                    SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));    
                }
                else
                {
                    SendReply(writer, Payloads.CreateStatusFailMsg(
                        0x84, "Failed to add player to server", payload.TicketId));
                }
            }
        }

        private void HandleLeaveServer(LeaveServer payload, PayloadWriter writer)
        {
            // TODO error messages ?
            Server server = Program.Servers.Get(_joinedServerId);
            if (server == null)
            {
                SendReply(writer, Payloads.CreateStatusFailMsg("User has not joined any server", payload.TicketId));
                return;
            }

            uint connection;
            server.Players.TryRemove(Account.Id, out connection);
            _joinedServerId = 0;
            
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }
        
        private void HandleRequestUserIgnoreList(RequestUserIgnoreList payload, PayloadWriter writer)
        {
            // TODO
            ResultStatusMsg resultPayload = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload.Errorcode = 0;
            resultPayload.Errormsg = null;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);
        }

        private void HandleConnectToServer(ConnectToServer payload, PayloadWriter writer)
        {
            uint serverId = payload.ServerId;

            Server server = Program.Servers.Get(serverId);
            if (server == null)
            {
                ResultStatusMsg resultPayload = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload.Errorcode = 1;
                resultPayload.Errormsg = "Unknown server";
                resultPayload.TicketId = payload.TicketId;
                SendReply(writer, resultPayload);
                return;
            }

            byte[] nonce = Crypto.CreateNonce();

            PlayerConnecting resultPayload2 = Payloads.CreatePayload<PlayerConnecting>();
            resultPayload2.Nonce = nonce;
            resultPayload2.CharId = Account.Id;
            resultPayload2.Name = Account.PlayerName;
            resultPayload2.OwnerId = Account.Id;
            resultPayload2.OwnerName = Account.UserName;
            resultPayload2.GuildId = 0;
            resultPayload2.GuildName = null;
            resultPayload2.GuildRole = 0;
            resultPayload2.Data = _nicknameData;
            SendToLobbyConnection(server.ConnectionId, resultPayload2);

            ConnectToServerReply resultPayload3 = Payloads.CreatePayload<ConnectToServerReply>();
            resultPayload3.PermId = Account.Id;
            resultPayload3.ServerId = serverId;
            resultPayload3.Ip = server.Ip;
            resultPayload3.Port = server.Port;
            resultPayload3.Nonce = nonce;
            resultPayload3.Errorcode = 0;
            resultPayload3.Errormsg = null;
            resultPayload3.TicketId = payload.TicketId;
            SendReply(writer, resultPayload3);
        }

        private void HandleUpdateServerInfo(UpdateServerInfo payload, PayloadWriter writer)
        {
            if (_server == null || _server.Id != payload.ServerId)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "No server";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }
            
            // TODO
            _server.Name = payload.Name;
            _server.Description = payload.Description;
            _server.MaxPlayers = (byte) (payload.MaxPlayers - payload.SlotsOccupied);
            _server.Level = payload.Level;
            _server.GameMode = payload.GameMode;
            _server.Hardcore = payload.Hardcore;
            _server.Map = payload.Map;
            _server.Running = payload.Running;
            _server.Data = payload.Data;
            _server.PropertyMask = payload.PropertyMask;

            SendServerUpdates(payload.TicketId);
            
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
            
            Logger.Log($"Server {payload.Name} got updated by {Account.UserName}");
        }
        private void HandleUpdateServerInfoOld(UpdateServerInfo payload, PayloadWriter writer)
        {
            if (_server == null)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "No server";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            /*
            _server.Name = payload.Name;
            _server.Description = payload.Description;
            _server.MaxPlayers = payload.MaxPlayers;
            _server.RoomId = payload.RoomId;
            _server.Level = payload.Level;
            _server.GameMode = payload.GameMode;
            _server.Hardcore = payload.Hardcore;
            _server.Running = payload.Running;
            _server.LockedConfig = payload.LockedConfig;
            _server.Data = payload.Data;
            if (payload.Cipher == null)
            {
                _server.NeedsPassword = false;
                _server.Password = null;
            }
            else
            {
                byte[] serverPassword = Crypto.HandleCipher(payload.Cipher, SessionKey);
                int length = System.Array.FindIndex(serverPassword, b => b == 0);
                string password = System.Text.Encoding.ASCII.GetString(serverPassword, 0, length);
                Logger.LogDebug($" Server password is: {password}");

                _server.NeedsPassword = true;
                _server.Password = password;
            }
            */

            SendServerUpdates();

            ResultStatusMsg resultPayload2 = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload2.Errorcode = 0;
            resultPayload2.Errormsg = null;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);
        }

        private void HandlePlayerJoinedServer(PlayerJoinedServer payload, PayloadWriter writer)
        {
            if (_server == null)
            {
                return;
            }

            _server.Players.TryAdd(payload.PermId, Connection);
            SendServerUpdates();
        }

        private void HandlePlayerLeftServer(PlayerLeftServer payload, PayloadWriter writer)
        {
            if (_server == null)
            {
                return;
            }

            uint dummy;
            _server.Players.TryRemove(payload.PermId, out dummy);
            SendServerUpdates();
        }

        private void HandleUnlistServer(UnlistServer payload, PayloadWriter writer)
        {
            // TODO
            /*
             * TicketIds:
             * 0xB (11): RemoveGameServer
             * 0xE (14): StartGameServer 
             */
            
            switch (payload.TicketId)
            {
                case 11:
                    Program.Servers.Remove(payload.ServerId);
                    NotifyUnlistServer(payload.ServerId, payload.Running);

                    if (_server != null && _server.Id == payload.ServerId)
                    {
                        _server = null;
                    }
                    break;
                case 14:
                    Thread.Sleep(1000);
                    
                    Server server = Program.Servers.Get(payload.ServerId);
                    if (server == null)
                    {
                        SendReply(writer, Payloads.CreateStatusFailMsg("ServerId does not exist", payload.TicketId));
                        return;
                    }
                    server.Running = payload.Running;

                    KeyValuePair<uint, uint>[] players = server.Players.ToArray();
                    foreach (var player in players)
                    {
                        var gameServerData = CreateServerInfoPayload(server, payload.TicketId);
                        SendToLobbyConnection(player.Value, gameServerData);
                    }
                    //SendServerUpdates(payload.TicketId, server);
                    break;
            }
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }
        
        private void SendServerUpdates(uint ticketId = 0, Server server = null)
        {
            if (server == null)
            {
                server = _server;   
            }
            GameServerData gameServerData = CreateServerInfoPayload(server, ticketId);
            KeyValuePair<uint, uint>[] servers = ServerUpdateReceivers.ToArray();
            foreach (KeyValuePair<uint, uint> updateRecv in servers)
            {
                SendToLobbyConnection(updateRecv.Key, gameServerData);
            }
        }

        private void NotifyUnlistServer(uint serverId, bool running)
        {
            UnlistServer unlistInfo = Payloads.CreatePayload<UnlistServer>();
            unlistInfo.ServerId = serverId;
            unlistInfo.Running = running;
            KeyValuePair<uint, uint>[] servers = ServerUpdateReceivers.ToArray();
            foreach (KeyValuePair<uint, uint> server in servers)
            {
                SendToLobbyConnection(server.Key, unlistInfo);
            }
        }

        private void HandlePropertyGet(PropertyGet payload, PayloadWriter writer)
        {
            var propertyData = Payloads.CreatePayload<PropertyData>();
            propertyData.Kategory = payload.Kategory;
            propertyData.Index = payload.Index;
            propertyData.Value = 0;
            propertyData.TicketId = payload.TicketId;

            SendReply(writer, propertyData);
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }
        
        // Character related payloads
        private void HandleRemoveCharacter(RemoveCharacter payload, PayloadWriter writer)
        {
            // TODO
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }

        private void HandleAddCharacter(AddCharacter payload, PayloadWriter writer)
        {
            // TODO
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
        }
        
        // Chat related payloads
        
        private void HandleRegObserverGlobalChat(RegObserverGlobalChat payload, PayloadWriter writer)
        {
            if (GlobalChatReceivers.TryAdd(Connection, Account.Id))
            {
                SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));   
            }
            else
            {
                Logger.LogError($"Failed to add GlobalChatObserver for {Account.Id}");
            }
        }

        private void HandleDeregObserverGlobalChat(DeregObserverGlobalChat payload, PayloadWriter writer)
        {
            uint accountId;
            if (GlobalChatReceivers.TryRemove(Connection, out accountId))
            {
                SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));   
            }
            else
            {
                Logger.LogError($"Failed to DeregObserverChat for {accountId}");
            }
        }
        
        private void HandleChatPayload(ChatPayload payload, PayloadWriter writer)
        {
            // TODO:
            // - chat filter?

            // for debugging only
            if (payload.Txt.StartsWith("/s"))
            {
                ServerListTest(writer, payload.TicketId);
            }

            if (payload.Txt.StartsWith("/cg"))
            {
                string[] cmds = payload.Txt.Split(' ');
                if (cmds.Length < 3)
                {
                    var chatPayload = Payloads.CreatePayload<Chat>();
                    chatPayload.Txt = $"Usage: /cg <name> <data>";
                    chatPayload.FromId = 0;
                    SendReply(writer, chatPayload);
                    return;
                }
                ServerListTest(writer, payload.TicketId, cmds[1], Encoding.Default.GetBytes(cmds[2]));
            }
            
            var chatobservers = GlobalChatReceivers.ToArray();
            foreach (KeyValuePair<uint, uint> chatobserver in chatobservers)
            {
                var resultPayload = Payloads.CreatePayload<Chat>();
                resultPayload.Txt = payload.Txt;
                resultPayload.FromId = Account.Id;

                SendToLobbyConnection(chatobserver.Key, resultPayload);
            }
        }
    }
}
