using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LaptopKeyboardDoctor
{
    internal sealed class KeyboardMapControl : Control
    {
        private sealed class KeyVisual
        {
            public int VirtualKey;
            public string Label;
            public float Units;
        }

        private readonly List<List<KeyVisual>> _rows = new List<List<KeyVisual>>();
        private readonly Dictionary<int, KeyStateInfo> _states = new Dictionary<int, KeyStateInfo>();
        private readonly Dictionary<int, DiagnosticSeverity> _alerts = new Dictionary<int, DiagnosticSeverity>();

        public KeyboardMapControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(246, 248, 251);
            MinimumSize = new Size(700, 280);
            BuildLayout();
        }

        public void UpdateState(KeyStateInfo state)
        {
            _states[state.VirtualKey] = state;
            Invalidate();
        }

        public void MarkAlert(int virtualKey, DiagnosticSeverity severity)
        {
            _alerts[virtualKey] = severity;
            Invalidate();
        }

        public void ResetMap()
        {
            _states.Clear();
            _alerts.Clear();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            const float gap = 4f;
            float margin = 10f;
            float rowHeight = Math.Max(32f, (ClientSize.Height - margin * 2 - gap * (_rows.Count - 1)) / _rows.Count);

            for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                List<KeyVisual> row = _rows[rowIndex];
                float totalUnits = 0;
                foreach (KeyVisual key in row) totalUnits += key.Units;
                float unitWidth = (ClientSize.Width - margin * 2 - gap * (row.Count - 1)) / totalUnits;
                float x = margin;
                float y = margin + rowIndex * (rowHeight + gap);

                foreach (KeyVisual key in row)
                {
                    float width = key.Units * unitWidth;
                    RectangleF rect = new RectangleF(x, y, Math.Max(8f, width), rowHeight);
                    DrawKey(e.Graphics, key, rect);
                    x += width + gap;
                }
            }
        }

        private void DrawKey(Graphics graphics, KeyVisual key, RectangleF rect)
        {
            Color fill = Color.White;
            Color border = Color.FromArgb(194, 202, 214);
            Color text = Color.FromArgb(42, 50, 62);

            DiagnosticSeverity severity;
            KeyStateInfo state;
            if (_alerts.TryGetValue(key.VirtualKey, out severity))
            {
                fill = severity == DiagnosticSeverity.Critical ? Color.FromArgb(255, 214, 214) : Color.FromArgb(255, 238, 190);
                border = severity == DiagnosticSeverity.Critical ? Color.FromArgb(205, 45, 45) : Color.FromArgb(215, 139, 20);
            }
            else if (_states.TryGetValue(key.VirtualKey, out state))
            {
                if (state.IsDown)
                {
                    fill = Color.FromArgb(74, 144, 226);
                    border = Color.FromArgb(35, 98, 173);
                    text = Color.White;
                }
                else if (state.DownCount > 0)
                {
                    fill = Color.FromArgb(211, 244, 222);
                    border = Color.FromArgb(61, 158, 93);
                }
            }

            using (GraphicsPath path = RoundedRectangle(rect, 6f))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen borderPen = new Pen(border, 1.2f))
            {
                graphics.FillPath(fillBrush, path);
                graphics.DrawPath(borderPen, path);
            }

            float fontSize = rect.Width < 35 ? 7.5f : 8.5f;
            using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(text))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString(key.Label, font, textBrush, rect, format);
            }
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

        private KeyVisual K(int virtualKey, string label, float units)
        {
            return new KeyVisual { VirtualKey = virtualKey, Label = label, Units = units };
        }

        private void BuildLayout()
        {
            _rows.Add(new List<KeyVisual>
            {
                K(0x1B,"Esc",1.2f), K(0x70,"F1",1), K(0x71,"F2",1), K(0x72,"F3",1), K(0x73,"F4",1),
                K(0x74,"F5",1), K(0x75,"F6",1), K(0x76,"F7",1), K(0x77,"F8",1), K(0x78,"F9",1),
                K(0x79,"F10",1), K(0x7A,"F11",1), K(0x7B,"F12",1), K(0x2C,"PrtSc",1.2f), K(0x2E,"Del",1.1f)
            });
            _rows.Add(new List<KeyVisual>
            {
                K(0xC0,"`",1), K(0x31,"1",1), K(0x32,"2",1), K(0x33,"3",1), K(0x34,"4",1), K(0x35,"5",1),
                K(0x36,"6",1), K(0x37,"7",1), K(0x38,"8",1), K(0x39,"9",1), K(0x30,"0",1), K(0xBD,"-",1),
                K(0xBB,"=",1), K(0x08,"Backspace",2.1f)
            });
            _rows.Add(new List<KeyVisual>
            {
                K(0x09,"Tab",1.5f), K(0x51,"Q",1), K(0x57,"W",1), K(0x45,"E",1), K(0x52,"R",1), K(0x54,"T",1),
                K(0x59,"Y",1), K(0x55,"U",1), K(0x49,"I",1), K(0x4F,"O",1), K(0x50,"P",1), K(0xDB,"[",1),
                K(0xDD,"]",1), K(0xDC,"\\",1.6f)
            });
            _rows.Add(new List<KeyVisual>
            {
                K(0x14,"Caps",1.8f), K(0x41,"A",1), K(0x53,"S",1), K(0x44,"D",1), K(0x46,"F",1), K(0x47,"G",1),
                K(0x48,"H",1), K(0x4A,"J",1), K(0x4B,"K",1), K(0x4C,"L",1), K(0xBA,";",1), K(0xDE,"'",1),
                K(0x0D,"Enter",2.35f)
            });
            _rows.Add(new List<KeyVisual>
            {
                K(0xA0,"L Shift",2.35f), K(0x5A,"Z",1), K(0x58,"X",1), K(0x43,"C",1), K(0x56,"V",1), K(0x42,"B",1),
                K(0x4E,"N",1), K(0x4D,"M",1), K(0xBC,",",1), K(0xBE,".",1), K(0xBF,"/",1), K(0xA1,"R Shift",2.8f)
            });
            _rows.Add(new List<KeyVisual>
            {
                K(0xA2,"L Ctrl",1.4f), K(0x5B,"Win",1.2f), K(0xA4,"L Alt",1.3f), K(0x20,"Space",6.1f),
                K(0xA5,"R Alt",1.3f), K(0x5D,"Menu",1.2f), K(0xA3,"R Ctrl",1.4f), K(0x25,"←",1),
                K(0x26,"↑",1), K(0x28,"↓",1), K(0x27,"→",1)
            });
        }
    }
}
