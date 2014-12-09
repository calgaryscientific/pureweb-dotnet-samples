//
// Copyright (c) 2012 Calgary Scientific Inc., all rights reserved.
//

using System;
using System.Diagnostics;
using log4net;

namespace DDxServiceCs
{
    class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            try
            {
                DDx app = new DDx();
                app.Go(args);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                throw;
            }
        }
    }
}
