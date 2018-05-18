﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TileShop.Core;

namespace TileShop.ExtensionMethods
{
    public static class ResourceTreeExtensions
    {
        public static ProjectResourceBase FindResource(this IDictionary<string, ProjectResourceBase> tree, string resourceKey)
        {
            if (String.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException();

            var paths = resourceKey.Split('\\');
            ProjectResourceBase node;

            if (tree.ContainsKey(paths[0]))
                node = tree[paths[0]];
            else
                return null;
                //throw new KeyNotFoundException($"Resource {paths[0]} in {searchPath} not found");

            for(int i = 1; i < paths.Length; i++)
            {
                if (node.ChildResources.ContainsKey(paths[i]))
                    node = node.ChildResources[paths[i]];
                else
                    return null;
                    //throw new KeyNotFoundException($"Resource {paths[i]} in {searchPath} not found");
            }

            return node;
        }

        public static bool TryGetResource(this IDictionary<string, ProjectResourceBase> tree, string resourceKey, out ProjectResourceBase resource)
        {
            if (String.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException();

            var paths = resourceKey.Split('\\');
            var nodeVisitor = tree;
            ProjectResourceBase node = null;

            for(int i = 0; i < paths.Length; i++)
            {
                if(nodeVisitor.TryGetValue(paths[i], out node))
                    nodeVisitor = node.ChildResources;
                else
                {
                    resource = null;
                    return false;
                }
            }

            resource = node;
            return true;
        }

        public static bool ContainsResource(this IDictionary<string, ProjectResourceBase> tree, string resourceKey)
        {
            if (String.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException();

            var paths = resourceKey.Split('\\');
            var nodeVisitor = tree;
            ProjectResourceBase node = null;

            for (int i = 0; i < paths.Length; i++)
            {
                if (nodeVisitor.TryGetValue(paths[i], out node))
                    nodeVisitor = node.ChildResources;
                else
                    return false;
            }

            return true;
        }

        public static void AddResource(this IDictionary<string, ProjectResourceBase> tree, string resourceKey, ProjectResourceBase resource)
        {
            if (String.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException();

            string parentResourceKey = Path.GetDirectoryName(resourceKey);

            if (String.IsNullOrWhiteSpace(parentResourceKey)) // Add to root
            {
                tree.Add(resource.Name, resource);
            }
            else // Add to Parent Resource
            {
                ProjectResourceBase parent;
                if (!tree.TryGetResource(parentResourceKey, out parent))
                    throw new KeyNotFoundException($"{nameof(AddResource)} could not locate parent resource {parentResourceKey} for {resource.Name}");

                parent.ChildResources.Add(resource.Name, resource);
            }
        }

        public static void RemoveResource(this IDictionary<string, ProjectResourceBase> tree, string resourceKey)
        {
            if (String.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException();

            string parentResourceKey = Path.GetDirectoryName(resourceKey);
            string resourceName = Path.GetDirectoryName(resourceKey);

            if (String.IsNullOrWhiteSpace(parentResourceKey)) // Parent is root
            {
                tree.Remove(resourceKey);
            }
            else // Remove from Parent Resource
            {
                ProjectResourceBase parentResource;
                if (!tree.TryGetResource(parentResourceKey, out parentResource))
                    throw new KeyNotFoundException($"{nameof(RemoveResource)} could not locate parent resource {parentResourceKey} for {resourceName}");

                parentResource.ChildResources.Remove(resourceName);
            }
        }

        public static void ReplaceResource(this IDictionary<string, ProjectResourceBase> tree, string resourceKey, ProjectResourceBase newResource)
        {
            if (String.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException();

            string parentResourceKey = Path.GetDirectoryName(resourceKey);
            string resourceName = Path.GetDirectoryName(resourceKey);

            if (String.IsNullOrWhiteSpace(parentResourceKey)) // Resource is attached to root
            {
                if (tree.ContainsKey(newResource.Name))
                    tree[newResource.Name] = newResource;
            }
            else // Replace from the Parent Resource
            {
                ProjectResourceBase parentResource;
                if(!tree.TryGetResource(parentResourceKey, out parentResource))
                    throw new KeyNotFoundException($"{nameof(ReplaceResource)} could not locate parent resource {parentResourceKey} for {resourceKey}");

                parentResource.ChildResources[resourceName] = newResource;
            }

        }

        /// <summary>
        /// Allows depth-first iteration over a resource tree
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns></returns>
        /// <remarks>Idea adapted from https://www.benjamin.pizza/posts/2017-11-13-recursion-without-recursion.html 
        /// Implementation adapted from https://blogs.msdn.microsoft.com/wesdyer/2007/03/23/all-about-iterators/
        /// </remarks>
        public static IEnumerable<ProjectResourceBase> SelfAndDescendants(this IDictionary<string, ProjectResourceBase> tree)
        {
            Stack<ProjectResourceBase> nodeStack = new Stack<ProjectResourceBase>();

            foreach(var node in tree.Values)
                nodeStack.Push(node);

            while(nodeStack.Count > 0)
            {
                var node = nodeStack.Pop();
                yield return node;
                foreach (var child in node.ChildResources.Values)
                    nodeStack.Push(child);
            }
        }

        /// <summary>
        /// Ancestors of the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        public static IEnumerable<ProjectResourceBase> Ancestors(this ProjectResourceBase node)
        {
            var parentVisitor = node.Parent;

            while(parentVisitor != null)
            {
                yield return parentVisitor;
                parentVisitor = parentVisitor.Parent;
            }
        }
    }
}
