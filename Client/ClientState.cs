using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using Shared;
using Shared.Enums;
using Shared.Messages;
using Shared.NetMessages;

namespace Client
{
    public class ClientState : BaseClientState
    {
        public float FrameTime;

        public ClientState() {
            FrameTime = 0;
        }

        public override bool ProcessConnectionlessPacket(NetPacket packet) {
            Debug.Assert(packet != null);
            return base.ProcessConnectionlessPacket(packet);
        }

        public override void FullConnect(EndPoint addr) {
            base.FullConnect(addr);

            NetChannel.SetDataRate(Program.ClRate);

            Console.WriteLine("Connected to {0}", addr);
        }

        public override void ConnectionClosing(string reason) {
            if ( SignonState > ESignonState.None ) {
                Console.WriteLine("Disconnect: {0}", reason);
                // TODO: Disable loading screen
            }
        }

        public override void ConnectionCrashed(string reason) {
            if ( SignonState > ESignonState.None ) {
                Console.WriteLine("Disconnect: {0}", reason);
            }
        }

        public override void Disconnect( bool showMainMenu ) {
            base.Disconnect(showMainMenu);

            // TODO: Stop all sounds
            // TODO: Show main menu

            Clear();
        }

        public override void FileDenied(string fileName, uint transfterId) {
            // TODO: DownloadManager.FileDenied
        }

        public override void FileReceived(string fileName, uint transferId) {
            // TODO: DownloadManager.FileReceived
        }
        
        public override void FileRequested(string fileName, uint transferId) {
            Console.WriteLine("File '{0}' requested from server {1}", fileName, NetChannel.GetRemoteAddress());

            if ( !Program.ClAllowUpload ) {
                Console.WriteLine("File uploading disabled.");
                NetChannel.DenyFile( fileName, transferId );
                return;
            }

            // TODO: Check if file valid for uploading
            NetChannel.SendFile(fileName, transferId);
        }

        public override void RunFrame(double time) {
            base.RunFrame(time);

            if ( NetChannel != null ) {
                NetChannel.SetDataRate( Program.ClRate );
            }
        }

        public override bool ProcessTick(NetMessageTick msg) {
            var tick = msg.Tick;

            // TODO: Set clock server tick
            // TODO: Store remembered server tick time -> tick * IntervalPerTick

            NetChannel.SetRemoteFramerate(msg.HostFrameTime, msg.HostFrameTimeStdDeviation);
            return true;
        }

        public void SetFrameTime(float time) {
            FrameTime = time;
        }
    }
}
