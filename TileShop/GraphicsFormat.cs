﻿using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TileShop
{
    /// <summary>
    /// Defines image assembly features
    /// </summary>
    public class ImageProperty
    {
        public int ColorDepth;
        public bool RowInterlace;

        /// <summary>
        /// Original placement pattern of pixels as specified by the codec
        /// </summary>
        public int[] RowPixelPattern
        {
            get { return rowPixelPattern; }
            private set { rowPixelPattern = value; }
        }
        private int[] rowPixelPattern;

        /// <summary>
        /// Placement pattern of pixels extended to the width of the element
        /// </summary>
        public int[] RowExtendedPixelPattern;

        public ImageProperty()
        {

        }

        public ImageProperty(int colorDepth, bool rowInterlace, int[] rowPixelPattern)
        {
            ColorDepth = colorDepth;
            RowInterlace = rowInterlace;
            RowPixelPattern = rowPixelPattern;
            RowExtendedPixelPattern = RowPixelPattern;
        }

        public void ExtendRowPattern(int Width)
        {
            if (RowExtendedPixelPattern.Length == Width) // Previously sized
                return;

            int cycles = (Width + RowPixelPattern.Length - 1) / RowPixelPattern.Length;

            RowExtendedPixelPattern = new int[Width];

            int index = 0; // Index into RowExtendedPixelPattern
            int pix = 0;   // Starting pixel location along scanline for current pattern

            for(int i = 0; i < cycles; i++)
            {
                for (int j = 0; j < RowPixelPattern.Length; j++, index++)
                {
                    if (index >= RowExtendedPixelPattern.Length)
                        break;

                    RowExtendedPixelPattern[index] = pix + RowPixelPattern[j];
                }

                pix += RowPixelPattern.Length;
            }
        }
    }

    /// <summary>
    /// GraphicsFormat describes properties relating to decoding/encoding a general graphics format
    /// </summary>
    public class GraphicsFormat
    {
        /// <summary>
        /// The name of the codec
        /// </summary>
        public string Name;

        /// <summary>
        /// Returns true if the codec requires fixed size elements or false if the codec operates on variable size elements
        /// </summary>
        public bool FixedSize { get; private set; }
        public string ImageType; // "tiled" or "linear"

        /// <summary>
        /// The color depth of the format
        /// </summary>
        public int ColorDepth;

        /// <summary>
        /// ColorType defines how pixel data is translated into color data (values: "indexed" or "direct")
        /// </summary>
        public string ColorType;

        /// <summary>
        /// Specifies how individual bits of each color are merged according to priority
        ///   Ex: [3, 2, 0, 1] implies the first bit read will merge into bit 3,
        ///   second bit read into bit 2, third bit read into bit 0, fourth bit read into bit 1
        /// </summary>
        public int[] MergePriority;

        /// <summary>
        /// Current Width of the graphics format
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Current Height of the graphics format
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Number of bits to skip after each row
        /// </summary>
        public int RowStride { get; private set; }

        /// <summary>
        /// Number of bits to skip after each element
        /// </summary>
        public int ElementStride { get; private set; }

        /// <summary>
        /// Size of an element in bits
        /// </summary>
        /// <returns></returns>
        public int Size() { return (Width + RowStride) * Height * ColorDepth + ElementStride; }

        public List<ImageProperty> ImagePropertyList = new List<ImageProperty>();

        // Processing Operations
        public bool HFlip;
        public bool VFlip;
        public bool Remap;

        // Pixel remap operations

        // Load a codec via XML format
        public bool LoadFromXml(string Filename)
        {
            ImagePropertyList.Clear();

            XElement xe = XElement.Load(Filename);

            Name = xe.Attribute("name").Value;

            var codecs = xe.Descendants("codec")
                .Select(e => new
                {
                    colortype = e.Descendants("colortype").First().Value,
                    colordepth = e.Descendants("colordepth").First().Value,
                    imagetype = e.Descendants("imagetype").First().Value,
                    height = e.Descendants("height").First().Value,
                    width = e.Descendants("width").First().Value,
                    fixedsize = e.Descendants("fixedsize").First().Value,
                    mergepriority = e.Descendants("mergepriority").First().Value
                });

            ColorType = codecs.First().colortype;
            ColorDepth = int.Parse(codecs.First().colordepth);
            ImageType = codecs.First().imagetype;
            Width = int.Parse(codecs.First().width);
            Height = int.Parse(codecs.First().height);
            FixedSize = bool.Parse(codecs.First().fixedsize);

            string mergestring = codecs.First().mergepriority;
            mergestring.Replace(" ", "");
            string[] mergeInts = mergestring.Split(',');

            if (mergeInts.Length != ColorDepth)
                throw new Exception("The number of entries in mergepriority does not match the colordepth");

            MergePriority = new int[ColorDepth];

            for (int i = 0; i < mergeInts.Length; i++)
                MergePriority[i] = int.Parse(mergeInts[i]);

            var images = xe.Descendants("image")
                         .Select(e => new
                         {
                             colordepth = e.Descendants("colordepth").First().Value,
                             rowinterlace = e.Descendants("rowinterlace").First().Value,
                             rowpixelpattern = e.Descendants("rowpixelpattern")
                         });

            foreach(var image in images)
            {
                int[] rowPixelPattern;

                if (image.rowpixelpattern.Count() > 0) // Parse rowpixelpattern
                {
                    string order = image.rowpixelpattern.First().Value;
                    order.Replace(" ", "");
                    string[] orderInts = order.Split(',');

                    rowPixelPattern = new int[orderInts.Length];

                    for (int i = 0; i < orderInts.Length; i++)
                        rowPixelPattern[i] = int.Parse(orderInts[i]);
                }
                else // Create a default rowpixelpattern in numeric order for the entire row
                {
                    rowPixelPattern = new int[Width];

                    for (int i = 0; i < Width; i++)
                        rowPixelPattern[i] = i;
                }

                ImageProperty ip = new ImageProperty(int.Parse(image.colordepth), bool.Parse(image.rowinterlace), rowPixelPattern);
                ip.ExtendRowPattern(Width);
                ImagePropertyList.Add(ip);
            }

            return true;
        }

        public void Resize(int width, int height)
        {
            Height = height;
            Width = width;

            for (int i = 0; i < ImagePropertyList.Count; i++)
                ImagePropertyList[i].ExtendRowPattern(Width);
        }
    }
}
