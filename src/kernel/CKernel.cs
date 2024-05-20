using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CWebServerLib {
    public static class CKernel {
        #region Entry Point
        /// <summary>The main entry point for the application.</summary>
        [STAThread]
        static int Main() {
            return CGlobals.g.registerAppMain( "CWebServerLib", run );
        }

        public static bool run( CList<string> args ) {
            var srv = new CWebServer();
            srv.serverStartEx3(new string[] {
                @"https://*:9091/"
            });
            while (srv.ServerIsListening) {
                srv_ContextReceived( srv, srv.getContext(), false );
            }

            return true;
        }

        static void srv_ContextReceived( CWebServer sender, System.Net.HttpListenerContext context, bool isAsync ) {
            context.Response.Close( "VIO WEBSERVER IS RUNNING".toBuffer(), false );
        }
        #endregion
    }
}
