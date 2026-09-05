using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LaptopKeyboardDoctor
{
    internal sealed class TouchpadMapControl : Control
    {
        private float _cursorX = 0.5f;
        private float _cursorY = 0.42f;
        private bool _leftDown;
        private bool _rightDown;
        private long _moveEvents;
        private long _scrollEvents;

        public TouchpadMapControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            MinimumSize = new Size(420, 300);
        }

        public void Apply(KeyEvidence evidence, PointerStateSnapshot snapshot)
        {
            if (evidence.DeltaX != 0 || evidence.DeltaY != 0)
            {
                _cursorX = Clamp(_cursorX + evidence.DeltaX / 900f, 0.04f, 0.96f);
                _cursorY = Clamp(_cursorY + evidence.DeltaY / 650f, 0.05f, 0.78f);
            }
            _leftDown = snapshot.LeftDown;
            _rightDown = snapshot.RightDown;
            _moveEvents = snapshot.MoveEvents;
            _scrollEvents = snapshot.ScrollEvents;
            Invalidate();
        }

        public void ResetMap()
        {
            _cursorX = 0.5f;
            _cursorY = 0.42f;
            _leftDown = false;
            _rightDown = false;
            _moveEvents = 0;
            _scrollEvents = 0;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF pad = new RectangleF(28, 18, Math.Max(120, ClientSize.Width - 56), Math.Max(150, ClientSize.Height - 52));
            float buttonTop = pad.Top + pad.Height * 0.80f;
            RectangleF left = new RectangleF(pad.Left, buttonTop, pad.Width / 2f, pad.Bottom - buttonTop);
            RectangleF right = new RectangleF(pad.Left + pad.Width / 2f, buttonTop, pad.Width / 2f, pad.Bottom - buttonTop);

            using (GraphicsPath outline = RoundedRectangle(pad, 18f))
            using (SolidBrush padBrush = new SolidBrush(Color.FromArgb(239, 243, 248)))
            using (Pen outlinePen = new Pen(Color.FromArgb(95, 112, 132), 2f))
            {
                e.Graphics.FillPath(padBrush, outline);
                e.Graphics.DrawPath(outlinePen, outline);
            }
            using (SolidBrush leftBrush = new SolidBrush(_leftDown ? Color.FromArgb(80, 154, 225) : Color.FromArgb(220, 228, 238)))
            using (SolidBrush rightBrush = new SolidBrush(_rightDown ? Color.FromArgb(80, 154, 225) : Color.FromArgb(220, 228, 238)))
            using (Pen separator = new Pen(Color.FromArgb(150, 163, 180), 1.2f))
            {
                e.Graphics.FillRectangle(leftBrush, left);
                e.Graphics.FillRectangle(rightBrush, right);
                e.Graphics.DrawLine(separator, pad.Left, buttonTop, pad.Right, buttonTop);
                e.Graphics.DrawLine(separator, pad.Left + pad.Width / 2f, buttonTop, pad.Left + pad.Width / 2f, pad.Bottom);
            }

            float px = pad.Left + _cursorX * pad.Width;
            float py = pad.Top + _cursorY * pad.Height;
            using (SolidBrush cursor = new SolidBrush(Color.FromArgb(35, 105, 185)))
            using (Pen halo = new Pen(Color.FromArgb(120, 35, 105, 185), 3f))
            {
                e.Graphics.DrawEllipse(halo, px - 11, py - 11, 22, 22);
                e.Graphics.FillEllipse(cursor, px - 5, py - 5, 10, 10);
            }

            using (Font labelFont = new Font("Segoe UI Semibold", 10F))
            using (Font smallFont = new Font("Segoe UI", 8.5F))
            using (SolidBrush text = new SolidBrush(Color.FromArgb(45, 56, 70)))
            using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                e.Graphics.DrawString("TRÁI", labelFont, _leftDown ? Brushes.White : text, left, center);
                e.Graphics.DrawString("PHẢI", labelFont, _rightDown ? Brushes.White : text, right, center);
                e.Graphics.DrawString("Di chuyển: " + _moveEvents + "  ·  Cuộn: " + _scrollEvents, smallFont, text, new RectangleF(pad.Left, pad.Bottom + 7, pad.Width, 24), center);
            }
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
        {
            float diameter = radius * 2f;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
