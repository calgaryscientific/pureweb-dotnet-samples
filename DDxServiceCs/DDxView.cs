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
using PureWeb.Xml;

namespace DDxServiceCs
{
    class DDxView : IRenderedView
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(DDxView));

        public String m_viewName;
        int m_colorIndex;
        Size m_clientSize;
        public static int m_colorCount ;
        private readonly IRemoteRenderer m_remoteRenderer;
        private long m_wheelDelta;

        public DDxView(int index, StateManager stateManager)
        {
            m_viewName = String.Format("/DDx/View{0}", index);
            m_remoteRenderer = stateManager.ViewManager;
            stateManager.ViewManager.RegisterView(m_viewName, this);
            m_colorIndex = m_colorCount++;
            m_wheelDelta = 0;
        }

        public virtual void SetClientSize(Size clientSize)
        {
            m_clientSize = clientSize;
        }

        public virtual Size GetActualSize()
        {
            return m_clientSize;
        }

        public bool RequiresRender()
        {
            return false;
        }

        public virtual void RenderView(RenderTarget target)
        {
            PureWebColor bgColor;
            var image = target.Image;
    
            switch(m_colorIndex % 6)
            {
            case 1:
                bgColor = PureWebColor.FromKnownColor(PureWebKnownColor.DarkBlue);
                break;
            case 2:
                bgColor = PureWebColor.FromKnownColor(PureWebKnownColor.DarkGreen);
                break;
            case 3:
                bgColor = PureWebColor.FromKnownColor(PureWebKnownColor.DarkMagenta);
                break;
            case 4:
                bgColor = PureWebColor.FromKnownColor(PureWebKnownColor.DarkCyan);
                break;
            case 5:
                bgColor = PureWebColor.FromKnownColor(PureWebKnownColor.DarkRed);
                break;
            default:
                bgColor = PureWebColor.FromKnownColor(PureWebKnownColor.Black);
                break;
            }

            Canvas canvas = new Canvas(ref image);
            canvas.Clear(bgColor);   

            bool gridOn = true;
            int margin = 0;
            int width = 4;
            int spacing = 100;

            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                state.SetValue(String.Format("/DDx/{0}/BackgroundColor", m_viewName), bgColor);
                gridOn = state.GetValueAs<bool>(DDx._DDx_GRID_ON);
                margin = state.GetValueAs<int>(DDx._DDx_GRID_MARGIN);
                width = state.GetValueAs<int>(DDx._DDx_GRID_LINEWIDTH);
                spacing = state.GetValueAs<int>(DDx._DDx_GRID_LINESPACING);
            }

            if (gridOn)
            {
                Int32 w = image.Width;
                Int32 h = image.Height;
                PureWebColor lineColor = PureWebColor.FromKnownColor(PureWebKnownColor.DarkSlateBlue);

                for (int x = spacing; x < w; x += spacing)
                {
                    canvas.DrawVLine(lineColor, width, margin, h-margin, x);
                }

                for (int y = spacing; y < h; y += spacing)
                {
                    canvas.DrawHLine(lineColor, width, margin, w-margin, y);
                }
            }
        }

        public void PostKeyEvent(PureWebKeyboardEventArgs keyEvent)
        {
            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                String path = String.Format("/DDx/{0}/KeyEvent", m_viewName);
                state.SetValue(path + "/Type", keyEvent.EventType);
                state.SetValue(path + "/KeyCode", keyEvent.KeyCode);
                state.SetValue(path + "/CharacterCode", keyEvent.CharacterCode);
                state.SetValue(path + "/Modifiers", keyEvent.Modifiers);
            }
        }

        public void PostMouseEvent(PureWebMouseEventArgs mouseEvent)
        {
            m_wheelDelta += (long)mouseEvent.Delta;

            using (XmlStateLock state = StateManager.Instance.LockAppState())
            {
                String path = String.Format("/DDx/{0}/MouseEvent", m_viewName);
                state.SetValue(path + "/Type", mouseEvent.EventType);
                state.SetValue(path + "/X", mouseEvent.X);
                state.SetValue(path + "/Y", mouseEvent.Y);
                state.SetValue(path + "/Buttons", mouseEvent.Buttons);
                state.SetValue(path + "/ChangedButton", mouseEvent.ChangedButton);
                state.SetValue(path + "/Modifiers", mouseEvent.Modifiers);
                state.SetValue(path + "/Delta", m_wheelDelta);
            }

            if (mouseEvent.EventType == MouseEventType.MouseDoubleClick &&
                mouseEvent.Buttons == MouseButtons.Left)
            {
                ++m_colorIndex;
                RenderDeferred();
            }
        }

        public void RenderDeferred()
        {
            m_remoteRenderer.RenderViewDeferred(m_viewName);
        }

        public int BkColorIndex
        {
            get { return m_colorIndex; }
            set { m_colorIndex = value; }
        }
    }
}
