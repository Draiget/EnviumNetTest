using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

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

        
    }
}
