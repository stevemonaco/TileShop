﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TileShop.Core
{
    public abstract class ProjectResourceBase
    {
        /// <summary>
        /// Identifying name of the resource
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Gets or sets the parent.
        /// </summary>
        /// <value>
        /// The parent.
        /// </value>
        public ProjectResourceBase Parent { get; set; }

        /// <summary>
        /// The child resources
        /// </summary>
        internal ICollection<KeyValuePair<string, ProjectResourceBase>> ChildResources;

        /// <summary>
        /// Determines if the ProjectResource can contain child resources
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can contain child resources; otherwise, <c>false</c>.
        /// </value>
        public bool CanContainChildResources { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the ProjectResource should be serialized.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [should be serialized]; otherwise, <c>false</c>.
        /// </value>
        public bool ShouldBeSerialized { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is leased.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is leased; otherwise, <c>false</c>.
        /// </value>
        public bool IsLeased { get; set; } = false;

        /// <summary>
        /// Rename a resource with a new name
        /// </summary>
        /// <param name="name">The new name.</param>
        public virtual void Rename(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Deep-clone copy of the object
        /// </summary>
        /// <returns></returns>
        public abstract ProjectResourceBase Clone();

        /// <summary>
        /// Serializes resource into an XElement
        /// </summary>
        /// <returns></returns>
        public abstract XElement Serialize();

        /// <summary>
        /// Deserialize XElement into ProjectResource
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public abstract bool Deserialize(XElement element);
    }
}