﻿using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using TileShop.ExtensionMethods;

namespace TileShop.Core
{
    /// <summary>
    /// Provides XML support for reading/writing Game Descriptor Files and loading the content into the ProjectExplorerControl
    /// </summary>
    public class GameDescriptorSerializer
    {
        /// <summary>
        /// Base directory for all file locations on disk
        /// </summary>
        private string baseDirectory = null;

        #region XML Deserialization methods
        /// <summary>
        /// Loads data files, palettes, and arrangers from XML
        /// Sets up the nodes in the project tree
        /// The project should be previously cleared before calling
        /// </summary>
        /// <param name="XmlFileName">Path to the project XML file</param>
        /// <returns></returns>
        public bool LoadProject(string XmlFileName)
        {
            if (String.IsNullOrEmpty(XmlFileName))
                throw new ArgumentException();

            XElement doc = XElement.Load(XmlFileName);
            XElement projectNode = doc.Element("project");

            baseDirectory = Path.GetDirectoryName(XmlFileName);

            Directory.SetCurrentDirectory(baseDirectory);

            /*var settings = xe.Descendants("settings")
                .Select(e => new
                {
                    numberformat = e.Descendants("filelocationnumberformat").First().Value
                });*/

            // Iterate over each project child node
            foreach (XElement node in projectNode.Elements())
            {
                if (node.Name == "folder")
                    AddFolderNode(node);
                else if (node.Name == "datafile")
                    AddDataFileNode(node);
                else if (node.Name == "palette")
                    AddPaletteNode(node);
                else if (node.Name == "arranger")
                    AddArrangerNode(node);
            }

            return true;
        }

        /// <summary>
        /// Recursive method that adds a folder node and all child nodes to the ProjectExplorerControl
        /// </summary>
        /// <param name="folderNode"></param>
        /// <returns></returns>
        private bool AddFolderNode(XElement folderNode)
        {
            foreach (XElement node in folderNode.Elements())
            {
                if (node.Name == "folder")
                    AddFolderNode(node);
                else if (node.Name == "datafile")
                    AddDataFileNode(node);
                else if (node.Name == "palette")
                    AddPaletteNode(node);
                else if (node.Name == "arranger")
                    AddArrangerNode(node);
            }

            return true;
        }

        private bool AddDataFileNode(XElement fileNode)
        {
            string name = fileNode.Attribute("name").Value;
            string location = fileNode.Attribute("location").Value;

            DataFile df = new DataFile(name);
            df.Open(location);

            ResourceManager.Instance.AddResource(fileNode.NodeKey(), df);
            return true;
        }

        private bool AddPaletteNode(XElement paletteNode)
        {
            string name = paletteNode.Attribute("name").Value;
            long fileoffset = long.Parse(paletteNode.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber);
            string datafile = paletteNode.Attribute("datafile").Value;
            int entries = int.Parse(paletteNode.Attribute("entries").Value);
            string formatname = paletteNode.Attribute("format").Value;
            bool zeroindextransparent = bool.Parse(paletteNode.Attribute("zeroindextransparent").Value);

            FileBitAddress address;
            if (paletteNode.Attribute("bitoffset") == null)
                address = new FileBitAddress(fileoffset, 0);
            else
                address = new FileBitAddress(fileoffset, int.Parse(paletteNode.Attribute("bitoffset").Value));

            Palette pal = new Palette(name);
            ColorModel format = Palette.StringToColorModel(formatname);

            pal.LoadPalette(datafile, address, format, zeroindextransparent, entries);

            ResourceManager.Instance.AddResource(paletteNode.NodeKey(), pal);

            return true;
        }

        private bool AddArrangerNode(XElement arrangerNode)
        {
            string name = arrangerNode.Attribute("name").Value;
            int elementsx = int.Parse(arrangerNode.Attribute("elementsx").Value); // Width of arranger in elements
            int elementsy = int.Parse(arrangerNode.Attribute("elementsy").Value); // Height of arranger in elements
            int width = int.Parse(arrangerNode.Attribute("width").Value); // Width of element in pixels
            int height = int.Parse(arrangerNode.Attribute("height").Value); // Height of element in pixels
            string defaultformat = arrangerNode.Attribute("defaultformat").Value;
            string defaultdatafile = arrangerNode.Attribute("defaultdatafile").Value;
            string defaultpalette = arrangerNode.Attribute("defaultpalette").Value;
            string layoutName = arrangerNode.Attribute("layout").Value;
            IEnumerable<XElement> elementList = arrangerNode.Descendants("element");

            Arranger arr;
            ArrangerLayout layout;

            if (layoutName == "tiled")
                layout = ArrangerLayout.TiledArranger;
            else if (layoutName == "linear")
                layout = ArrangerLayout.LinearArranger;
            else
                throw new XmlException("Incorrect arranger layout type ('" + layoutName + "') for " + name);

            arr = Arranger.NewScatteredArranger(layout, elementsx, elementsy, width, height);
            arr.Rename(name);

            var xmlElements = elementList.Select(e => new
            {
                fileoffset = long.Parse(e.Attribute("fileoffset").Value, System.Globalization.NumberStyles.HexNumber),
                bitoffset = e.Attribute("bitoffset"),
                posx = int.Parse(e.Attribute("posx").Value),
                posy = int.Parse(e.Attribute("posy").Value),
                format = e.Attribute("format"),
                palette = e.Attribute("palette"),
                file = e.Attribute("file")
            });

            foreach (var xmlElement in xmlElements)
            {
                ArrangerElement el = arr.GetElement(xmlElement.posx, xmlElement.posy);

                el.DataFileKey = xmlElement.file?.Value ?? defaultdatafile;
                el.PaletteKey = xmlElement.palette?.Value ?? defaultpalette;
                el.FormatName = xmlElement.format?.Value ?? defaultformat;

                if (xmlElement.bitoffset != null)
                    el.FileAddress = new FileBitAddress(xmlElement.fileoffset, int.Parse(xmlElement.bitoffset.Value));
                else
                    el.FileAddress = new FileBitAddress(xmlElement.fileoffset, 0);

                el.Height = height;
                el.Width = width;
                el.X1 = xmlElement.posx * el.Width;
                el.Y1 = xmlElement.posy * el.Height;
                el.X2 = el.X1 + el.Width - 1;
                el.Y2 = el.Y1 + el.Height - 1;

                el.AllocateBuffers();

                arr.SetElement(el, xmlElement.posx, xmlElement.posy);
            }

            ResourceManager.Instance.AddResource(arrangerNode.NodeKey(), arr);
            //ptf.AddArranger(arr, arrangerNode.NodePath());
            return true;
        }
        #endregion

        #region XML Serialization methods
        /// <summary>
        /// Iterates over tree nodes and saves project settings to XML
        /// </summary>
        /// <param name="XmlFileName"></param>
        /// <returns></returns>
        public bool SaveProject(string XmlFileName)
        {
            if (String.IsNullOrEmpty(XmlFileName))
                throw new ArgumentException();

            var xmlRoot = new XElement("gdf");
            var projectRoot = new XElement("project");
            var settingsRoot = new XElement("settings");

            xmlRoot.Add(settingsRoot);

            /*foreach (TreeNode tn in tnc)
            {
                if (tn is FolderNode folderNode)
                    projectRoot.Add(SaveFolderNode(folderNode));
                else if (tn is DataFileNode fileNode)
                    projectRoot.Add(SaveDataFileNode(fileNode));
                else if (tn is PaletteNode paletteNode)
                    projectRoot.Add(SavePaletteNode(paletteNode));
                else if (tn is ArrangerNode arrangerNode)
                    projectRoot.Add(SaveArrangerNode(arrangerNode));
            }*/

            xmlRoot.Add(projectRoot);

            xmlRoot.Save(XmlFileName);

            return true;
        }

        public XElement SaveFolderNode(FolderNode fn)
        {
            XElement xe = new XElement("folder");
            xe.SetAttributeValue("name", fn.Name);

            /*foreach (TreeNode tn in fn.Nodes)
            {
                if (tn is FolderNode folderNode)
                    xe.Add(SaveFolderNode(folderNode));
                else if (tn is DataFileNode fileNode)
                    SaveDataFileNode(fileNode);
                else if (tn is PaletteNode paletteNode)
                    SavePaletteNode(paletteNode);
                else if (tn is ArrangerNode arrangerNode)
                    SaveArrangerNode(arrangerNode);
            }*/

            return xe;
        }

        public XElement SaveDataFileNode(DataFileNode dfn)
        {
            DataFile df = ResourceManager.Instance.GetResource(dfn.GetNodeKey()) as DataFile;

            XElement xe = new XElement("datafile");
            xe.SetAttributeValue("name", df.Name);
            xe.SetAttributeValue("location", df.Location);

            return xe;
        }

        public XElement SavePaletteNode(PaletteNode pn)
        {
            Palette pal = ResourceManager.Instance.GetResource(pn.GetNodeKey()) as Palette;

            XElement xe = new XElement("palette");

            xe.SetAttributeValue("name", pal.Name);
            xe.SetAttributeValue("folder", pn.GetNodePath());
            xe.SetAttributeValue("fileoffset", String.Format("{0:X}", pal.FileAddress.FileOffset));
            xe.SetAttributeValue("bitoffset", String.Format("{0:X}", pal.FileAddress.BitOffset));
            xe.SetAttributeValue("datafile", pal.DataFileKey);
            xe.SetAttributeValue("format", Palette.ColorModelToString(pal.ColorModel));
            xe.SetAttributeValue("entries", pal.Entries);
            xe.SetAttributeValue("zeroindextransparent", pal.ZeroIndexTransparent);

            return xe;
        }

        public XElement SaveArrangerNode(ArrangerNode an)
        {
            Arranger arr = ResourceManager.Instance.GetResource(an.GetNodeKey()) as Arranger;
            XElement xe = new XElement("arranger");

            xe.SetAttributeValue("name", arr.Name);
            xe.SetAttributeValue("folder", an.GetNodePath());
            xe.SetAttributeValue("elementsx", arr.ArrangerElementSize.Width);
            xe.SetAttributeValue("elementsy", arr.ArrangerElementSize.Height);
            xe.SetAttributeValue("width", arr.ElementPixelSize.Width);
            xe.SetAttributeValue("height", arr.ElementPixelSize.Height);

            if (arr.Layout == ArrangerLayout.TiledArranger)
                xe.SetAttributeValue("layout", "tiled");
            else if (arr.Layout == ArrangerLayout.LinearArranger)
                xe.SetAttributeValue("layout", "linear");

            string DefaultPalette = FindMostFrequentElementValue(arr, "PaletteName");
            string DefaultFile = FindMostFrequentElementValue(arr, "FileName");
            string DefaultFormat = FindMostFrequentElementValue(arr, "FormatName");

            xe.SetAttributeValue("defaultformat", DefaultFormat);
            xe.SetAttributeValue("defaultfile", DefaultFile);
            xe.SetAttributeValue("defaultpalette", DefaultPalette);

            for (int y = 0; y < arr.ArrangerElementSize.Height; y++)
            {
                for (int x = 0; x < arr.ArrangerElementSize.Width; x++)
                {
                    var graphic = new XElement("graphic");
                    ArrangerElement el = arr.GetElement(x, y);

                    graphic.SetAttributeValue("fileoffset", String.Format("{0:X}", el.FileAddress.FileOffset));
                    graphic.SetAttributeValue("bitoffset", String.Format("{0:X}", el.FileAddress.BitOffset));
                    graphic.SetAttributeValue("posx", x);
                    graphic.SetAttributeValue("posy", y);
                    if (el.FormatName != DefaultFormat)
                        graphic.SetAttributeValue("format", el.FormatName);
                    if (el.DataFileKey != DefaultFile)
                        graphic.SetAttributeValue("file", el.DataFileKey);
                    if (el.PaletteKey != DefaultPalette)
                        graphic.SetAttributeValue("palette", el.PaletteKey);

                    xe.Add(graphic);
                }
            }

            return xe;
        }
        #endregion

        /// <summary>
        /// Find most frequent of an attribute within an arranger's elements
        /// </summary>
        /// <param name="arr">Arranger to search</param>
        /// <param name="attributeName">Name of the attribute to find most frequent value of</param>
        /// <returns></returns>
        private string FindMostFrequentElementValue(Arranger arr, string attributeName)
        {
            Type T = typeof(ArrangerElement);
            PropertyInfo P = T.GetProperty(attributeName);

            var query = from ArrangerElement el in arr.ElementGrid
                        group el by P.GetValue(el) into grp
                        select new { key = grp.Key, count = grp.Count() };

            return query.MaxBy(x => x.count).key as string;

            /*Dictionary<string, int> freq = new Dictionary<string, int>();
            Type T = typeof(ArrangerElement);
            PropertyInfo P = T.GetProperty(attributeName);

            foreach (ArrangerElement el in arr.ElementGrid)
            {
                string s = (string)P.GetValue(el);

                if (s == "")
                    continue;

                if (freq.ContainsKey(s))
                    freq[s]++;
                else
                    freq.Add(s, 1);
            }

            var max = freq.FirstOrDefault(x => x.Value == freq.Values.Max()).Key;*/

            //return max;
        }
    }
}
