﻿using System;

namespace TileShop.Core
{
    /// <summary>
    /// Manages the storage and conversion of foreign colors
    /// </summary>
    struct ForeignColor
    {
        /// <summary>
        /// Gets or sets the foreign color value
        /// </summary>
        public UInt32 Color { get; set; }

        /// <summary>
        /// Construct a ForeignColor
        /// </summary>
        /// <param name="color">Foreign Color ARGB value</param>
        ForeignColor(UInt32 color)
        {
            Color = color;
        }

        ForeignColor(byte A, byte R, byte G, byte B, ColorModel colorFormat)
        {
            switch (colorFormat)
            {
                // TODO: Validate color ranges
                case ColorModel.BGR15:
                    Color = R;
                    Color |= ((uint)G << 5);
                    Color |= ((uint)B << 10);
                    break;
                case ColorModel.ABGR16:
                    Color = R;
                    Color |= ((uint)G << 5);
                    Color |= ((uint)B << 10);
                    Color |= ((uint)A << 15);
                    break;
                case ColorModel.RGB15:
                    Color = B;
                    Color |= ((uint)G << 5);
                    Color |= ((uint)R << 10);
                    Color |= ((uint)A << 15);
                    break;
                default:
                    throw new ArgumentException("Unsupported PaletteColorFormat");
            }
        }

        #region Color Channel Helper Functions
        /// <summary>
        /// Gets the foreign alpha channel intensity
        /// </summary>
        /// <returns></returns>
        public byte A(ColorModel format)
        {
            switch (format)
            {
                case ColorModel.RGB15:
                case ColorModel.BGR15:
                    return 0;
                case ColorModel.ABGR16:
                    return (byte)((Color & 0x8000) >> 15);
                default:
                    throw new ArgumentException("Unsupported PaletteColorFormat " + format.ToString());
            }
        }

        /// <summary>
        /// Gets the foreign red channel intensity
        /// </summary>
        /// <returns></returns>
        public byte R(ColorModel format)
        {
            switch (format)
            {
                case ColorModel.BGR15:
                case ColorModel.ABGR16:
                    return (byte)(Color & 0x1F);
                case ColorModel.RGB15:
                    return (byte)((Color & 0x7C00) >> 10);
                default:
                    throw new ArgumentException("Unsupported PaletteColorFormat " + format.ToString());
            }
        }

        /// <summary>
        /// Gets the foreign green channel intensity
        /// </summary>
        /// <returns></returns>
        public byte G(ColorModel format)
        {
            switch (format)
            {
                case ColorModel.BGR15:
                case ColorModel.ABGR16:
                    return (byte)((Color & 0x3E0) >> 5);
                case ColorModel.RGB15:
                    return (byte)((Color & 0x3E0) >> 5);
                default:
                    throw new ArgumentException("Unsupported PaletteColorFormat " + format.ToString());
            }
        }

        /// <summary>
        /// Gets the foreign blue channel intensity
        /// </summary>
        /// <returns></returns>
        public byte B(ColorModel format)
        {
            switch (format)
            {
                case ColorModel.BGR15:
                case ColorModel.ABGR16:
                    return (byte)((Color & 0x7C00) >> 10);
                case ColorModel.RGB15:
                    return (byte)(Color & 0x1F);
                default:
                    throw new ArgumentException("Unsupported PaletteColorFormat " + format.ToString());
            }
        }

        /// <summary>
        /// Splits the foreign color into its foreign color components
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public (byte A, byte R, byte G, byte B) Split(ColorModel format) => (A(format), R(format), G(format), B(format));

        #endregion

        #region Foreign to Native Conversion Functions

        /// <summary>
        /// Splits the foreign color into its native color components
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public (byte A, byte R, byte G, byte B) SplitToNative(ColorModel format)
        {
            NativeColor nc = ToNativeColor(format);
            return nc.Split();
        }

        /// <summary>
        /// Converts into a NativeColor
        /// </summary>
        /// <param name="format">PaletteColorFormat of foreignColor</param>
        /// <returns>Local ARGB32 color value</returns>
        public NativeColor ToNativeColor(ColorModel format)
        {
            NativeColor nc = (NativeColor)0x00000000;
            (byte A, byte R, byte G, byte B) = Split(format);

            switch(format)
            {
                case ColorModel.BGR15:
                    nc.Color = ((uint)R << 19); // Red
                    nc.Color |= (uint)G << 11; // Green
                    nc.Color |= (uint)B << 3; // Blue
                    nc.Color |= 0xFF000000; // Alpha
                    break;
                case ColorModel.ABGR16:
                    nc.Color = (uint)R << 19; // Red
                    nc.Color |= (uint)G << 11; // Green
                    nc.Color |= (uint)B << 3; // Blue
                    nc.Color |= (uint)(A * 255) << 24; // Alpha
                    break;
                case ColorModel.RGB15:
                    nc.Color = (uint)R << 19; // Red
                    nc.Color |= (uint)G << 11; // Green
                    nc.Color |= (uint)B << 3; // Blue
                    nc.Color |= 0xFF000000; // Alpha
                    break;
                default:
                    throw new ArgumentException("Unsupported PaletteColorFormat " + format.ToString());
            }

            return nc;
        }

        #endregion
    }
}
