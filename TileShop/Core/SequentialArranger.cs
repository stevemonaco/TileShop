﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Linq;
using System.Linq;

namespace TileShop.Core
{
    public class SequentialArranger : Arranger
    {
        /// <summary>
        /// Gets the filesize of the file associated with a Sequential Arranger
        /// </summary>
        public long FileSize { get; private set; }

        /// <summary>
        /// Gets the filesize of the file associated with a Sequential Arranger
        /// </summary>
        public long FileAddress { get; private set; }

        /// <summary>
        /// Number of bits required to be read from file sequentially
        /// </summary>
        public long ArrangerBitSize { get; private set; }

        public SequentialArranger()
        {
        }

        public SequentialArranger(int arrangerWidth, int arrangerHeight, string dataFileKey, GraphicsFormat format)
        {
            DataFile df = ResourceManager.Instance.GetResource(dataFileKey) as DataFile;

            Mode = ArrangerMode.SequentialArranger;
            FileSize = df.Stream.Length;
            Name = df.Name;

            Resize(arrangerWidth, arrangerHeight, dataFileKey, format);
        }

        /// <summary>
        /// Resizes a Sequential Arranger to the specified number of Elements and repopulates Element data
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <returns></returns>
        public override void Resize(int arrangerWidth, int arrangerHeight)
        {
            if (Mode != ArrangerMode.SequentialArranger)
                throw new ArgumentException();

            Resize(arrangerWidth, arrangerHeight, ElementGrid[0, 0].DataFileKey, ResourceManager.Instance.GetGraphicsFormat(ElementGrid[0, 0].FormatName));
        }

        /// <summary>
        /// Resizes a Sequential Arranger with a new number of elements
        /// </summary>
        /// <param name="arrangerWidth">Width of Arranger in Elements</param>
        /// <param name="arrangerHeight">Height of Arranger in Elements</param>
        /// <param name="dataFileKey">DataFile key in FileManager</param>
        /// <param name="format">GraphicsFormat for encoding/decoding Elements</param>
        /// <returns></returns>
        private FileBitAddress Resize(int arrangerWidth, int arrangerHeight, string dataFileKey, GraphicsFormat format)
        {
            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            FileBitAddress address;

            if (ElementGrid == null) // New Arranger being resized
                address = 0;
            else
                address = GetInitialSequentialFileAddress();

            ElementGrid = new ArrangerElement[arrangerWidth, arrangerHeight];

            int x = 0;
            int y = 0;

            for (int i = 0; i < arrangerHeight; i++)
            {
                x = 0;
                for (int j = 0; j < arrangerWidth; j++)
                {
                    ArrangerElement el = new ArrangerElement()
                    {
                        FileAddress = address,
                        X1 = x,
                        Y1 = y,
                        X2 = x + ElementPixelSize.Width - 1,
                        Y2 = y + ElementPixelSize.Height - 1,
                        Width = ElementPixelSize.Width,
                        Height = ElementPixelSize.Height,
                        DataFileKey = dataFileKey,
                        FormatName = format.Name,
                    };
                    if (el.ElementData.Count == 0 || el.MergedData == null)
                        el.AllocateBuffers();

                    ElementGrid[j, i] = el;

                    if (format.Layout == ImageLayout.Tiled)
                        address += el.StorageSize;
                    else // Linear
                        address += (ElementPixelSize.Width + format.RowStride) * format.ColorDepth / 4; // TODO: Fix sequential arranger offsets to be bit-wise

                    x += ElementPixelSize.Width;
                }
                y += ElementPixelSize.Height;
            }

            ArrangerElement lastElem = ElementGrid[arrangerWidth - 1, arrangerHeight - 1];
            ArrangerPixelSize = new Size(lastElem.X2 + 1, lastElem.Y2 + 1);
            ArrangerElementSize = new Size(arrangerWidth, arrangerHeight);
            ElementPixelSize = new Size(ElementPixelSize.Width, ElementPixelSize.Height);

            ArrangerBitSize = arrangerWidth * arrangerHeight * lastElem.StorageSize;

            address = GetInitialSequentialFileAddress();
            address = this.Move(address);

            return address;
        }

        /// <summary>
        /// Gets the initial file address of a Sequential Arranger
        /// </summary>
        /// <returns></returns>
        public FileBitAddress GetInitialSequentialFileAddress()
        {
            if (ElementGrid == null)
                throw new NullReferenceException();

            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            return ElementGrid[0, 0].FileAddress;
        }

        /// <summary>
        /// Gets the GraphicsFormat name for a Sequential Arranger
        /// </summary>
        /// <returns></returns>
        public string GetSequentialGraphicsFormat()
        {
            if (ElementGrid == null)
                throw new NullReferenceException();

            return ElementGrid[0, 0].FormatName;
        }

        /// <summary>
        /// Sets the GraphicsFormat name and Element size for a Sequential Arranger
        /// </summary>
        /// <param name="Format">Name of the GraphicsFormat</param>
        /// <param name="ElementSize">Size of each Element in pixels</param>
        /// <returns></returns>
        public bool SetGraphicsFormat(string Format, Size ElementSize)
        {
            if (ElementGrid == null)
                throw new NullReferenceException();

            if (Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            FileBitAddress address = ElementGrid[0, 0].FileAddress;
            GraphicsFormat fmt = ResourceManager.Instance.GetGraphicsFormat(Format);

            ElementPixelSize = ElementSize;

            int elembitsize = fmt.StorageSize(ElementSize.Width, ElementSize.Height);
            ArrangerBitSize = ArrangerElementSize.Width * ArrangerElementSize.Height * elembitsize;

            if (FileSize * 8 < address + ArrangerBitSize)
                address = new FileBitAddress(FileSize * 8 - ArrangerBitSize);

            for (int i = 0; i < ArrangerElementSize.Height; i++)
            {
                for (int j = 0; j < ArrangerElementSize.Width; j++)
                {
                    ElementGrid[j, i].FileAddress = address;
                    ElementGrid[j, i].FormatName = Format;
                    ElementGrid[j, i].Width = ElementPixelSize.Width;
                    ElementGrid[j, i].Height = ElementPixelSize.Height;
                    ElementGrid[j, i].X1 = j * ElementPixelSize.Width;
                    ElementGrid[j, i].X2 = j * ElementPixelSize.Width + (ElementPixelSize.Width - 1);
                    ElementGrid[j, i].Y1 = i * ElementPixelSize.Height;
                    ElementGrid[j, i].Y2 = i * ElementPixelSize.Height + (ElementPixelSize.Height - 1);
                    ElementGrid[j, i].AllocateBuffers();
                    address += elembitsize;
                }
            }

            ArrangerElement LastElem = ElementGrid[ArrangerElementSize.Width - 1, ArrangerElementSize.Height - 1];
            ArrangerPixelSize = new Size(LastElem.X2 + 1, LastElem.Y2 + 1);

            return true;
        }

        #region ProjectResource implementation
        public override ProjectResourceBase Clone()
        {
            Arranger arr = new SequentialArranger()
            {
                ElementGrid = new ArrangerElement[ArrangerElementSize.Width, ArrangerElementSize.Height],
                ArrangerElementSize = ArrangerElementSize,
                ElementPixelSize = ElementPixelSize,
                ArrangerPixelSize = ArrangerPixelSize,
                Mode = Mode,
                Name = Name,
                FileSize = FileSize,
                ArrangerBitSize = ArrangerBitSize
            };

            for (int y = 0; y < ArrangerElementSize.Height; y++)
                for (int x = 0; x < ArrangerElementSize.Width; x++)
                    arr.SetElement(ElementGrid[x, y], x, y);

            return arr;
        }

        public override XElement Serialize()
        {
            throw new NotImplementedException();
        }

        public override bool Deserialize(XElement element)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
