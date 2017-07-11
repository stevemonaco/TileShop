﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TileShop
{
    /// <summary>
    /// Contains Arranger utility functions more relevant for GUI features
    /// </summary>
    public static class ArrangerExtensions
    {
        /// <summary>
        /// Gets a set of all distinct Palette keys used in an Arranger
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static HashSet<string> GetPaletteKeySet(this Arranger self)
        {
            HashSet<string> palSet = new HashSet<string>();

            for (int x = 0; x < self.ArrangerElementSize.Width; x++)
            {
                for (int y = 0; y < self.ArrangerElementSize.Height; y++)
                {
                    palSet.Add(self.ElementGrid[x, y].PaletteKey);
                }
            }

            return palSet;
        }

        /// <summary>
        /// Moves a Sequential Arranger's file position and updates each Element
        /// Will not move outside of the bounds of the underlying file
        /// </summary>
        /// <param name="moveType">Type of move requested</param>
        /// <returns>Updated address of first element</returns>
        public static FileBitAddress Move(this Arranger self, ArrangerMoveType moveType)
        {
            if (self.Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            if (self.ElementGrid == null)
                throw new NullReferenceException();

            FileBitAddress address = self.ElementGrid[0, 0].FileAddress;
            FileBitAddress delta;

            switch (moveType) // Calculate the new address based on the movement command. Negative and post-EOF addresses are handled after the switch
            {
                case ArrangerMoveType.ByteDown:
                    address += 8;
                    break;
                case ArrangerMoveType.ByteUp:
                    address -= 8;
                    break;
                case ArrangerMoveType.RowDown:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize;
                    address += delta;
                    break;
                case ArrangerMoveType.RowUp:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize;
                    address -= delta;
                    break;
                case ArrangerMoveType.ColRight:
                    delta = self.ElementGrid[0, 0].StorageSize;
                    address += delta;
                    break;
                case ArrangerMoveType.ColLeft:
                    delta = self.ElementGrid[0, 0].StorageSize;
                    address -= delta;
                    break;
                case ArrangerMoveType.PageDown:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize * self.ArrangerElementSize.Height / 2;
                    address += delta;
                    break;
                case ArrangerMoveType.PageUp:
                    delta = self.ArrangerElementSize.Width * self.ElementGrid[0, 0].StorageSize * self.ArrangerElementSize.Height / 2;
                    address -= delta;
                    break;
                case ArrangerMoveType.Home:
                    address = 0;
                    break;
                case ArrangerMoveType.End:
                    address = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);
                    break;
            }

            if (address + self.ArrangerBitSize > self.FileSize * 8) // Calculated address is past EOF (first)
                address = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);

            if (address < 0) // Calculated address is before start of file (second)
                address = 0;

            self.Move(address);

            return address;
        }

        /// <summary>
        /// Moves the sequential arranger to the specified address
        /// If the arranger will overflow the file, then seek only to the furthest offset
        /// </summary>
        /// <param name="absoluteAddress">Specified address to move the arranger to</param>
        /// <returns></returns>
        public static FileBitAddress Move(this Arranger self, FileBitAddress absoluteAddress)
        {
            if (self.Mode != ArrangerMode.SequentialArranger)
                throw new InvalidOperationException();

            if (self.ElementGrid == null)
                throw new NullReferenceException();

            FileBitAddress address;
            FileBitAddress testaddress = absoluteAddress + self.ArrangerBitSize; // Tests the bounds of the arranger vs the file size

            if (self.FileSize * 8 < self.ArrangerBitSize) // Arranger needs more bits than the entire file
                address = new FileBitAddress(0, 0);
            else if (testaddress.Bits() > self.FileSize * 8)
                address = new FileBitAddress(self.FileSize * 8 - self.ArrangerBitSize);
            else
                address = absoluteAddress;

            int ElementStorageSize = self.ElementGrid[0, 0].StorageSize;

            for (int i = 0; i < self.ArrangerElementSize.Height; i++)
            {
                for (int j = 0; j < self.ArrangerElementSize.Width; j++)
                {
                    self.ElementGrid[j, i].FileAddress = address;
                    address += ElementStorageSize;
                }
            }

            return self.ElementGrid[0, 0].FileAddress;
        }
    }
}