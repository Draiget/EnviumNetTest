using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Server.Clients;
using Shared;
using Shared.Channel;
using Shared.Enums;

namespace Server
{
    public class GameServer : BaseServer
    {
        public GameServer(Socket serverSocket) : base(serverSocket) {
        }

        public override void RemoveClientFromGame(BaseClient client) {
            var cl = (GameClient) client;

            if ( cl == null || !cl.IsSpawned() || !IsActive() ) {
                return;
            }

            Program.ServerPluginHandler.ClientDisconnect(cl);
        }

        public override void Shutdown() {
            base.Shutdown();

            // TODO: Make shutdown event
            Console.WriteLine("Server studown.");
        }

        public override void SendClientMessages(bool sendSnapshots) {
            var receivingClients = new List<GameClient>();
            foreach (var cl in Clients) {
                var client = (GameClient) cl;

                if ( !client.ShouldSendMessage() ) {
                    continue;
                }

                if (sendSnapshots && client.IsActive()) {
                    receivingClients.Add(client);
                } else {

                    // if client never send a netchannl packet yet, send ConnectionAccept because it could get lost in multiplayer
                    if ( client.NetChannel.GetSequenceNr( EFlowType.Incoming ) == 0) {
                        Networking.OutOfBandPrintf(Socket, client.NetChannel.GetRemoteAddress(), "{0}00000000000000", EConnectionType.ConnectionAccept);
                    }

                    client.NetChannel.Transmit();
                    client.UpdateSendState();
                }
            }

            if( receivingClients.Count > 0 ) {
                // if any client wants an update, take new snapshot now
                var snapshot = Program.FrameSnapshotManager.TakeTickSnapshot(TickCount);

                // copy temp ents references to pSnapshot
                CopyTempEntities(snapshot);

                foreach (var cl in receivingClients) {
                    var frame = cl.GetSendFrame();
                    if ( frame == null ) {
                        continue;
                    }

                    cl.SendSnapshot();
                    cl.UpdateSendState();
                }

                snapshot.ReleaseRefrence();
            }
        }

        public void CopyTempEntities( FrameSnapshot snapshot ) {
            
        }
    }
}
