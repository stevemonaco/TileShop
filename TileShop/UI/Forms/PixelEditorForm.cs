﻿using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using TileShop.Core;
using TileShop.ExtensionMethods;

namespace TileShop
{
    public enum PixelDrawState { PencilState = 0, PickerState };

    public partial class PixelEditorForm : EditorDockContent
    {
        Arranger EditArranger = null;
        RenderManager rm = null;
        Size PixelMargin;
        Rectangle DisplayRect;
        TextureBrush TransparentBrush;

        int Zoom = 24;
        PixelDrawState DrawState = PixelDrawState.PencilState;
        bool RenderTransparency = true;
        bool PencilDragActive = false;

        /// <summary>
        /// Gets whether the form has closed
        /// </summary>
        public bool IsClosed { get; set; }

        public PixelEditorForm()
        {
            InitializeComponent();

            typeof(Panel).InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, PixelPanel, new object[] { true }); // Enable double buffering of the PixelPanel

            PixelMargin = new Size(12, 4);
            DisplayRect = new Rectangle(new Point(PixelMargin.Width, PixelMargin.Height), new Size(0, 0));
            TransparentBrush = new TextureBrush(Properties.Resources.TransparentBrushPattern);

            SetDrawState(PixelDrawState.PencilState);
            ContentSourceName = "Pixel Editor";
            SwatchControl.Hide();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.G) // Toggle Gridlines
            {
                GridlinesButton.Checked ^= true;
                PixelPanel.Invalidate();
                return true;
            }
            else if (keyData == Keys.Z) // Zoom in
            {
                Zoom++;
                ResizePanels();
                return true;
            }
            else if (keyData == Keys.X) // Zoom out
            {
                if (Zoom > 1)
                {
                    Zoom--;
                    ResizePanels();
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        public override bool ReloadContent()
        {
            if (EditArranger is null)
                return false;

            DisplayRect.Size = new Size(EditArranger.ArrangerPixelSize.Width * Zoom, EditArranger.ArrangerPixelSize.Height * Zoom);
            PixelPanel.Height = EditArranger.ArrangerPixelSize.Height * Zoom + PixelMargin.Height;
            rm.Invalidate();
            PixelPanel.Invalidate();

            string palName = EditArranger.GetElement(0, 0).PaletteKey;
            string formatName = EditArranger.GetElement(0, 0).FormatName;
            SwatchControl.ShowPalette(ResourceManager.GetResource<Palette>(palName), 1 << ResourceManager.GetGraphicsFormat(formatName).ColorDepth);
            SwatchControl.SelectedIndex = 0;
            SwatchControl.Show();
            SwatchControl.Invalidate();

            ContainsModifiedContent = false;
            return true;
        }

        public void ClearArranger()
        {
            EditArranger = null;
            rm = null;
            DisplayRect = new Rectangle(new Point(PixelMargin.Width, PixelMargin.Height), new Size(0, 0));

            SwatchControl.Hide();

            ContentSourceName = "Pixel Editor";
        }

        public override bool SaveContent()
        {
            if (EditArranger is null)
                return false;

            rm.SaveImage(EditArranger);
            ContainsModifiedContent = false;
            return true;
        }

        public override bool RefreshContent()
        {
            return false;
        }

        public void SetEditArranger(Arranger arr)
        {
            EditArranger = arr;
            rm = new RenderManager();

            if(EditArranger is null)
            {
                ClearArranger();
                PixelPanel.Invalidate();
                return;
            }

            ResizePanels();

            string palName = EditArranger.GetElement(0, 0).PaletteKey;
            string formatName = EditArranger.GetElement(0, 0).FormatName;
            SwatchControl.ShowPalette(ResourceManager.GetResource<Palette>(palName), 1 << ResourceManager.GetGraphicsFormat(formatName).ColorDepth);
            SwatchControl.SelectedIndex = 0;
            SwatchControl.Show();
        }

        private void ResizePanels()
        {
            DisplayRect.Size = new Size(EditArranger.ArrangerPixelSize.Width * Zoom, EditArranger.ArrangerPixelSize.Height * Zoom);
            PixelPanel.Height = EditArranger.ArrangerPixelSize.Height * Zoom + PixelMargin.Height;
            PixelPanel.Invalidate();
            Invalidate();
        }

        private void PixelPanel_Paint(object sender, PaintEventArgs e)
        {
            if (EditArranger is null)
            {
                base.OnPaint(e);
                return;
            }

            rm.Render(EditArranger);

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            if(RenderTransparency)
                e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            else
                e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;

            Rectangle src = new Rectangle(0, 0, EditArranger.ArrangerPixelSize.Width, EditArranger.ArrangerPixelSize.Height);
            Rectangle dest = new Rectangle(PixelMargin.Width, PixelMargin.Height,
                EditArranger.ArrangerPixelSize.Width * Zoom, EditArranger.ArrangerPixelSize.Height * Zoom);

            e.Graphics.FillRectangle(TransparentBrush, DisplayRect);
            e.Graphics.DrawImage(rm.Image, dest, src, GraphicsUnit.Pixel);

            if(GridlinesButton.Checked)
                DrawPixelGridlines(e.Graphics);
        }

        private void DrawPixelGridlines(Graphics g)
        {
            int dx = Zoom;
            int dy = Zoom;

            for (int y = PixelMargin.Height; y < EditArranger.ArrangerPixelSize.Height * Zoom + PixelMargin.Height; y += dy) // Draw horizontal lines
                g.DrawLine(Pens.White, PixelMargin.Width, y, EditArranger.ArrangerPixelSize.Width * Zoom + PixelMargin.Width, y);

            for (int x = PixelMargin.Width; x < EditArranger.ArrangerPixelSize.Width * Zoom + PixelMargin.Width; x += dx) // Draw vertical lines
                g.DrawLine(Pens.White, x, PixelMargin.Height, x, EditArranger.ArrangerPixelSize.Height * Zoom + PixelMargin.Height);
        }

        private void SetDrawState(PixelDrawState state)
        {
            // Reset buttons
            PencilButton.Checked = false;
            PickerButton.Checked = false;

            DrawState = state;

            switch(state)
            {
                case PixelDrawState.PencilState:
                    PencilButton.Checked = true;
                    PixelPanel.Cursor = ResourceManager.GetCursor("PencilCursor");
                    break;
                case PixelDrawState.PickerState:
                    PickerButton.Checked = true;
                    PixelPanel.Cursor = ResourceManager.GetCursor("PickerCursor");
                    break;
            }
        }

        private void PencilButton_Click(object sender, EventArgs e)
        {
            SetDrawState(PixelDrawState.PencilState);
        }

        private void PickerButton_Click(object sender, EventArgs e)
        {
            SetDrawState(PixelDrawState.PickerState);
        }

        private void GridlinesButton_Click(object sender, EventArgs e)
        {
            PixelPanel.Invalidate();
        }

        private void PixelPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (!DisplayRect.Contains(e.Location) || EditArranger is null)
                return;

            Point unscaledLoc = new Point(); // Location in pixels
            unscaledLoc.X = (e.Location.X - PixelMargin.Width) / Zoom;
            unscaledLoc.Y = (e.Location.Y - PixelMargin.Height) / Zoom;

            if(DrawState == PixelDrawState.PencilState)
            {
                Palette pal = ResourceManager.GetResource<Palette>(EditArranger.GetElement(0, 0).PaletteKey);
                SetPixel(unscaledLoc.X, unscaledLoc.Y, Color.FromArgb((int)pal[SwatchControl.SelectedIndex].Color));

                PencilDragActive = true;
            }
            else if(DrawState == PixelDrawState.PickerState)
            {
                Color c = rm.GetPixel(unscaledLoc.X, unscaledLoc.Y);
                Palette pal = ResourceManager.GetResource<Palette>(EditArranger.GetElement(0, 0).PaletteKey);
                SwatchControl.SelectedIndex = pal.GetIndexByNativeColor(c.ToNativeColor(), true);
            }
        }

        private void PixelPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (DisplayRect.Contains(e.Location) && EditArranger != null) // TODO: Optimize so the cursor isn't set on every move
            {
                if (DrawState == PixelDrawState.PencilState)
                {
                    PixelPanel.Cursor = ResourceManager.GetCursor("PencilCursor");
                    if(PencilDragActive)
                    {
                        Point unscaledLoc = new Point(); // Location in pixels
                        unscaledLoc.X = (e.Location.X - PixelMargin.Width) / Zoom;
                        unscaledLoc.Y = (e.Location.Y - PixelMargin.Height) / Zoom;

                        Palette pal = ResourceManager.GetResource<Palette>(EditArranger.GetElement(0, 0).PaletteKey);
                        SetPixel(unscaledLoc.X, unscaledLoc.Y, Color.FromArgb((int)pal[SwatchControl.SelectedIndex].Color));
                    }

                }
                else if (DrawState == PixelDrawState.PickerState)
                    PixelPanel.Cursor = ResourceManager.GetCursor("PickerCursor");
            }
            else
                PixelPanel.Cursor = Cursors.Arrow;
        }

        private void SetPixel(int X, int Y, Color color)
        {
            Palette pal = ResourceManager.GetResource<Palette>(EditArranger.GetElement(0, 0).PaletteKey);
            Color col = pal[SwatchControl.SelectedIndex].ToColor();
            if (rm.GetPixel(X, Y) != col)
            {
                rm.SetPixel(X, Y, Color.FromArgb((int)pal[SwatchControl.SelectedIndex].Color));
                ContainsModifiedContent = true;
                OnContentModified(EventArgs.Empty);
                PixelPanel.Invalidate();
            }
        }

        private void PixelEditorForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            IsClosed = true;
        }

        private void ReloadButton_Click(object sender, EventArgs e)
        {
            ReloadContent();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            SaveContent();
        }

        private void PixelPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (PencilDragActive)
                PencilDragActive = false;
        }
    }
}
