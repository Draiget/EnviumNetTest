using Shared;
using Shared.Enums;

namespace Server.Clients
{
    public class GameClient : BaseClient
    {
        public GameClient(BaseServer server) : base(server) {
            Clear();

            Server = server;
        }

        public override void Disconnect(string format, params object[] args) {
            if ( SignonState == ESignonState.None ) {
                return;
            }

            Server.RemoveClientFromGame(this);
        }

        public override bool SetSignonState(ESignonState state) {
            if ( state == ESignonState.Connected ) {
                if ( !CheckConnect() ) {
                    return false;
                }

                NetChannel.SetTimeout( Networking.SignonTimeout );
            } else if ( state == ESignonState.Full) {
                NetChannel.SetTimeout( Program.SvTimeout );
            }

            return base.SetSignonState(state);
        }

        public override bool SendSignonData() {
            // TODO: Check server class tables

            if ( !base.SendSignonData()) {
                return false;
            }

            return true;
        }

        public bool CheckConnect() {
            var rejectReason = "Connection rejected by game";
            if( !Program.ServerPluginHandler.ClientConnect(this, GetClientName(), NetChannel.GetRemoteAddress(), ref rejectReason) ) {
                Disconnect(rejectReason);
                return false;
            }

            return true;
        }

        public override void ActivatePlayer() {
            base.ActivatePlayer();

            Program.ServerPluginHandler.ClientActive(this);
            // TODO: Player active game event
        }

        public override void Reconnect() {
            Server.RemoveClientFromGame(this);
            base.Reconnect();
        }
    }
}
