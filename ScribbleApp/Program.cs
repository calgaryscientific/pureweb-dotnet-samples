// Copyright 2013-2019 Calgary Scientific Inc. (operating under the brand name of PureWeb)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PureWeb.Server;
using System.Windows.Threading;
using System.Net.Sockets;
using System.Net;

namespace ScribbleApp
{
    static class Program
    {
        public static PureWeb.Server.StateManager StateManager;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            StateManager = new PureWeb.Server.StateManager(new PureWeb.Server.WindowsDispatcher());
            StateManager.Uninitialized += new EventHandler(StateManager_Uninitialized);
            
            StateManagerServer server = new StateManagerServer();

            // if no address provided for the PureWeb server then start as a managed service, otherwise
            // start as a unmanaged service

            if (args.Length == 0)
            {
                server.Start(StateManager);
            }
            else
            {
                var port = 8082;
                if (args.Length > 1)
                {
                    port = int.Parse(args[1]);
                }
                server.Start(StateManager, IPAddress.Parse(args[0]), port);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            server.Stop(TimeSpan.FromMilliseconds(250));
        }

        static void StateManager_Uninitialized(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
