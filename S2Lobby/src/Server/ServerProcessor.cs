using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Schema;
using S2Library.Protocol;

namespace S2Lobby
{
    public class ServerProcessor : NetworkProcessor
    {
        protected readonly Serializer _Logger;

        protected readonly Database Database;
        protected Account Account;

        private byte[] _sharedSecret;
        protected byte[] SessionKey;

        public static readonly ConcurrentDictionary<uint, uint> GlobalLoginReceivers = new ConcurrentDictionary<uint, uint>();
        public static readonly ConcurrentDictionary<uint, Account> GlobalUsersOnline = new ConcurrentDictionary<uint, Account>();

        public ServerProcessor(Program program, uint connection) : base(program, connection)
        {
            _Logger = new PayloadLogger(Logger.LogDebug);
            Database = new Database(program);
        }

        public override void Close()
        {
            Database.Dispose();
        }

        public void SendToLobbyConnection(uint connection, PayloadPrefix message)
        {
            LobbyProcessor processor = Program.GetLobbyProcessor(connection);
            if (processor == null)
            {
                return;
            }

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            PayloadWriter payloadWriter = new PayloadWriter(writer);

            message.Serialize(payloadWriter);

            Logger.LogDebug($" --- Payload sending to {connection}: {(Payloads.Types)message.Type2} ---");
            message.Serialize(_Logger);

            processor.SendReply(MessageContainer.Types.ApplicationMessage, stream);

            writer.Close();
            stream.Close();
        }

        public void SendToLobbyConnection(uint connection, TestPayloadPrefix message)
        {
            LobbyProcessor processor = Program.GetLobbyProcessor(connection);
            if (processor == null)
            {
                return;
            }

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            var payloadWriter = new PayloadWriter(writer);

            message.Serialize(payloadWriter);

            Logger.LogDebug($" --- Payload sending to {connection}: {(Payloads.Types)message.Type} ---");
            message.Serialize(_Logger);

            processor.SendReply(MessageContainer.Types.ApplicationMessage, stream);

            writer.Close();
            stream.Close();
        }
        
        public void CloseLobbyProcessor(uint connection)
        {
            LobbyProcessor processor = Program.GetLobbyProcessor(connection);
            if (processor == null)
            {
                return;
            }
            processor.Close();
        }
        
        protected sealed override void HandleMessage(BinaryReader reader, BinaryWriter writer)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            PayloadReader payloadReader = new PayloadReader(reader);
            PayloadWriter payloadWriter = new PayloadWriter(writer);

            PayloadPrefix prefix = new PayloadPrefix();
            prefix.Serialize(payloadReader);


            if (prefix.Magic == PayloadPrefix.PayloadMagic)
            {
                if (prefix.Type1 != prefix.Type2)
                {
                    Logger.Log($" Corrupt payload type, first is {prefix.Type1:X04} but second is {prefix.Type2:X04}");
                }

                Payloads.Types payloadType = (Payloads.Types)prefix.Type2;
                PayloadPrefix payload = Payloads.CreatePayload(payloadType);

                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                payload.Serialize(payloadReader);

                HandlePayloadType(payloadType, payload, payloadWriter);
            }
            else if (prefix.Magic == ChatPayloadPrefix.PayloadMagic)
            {
                if (prefix.Type1 != 0)
                {
                    Logger.Log($" Corrupt payload chatTypes, is {prefix.Type1:X04} but expected 0");
                }

                ChatPayloads.ChatTypes chatPayloadType = (ChatPayloads.ChatTypes)prefix.Type2;
                ChatPayloadPrefix chatPayload = ChatPayloads.CreateChatPayload(chatPayloadType);

                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                chatPayload.Serialize(payloadReader);

                HandleChatPayloadType(chatPayloadType, chatPayload, payloadWriter);
            }
            else
            {
                Logger.Log($" Incorrect payload magic, is {prefix.Magic:X04} but should be {PayloadPrefix.PayloadMagic:X04}");
            }

        }

        protected virtual bool HandlePayloadType(Payloads.Types payloadType, PayloadPrefix payload, PayloadWriter writer)
        {
            Logger.LogDebug($" --- Payload received: {payloadType} ---");
            payload.Serialize(_Logger);

            switch (payloadType)
            {
                case Payloads.Types.VersionCheck:
                    HandleVersionCheck((VersionCheck) payload, writer);
                    return true;
                case Payloads.Types.Login:
                    HandleLogin((Login) payload, writer);
                    return true;
                case Payloads.Types.RegisterUser:
                    HandleRegisterUser((RegisterUser) payload, writer);
                    return true;
                case Payloads.Types.LoginUser:
                    HandleLoginUser((LoginUser) payload, writer);
                    return true;
                case Payloads.Types.LoginServer:
                    HandleLoginServer((LoginServer) payload, writer);
                    return true;
                case Payloads.Types.RequestLogin:
                    HandleRequestLogin((RequestLogin) payload, writer);
                    return true;
                case Payloads.Types.RequestCreateAccount:
                    HandleCreateAccount((RequestCreateAccount) payload, writer);
                    return true;
                
                default:
                    return false;
            }
        }

        protected virtual void HandleChatPayloadType(ChatPayloads.ChatTypes chatPayloadType, ChatPayloadPrefix chatPayload, PayloadWriter payloadWriter)
        {
            Logger.LogDebug(" Unknown chat payload message received");
            chatPayload.Serialize(_Logger);
        }

        protected void SendReply(PayloadWriter writer, PayloadPrefix payload)
        {
            payload.Serialize(writer);
            SendReply(MessageContainer.Types.ApplicationMessage, writer.BaseStream);

            Logger.LogDebug($" --- Payload sending: {(Payloads.Types)payload.Type2} ---");
            payload.Serialize(_Logger);
        }

        private void HandleVersionCheck(VersionCheck payload, PayloadWriter writer)
        {
            ResultStatusMsg resultPayload = Payloads.CreatePayload<ResultStatusMsg>();
            resultPayload.Errorcode = 0;
            resultPayload.Errormsg = null;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);
        }

        private void HandleRequestLogin(RequestLogin payload, PayloadWriter writer)
        {
            /*
             * ERROR codes:
             * 0x0: OK
             * 0x1B: CD Key invalid
             * 0x3D: auth failed
             * 0x3E: wrong version
             */
            
            if (payload.Patchlevel != Constants.Patchlevel)
            {
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x3E, "Patchlevel does not match", payload.TicketId));
                return;
            }
            
            Account = Program.Accounts.Get(Database.Connection, payload.Nick);
            if (Account == null)
            {
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x3D, "Username does not exist", payload.TicketId));
                return;
            }
            
            if (!Serializer.CompareArrays(payload.Password, Account.Password))
            {
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x3D, "Wrong password", payload.TicketId));
                return;
            }

            GlobalUsersOnline.TryAdd(Connection, Account);
            
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
            
            Logger.Log($"User {Account.UserName} logged in");
            
            NotifyLoginReceivers(Account.Id, Account.UserName);
        }

        private void HandleCreateAccount(RequestCreateAccount payload, PayloadWriter writer)
        {
            /*
             * ERR codes:
             * 0x0: OK
             * 0x1A: CD key invalid
             * 0x29: user already exists
             * 0x3E: wrong version
             */

            if (payload.Patchlevel != Constants.Patchlevel)
            {
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x3E, "Patchlevel does not match", payload.TicketId));
                return;
            }
            
            uint res = Program.Accounts.Create(Database.Connection, payload.Nick, payload.Password, payload.CdKey);
            if (res == 0)
            {
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x29, "Username already in use", payload.TicketId));
                return;
            }
            
            Account = Program.Accounts.Get(Database.Connection, payload.Nick);
            if (Account == null)
            {
                // Sending 1A because of lack of proper error code
                SendReply(writer, Payloads.CreateStatusFailMsg(
                    0x1A, "Account not created", payload.TicketId));
                return;
            }

            GlobalUsersOnline.TryAdd(Connection, Account);
            
            SendReply(writer, Payloads.CreateStatusOkMsg(payload.TicketId));
            
            Logger.Log($"User {Account.UserName} has registered");
            
            NotifyLoginReceivers(Account.Id, Account.UserName);
        }

        private void NotifyLoginReceivers(uint userId, string userName)
        {
            foreach (KeyValuePair<uint, uint> loginObserver in GlobalLoginReceivers.ToArray())
            {
                var resultPayload = Payloads.CreatePayload<UserLoggedIn>();
                resultPayload.UserId = userId;
                resultPayload.Name = userName;
                
                SendToLobbyConnection(loginObserver.Key, resultPayload);
            }
        }
        
        private void HandleLogin(Login payload, PayloadWriter writer)
        {
            byte[] loginKey = payload.Key;

            _sharedSecret = Crypto.CreateSecretKey();
            byte[] result = Crypto.HandleKey(loginKey, _sharedSecret);

            LoginReply resultPayload = Payloads.CreatePayload<LoginReply>();
            resultPayload.Cipher = result;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);
        }
        
        private void HandleRegisterUser(RegisterUser payload, PayloadWriter writer)
        {
            byte[] loginCipher = payload.Cipher;

            byte[] result = Crypto.HandleCipher(loginCipher, _sharedSecret);
            Logger.LogDebug($" User: {Serializer.DumpBytes(result)}");

            MemoryStream stream = new MemoryStream(result);
            BinaryReader reader = new BinaryReader(stream);

            bool invalid = false;

            byte[] nameBytes = null;
            int nameLength = reader.ReadByte();
            if (nameLength < 32)
            {
                nameBytes = reader.ReadBytes(nameLength);
            }
            else
            {
                invalid = true;
            }

            byte[] passwordBytes = null;
            if (!invalid)
            {
                int passwordLength = reader.ReadByte();
                if (nameLength < 32)
                {
                    passwordBytes = reader.ReadBytes(passwordLength);
                }
                else
                {
                    invalid = true;
                }
            }

            byte[] cdKey = null;
            if (!invalid)
            {
                int keysLength = reader.ReadByte();
                int keyPool = reader.ReadByte();
                int keyLength = reader.ReadByte();
                if (keysLength != 1 || keyPool != 1 || keyLength != 16)
                {
                    invalid = true;
                }
                else
                {
                    cdKey = reader.ReadBytes(keyLength);
                }
            }

            if (invalid)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "Encryption failure";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            string name = Encoding.ASCII.GetString(nameBytes);
            byte[] password = Crypto.HashPassword(passwordBytes);

            uint id = Program.Accounts.Create(Database.Connection, name, password, cdKey);
            if (id == 0)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Username already in use";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            Account = Program.Accounts.Get(Database.Connection, name);
            if (Account == null)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Account not created";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            SessionKey = Crypto.CreateSecretKey();
            byte[] secret = Crypto.HandleSessionKey(SessionKey, _sharedSecret);

            LoginReplyCipher resultPayload2 = Payloads.CreatePayload<LoginReplyCipher>();
            resultPayload2.PermId = Account.Id;
            resultPayload2.Cipher = secret;
            resultPayload2.TicketId = payload.TicketId;
            SendReply(writer, resultPayload2);

            Logger.Log($"Account created for {name}");
        }

        private void HandleLoginUser(LoginUser payload, PayloadWriter writer)
        {
            byte[] loginCipher = payload.Cipher;

            byte[] result = Crypto.HandleCipher(loginCipher, _sharedSecret);
            Logger.LogDebug($" User: {Serializer.DumpBytes(result)}");

            MemoryStream stream = new MemoryStream(result);
            BinaryReader reader = new BinaryReader(stream);

            bool invalid = false;

            byte[] nameBytes = null;
            int nameLength = reader.ReadByte();
            if (nameLength < 32)
            {
                nameBytes = reader.ReadBytes(nameLength);
            }
            else
            {
                invalid = true;
            }

            byte[] passwordBytes = null;
            if (!invalid)
            {
                int passwordLength = reader.ReadByte();
                if (nameLength < 32)
                {
                    passwordBytes = reader.ReadBytes(passwordLength);
                }
                else
                {
                    invalid = true;
                }
            }

            if (invalid)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "Encryption failure";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            string name = Encoding.ASCII.GetString(nameBytes);
            byte[] password = Crypto.HashPassword(passwordBytes);

            #if DEBUG
            Logger.LogDebug($"Username: {name} | Password: {Serializer.DumpBytes(password)}");
            #endif
            
            Account = Program.Accounts.Get(Database.Connection, name);
            if (Account == null)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Account not found";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            if (!Serializer.CompareArrays(password, Account.Password))
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Wrong password";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            SessionKey = Crypto.CreateSecretKey();
            byte[] secret = Crypto.HandleSessionKey(SessionKey, _sharedSecret);

            LoginReplyCipher resultPayload = Payloads.CreatePayload<LoginReplyCipher>();
            resultPayload.PermId = Account.Id;
            resultPayload.Cipher = secret;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);

            Logger.Log($"User {name} logged in");
        }

        private void HandleLoginServer(LoginServer payload, PayloadWriter writer)
        {
            byte[] loginCipher = payload.Cipher;

            byte[] result = Crypto.HandleCipher(loginCipher, _sharedSecret);
            Logger.LogDebug($" User: {Serializer.DumpBytes(result)}");

            MemoryStream stream = new MemoryStream(result);
            BinaryReader reader = new BinaryReader(stream);

            bool invalid = false;

            byte[] nameBytes = null;
            int nameLength = reader.ReadByte();
            if (nameLength < 32)
            {
                nameBytes = reader.ReadBytes(nameLength);
            }
            else
            {
                invalid = true;
            }

            byte[] passwordBytes = null;
            if (!invalid)
            {
                int passwordLength = reader.ReadByte();
                if (nameLength < 32)
                {
                    passwordBytes = reader.ReadBytes(passwordLength);
                }
                else
                {
                    invalid = true;
                }
            }

            if (invalid)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 3;
                resultPayload1.Errormsg = "Encryption failure";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            string name = Encoding.ASCII.GetString(nameBytes);
            byte[] password = Crypto.HashPassword(passwordBytes);

            Account = Program.Accounts.Get(Database.Connection, name);
            if (Account == null)
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Account not found";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            if (!Serializer.CompareArrays(password, Account.Password))
            {
                ResultStatusMsg resultPayload1 = Payloads.CreatePayload<ResultStatusMsg>();
                resultPayload1.Errorcode = 1;
                resultPayload1.Errormsg = "Wrong password";
                resultPayload1.TicketId = payload.TicketId;
                SendReply(writer, resultPayload1);
                return;
            }

            SessionKey = Crypto.CreateSecretKey();
            byte[] secret = Crypto.HandleSessionKey(SessionKey, _sharedSecret);

            LoginReplyCipher resultPayload = Payloads.CreatePayload<LoginReplyCipher>();
            resultPayload.PermId = Account.Id;
            resultPayload.Cipher = secret;
            resultPayload.TicketId = payload.TicketId;
            SendReply(writer, resultPayload);

            Logger.Log($"Server logged in for user {name}");
        }
    }
}
