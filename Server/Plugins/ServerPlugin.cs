using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Server.Clients;

namespace Server.Plugins
{
    public class ServerPlugin
    {
        private List<Plugin> _plugins; 

        public ServerPlugin() {
            _plugins = new List<Plugin>();
        }

        public void LoadPlugins() {
            _plugins.Clear();
            foreach (var pluginFile in Directory.GetFiles("plugins\\", "*.dll", SearchOption.TopDirectoryOnly)) {
                LoadPlugin(pluginFile);
            }
        }

        public void UnloadPlugins() {
            foreach (var plugin in _plugins) {
                plugin.Unload();
                _plugins.Remove(plugin);
            }
        }

        public void DisablePlugins() {
            foreach (var plugin in _plugins) {
                plugin.Disable(true);
            }
        }

        public void EnablePlugins() {
            foreach( var plugin in _plugins ) {
                plugin.Disable(false);
            }
        }

        public bool LoadPlugin(string fileName) {
            var plugin = new Plugin();
            if ( plugin.Load(fileName) ) {
                _plugins.Add(plugin);
                return true;
            }

            return false;
        }

        public virtual void ServerActivate() {
            foreach( var plugin in _plugins.Where(plugin => !plugin.IsDisabled) ) {
                plugin.Callbacks.ServerActivate();
            }
        }

        public virtual void GameFrame() {
            foreach( var plugin in _plugins.Where(plugin => !plugin.IsDisabled) ) {
                plugin.Callbacks.GameFrame();
            }
        }

        public virtual void LevelShutdown() {
            foreach( var plugin in _plugins.Where(plugin => !plugin.IsDisabled) ) {
                plugin.Callbacks.LevelShutdown();
            }
        }

        public virtual void ClientActive(BaseClient client) {
            foreach( var plugin in _plugins.Where(plugin => !plugin.IsDisabled) ) {
                plugin.Callbacks.ClientActive(client);
            }
        }

        public virtual void ClientDisconnect(BaseClient client) {
            foreach( var plugin in _plugins.Where(plugin => !plugin.IsDisabled) ) {
                plugin.Callbacks.ClientDisconnect(client);
            }
        }

        public virtual void ClientPutInServer(BaseClient client, string name) {
            foreach( var plugin in _plugins.Where(plugin => !plugin.IsDisabled) ) {
                plugin.Callbacks.ClientPutInServer(client, name);
            }
        }

        public virtual bool ClientConnect( BaseClient client, string playerName, EndPoint addr, ref string rejectReason ) {
            var allowConnect = true;
            var savedRetVal = true;
            var retValOverriden = false;
            foreach (var plugin in _plugins.Where(plugin => !plugin.IsDisabled)) {
                var result = plugin.Callbacks.ClientConnect(ref allowConnect, client, playerName, addr, ref rejectReason);
                if( result == EPluginResult.Stop) {
                    return allowConnect;
                }

                if( result == EPluginResult.Override && !retValOverriden ) {
                    savedRetVal = allowConnect;
                    retValOverriden = true;
                }
            }

            return retValOverriden ? savedRetVal : allowConnect;
        }

        public virtual void ClientCommand( BaseClient client ) {
            foreach (var plugin in _plugins.Where(plugin => !plugin.IsDisabled)) {
                var result = plugin.Callbacks.ClientCommand(client);
                if ( result == EPluginResult.Stop ) {
                    return;
                }
            }
        }

        public virtual void NetworkIdValidated( string playerName, string networkId ) {
            foreach (var plugin in _plugins.Where(plugin => !plugin.IsDisabled)) {
                var result = plugin.Callbacks.NetworkIdValidated(playerName, networkId);
                if ( result == EPluginResult.Stop ) {
                    return;
                }
            }
        }
    }
}
