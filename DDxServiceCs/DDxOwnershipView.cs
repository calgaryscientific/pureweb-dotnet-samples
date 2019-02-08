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
using System.Drawing;
using log4net;
using PureWeb;
using PureWeb.Server;
using PureWeb.Ui;

namespace DDxServiceCs
{
    class DDxOwnershipView : IRenderedView
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof (DDxOwnershipView));

        public String m_viewName;
        private Size m_clientSize;
        private IRemoteRenderer m_remoteRenderer;

        public DDxOwnershipView(StateManager stateManager)
        {
            m_viewName = "DDx_OwnershipView";
            m_remoteRenderer = stateManager.ViewManager;
            stateManager.ViewManager.RegisterView(m_viewName, this);
        }

        public virtual void SetClientSize(Size clientSize)
        {
            if (clientSize != m_clientSize)
            {
                m_clientSize = clientSize;
            }
        }

        public virtual Size GetActualSize()
        {
            if (m_clientSize.IsEmpty)
                return DDx.DefaultSize;

            return m_clientSize; 
        }

        public bool RequiresRender()
        {
            return false;
        }

        public virtual void RenderView(RenderTarget target)
        {
                var image = target.Image;

                Canvas c = new Canvas(ref image);
                c.Clear(PureWebColor.FromKnownColor(PureWebKnownColor.DarkBlue));
                for (int i = 0; i < image.Height; i = i + 20)
                {
                    c.DrawHLine(PureWebColor.FromKnownColor(PureWebKnownColor.AntiqueWhite), 2, 0, image.Width, i);
                }
        }

        public void PostMouseEvent(PureWebMouseEventArgs mouseEvent)
        {
        }
       
        public void PostKeyEvent(PureWebKeyboardEventArgs keyEvent)
        {
        }

        public void RenderDeferred()
        {
            m_remoteRenderer.RenderViewDeferred(m_viewName);
        }

    }
}
