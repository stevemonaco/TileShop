﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using ColorMine.ColorSpaces.Comparisons;
using ColorMine.ColorSpaces.Conversions;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TileShop.Core
{
    public enum ColorModel { RGB24 = 0, ARGB32, BGR15, ABGR16, RGB15, NES }

    /// <summary>
    /// Storage source of the palette
    /// ProjectFile palettes are stored in the XML project file
    /// </summary>
    public enum PaletteStorageSource { File = 0, ProjectFile }

    /// <summary>
    /// Palette manages the loading of palettes and colors from a variety of color formats
    /// Local colors are internally ARGB32
    /// Foreign colors are the same as the target system
    /// </summary>
    public class Palette : IProjectResource
    {
        #region Properties
        /// <summary>
        /// Identifying name of the palette
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// ColorModel of the palette
        /// </summary>
        public ColorModel ColorModel { get; private set; }

        /// <summary>
        /// DataFile key which contains the palette
        /// </summary>
        public string DataFileKey { get; private set; }

        /// <summary>
        /// Address of the palette within the file
        /// </summary>
        public FileBitAddress FileAddress { get; private set; }

        /// <summary>
        /// Number of color entries in the palette
        /// </summary>
        public int Entries { get; private set; }

        /// <summary>
        /// Specifies if the Palette has an alpha channel
        /// </summary>
        public bool HasAlpha { get; private set; }

        /// <summary>
        /// Specifies if the palette's 0-index is automatically treated as transparent
        /// </summary>
        public bool ZeroIndexTransparent { get; private set; }

        /// <summary>
        /// Specifies the palette's storage source
        /// </summary>
        public PaletteStorageSource StorageSource { get; private set; }

        /// <summary>
        /// Gets the internal palette containing native ARGB32 colors
        /// </summary>
        public NativeColor[] NativePalette { get; private set; }

        /// <summary>
        /// Gets the internal palette containing foreign colors
        /// </summary>
        public ForeignColor[] ForeignPalette { get; private set; }
        #endregion

        /// <summary>
        /// Constructs a new named Palette object
        /// </summary>
        /// <param name="PaletteName">Name of the palette</param>
        public Palette(string PaletteName)
        {
            Name = PaletteName;

            HasAlpha = false;
            Entries = 0;
            DataFileKey = null;
            ZeroIndexTransparent = true;
        }

        /// <summary>
        /// Renames a Palette to a new name
        /// </summary>
        /// <param name="name"></param>
        public void Rename(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Reloads the palette data from its underlying source
        /// </summary>
        /// <returns></returns>
        public bool Reload()
        {
            return LoadPalette(DataFileKey, FileAddress, ColorModel, ZeroIndexTransparent, Entries);
        }

        /// <summary>
        /// Sets the DataFile key source. Does not reload.
        /// </summary>
        /// <param name="fileKey">New DataFile key</param>
        /// <returns></returns>
        public void SetFileKey(string fileKey)
        {
            DataFileKey = fileKey;
        }

        /// <summary>
        /// Load a 256-entry palette from a new file in ARGB32 format
        /// </summary>
        /// <param name="filename">Path to the palette file on disk</param>
        /// <returns>Success value</returns>
        public bool LoadPalette(string filename)
        {
            int entrySize = 256;

            NativePalette = new NativeColor[entrySize];
            ForeignPalette = new ForeignColor[entrySize];

            BinaryReader br = new BinaryReader(File.OpenRead(filename));

            int numColors = (int)br.BaseStream.Length / 4;

            if (numColors != entrySize)
                return false;

            for (int idx = 0; idx < numColors; idx++)
            {
                uint color = br.ReadUInt32();
                NativeColor nativeColor = (NativeColor) (color | 0xFF000000); // Disable transparency
                NativePalette[idx] = nativeColor;
                ForeignPalette[idx] = (ForeignColor)color;
            }

            ColorModel = ColorModel.ARGB32;
            FileAddress = new FileBitAddress(0, 0);
            DataFileKey = filename;
            Entries = entrySize;
            StorageSource = PaletteStorageSource.File;

            return true;
        }

        /// <summary>
        /// Load palette from an unopened file
        /// </summary>
        /// <param name="fileName">Name of the file on disk</param>
        /// <param name="address">File address to the beginning of the palette</param>
        /// <param name="format">Color format of the palette</param>
        /// <param name="zeroIndexTransparent">If the 0-index of the palette is automatically transparent</param>
        /// <param name="numEntries">Number of entries the palette contains</param>
        /// <returns>Success value</returns>
        public bool LoadPaletteFromFileName(string fileName, FileBitAddress address, ColorModel format, bool zeroIndexTransparent, int numEntries)
        {
            if (numEntries > 256)
                throw new ArgumentException("Maximum palette size must be 256 entries or less");

            NativePalette = new NativeColor[numEntries];
            ForeignPalette = new ForeignColor[numEntries];

            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                int readSize;

                switch (format)
                {
                    case ColorModel.BGR15:
                    case ColorModel.RGB15:
                        readSize = 2;
                        HasAlpha = false;
                        break;
                    case ColorModel.ABGR16:
                        readSize = 2;
                        HasAlpha = true;
                        break;
                    case ColorModel.RGB24:
                        readSize = 3;
                        HasAlpha = false;
                        break;
                    case ColorModel.ARGB32:
                        readSize = 4;
                        HasAlpha = true;
                        break;
                    default:
                        throw new NotSupportedException("An unsupported palette format was attempted to be read");
                }

                byte[] tempPalette = fs.ReadUnshifted(address, readSize * 8 * numEntries, true);
                BitStream bs = BitStream.OpenRead(tempPalette, readSize * 8 * numEntries);

                for (int i = 0; i < numEntries; i++)
                {
                    uint foreignColor;

                    if (readSize == 1)
                        foreignColor = bs.ReadByte();
                    else if (readSize == 2)
                    {
                        foreignColor = bs.ReadByte();
                        foreignColor |= ((uint)bs.ReadByte()) << 8;
                    }
                    else if (readSize == 3)
                    {
                        foreignColor = bs.ReadByte();
                        foreignColor |= ((uint)bs.ReadByte()) << 8;
                        foreignColor |= ((uint)bs.ReadByte()) << 16;
                    }
                    else if (readSize == 4)
                    {
                        foreignColor = bs.ReadByte();
                        foreignColor |= ((uint)bs.ReadByte()) << 8;
                        foreignColor |= ((uint)bs.ReadByte()) << 16;
                        foreignColor |= ((uint)bs.ReadByte()) << 24;
                    }
                    else
                        throw new NotSupportedException("Palette formats with entry sizes larger than 4 bytes are not supported");

                    uint nativeColor = ForeignToNativeArgb((ForeignColor)foreignColor, format);
                    ForeignPalette[i] = (ForeignColor) foreignColor;
                    NativePalette[i] = (NativeColor) nativeColor;
                }
            }

            ZeroIndexTransparent = zeroIndexTransparent;

            if (ZeroIndexTransparent)
                NativePalette[0].Color &= 0x00FFFFFF;

            ColorModel = format;
            FileAddress = address;
            DataFileKey = fileName; // TODO: Remove this and set key externally or through method
            Entries = numEntries;
            StorageSource = PaletteStorageSource.File;

            return true;
        }

        /// <summary>
        /// Load palette from a previously opened file
        /// </summary>
        /// <param name="dataFileKey">DataFile key in FileManager</param>
        /// <param name="address">File address to the beginning of the palette</param>
        /// <param name="format">Color format of the palette</param>
        /// <param name="zeroIndexTransparent">If the 0-index of the palette is automatically transparent</param>
        /// <param name="numEntries">Number of entries the palette contains</param>
        /// <returns>Success value</returns>
        public bool LoadPalette(string dataFileKey, FileBitAddress address, ColorModel format, bool zeroIndexTransparent, int numEntries)
        {
            if (numEntries > 256)
                throw new ArgumentException("Maximum palette size must be 256 entries or less");

            NativePalette = new NativeColor[numEntries];
            ForeignPalette = new ForeignColor[numEntries];

            FileStream fs = ((DataFile)ResourceManager.Instance.GetResource(dataFileKey)).Stream;

            int readSize;

            switch(format)
            {
                case ColorModel.BGR15: case ColorModel.RGB15:
                    readSize = 2;
                    HasAlpha = false;
                    break;
                case ColorModel.ABGR16:
                    readSize = 2;
                    HasAlpha = true;
                    break;
                case ColorModel.RGB24:
                    readSize = 3;
                    HasAlpha = false;
                    break;
                case ColorModel.ARGB32:
                    readSize = 4;
                    HasAlpha = true;
                    break;
                default:
                    throw new NotSupportedException("An unsupported palette format was attempted to be read");
            }

            byte[] tempPalette = fs.ReadUnshifted(address, readSize * 8 * numEntries, true);
            BitStream bs = BitStream.OpenRead(tempPalette, readSize * 8 * numEntries);

            for (int i = 0; i < numEntries; i++)
            {
                uint foreignColor;

                if (readSize == 1)
                    foreignColor = bs.ReadByte();
                else if (readSize == 2)
                {
                    foreignColor = bs.ReadByte();
                    foreignColor |= ((uint)bs.ReadByte()) << 8;
                }
                else if (readSize == 3)
                {
                    foreignColor = bs.ReadByte();
                    foreignColor |= ((uint)bs.ReadByte()) << 8;
                    foreignColor |= ((uint)bs.ReadByte()) << 16;
                }
                else if (readSize == 4)
                {
                    foreignColor = bs.ReadByte();
                    foreignColor |= ((uint)bs.ReadByte()) << 8;
                    foreignColor |= ((uint)bs.ReadByte()) << 16;
                    foreignColor |= ((uint)bs.ReadByte()) << 24;
                }
                else
                    throw new NotSupportedException("Palette formats with entry sizes larger than 4 bytes are not supported");

                ForeignPalette[i] = (ForeignColor)foreignColor;
                uint nativeColor = ForeignToNativeArgb((ForeignColor)foreignColor, format);
                NativePalette[i] = (NativeColor) nativeColor;
            }

            ZeroIndexTransparent = zeroIndexTransparent;

            if (ZeroIndexTransparent)
                NativePalette[0].Color &= 0x00FFFFFF;

            ColorModel = format;
            FileAddress = address;
            DataFileKey = dataFileKey;
            Entries = numEntries;
            StorageSource = PaletteStorageSource.File;

            return true;
        }

        /// <summary>
        /// Converts a foreign color to a local color
        /// </summary>
        /// <param name="foreignColor">Foreign color to be converted</param>
        /// <param name="model">ColorModel of foreignColor</param>
        /// <returns>Local ARGB32 color value</returns>
        public static uint ForeignToNativeArgb(ForeignColor foreignColor, ColorModel model)
        {
            uint localColor;

            (byte A, byte R, byte G, byte B) = SplitForeignColor((uint)foreignColor, model);

            if (model == ColorModel.BGR15)
            {
                localColor = (uint)R << 19; // Red
                localColor |= (uint)G << 11; // Green
                localColor |= (uint)B << 3; // Blue
                localColor |= 0xFF000000; // Alpha
            }
            else if (model == ColorModel.ABGR16)
            {
                localColor = (uint)R << 19; // Red
                localColor |= (uint)G << 11; // Green
                localColor |= (uint)B << 3; // Blue
                localColor |= ((uint)A * 255) << 24; // Alpha
            }
            else if (model == ColorModel.RGB15)
            {
                localColor = (uint)R << 19; // Red
                localColor |= (uint)G << 11; // Green
                localColor |= (uint)B << 3; // Blue
                localColor |= 0xFF000000; // Alpha
            }
            else
                throw new ArgumentException("Unsupported ColorModel");

            return localColor;
        }

        /// <summary>
        /// Converts a foreign color to a local color
        /// </summary>
        /// <param name="A">Foreign alpha channel to be converted</param>
        /// <param name="R">Foreign red channel to be converted</param>
        /// <param name="G">Foreign green channel to be converted</param>
        /// <param name="B">Foreign blue channel to be converted</param>
        /// <param name="model">ColorModel of foreignColor</param>
        /// <returns>Local ARGB32 color value</returns>
        public static uint ForeignToLocalArgb(byte A, byte R, byte G, byte B, ColorModel model)
        {
            uint localColor;

            if (model == ColorModel.BGR15)
            {
                localColor = (uint) R << 19; // Red
                localColor |= (uint) G << 11; // Green
                localColor |= (uint) B << 3; // Blue
                localColor |= 0xFF000000; // Alpha
            }
            else if (model == ColorModel.ABGR16)
            {
                localColor = (uint)R << 19; // Red
                localColor |= (uint)G << 11; // Green
                localColor |= (uint)B << 3; // Blue
                localColor |= (uint) (A * 255) << 24; // Alpha
            }
            else if (model == ColorModel.RGB15)
            {
                localColor = (uint)R << 19; // Red
                localColor |= (uint)G << 11; // Green
                localColor |= (uint)B << 3; // Blue
                localColor |= 0xFF000000; // Alpha
            }
            else
                throw new ArgumentException("Unsupported ColorModel");

            return localColor;
        }

        /// <summary>
        /// Returns the native color at the specified index
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns>Native ARGB32 color</returns>
        public NativeColor this[int index]
        {
            get
            {
                if (NativePalette == null)
                    throw new ArgumentNullException();

                return NativePalette[index];
            }
        }

        /// <summary>
        /// Returns the local color at the specified index
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns>Local Color</returns>
        public Color GetLocalColor(int index)
        {
            if (NativePalette == null)
                throw new ArgumentNullException();

            return Color.FromArgb((int)NativePalette[index].Color);
        }

        /// <summary>
        /// Finds the palette index associated with a specific color
        /// </summary>
        /// <param name="color">Local color to search for</param>
        /// <param name="exactColorOnly">true to return only an exactly matched color; false to match the closest color</param>
        /// <returns>A palette index matching the specified color</returns>
        public byte GetIndexByLocalColor(Color color, bool exactColorOnly)
        {
            uint c = ColorToUint(color);

            return GetIndexByColor(c, exactColorOnly);
        }

        /// <summary>
        /// Returns a palette index matching the specified ARGB32 value
        /// </summary>
        /// <param name="color">ARGB32 value to search for</param>
        /// <param name="exactColorOnly">true to return only exactly matched colors; false to match the closest color</param>
        /// <returns>A palette index matching the specified colort</returns>
        public byte GetIndexByColor(uint color, bool exactColorOnly)
        {
            if (exactColorOnly)
            {
                for (byte i = 0; i < Entries; i++)
                {
                    if (NativePalette[i].Color == color)
                        return i;
                }

                // Failed to find the exact color in the palette
                throw new Exception();
            }

            // Color matching involves converting colors to hue-saturation-luminance and comparing
            var c1 = new ColorMine.ColorSpaces.Rgb { R = RFromARGB(color), G = GFromARGB(color), B = BFromARGB(color) };
            var h1 = c1.To<ColorMine.ColorSpaces.Hsl>();

            double MinDistance = double.MaxValue;
            byte MinIndex = 0;
            Cie94Comparison comparator = new Cie94Comparison(Cie94Comparison.Application.GraphicArts);

            for(byte i = 0; i < Entries; i++)
            {
                var c2 = new ColorMine.ColorSpaces.Rgb { R = RFromARGB((uint)NativePalette[i]), G = GFromARGB((uint)NativePalette[i]), B = BFromARGB((uint)NativePalette[i]) };
                var h2 = c2.To<ColorMine.ColorSpaces.Hsl>();

                double Distance = c1.Compare(c2, comparator);

                if(Distance < MinDistance)
                {
                    MinDistance = Distance;
                    MinIndex = i;
                }
            }

            return MinIndex;
        }

        private uint ColorToUint(Color color)
        {
            uint c = ((uint)color.A) << 24 | ((uint)color.R) << 16 | ((uint)color.G) << 8 | ((uint)color.B);
            return c;
        }

        private uint AFromARGB(uint color)
        {
            return (color & 0xFF000000) >> 24;
        }

        private uint RFromARGB(uint color)
        {
            return (color & 0xFF0000) >> 16;
        }

        private uint GFromARGB(uint color)
        {
            return (color & 0xFF00) >> 8;
        }

        private uint BFromARGB(uint color)
        {
            return color & 0xFF;
        }

        /// <summary>
        /// Splits a foreign color into its foreign color components
        /// </summary>
        /// <param name="index">Palette index of specified color</param>
        /// <returns>A tuple containing the alpha, red, green, and blue color components of the foreign color</returns>
        public (byte A, byte R, byte G, byte B) SplitForeignColor(int index)
        {
            uint foreignColor = ForeignPalette[index].Color;

            return SplitForeignColor(foreignColor, ColorModel);
        }

        /// <summary>
        /// Splits a foreign color into its foreign color components
        /// </summary>
        /// <param name="foreignColor">Foreign color to be converted</param>
        /// <param name="ColorModel">ColorModel of foreignColor</param>
        /// <returns>A tuple containing the alpha, red, green, and blue color components</returns>
        public static (byte A, byte R, byte G, byte B) SplitForeignColor(uint foreignColor, ColorModel ColorModel)
        {
            byte A, R, G, B;

            switch (ColorModel)
            {
                case ColorModel.BGR15: case ColorModel.ABGR16:
                    R = (byte)(foreignColor & 0x1F);
                    G = (byte)((foreignColor & 0x3E0) >> 5);
                    B = (byte)((foreignColor & 0x7C00) >> 10);
                    A = (byte)((foreignColor & 0x8000) >> 15);
                    break;
                case ColorModel.RGB15:
                    R = (byte)((foreignColor & 0x7C00) >> 10);
                    G = (byte)((foreignColor & 0x3E0) >> 5);
                    B = (byte)(foreignColor & 0x1F);
                    A = (byte)((foreignColor & 0x8000) >> 15);
                    break;
                default:
                    throw new ArgumentException("Unsupported ColorModel");
            }

            return (A, R, G, B);
        }

        public static uint MakeForeignColor(byte A, byte R, byte G, byte B, ColorModel colorFormat)
        {
            uint foreignColor = 0;

            switch (colorFormat)
            {
                // TODO: Validate color ranges
                case ColorModel.BGR15: case ColorModel.ABGR16:
                    foreignColor |= R;
                    foreignColor |= ((uint)G << 5);
                    foreignColor |= ((uint)B << 10);
                    foreignColor |= ((uint)A << 15);
                    break;
                case ColorModel.RGB15:
                    foreignColor |= B;
                    foreignColor |= ((uint)G << 5);
                    foreignColor |= ((uint)R << 10);
                    foreignColor |= ((uint)A << 15);
                    break;
                default:
                    throw new ArgumentException("Unsupported ColorModel");
            }

            return foreignColor;
        }

        /// <summary>
        /// Accesses the palette and returns the foreign alpha channel value
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns></returns>
        public uint ForeignAlpha(int index)
        {
            (byte A, byte R, byte G, byte B) = SplitForeignColor(ForeignPalette[index].Color, ColorModel);

            return R;
        }

        /// <summary>
        /// Accesses the palette and returns the foreign red channel value
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns></returns>
        public uint ForeignRed(int index)
        {
            (byte A, byte R, byte G, byte B) = SplitForeignColor(ForeignPalette[index].Color, ColorModel);

            return R;
        }

        /// <summary>
        /// Accesses the palette and returns the foreign blue channel value
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns></returns>
        public uint ForeignBlue(int index)
        {
            (byte A, byte R, byte G, byte B) = SplitForeignColor(ForeignPalette[index].Color, ColorModel);

            return B;
        }

        /// <summary>
        /// Accesses the palette and returns the foreign green channel value
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <returns></returns>
        public uint ForeignGreen(int index)
        {
            (byte A, byte R, byte G, byte B) = SplitForeignColor(ForeignPalette[index].Color, ColorModel);

            return G;
        }

        /// <summary>
        /// Replaces the color at a particular palette index with the specified foreign color
        /// Additionally, updates the local color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <param name=""></param>
        public void SetPaletteForeignColor(int index, uint foreignColor)
        {
            if (ForeignPalette == null)
                throw new NullReferenceException();

            if (index >= Entries)
                throw new ArgumentOutOfRangeException("index", index, "Index is outside the range of number of entries in the palette");

            ForeignPalette[index] = (ForeignColor) foreignColor;
            NativePalette[index] = (NativeColor) ForeignToNativeArgb((ForeignColor)foreignColor, ColorModel);
        }

        /// <summary>
        /// Replaces the color at a particular palette index with the specified foreign color
        /// Additionally, updates the local color in the palette
        /// </summary>
        /// <param name="index">Zero-based palette index</param>
        /// <param name=""></param>
        public void SetPaletteForeignColor(int index, byte A, byte R, byte G, byte B)
        {
            uint foreignColor = Palette.MakeForeignColor(A, R, G, B, ColorModel);
            SetPaletteForeignColor(index, foreignColor);
        }

        /// <summary>
        /// Saves palette's foreign colors to its underlying source and location
        /// </summary>
        /// <returns>Success value</returns>
        public bool SavePalette()
        {
            if(StorageSource == PaletteStorageSource.File)
            {
                int writeSize;

                switch (ColorModel)
                {
                    case ColorModel.BGR15:
                        writeSize = 2;
                        HasAlpha = false;
                        break;
                    case ColorModel.ABGR16:
                        writeSize = 2;
                        HasAlpha = true;
                        break;
                    case ColorModel.RGB24:
                        writeSize = 3;
                        HasAlpha = false;
                        break;
                    case ColorModel.ARGB32:
                        writeSize = 4;
                        HasAlpha = true;
                        break;
                    case ColorModel.RGB15:
                        writeSize = 2;
                        HasAlpha = false;
                        break;
                    default:
                        throw new NotSupportedException("An unsupported palette format was attempted to be read");
                }

                FileStream fs = ((DataFile)ResourceManager.Instance.GetResource(DataFileKey)).Stream;
                BinaryWriter bw = new BinaryWriter(fs);

                fs.Seek(FileAddress.FileOffset, SeekOrigin.Begin); // TODO: Recode this for bitwise writing

                for (int i = 0; i < Entries; i++)
                {
                    if (writeSize == 1)
                        bw.Write((byte)ForeignPalette[i].Color);
                    else if (writeSize == 2)
                        bw.Write((short)ForeignPalette[i].Color);
                    else if (writeSize == 3)
                    {
                        bw.Write((byte)(ForeignPalette[i].Color & 0xFF));
                        bw.Write((byte)((ForeignPalette[i].Color >> 8) & 0xFF));
                        bw.Write((byte)((ForeignPalette[i].Color >> 16) & 0xFF));
                    }
                    else if (writeSize == 4)
                        bw.Write(ForeignPalette[i].Color);
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the string name associated with a ColorModel object
        /// </summary>
        /// <param name="ColorModelName">Name of the ColorModel to retrieve</param>
        /// <returns>A string name describing the ColorModel</returns>
        public static ColorModel StringToColorModel(string ColorModelName)
        {
            switch(ColorModelName)
            {
                case "RGB24":
                    return ColorModel.RGB24;
                case "ARGB32":
                    return ColorModel.ARGB32;
                case "BGR15":
                    return ColorModel.BGR15;
                case "ABGR16":
                    return ColorModel.ABGR16;
                case "RGB15":
                    return ColorModel.RGB15;
                case "NES":
                    return ColorModel.NES;
                default:
                    throw new ArgumentException("ColorModel " + ColorModelName + " is not supported");
            }
        }

        public static string ColorFormatToString(ColorModel colorFormat)
        {
            switch (colorFormat)
            {
                case ColorModel.RGB24:
                    return "RGB24";
                case ColorModel.ARGB32:
                    return "ARGB32";
                case ColorModel.BGR15:
                    return "BGR15";
                case ColorModel.ABGR16:
                    return "ABGR16";
                case ColorModel.RGB15:
                    return "RGB15";
                case ColorModel.NES:
                    return "NES";
                default:
                    throw new ArgumentException("ColorModel " + colorFormat.ToString() + " is not supported");
            }
        }

        public static List<string> GetColorModelNames()
        {
            return Enum.GetNames(typeof(ColorModel)).Cast<string>().ToList<string>();
        }

        /// <summary>
        /// Creates a deep copy of the palette
        /// </summary>
        /// <returns></returns>
        public IProjectResource Clone()
        {
            Palette pal = new Palette(Name)
            {
                ColorModel = this.ColorModel,
                FileAddress = this.FileAddress,
                DataFileKey = this.DataFileKey,
                Entries = this.Entries,
                HasAlpha = this.HasAlpha,
                ZeroIndexTransparent = this.ZeroIndexTransparent,
                StorageSource = this.StorageSource,
                NativePalette = new NativeColor[this.Entries],
                ForeignPalette = new ForeignColor[this.Entries]
            };

            NativePalette.CopyTo(pal.NativePalette, 0);
            ForeignPalette.CopyTo(pal.ForeignPalette, 0);

            return pal;
        }
    }

    public class PaletteNotFoundException: Exception
    {
    }
}
