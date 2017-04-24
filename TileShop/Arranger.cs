﻿using System;
using System.Collections.Generic;
using System.Drawing;

namespace TileShop
{
    /// <summary>
    /// Arranger mode for the arranger.
    /// Sequential arrangers are for sequential file access
    /// Scattered arrangers are for accessing many files and file offsets in a single arranger
    /// Memory arrangers are used as a scratchpad
    /// </summary>
    public enum ArrangerMode { SequentialArranger = 0, ScatteredArranger, MemoryArranger };

    /// <summary>
    /// Move operations for sequential arrangers
    /// </summary>
    public enum ArrangerMoveType { ByteDown = 0, ByteUp, RowDown, RowUp, ColRight, ColLeft, PageDown, PageUp, Home, End, Absolute };

    public class Arranger
    {
        // General Arranger variables

        /// <summary>
        /// Gets individual elements from the arranger
        /// </summary>
        public ArrangerElement[,] ElementList { get; private set; }

        /// <summary>
        /// Gets the Size of the entire arranger in elements
        /// </summary>
        public Size ArrangerElementSize { get; private set; }

        /// <summary>
        /// Gets the Size of the entire arranger in unzoomed pixels
        /// </summary>
        public Size ArrangerPixelSize { get; private set; }

        /// <summary>
        /// Gets the Size of an individual element in unzoomed pixels
        /// Only valid for sequential arranger
        /// </summary>
        public Size ElementPixelSize { get; private set; }

        /// <summary>
        /// Gets the Mode of the arranger
        /// </summary>
        public ArrangerMode Mode { get; private set; }

        /// <summary>
        /// Gets or sets the name of the arranger
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets whether the arranger mode is sequential or not
        /// </summary>
        public bool IsSequential { get; private set; }

        /// <summary>
        /// Gets the filesize of the file associated with a sequential arranger
        /// </summary>
        public long FileSize
        {
            get
            {
                if (Mode != ArrangerMode.SequentialArranger)
                    throw new InvalidOperationException("Cannot retrieve the FileSize for an arranger that is not a SequentialArranger");
                else
                    return fileSize;
            }
            private set { fileSize = value; }
        }
        private long fileSize;

        /// <summary>
        /// Number of bits required to be read from file sequentially
        /// </summary>
        public long ArrangerBitSize
        {
            get
            {
                if (Mode != ArrangerMode.SequentialArranger)
                    throw new InvalidOperationException("Cannot retrieve the ArrangerBitSize for an arranger that is not a SequentialArranger");
                else
                    return arrangerBitSize;
            }
            private set { arrangerBitSize = value; }
        }
        private long arrangerBitSize;

        // Scattered Arranger variables
        //public string DefaultCodec { get; private set; }
        //public string DefaultFile { get; private set; }
        //public string DefaultPalette { get; private set; }

        private Arranger()
        {
        }

        /*void NewBlankArranger(int ElementsX, int ElementsY, GraphicsFormat format)
        {
            if (format == null)
                throw new ArgumentNullException();

            ElementList = new ArrangerElement[ElementsX, ElementsY];

            int x = 0;
            int y = 0;

            for (int i = 0; i < ElementsY; i++)
            {
                x = 0;
                for (int j = 0; j < ElementsX; j++)
                {
                    ArrangerElement el = new ArrangerElement();
                    // Filename
                    el.FileOffset = 0;
                    el.X1 = x;
                    el.Y1 = y;
                    el.X2 = x + format.Width - 1;
                    el.Y2 = y + format.Height - 1;
                    el.Format = format.Name;
                    el.Palette = "Default";
                    ElementList[j, i] = el;

                    x += format.Width;
                }
                y += format.Height;
            }

            ArrangerElement LastElem = ElementList[ElementsX - 1, ElementsY - 1];
            ArrangerPixelSize = new Size(LastElem.X2, LastElem.Y2);
        }*/

        /// <summary>
        /// Creates a new instance of an arranger in sequential reading mode
        /// </summary>
        /// <param name="ElementsX">Number of elements in the horizontal direction</param>
        /// <param name="ElementsY">Number of elements in the vertical direction</param>
        /// <param name="Filename">Filename of a file already loaded into the FileManager</param>
        /// <param name="format">Graphics format of the element</param>
        /// <returns></returns>
        public static Arranger NewSequentialArranger(int ElementsX, int ElementsY, string Filename, GraphicsFormat format)
        {
            Arranger arr = new Arranger()
            {
                Mode = ArrangerMode.SequentialArranger,
                IsSequential = true,
                FileSize = FileManager.Instance.GetFileStream(Filename).Length,
                Name = Filename
            };
            arr.ResizeSequentialArranger(ElementsX, ElementsY, Filename, format);

            return arr;
        }

        private FileBitAddress ResizeSequentialArranger(int ElementsX, int ElementsY, string Filename, GraphicsFormat format)
        {
            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            FileBitAddress address = GetInitialSequentialFileAddress();

            ElementList = new ArrangerElement[ElementsX, ElementsY];
            ArrangerBitSize = ElementsX * ElementsY * format.Size();
            int x = 0;
            int y = 0;

            for (int i = 0; i < ElementsY; i++)
            {
                x = 0;
                for (int j = 0; j < ElementsX; j++)
                {
                    ArrangerElement el = new ArrangerElement()
                    {
                        FileAddress = address,
                        X1 = x,
                        Y1 = y,
                        X2 = x + format.Width - 1,
                        Y2 = y + format.Height - 1,
                        Width = format.Width,
                        Height = format.Height,
                        FileName = Filename,
                        FormatName = format.Name,
                        PaletteName = "Default"
                    };
                    if (el.TileData.Count == 0 || el.MergedData == null)
                        el.AllocateBuffers();

                    ElementList[j, i] = el;

                    if (format.ImageType == "tiled")
                        address += format.Size();
                    else // Linear
                        address += (format.Width + format.RowStride) * format.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise

                    x += format.Width;
                }
                y += format.Height;
            }

            ArrangerElement LastElem = ElementList[ElementsX - 1, ElementsY - 1];
            ArrangerPixelSize = new Size(LastElem.X2 + 1, LastElem.Y2 + 1);
            ArrangerElementSize = new Size(ElementsX, ElementsY);
            ElementPixelSize = new Size(format.Width, format.Height);

            address = GetInitialSequentialFileAddress();
            address = Move(address);

            return address;
        }

        /// <summary>
        /// Resizes a sequential arranger to the specified number of elements and repopulates element data
        /// </summary>
        /// <param name="ElementsX"></param>
        /// <param name="ElementsY"></param>
        /// <returns></returns>
        public FileBitAddress ResizeSequentialArranger(int ElementsX, int ElementsY)
        {
            if (Mode != ArrangerMode.SequentialArranger)
                throw new ArgumentException();

            return ResizeSequentialArranger(ElementsX, ElementsY, ElementList[0, 0].FileName, FileManager.Instance.GetGraphicsFormat(ElementList[0, 0].FormatName));
        }

        public static Arranger NewScatteredArranger(int ElementsX, int ElementsY, int Width, int Height)
        {
            Arranger arr = new Arranger();
            arr.Mode = ArrangerMode.ScatteredArranger;
            arr.IsSequential = false;

            arr.ElementList = new ArrangerElement[ElementsX, ElementsY];

            int x = 0;
            int y = 0;

            for (int i = 0; i < ElementsY; i++)
            {
                x = 0;
                for (int j = 0; j < ElementsX; j++)
                {
                    ArrangerElement el = new ArrangerElement()
                    {
                        FileName = "",
                        FileAddress = new FileBitAddress(0, 0),
                        X1 = x,
                        Y1 = y,
                        X2 = x + Width - 1,
                        Y2 = y + Height - 1,
                        Width = Width,
                        Height = Height,
                        FormatName = "",
                        PaletteName = "Default"
                    };
                    arr.ElementList[j, i] = el;

                    x += Width;
                }
                y += Height;
            }

            ArrangerElement LastElem = arr.ElementList[ElementsX - 1, ElementsY - 1];
            arr.ArrangerPixelSize = new Size(LastElem.X2 + 1, LastElem.Y2 + 1);
            arr.ArrangerElementSize = new Size(ElementsX, ElementsY);
            arr.ElementPixelSize = new Size(Width, Height);

            return arr;
        }

        // Creates a new arranger based on a selection
        public Arranger CreateSubArranger(string ArrangerName, int ArrangerPosX, int ArrangerPosY, int ElementsX, int ElementsY)
        {
            Arranger arr = new Arranger()
            {
                Mode = ArrangerMode.ScatteredArranger, // Default to scattered arranger due to selections not being the full width of the parent arranger
                Name = ArrangerName,
                ElementList = new ArrangerElement[ElementsX, ElementsY],
                ArrangerElementSize = new Size(ElementsX, ElementsY),
                ElementPixelSize = ElementPixelSize,
                ArrangerPixelSize = new Size(ElementPixelSize.Width * ElementsX, ElementPixelSize.Height * ElementsY)
            };

            if (Mode == ArrangerMode.SequentialArranger)
            {
                arr.IsSequential = IsSequential;
                arr.FileSize = FileSize;
            }

            for (int srcy = ArrangerPosY, desty = 0; srcy < ArrangerPosY + ElementsY; srcy++, desty++)
            {
                for (int srcx = ArrangerPosX, destx = 0; srcx < ArrangerPosX + ElementsX; srcx++, destx++)
                {
                    ArrangerElement el = GetElement(srcx, srcy).Clone();
                    el.X1 = destx * arr.ElementPixelSize.Width;
                    el.X2 = el.X1 + arr.ElementPixelSize.Width - 1;
                    el.Y1 = desty * arr.ElementPixelSize.Height;
                    el.Y2 = el.Y1 + arr.ElementPixelSize.Height - 1;
                    arr.SetElement(el, destx, desty);
                }
            }

            return arr;
        }

        /// <summary>
        /// Sets element to a position in the element array using a shallow copy
        /// </summary>
        /// <param name="element"></param>
        /// <param name="posx"></param>
        /// <param name="posy"></param>
        public void SetElement(ArrangerElement element, int posx, int posy)
        {
            if (ElementList == null)
                throw new ArgumentNullException();

            if (posx > ArrangerElementSize.Width || posy > ArrangerElementSize.Height)
                throw new ArgumentOutOfRangeException();

            ElementList[posx, posy] = element;
        }

        public ArrangerElement GetElement(int posx, int posy)
        {
            if (ElementList == null)
                throw new ArgumentNullException();

            return ElementList[posx, posy];
        }

        public Rectangle GetSelectionPixelRect(Rectangle r)
        {
            int x1 = r.Left;
            int x2 = r.Right;
            int y1 = r.Top;
            int y2 = r.Bottom;

            // Extend rectangle to include the entirety of partially selected tiles
            foreach(ArrangerElement el in ElementList)
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
            if (x2 >= ArrangerPixelSize.Width)
                x2 = ArrangerPixelSize.Width;
            if (y2 >= ArrangerPixelSize.Height)
                y2 = ArrangerPixelSize.Height;

            return new Rectangle(x1, y1, x2 - x1, y2 - y1);
        }

        /// <summary>
        /// Moves a sequential arranger and updates each element
        /// Will not move outside of the bounds of the underlying file
        /// </summary>
        /// <param name="MoveType">Type of move requested</param>
        /// <returns>Updated address of first element</returns>
        public FileBitAddress Move(ArrangerMoveType MoveType)
        {
            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            FileBitAddress address = ElementList[0, 0].FileAddress;
            FileBitAddress delta;

            switch (MoveType) // Calculate the new address based on the movement command. Negative and post-EOF addresses are handled after the switch
            {
                case ArrangerMoveType.ByteDown:
                    address += 8;
                    break;
                case ArrangerMoveType.ByteUp:
                    address -= 8;
                    break;
                case ArrangerMoveType.RowDown:
                    delta = ArrangerElementSize.Width * FileManager.Instance.GetGraphicsFormat(ElementList[0, 0].FormatName).Size();
                    address += delta;
                    break;
                case ArrangerMoveType.RowUp:
                    delta = ArrangerElementSize.Width * FileManager.Instance.GetGraphicsFormat(ElementList[0, 0].FormatName).Size();
                    address -= delta;
                    break;
                case ArrangerMoveType.ColRight:
                    delta = FileManager.Instance.GetGraphicsFormat(ElementList[0, 0].FormatName).Size();
                    address += delta;
                    break;
                case ArrangerMoveType.ColLeft:
                    delta = FileManager.Instance.GetGraphicsFormat(ElementList[0, 0].FormatName).Size();
                    address -= delta;
                    break;
                case ArrangerMoveType.PageDown:
                    delta = ArrangerElementSize.Width * FileManager.Instance.GetGraphicsFormat(ElementList[0, 0].FormatName).Size() * ArrangerElementSize.Height / 2;
                    address += delta;
                    break;
                case ArrangerMoveType.PageUp:
                    delta = ArrangerElementSize.Width * FileManager.Instance.GetGraphicsFormat(ElementList[0, 0].FormatName).Size() * ArrangerElementSize.Height / 2;
                    address -= delta;
                    break;
                case ArrangerMoveType.Home:
                    address = 0;
                    break;
                case ArrangerMoveType.End:
                    address = new FileBitAddress(FileSize * 8 - ArrangerBitSize);
                    break;
            }

            if (address + ArrangerBitSize > FileSize * 8) // Calculated address is past EOF (first)
                address = new FileBitAddress(FileSize * 8 - ArrangerBitSize);

            if (address < 0) // Calculated address is before start of file (second)
                address = 0;

            SetNewSequentialFileOffset(address);

            return address;
        }

        /// <summary>
        /// Moves the sequential arranger to the specified address
        /// If the arranger will overflow the file, then seek only to the furthest offset
        /// </summary>
        /// <param name="AbsoluteAddress">Specified address to move the arranger to</param>
        /// <returns></returns>
        FileBitAddress Move(FileBitAddress AbsoluteAddress)
        {
            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            FileBitAddress address;
            FileBitAddress testaddress = AbsoluteAddress + ArrangerBitSize; // Tests the bounds of the arranger vs the file size

            if (FileSize * 8 < ArrangerBitSize) // Arranger needs more bits than the entire file
                address = new FileBitAddress(0, 0);
            else if (testaddress.Bits() > FileSize * 8)
                address = new FileBitAddress(FileSize * 8 - ArrangerBitSize);
            else
                address = AbsoluteAddress;

            SetNewSequentialFileOffset(address);
            return address;
        }

        void SetNewSequentialFileOffset(FileBitAddress FileAddress)
        {
            if (ElementList == null)
                throw new NullReferenceException();

            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            for (int i = 0; i < ArrangerElementSize.Height; i++)
            {
                for (int j = 0; j < ArrangerElementSize.Width; j++)
                {
                    ElementList[j, i].FileAddress = FileAddress;
                    FileAddress += FileManager.Instance.GetGraphicsFormat(ElementList[j, i].FormatName).Size();
                }
            }
        }

        public FileBitAddress GetInitialSequentialFileAddress()
        {
            if (ElementList != null)
                return ElementList[0, 0].FileAddress;
            else
                return new FileBitAddress(0, 0);
        }

        public string GetSequentialGraphicsFormat()
        {
            if (ElementList == null)
                throw new NullReferenceException();

            return ElementList[0, 0].FormatName;
        }

        public bool SetGraphicsFormat(string Format)
        {
            if (ElementList == null)
                throw new NullReferenceException();

            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            FileBitAddress address = ElementList[0, 0].FileAddress;
            GraphicsFormat fmt = FileManager.Instance.GetGraphicsFormat(Format);

            ElementPixelSize = new Size(fmt.Width, fmt.Height);

            int elembitsize = fmt.Size();
            ArrangerBitSize = ArrangerElementSize.Width * ArrangerElementSize.Height * elembitsize;

            if (FileSize * 8 < address + ArrangerBitSize)
                address = new FileBitAddress(FileSize * 8 - ArrangerBitSize);

            for (int i = 0; i < ArrangerElementSize.Height; i++)
            {
                for (int j = 0; j < ArrangerElementSize.Width; j++)
                {
                    ElementList[j, i].FileAddress = address;
                    ElementList[j, i].FormatName = Format;
                    ElementList[j, i].Width = fmt.Width;
                    ElementList[j, i].Height = fmt.Height;
                    ElementList[j, i].X1 = j * fmt.Width;
                    ElementList[j, i].X2 = j * fmt.Width + (fmt.Width - 1);
                    ElementList[j, i].Y1 = i * fmt.Height;
                    ElementList[j, i].Y2 = i * fmt.Height + (fmt.Height - 1);
                    ElementList[j, i].AllocateBuffers();
                    address += elembitsize;
                }
            }

            ArrangerElement LastElem = ElementList[ArrangerElementSize.Width - 1, ArrangerElementSize.Height - 1];
            ArrangerPixelSize = new Size(LastElem.X2 + 1, LastElem.Y2 + 1);

            return true;
        }

        /// <summary>
        /// Creates a deep clone of the Arranger
        /// </summary>
        /// <returns></returns>
        public Arranger Clone()
        {
            Arranger arr = new Arranger()
            {
                ElementList = new ArrangerElement[ArrangerElementSize.Width, ArrangerElementSize.Height],
                ArrangerElementSize = ArrangerElementSize,
                ElementPixelSize = ElementPixelSize,
                ArrangerPixelSize = ArrangerPixelSize,
                Mode = Mode,
                Name = Name,
                IsSequential = IsSequential
            };

            for (int y = 0; y < ArrangerElementSize.Height; y++)
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                    arr.SetElement(ElementList[x, y], x, y);

            if (IsSequential)
            {
                arr.FileSize = fileSize;
                arr.ArrangerBitSize = arrangerBitSize;
            }

            return arr;
        }
    }

    /// <summary>
    /// Contains all necessary data to encode/decode a single element in the arranger
    /// </summary>
    public class ArrangerElement
    {
        public string FileName { get; set; }
        public FileBitAddress FileAddress { get; set; }
        //public long FileOffset { get; set; }
        public string FormatName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string PaletteName { get; set; }

        /// <summary>
        /// Left X-coordinate of the element
        /// </summary>
        public int X1 { get; set; } // Locations in unzoomed coordinates
        /// <summary>
        /// Top Y-coordinate of the element
        /// </summary>
        public int Y1 { get; set; }
        /// <summary>
        /// Right X-coordinate of the element, inclusive
        /// </summary>
        public int X2 { get; set; }
        /// Bottom Y-coordinate of the element, inclusive
        public int Y2 { get; set; }

        // Preallocated TileData buffers
        public List<byte[]> TileData = new List<byte[]>();
        public byte[] MergedData;

        /// <summary>
        /// Used to allocate internal buffers to hold graphical data specific to the element
        /// </summary>
        public void AllocateBuffers()
        {
            GraphicsFormat format = FileManager.Instance.GetGraphicsFormat(FormatName);
            TileData.Clear();
            for (int i = 0; i < format.ColorDepth; i++)
            {
                byte[] data = new byte[Width * Height];
                TileData.Add(data);
            }

            MergedData = new byte[Width * Height];
        }

        public ArrangerElement()
        {
            FileName = "";
            FileAddress = new FileBitAddress(0, 0);
            FormatName = "";
            Width = 0;
            Height = 0;
            PaletteName = "Default";
            X1 = 0;
            X2 = 0;
            Y1 = 0;
            Y2 = 0;
        }

        /// <summary>
        /// Creates a deep clone
        /// </summary>
        /// <returns></returns>
        public ArrangerElement Clone()
        {
            ArrangerElement el = new ArrangerElement()
            {
                FileName = FileName,
                FileAddress = FileAddress,
                FormatName = FormatName,
                Width = Width,
                Height = Height,
                PaletteName = PaletteName,
                X1 = X1,
                Y1 = Y1,
                X2 = X2,
                Y2 = Y2
            };

            // Copy MergedData
            el.AllocateBuffers();
            for (int i = 0; i < MergedData.Length; i++)
                el.MergedData[i] = MergedData[i];

            // Copy TileData
            GraphicsFormat format = FileManager.Instance.GetGraphicsFormat(FormatName);
            for (int i = 0; i < TileData.Count; i++)
                for (int j = 0; j < TileData[i].Length; j++)
                    el.TileData[i][j] = TileData[i][j];

            return el;
        }
    }
}
