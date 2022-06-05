using System;
using System.Drawing;
using System.Windows.Forms;

namespace ACEPatcher.Controls
{
    public class DarkMenuRenderer : ToolStripRenderer
    {
        #region Initialisation Region

        protected override void Initialize(ToolStrip toolStrip)
        {
            base.Initialize(toolStrip);

            toolStrip.BackColor = Color.FromArgb(27, 27, 28);
            toolStrip.ForeColor = Color.FromArgb(220, 220, 220);
        }

        protected override void InitializeItem(ToolStripItem item)
        {
            base.InitializeItem(item);

            item.BackColor = Color.FromArgb(27, 27, 28);
            item.ForeColor = Color.FromArgb(220, 220, 220);

            if (item.GetType() == typeof(ToolStripSeparator))
            {
                item.Margin = new Padding(0, 0, 0, 0);
            }
        }

        #endregion

        #region Render Region

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Color.FromArgb(27, 27, 28)))
            {
                g.FillRectangle(b, e.AffectedBounds);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;

            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);

            using (var p = new Pen(Color.FromArgb(51, 51, 51)))
            {
                g.DrawRectangle(p, rect);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;

            var rect = new Rectangle(e.ImageRectangle.Left - 2, e.ImageRectangle.Top + 2,
                                         e.ImageRectangle.Width + 4, e.ImageRectangle.Height + 4);

            using (var b = new SolidBrush(Color.FromArgb(62, 62, 64)))
            {
                g.FillRectangle(b, rect);
            }

            using (var p = new Pen(Color.FromArgb(0, 0, 56)))
            {
                var modRect = new Rectangle(rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
                g.DrawRectangle(p, modRect);
            }

        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            /*var g = e.Graphics;

            var rect = new Rectangle(1, 3, e.Item.Width, 1);

            using (var b = new SolidBrush(Color.FromArgb(81, 81, 81)))
            {
                g.FillRectangle(b, rect);
            }*/
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(220, 220, 220);
            e.ArrowRectangle = new Rectangle(new Point(e.ArrowRectangle.Left, e.ArrowRectangle.Top - 1), e.ArrowRectangle.Size);

            base.OnRenderArrow(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;

            e.Item.ForeColor = e.Item.Enabled ? Color.FromArgb(220, 220, 220) : Color.FromArgb(153, 153, 153);

            if (e.Item.Enabled)
            {

                var bgColor = e.Item.Selected ? Color.FromArgb(62, 62, 64) : e.Item.BackColor;

                // Normal item
                var rect = new Rectangle(0, 0, e.Item.Width, e.Item.Height);

                using (var b = new SolidBrush(bgColor))
                {
                    g.FillRectangle(b, rect);
                }

                // Header item on open menu
                if (e.Item.GetType() == typeof(ToolStripMenuItem))
                {
                    if (((ToolStripMenuItem)e.Item).DropDown.Visible && e.Item.IsOnDropDown == false)
                    {
                        using (var b = new SolidBrush(Color.FromArgb(27, 27, 28)))
                        {
                            g.FillRectangle(b, rect);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
