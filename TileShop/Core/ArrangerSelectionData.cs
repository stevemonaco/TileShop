﻿using System;
using System.Drawing;

namespace TileShop
{
    // Class to store a selection of arranger data

    /// <summary>
    /// Handles the data associated with arranger selections
    /// </summary>
    public class ArrangerSelectionData
    {
        /// <summary>
        /// Key to the Arranger which holds the data to be copied
        /// </summary>
        public string ArrangerKey { get; private set; }

        /// <summary>
        /// Upper left location of the selection, in element units
        /// </summary>
        public Point Location { get; private set; }

        /// <summary>
        /// Size of selection in number of elements
        /// </summary>
        public Size SelectionSize { get; private set; }

        /// <summary>
        /// State of having a finalized selection
        /// </summary>
        public bool HasSelection { get; private set; }

        /// <summary>
        /// State of currently making or resizing a selection
        /// </summary>
        public bool InSelection { get; private set; }

        /// <summary>
        /// State of currently dragging a finalized selection area
        /// </summary>
        public bool InDragState { get; private set; }

        /// <summary>
        /// State of the selection being changed or not
        /// </summary>
        public bool SelectionChanged { get; private set; }

        /// <summary>
        /// Sets the zoom level to translate coordinates appropriately between original element coordinates and a resized element coordinate system
        /// Must be greater than or equal to 1
        /// </summary>
        public int Zoom
        {
            get => zoom;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException();
                zoom = value;
            }
        }
        private int zoom;

        /// <summary>
        /// Beginning selection point in zoomed coordinates
        /// </summary>
        public Point BeginPoint { get; private set; }

        /// <summary>
        /// Ending selection point in zoomed coordinates
        /// </summary>
        public Point EndPoint { get; private set; }

        /// <summary>
        /// Location of elements selected in the underlying arranger
        /// </summary>
        public Rectangle SelectedElements { get; private set; }

        /// <summary>
        /// Rectangle containing selected pixels in zoomed coordinates
        /// </summary>
        public Rectangle SelectedClientRect { get; private set; }

        /// <summary>
        /// List of selected elements
        /// Must call PopulateData before retrieving
        /// </summary>
        public ArrangerElement[,] ElementList { get; private set; }

        public ArrangerSelectionData(string arrangerKey)
        {
            ArrangerKey = arrangerKey;
            ClearSelection();
        }

        public void ClearSelection()
        {
            Location = new Point(0, 0);
            SelectionSize = new Size(0, 0);
            ElementList = null;
            HasSelection = false;
            InSelection = false;
            InDragState = false;
            SelectionChanged = false;
            SelectedElements = new Rectangle(0, 0, 0, 0);
            SelectedClientRect = new Rectangle(0, 0, 0, 0);
            BeginPoint = new Point(0, 0);
            EndPoint = new Point(0, 0);
        }

        /// <summary>
        /// Populates ElementList for retrieval
        /// </summary>
        /// <returns></returns>
        public bool PopulateData()
        {
            if (!HasSelection)
                return false;

            Arranger arr = FileManager.Instance.GetArranger(ArrangerKey);

            ElementList = new ArrangerElement[SelectionSize.Width, SelectionSize.Height];
            for (int ysrc = SelectedElements.Y, ydest = 0; ydest < SelectionSize.Height; ydest++, ysrc++)
            {
                for (int xsrc = SelectedElements.X, xdest = 0; xdest < SelectionSize.Width; xdest++, xsrc++)
                {
                    ElementList[xdest, ydest] = arr.GetElement(xsrc, ysrc).Clone();
                }
            }

            return true;
        }

        /// <summary>
        /// Retrieves an element from the selected elements
        /// </summary>
        /// <param name="ElementX"></param>
        /// <param name="ElementY"></param>
        /// <returns></returns>
        public ArrangerElement GetElement(int ElementX, int ElementY)
        {
            return ElementList[ElementX, ElementY];
        }

        /// <summary>
        /// Begins a new selection
        /// </summary>
        /// <param name="beginPoint"></param>
        /// <param name="endPoint"></param>
        public void BeginSelection(Point beginPoint, Point endPoint)
        {
            HasSelection = true;
            InSelection = true;
            SelectionChanged = true;
            BeginPoint = beginPoint;
            EndPoint = endPoint;
            CalculateSelectionData();
        }

        /// <summary>
        /// Updates an in-progress selection with a new end point
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns>True if the selection was changed</returns>
        public bool UpdateSelection(Point endPoint)
        {
            if (EndPoint != endPoint)
            {
                EndPoint = endPoint;
                CalculateSelectionData();
                return true;
            }
            else // No need to set as the two points are equal
                return false;
        }

        /// <summary>
        /// Ends the selection and moves the selection into a finalized state
        /// </summary>
        public void EndSelection()
        {
            InSelection = false;
            Arranger arr = FileManager.Instance.GetArranger(ArrangerKey);
            Rectangle testBounds = new Rectangle(new Point(0, 0), arr.ArrangerElementSize);

            if (!SelectedElements.IntersectsWith(testBounds)) // No intersection means no selection
            {
                ClearSelection();
                HasSelection = false;
            }
        }

        /// <summary>
        /// Begins the drag and drop state for the current finalized selection
        /// </summary>
        public void BeginDragDrop()
        {
            InDragState = true;
        }

        /// <summary>
        /// Ends the drag and drop state for the current finalized selection
        /// </summary>
        public void EndDragDrop()
        {
            InDragState = false;
        }


        /// <summary>
        /// Translates a point in zoomed coordinates to an element location in the underlying arranger
        /// </summary>
        /// <param name="Location">Point in zoomed coordinates</param>
        /// <returns>Element location</returns>
        public Point PointToElementLocation(Point Location)
        {
            Point unzoomed = new Point(Location.X / Zoom, Location.Y / Zoom);

            Arranger arr = FileManager.Instance.GetArranger(ArrangerKey);

            // Search list for element
            for (int y = 0; y < arr.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < arr.ArrangerElementSize.Width; x++)
                {
                    ArrangerElement el = arr.ElementGrid[x, y];
                    if (unzoomed.X >= el.X1 && unzoomed.X <= el.X2 && unzoomed.Y >= el.Y1 && unzoomed.Y <= el.Y2)
                        return new Point(x, y);
                }
            }

            throw new ArgumentOutOfRangeException("Location is outside of the range of all ArrangerElements in ElementList");
        }

        /// <summary>
        /// Calculates a resized selection rectangle in zoomed coordinates (to fully cover tiles that are half-moused over) and populates
        /// selected elements and selection size for retrieval
        /// </summary>
        private void CalculateSelectionData()
        {
            Rectangle zoomed = PointsToRectangle(BeginPoint, EndPoint); // Rectangle in zoomed coordinates
            Rectangle unzoomed = ViewerToArrangerRectangle(zoomed);
            Rectangle unzoomedfull = GetSelectionPixelRect(unzoomed);

            SelectedClientRect = new Rectangle(unzoomedfull.X * Zoom, unzoomedfull.Y * Zoom, unzoomedfull.Width * Zoom, unzoomedfull.Height * Zoom);

            Arranger arr = FileManager.Instance.GetArranger(ArrangerKey);

            SelectedElements = new Rectangle(unzoomedfull.X / arr.ElementPixelSize.Width, unzoomedfull.Y / arr.ElementPixelSize.Height,
                unzoomedfull.Width / arr.ElementPixelSize.Width, unzoomedfull.Height / arr.ElementPixelSize.Height);

            SelectionSize = new Size(SelectedElements.Width, SelectedElements.Height);
        }

        /// <summary>
        /// Resizes the current selection rect to encompass the entirety of selected elements (tiles)
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        private Rectangle GetSelectionPixelRect(Rectangle r)
        {
            Arranger arr = FileManager.Instance.GetArranger(ArrangerKey);

            int x1 = r.Left;
            int x2 = r.Right;
            int y1 = r.Top;
            int y2 = r.Bottom;

            // Extend rectangle to include the entirety of partially selected tiles
            foreach (ArrangerElement el in arr.ElementGrid)
            {
                if (x1 > el.X1 && x1 <= el.X2)
                    x1 = el.X1;
                if (y1 > el.Y1 && y1 <= el.Y2)
                    y1 = el.Y1;
                if (x2 < el.X2 && x2 >= el.X1)
                    x2 = el.X2;
                if (y2 < el.Y2 && y2 >= el.Y1)
                    y2 = el.Y2;
            }

            x2++; // Fix edges
            y2++;

            // Clamp selection rectangle to max bounds of the arranger
            if (x1 < 0)
                x1 = 0;
            if (y1 < 0)
                y1 = 0;
            if (x2 >= arr.ArrangerPixelSize.Width)
                x2 = arr.ArrangerPixelSize.Width;
            if (y2 >= arr.ArrangerPixelSize.Height)
                y2 = arr.ArrangerPixelSize.Height;

            return new Rectangle(x1, y1, x2 - x1, y2 - y1);
        }

        private Rectangle ViewerToArrangerRectangle(Rectangle ClientRect)
        {
            int pleft = ClientRect.Left / Zoom;
            int ptop = ClientRect.Top / Zoom;
            int pright = (int)(ClientRect.Left / (float)Zoom + (ClientRect.Right - ClientRect.Left) / (float)Zoom);
            int pbottom = (int)(ClientRect.Top / (float)Zoom + (ClientRect.Bottom - ClientRect.Top) / (float)Zoom);

            Rectangle UnzoomedRect = new Rectangle(pleft, ptop, pright - pleft, pbottom - ptop);

            return UnzoomedRect;
        }

        private Rectangle PointsToRectangle(Point beginPoint, Point endPoint)
        {
            int top = beginPoint.Y < endPoint.Y ? beginPoint.Y : endPoint.Y;
            int bottom = beginPoint.Y > endPoint.Y ? beginPoint.Y : endPoint.Y;
            int left = beginPoint.X < endPoint.X ? beginPoint.X : endPoint.X;
            int right = beginPoint.X > endPoint.X ? beginPoint.X : endPoint.X;

            Rectangle rect = new Rectangle(left, top, (right - left), (bottom - top));
            return rect;
        }
    }
}