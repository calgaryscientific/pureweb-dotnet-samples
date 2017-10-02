//
// Copyright (c) 2012 Calgary Scientific Inc., all rights reserved.
//

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
