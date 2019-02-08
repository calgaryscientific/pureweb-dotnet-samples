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
using PureWeb;
using Image = PureWeb.Server.Image;

namespace DDxServiceCs
{
    class Canvas
    {
        Image m_image;

        public Canvas(ref Image image)
        {
            m_image = image;
        }

        public void Clear(PureWebColor color)
        {
            m_image.Graphics.Clear(SystemColor(color));
        }

        public void DrawHLine(PureWebColor color, int width, int x1, int x2, int y1)
        {
            Pen pen = new Pen(SystemColor(color), width);
            m_image.Graphics.DrawLine(pen, x1, y1, x2, y1);
        }

        public void DrawVLine(PureWebColor color, int width, int y1, int y2, int x1)
        {
            Pen pen = new Pen(SystemColor(color), width);
            m_image.Graphics.DrawLine(pen, x1, y1, x1, y2);
        }

        public void FillRect(PureWebColor color, int x1, int y1, int x2, int y2)
        {
            int width = Math.Abs(x2 - x1);
            int height = Math.Abs(y2 - y2);
            SolidBrush brush = new SolidBrush(SystemColor(color));
            m_image.Graphics.FillRectangle(brush, x1, y1, width, height);
        }

        public void FillCircle(PureWebColor color, int x, int y, int radius)
        {
            SolidBrush brush = new SolidBrush(SystemColor(color));
            m_image.Graphics.FillEllipse(brush, x - radius / 2, y - radius / 2, radius, radius);
        }

        public static Color SystemColor(PureWebColor color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
}
}
