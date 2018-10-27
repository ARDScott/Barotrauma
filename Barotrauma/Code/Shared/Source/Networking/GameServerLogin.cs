﻿using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Barotrauma.Networking
{
    class UnauthenticatedClient
    {
        public readonly NetConnection Connection;
        public readonly ulong SteamID;
        public readonly int Nonce;

        public bool WaitingSteamAuth;

        public int FailedAttempts;

        public float AuthTimer;
        
        public UnauthenticatedClient(NetConnection connection, int nonce, ulong steamID = 0)
        {
            Connection = connection;
            SteamID = steamID;
            Nonce = nonce;
            AuthTimer = 10.0f;
            FailedAttempts = 0;
        }
    }
    
    partial class GameServer : NetworkMember, ISerializableEntity
    {
        List<UnauthenticatedClient> unauthenticatedClients = new List<UnauthenticatedClient>();

        private void ReadClientSteamAuthRequest(NetIncomingMessage inc, out ulong clientSteamID)
        {
            clientSteamID = 0;
            if (!Steam.SteamManager.USE_STEAM)
            {
                //not using steam, handle auth normally
                HandleClientAuthRequest(inc.SenderConnection, 0);
                return;
            }
            
            clientSteamID = inc.ReadUInt64();
            int authTicketLength = inc.ReadInt32();
            inc.ReadBytes(authTicketLength, out byte[] authTicketData);

            DebugConsole.Log("Received a Steam auth request");
            DebugConsole.Log("  Steam ID: "+ clientSteamID);
            DebugConsole.Log("  Auth ticket length: " + authTicketLength);

            DebugConsole.Log("  Auth ticket data: " + ((authTicketData == null) ? "null" : authTicketData.Length.ToString()));

            if (banList.IsBanned("", clientSteamID))
            {
                return;
            }

            if (unauthenticatedClients.Any(uc => uc.Connection == inc.SenderConnection && uc.WaitingSteamAuth))
            {
                DebugConsole.Log("Duplicate request");
                return;
            }
            


            if (authTicketData == null)
            {
                DebugConsole.Log("Invalid request");
                return;
            }

            unauthenticatedClients.RemoveAll(uc => uc.Connection == inc.SenderConnection);
            var unauthClient = new UnauthenticatedClient(inc.SenderConnection, 0, clientSteamID)
            {
                AuthTimer = 100,
                WaitingSteamAuth = true
            };
            unauthenticatedClients.Add(unauthClient);
            
            if (!Steam.SteamManager.StartAuthSession(authTicketData, clientSteamID))
            {
                unauthenticatedClients.Remove(unauthClient);
                if (GameMain.Config.RequireSteamAuthentication)
                {
                    unauthClient.Connection.Disconnect(DisconnectReason.SteamAuthenticationFailed.ToString());
                }
                else
                {
                    DebugConsole.Log("Steam authentication failed, skipping to basic auth...");
                    HandleClientAuthRequest(inc.SenderConnection);
                    return;
                }
            }

            return;
        }

        public void OnAuthChange(ulong steamID, ulong ownerID, Facepunch.Steamworks.ServerAuth.Status status)
        {
            DebugConsole.Log("OnAuthChange");
            DebugConsole.Log("  Steam ID: " + steamID);
            DebugConsole.Log("  Owner ID: " + ownerID);
            DebugConsole.Log("  Status: " + status);
            
            UnauthenticatedClient unauthClient = unauthenticatedClients.Find(uc => uc.SteamID == ownerID);
            if (unauthClient != null)
            {
                switch (status)
                {
                    case Facepunch.Steamworks.ServerAuth.Status.OK:
                        ////steam authentication done, check password next
                        unauthenticatedClients.Remove(unauthClient);
                        HandleClientAuthRequest(unauthClient.Connection, unauthClient.SteamID);
                        break;
                    default:
                        unauthenticatedClients.Remove(unauthClient);
                        if (GameMain.Config.RequireSteamAuthentication)
                        {
                            unauthClient.Connection.Disconnect(DisconnectReason.SteamAuthenticationFailed.ToString() + "; (" + status.ToString() + ")");
                        }
                        else
                        {
                            DebugConsole.Log("Steam authentication failed (" + status.ToString() + "), skipping to basic auth...");
                            HandleClientAuthRequest(unauthClient.Connection);
                            return;
                        }
                        break;
                }
                return;
            }

            //kick connected client if status becomes invalid (e.g. VAC banned, not connected to steam)
            if (status != Facepunch.Steamworks.ServerAuth.Status.OK && GameMain.Config.RequireSteamAuthentication)
            {
                var connectedClient = connectedClients.Find(c => c.SteamID == ownerID);
                if (connectedClient != null)
                {
                    KickClient(connectedClient, TextManager.Get("DisconnectMessage.SteamAuthNoLongerValid").Replace("[status]", status.ToString()));
                }
            }
        }

        private void HandleClientAuthRequest(NetConnection connection, ulong steamID = 0)
        {
            if (GameMain.Config.RequireSteamAuthentication && steamID == 0)
            {
                connection.Disconnect(DisconnectReason.SteamAuthenticationRequired.ToString());
                return;
            }

            //client wants to know if server requires password
            if (ConnectedClients.Find(c => c.Connection == connection) != null)
            {
                //this client has already been authenticated
                return;
            }
            
            UnauthenticatedClient unauthClient = unauthenticatedClients.Find(uc => uc.Connection == connection);
            if (unauthClient == null)
            {
                //new client, generate nonce and add to unauth queue
                if (ConnectedClients.Count >= maxPlayers)
                {
                    //server is full, can't allow new connection
                    connection.Disconnect(DisconnectReason.ServerFull.ToString());
                    return;
                }

                int nonce = CryptoRandom.Instance.Next();
                unauthClient = new UnauthenticatedClient(connection, nonce, steamID);
                unauthenticatedClients.Add(unauthClient);
            }
            unauthClient.AuthTimer = 10.0f;
            //if the client is already in the queue, getting another unauth request means that our response was lost; resend
            NetOutgoingMessage nonceMsg = server.CreateMessage();
            nonceMsg.Write((byte)ServerPacketHeader.AUTH_RESPONSE);
            if (string.IsNullOrEmpty(password))
            {
                nonceMsg.Write(false); //false = no password
            }
            else
            {
                nonceMsg.Write(true); //true = password
                nonceMsg.Write((Int32)unauthClient.Nonce); //here's nonce, encrypt with this
            }
            CompressOutgoingMessage(nonceMsg);
            server.SendMessage(nonceMsg, connection, NetDeliveryMethod.Unreliable);
        }

        private void ClientInitRequest(NetIncomingMessage inc)
        {
            if (ConnectedClients.Find(c => c.Connection == inc.SenderConnection) != null)
            {
                //this client was already authenticated
                //another init request means they didn't get any update packets yet
                return;
            }

            UnauthenticatedClient unauthClient = unauthenticatedClients.Find(uc => uc.Connection == inc.SenderConnection);
            if (unauthClient == null)
            {
                //client did not ask for nonce first, can't authorize
                inc.SenderConnection.Disconnect(DisconnectReason.AuthenticationRequired.ToString());
                return;
            }

            if (!string.IsNullOrEmpty(password))
            {
                //decrypt message and compare password
                string saltedPw = password;
                saltedPw = saltedPw + Convert.ToString(unauthClient.Nonce);
                saltedPw = Encoding.UTF8.GetString(NetUtility.ComputeSHAHash(Encoding.UTF8.GetBytes(saltedPw)));
                string clPw = inc.ReadString();
                if (clPw != saltedPw)
                {
                    unauthClient.FailedAttempts++;
                    if (unauthClient.FailedAttempts > 3)
                    {
                        //disconnect and ban after too many failed attempts
                        banList.BanPlayer("Unnamed", unauthClient.Connection.RemoteEndPoint.Address.ToString(), TextManager.Get("DisconnectMessage.TooManyFailedLogins"), duration: null);
                        DisconnectUnauthClient(inc, unauthClient, DisconnectReason.TooManyFailedLogins, "");

                        Log(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " has been banned from the server (too many wrong passwords)", ServerLog.MessageType.Error);
                        DebugConsole.NewMessage(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " has been banned from the server (too many wrong passwords)", Color.Red);
                        return;
                    }
                    else
                    {
                        //not disconnecting the player here, because they'll still use the same connection and nonce if they try logging in again
                        NetOutgoingMessage reject = server.CreateMessage();
                        reject.Write((byte)ServerPacketHeader.AUTH_FAILURE);
                        reject.Write("Wrong password! You have " + Convert.ToString(4 - unauthClient.FailedAttempts) + " more attempts before you're banned from the server.");
                        Log(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " failed to join the server (incorrect password)", ServerLog.MessageType.Error);
                        DebugConsole.NewMessage(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " failed to join the server (incorrect password)", Color.Red);
                        CompressOutgoingMessage(reject);
                        server.SendMessage(reject, unauthClient.Connection, NetDeliveryMethod.Unreliable);
                        unauthClient.AuthTimer = 10.0f;
                        return;
                    }
                }
            }
            string clVersion = inc.ReadString();

            UInt16 contentPackageCount = inc.ReadUInt16();
            List<string> contentPackageNames = new List<string>();
            List<string> contentPackageHashes = new List<string>();
            for (int i = 0; i < contentPackageCount; i++)
            {
                contentPackageNames.Add(inc.ReadString());
                contentPackageHashes.Add(inc.ReadString());
            }

            string clName = Client.SanitizeName(inc.ReadString());
            if (string.IsNullOrWhiteSpace(clName))
            {
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.NoName, "");

                Log(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " couldn't join the server (no name given)", ServerLog.MessageType.Error);
                DebugConsole.NewMessage(inc.SenderConnection.RemoteEndPoint.Address.ToString() + " couldn't join the server (no name given)", Color.Red);
                return;
            }

            if (clVersion != GameMain.Version.ToString())
            {
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.InvalidVersion,
                    TextManager.Get("DisconnectMessage.InvalidVersion").Replace("[version]", GameMain.Version.ToString()).Replace("[clientversion]", clVersion));

                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong game version)", ServerLog.MessageType.Error);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (wrong game version)", Color.Red);
                return;
            }
            
            //check if the client is missing any of the content packages the server requires
            List<ContentPackage> missingPackages = new List<ContentPackage>();
            foreach (ContentPackage contentPackage in GameMain.SelectedPackages)
            {
                if (!contentPackage.HasMultiplayerIncompatibleContent) continue;
                bool packageFound = false;
                for (int i = 0; i < contentPackageCount; i++)
                {
                    if (contentPackageNames[i] == contentPackage.Name && contentPackageHashes[i] == contentPackage.MD5hash.Hash)
                    {
                        packageFound = true;
                        break;
                    }
                }
                if (!packageFound) missingPackages.Add(contentPackage);
            }

            if (missingPackages.Count == 1)
            {
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.MissingContentPackage, TextManager.Get("DisconnectMessage.MissingContentPackage").Replace("[missingcontentpackage]", GetPackageStr(missingPackages[0])));
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (missing content package " + GetPackageStr(missingPackages[0]) + ")", ServerLog.MessageType.Error);
                return;
            }
            else if (missingPackages.Count > 1)
            {
                List<string> packageStrs = new List<string>();
                missingPackages.ForEach(cp => packageStrs.Add(GetPackageStr(cp)));
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.MissingContentPackage, TextManager.Get("DisconnectMessage.MissingContentPackages").Replace("[missingcontentpackages]", string.Join(", ", packageStrs)));
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (missing content packages " + string.Join(", ", packageStrs) + ")", ServerLog.MessageType.Error);
                return;
            }

            string GetPackageStr(ContentPackage contentPackage)
            {
                return "\"" + contentPackage.Name + "\" (hash " + contentPackage.MD5hash.ShortHash + ")";
            }

            //check if the client is using any contentpackages that are not compatible with the server
            List<Pair<string, string>> incompatiblePackages = new List<Pair<string, string>>();
            for (int i = 0; i < contentPackageNames.Count; i++)
            {
                if (!GameMain.Config.SelectedContentPackages.Any(cp => cp.Name == contentPackageNames[i] && cp.MD5hash.Hash == contentPackageHashes[i]))
                {
                    incompatiblePackages.Add(new Pair<string, string>(contentPackageNames[i], contentPackageHashes[i]));
                }
            }

            if (incompatiblePackages.Count == 1)
            {
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.IncompatibleContentPackage, 
                    TextManager.Get("DisconnectMessage.IncompatibleContentPackage").Replace("[incompatiblecontentpackage]", GetPackageStr2(incompatiblePackages[0])));
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (incompatible content package " + GetPackageStr2(incompatiblePackages[0]) + ")", ServerLog.MessageType.Error);
                return;
            }
            else if (incompatiblePackages.Count > 1)
            {
                List<string> packageStrs = new List<string>();
                incompatiblePackages.ForEach(cp => packageStrs.Add(GetPackageStr2(cp)));
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.IncompatibleContentPackage, 
                    TextManager.Get("DisconnectMessage.IncompatibleContentPackages").Replace("[incompatiblecontentpackages]", string.Join(", ", packageStrs)));
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (incompatible content packages " + string.Join(", ", packageStrs) + ")", ServerLog.MessageType.Error);
                return;
            }
            
            string GetPackageStr2(Pair<string, string> nameAndHash)
            {
                return "\"" + nameAndHash.First + "\" (hash " + Md5Hash.GetShortHash(nameAndHash.Second) + ")";
            }

            if (!whitelist.IsWhiteListed(clName, inc.SenderConnection.RemoteEndPoint.Address.ToString()))
            {
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.NotOnWhitelist, "");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (not in whitelist)", ServerLog.MessageType.Error);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (not in whitelist)", Color.Red);
                return;
            }
            if (!Client.IsValidName(clName, this))
            {
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.InvalidName, "");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (invalid name)", ServerLog.MessageType.Error);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (invalid name)", Color.Red);
                return;
            }
            if (Homoglyphs.Compare(clName.ToLower(),Name.ToLower()))
            {
                DisconnectUnauthClient(inc, unauthClient, DisconnectReason.NameTaken, "");
                Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name taken by the server)", ServerLog.MessageType.Error);
                DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name taken by the server)", Color.Red);
                return;
            }
            Client nameTaken = ConnectedClients.Find(c => Homoglyphs.Compare(c.Name.ToLower(), clName.ToLower()));
            if (nameTaken != null)
            {
                if (nameTaken.Connection.RemoteEndPoint.Address.ToString() == inc.SenderEndPoint.Address.ToString())
                {
                    //both name and IP address match, replace this player's connection
                    nameTaken.Connection.Disconnect(DisconnectReason.SessionTaken.ToString());
                    nameTaken.Connection = unauthClient.Connection;
                    nameTaken.InitClientSync(); //reinitialize sync ids because this is a new connection
                    unauthenticatedClients.Remove(unauthClient);
                    unauthClient = null;
                    return;
                }
                else
                {
                    //can't authorize this client
                    DisconnectUnauthClient(inc, unauthClient, DisconnectReason.NameTaken, "");
                    Log(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name already taken)", ServerLog.MessageType.Error);
                    DebugConsole.NewMessage(clName + " (" + inc.SenderConnection.RemoteEndPoint.Address.ToString() + ") couldn't join the server (name already taken)", Color.Red);
                    return;
                }
            }

            //new client
            Client newClient = new Client(clName, GetNewClientID());
            newClient.InitClientSync();
            newClient.Connection = unauthClient.Connection;
            newClient.SteamID = unauthClient.SteamID;
            unauthenticatedClients.Remove(unauthClient);
            unauthClient = null;
            ConnectedClients.Add(newClient);

#if CLIENT
            GameMain.NetLobbyScreen.AddPlayer(newClient.Name);
#endif
            GameMain.Server.SendChatMessage(clName + " has joined the server.", ChatMessageType.Server, null);

            var savedPermissions = clientPermissions.Find(cp => 
                cp.SteamID > 0 ? 
                cp.SteamID == newClient.SteamID :            
                cp.IP == newClient.Connection.RemoteEndPoint.Address.ToString());

            if (savedPermissions != null)
            {
                newClient.SetPermissions(savedPermissions.Permissions, savedPermissions.PermittedCommands);
            }
            else
            {
                newClient.SetPermissions(ClientPermissions.None, new List<DebugConsole.Command>());
            }
        }
                
        private void DisconnectUnauthClient(NetIncomingMessage inc, UnauthenticatedClient unauthClient, DisconnectReason reason, string message)
        {
            inc.SenderConnection.Disconnect(reason.ToString() + "; " + message);

            if (unauthClient != null)
            {
                unauthenticatedClients.Remove(unauthClient);
            }
        }
    }
}
