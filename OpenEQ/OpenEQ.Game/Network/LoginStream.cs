﻿using System.Security.Cryptography;
using static System.Console;
using static System.Text.Encoding;
using static OpenEQ.Utility;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace OpenEQ.Network {
    public class LoginStream : EQStream {
        public event EventHandler<bool> LoginSuccess;
        public event EventHandler<List<ServerListElement>> ServerList;
        public event EventHandler<ServerListElement?> PlaySuccess;

        public uint accountID;
        public string sessionKey;

        ServerListElement? curPlay;

        byte[] cryptoBlob;
        bool triedOnce = false;
        public LoginStream(string host, int port) : base(host, port) {
            Connect();
        }

        public void Login(string username, string password) {
            cryptoBlob = Encrypt(username, password);
            if(triedOnce)
                Send(AppPacket.Create(LoginOp.Login, new Login(), cryptoBlob));
            else
                SendSessionRequest();
        }

        byte[] Encrypt(string username, string password) {
            var tbuf = new byte[username.Length + password.Length + 2];
            Array.Copy(ASCII.GetBytes(username), tbuf, username.Length);
            Array.Copy(ASCII.GetBytes(password), 0, tbuf, username.Length + 1, password.Length);
            tbuf[username.Length] = 0;
            tbuf[username.Length + password.Length + 1] = 0;
            return Encrypt(tbuf);
        }
        byte[] Encrypt(byte[] buffer) {
            var empty = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var des = new DESCrypto(empty, empty);
            return des.Encrypt(buffer);
        }
        byte[] Decrypt(byte[] buffer, int offset = 0) {
            var empty = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var des = new DESCrypto(empty, empty);
            return des.Decrypt(offset == 0 ? buffer : buffer.Sub(offset));
        }

        protected override void HandleSessionResponse(Packet packet) {
            Send(AppPacket.Create(LoginOp.SessionReady, new SessionReady()));
        }

        protected override void HandleAppPacket(AppPacket packet) {
            switch((LoginOp) packet.Opcode) {
                case LoginOp.ChatMessage:
                    Send(AppPacket.Create(LoginOp.Login, new Login(), cryptoBlob));
                    break;
                case LoginOp.LoginAccepted:
                    if(packet.Data.Length < 80)
                        LoginSuccess?.Invoke(this, false);
                    else {
                        var dec = Decrypt(packet.Data, 10);
                        var rep = LoginReply.Get(dec);
                        accountID = rep.acctID;
                        sessionKey = rep.key;
                        LoginSuccess?.Invoke(this, true);
                    }
                    break;
                case LoginOp.ServerListResponse:
                    var header = packet.Get<ServerListHeader>();
                    var off = 5 * 4;
                    var data = packet.Data;
                    var serverList = new List<ServerListElement>();
                    for(var i = 0; i < header.serverCount; ++i)
                        serverList.Add(new ServerListElement(data, ref off));
                    ServerList?.Invoke(this, serverList);
                    break;
                case LoginOp.PlayEverquestResponse:
                    var resp = packet.Get<PlayResponse>();

                    if(!resp.allowed)
                        curPlay = null;
                    
                    PlaySuccess?.Invoke(this, curPlay);
                    break;
                default:
                    WriteLine($"Unhandled packet in LoginStream: {(LoginOp) packet.Opcode} (0x{packet.Opcode:X04})");
                    Hexdump(packet.Data);
                    break;
            }
        }

        public void RequestServerList() {
            Send(AppPacket.Create(LoginOp.ServerListRequest));
        }

        public void Play(ServerListElement server) {
            curPlay = server;
            Send(AppPacket.Create(LoginOp.PlayEverquestRequest, new PlayRequest(server.runtimeID)));
        }
    }
}
