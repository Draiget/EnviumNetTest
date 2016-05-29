using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using Server.Clients;

namespace Server.Plugins
{
    public interface IServerPluginCallbacks
    {
        string GetPluginDescription();

        void Unload();
        bool Load();

        void Pause();
        void UnPause();

        void LevelInit(string levelName);
        void ServerActivate();
        void GameFrame();
        void LevelShutdown();

        void ClientActive( BaseClient client );
        void ClientDisconnect( BaseClient client );
        void ClientPutInServer( BaseClient client, string name );

        EPluginResult ClientConnect( ref bool allowConnect, BaseClient client, string name, EndPoint address, ref string rejectReason );

        // TODO: Add second 'Command' argument
        EPluginResult ClientCommand( BaseClient client );

        EPluginResult NetworkIdValidated( string playerName, string networkId );
    }
}
