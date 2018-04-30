﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;

namespace TileShop.Core
{
    /// <summary>
    /// DataFile manages access to user-modifiable files
    /// </summary>
    public class DataFile: ProjectResourceBase
    {
        public string Location { get; private set; }

        Lazy<FileStream> _stream;
        public FileStream Stream { get => _stream.Value; }

        public DataFile(string name): this(name, "")
        {
        }

        public DataFile(string name, string location)
        {
            Name = name;
            Location = location;

            _stream = new Lazy<FileStream>(() =>
            {
                if (String.IsNullOrWhiteSpace(Location))
                    throw new ArgumentException();

                return File.Open(Location, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            });
        }

        /// <summary>
        /// Renames a DataFile to a new name
        /// </summary>
        /// <param name="name"></param>
        public override void Rename(string name)
        {
            Name = name;
        }

        public override ProjectResourceBase Clone()
        {
            DataFile df = new DataFile(Name, Location);
            return df;
        }

        public void Close()
        {
            if (Stream != null)
                Stream.Close();
        }

        public override XElement Serialize()
        {
            throw new NotImplementedException();
        }

        public override bool Deserialize(XElement element)
        {
            Name = element.Attribute("name").Value;
            Location = element.Attribute("location").Value;

            return true;
        }
    }
}
