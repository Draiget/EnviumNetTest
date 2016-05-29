using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Server.Plugins
{
    public class Plugin
    {
        private string _pluginName;
        private bool _pluginDisabled;
        private IServerPluginCallbacks _pluginCallbacks;

        public Plugin() {
            _pluginDisabled = false;
            _pluginName = string.Empty;
            _pluginCallbacks = null;
        }

        ~Plugin() {
            if( _pluginCallbacks != null ) {
                Unload();
            }

            _pluginCallbacks = null;
        }

        public bool Load(string fileName) {
            if (File.Exists(fileName)) {
                // TODO: Load plugin
                var assembly = Assembly.LoadFile(fileName);
                if( assembly != null ) {
                    var types = assembly.GetTypes().Where(type => typeof (IServerPluginCallbacks).IsAssignableFrom(type));
                    foreach( var type in types ) {
                        _pluginCallbacks = (IServerPluginCallbacks)Activator.CreateInstance(type);
                        if( _pluginCallbacks == null ) {
                            Console.WriteLine("Could not get IServerPluginCallbacks interface from plugin \"{0}\"", fileName);
                            return false;
                        }
                    }

                    if( !_pluginCallbacks.Load() ) {
                        Console.WriteLine("Failed to load plugin \"{0}\"!", fileName);
                        return false;
                    }

                    _pluginName = _pluginCallbacks.GetPluginDescription();
                }
            } else {
                Console.WriteLine("Unable to load plugin \"{0}\"!", fileName);
                return false;
            }

            return true;
        }

        public void Unload() {
            if( _pluginCallbacks != null ) {
                _pluginCallbacks.Unload();
            }

            _pluginCallbacks = null;
        }

        public void Disable(bool state) {
            if (state) {
                _pluginCallbacks.Pause();
            } else {
                _pluginCallbacks.UnPause();
            }

            _pluginDisabled = state;
        }

        public bool IsDisabled {
            get { return _pluginDisabled; }
        }

        public string Name {
            get { return _pluginName; }
        }

        public IServerPluginCallbacks Callbacks {
            get { return _pluginCallbacks; }
        }
    }
}
